using System.Collections.Generic;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>Hauls cut short at sunset (WC-35, T-291). Early fetch is off (T-491), so there are no early-fetch tests.</summary>
    public class HaulFetchAndSunsetTests
    {
        private const long AdultBirth = -4_000_000;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld NewWorld(int month, int day, int hour)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            return SimWorld.FromMapData(map, new SimTuning
            {
                StartMonth = month, StartDay = day, StartHour = hour, StartMinute = 0, StartWeekday = Weekday.Monday,
                OxLoadLogs = 2,
            });
        }

        // Area x 90..120, z 90..110; hut (100,100); storage (id 2) at (110,100); ox rests at (ox, oz).
        private static Operation Setup(SimWorld w, int workers, double oxX, double oxZ)
        {
            CommandResult r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) }), AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Operation op = w.Operations.FindByArea((int)r.Created[0]);
            w.Footprints.Add(1, ModuleTypes.WoodcuttingHut, M(100, 100), 0);
            w.Operations.AddModule(op.Id, 1);
            w.Footprints.Add(2, ModuleTypes.LogStorage, M(110, 100), 0);
            w.Operations.AddModule(op.Id, 2);
            w.People.AddFamily(1, "F", 100, 100);
            for (int i = 0; i < workers; i++) w.People.AddCitizen(10 + i, "C" + i, Sex.Male, AdultBirth, 1, 100, 100);
            Assert.IsTrue(w.Execute(new AssignFamily(op.Id, 1)).IsAccepted);
            Ox ox = w.People.AddOx(50, "O", oxX, oxZ);
            ox.State = OxState.Resting((long)(oxX * 1000), (long)(oxZ * 1000));
            return op;
        }

        private static List<TaskRecord> Hauls(SimWorld w, Operation op)
        {
            var r = new List<TaskRecord>();
            foreach (TaskRecord t in w.Tasks.ByOwner(op.Id)) if (t.Def.Id == HaulSystem.HaulTaskId) r.Add(t);
            return r;
        }

        private static LedgerLocation Store(SimWorld w) => w.Inventory.GetOrAddStorage(2, w.Footprints.Get(2).Type);

        // Dec 21: sunrise 08:00, sunset 16:00. Jun 21: sunrise 04:00, sunset 20:00. A far pile at (100,190).
        // The pile holds 2 logs, one ox load (OxLoadLogs), so one haul carries it all.
        [TestCase(12, 21, 13, TestName = "Winter")]
        [TestCase(6, 21, 17, TestName = "Summer")]
        public void SunsetDrop_FarPile_UnloadsWhereItStands_ReturnsOx_ReleasesOldPile_NextDayPicksItUp(int month, int day, int hour)
        {
            SimWorld w = NewWorld(month, day, hour);
            Operation op = Setup(w, workers: 1, oxX: 105, oxZ: 104);
            LedgerLocation oldPile = w.Inventory.ProduceOnGround(M(100, 190), op.Id, Items.Log, 2);
            long end = w.WorkDay.WindowOf(w, WorkDay.DayStart(w)).End;

            TaskRecord haul = null;
            while (w.Tick < end - 1)
            {
                w.Step(1);
                if (haul == null && Hauls(w, op).Count > 0) haul = Hauls(w, op)[0];
            }
            Assert.IsNotNull(haul);
            Assert.AreEqual(TaskState.Active, haul.State, "haul still under way just before sunset");
            Assert.AreEqual(3, haul.CurrentStep, "loaded and leading to storage");
            Assert.AreEqual(2, w.OxCarrier(w.People.FindOx(50)).CountOf(Items.Log.Id));

            while (haul.State == TaskState.Active && w.Tick < end + 300) w.Step(1);
            Assert.AreEqual(TaskState.Cancelled, haul.State);
            Assert.GreaterOrEqual(w.Tick, end + w.Tuning.OxUnloadMinutes, "unload time applies before the haul ends");

            // New pile, owned by the operation, away from the old one and from the storage; the old pile is empty.
            LedgerLocation dropped = null;
            foreach (LedgerLocation l in w.Inventory.Locations)
                if (l.Kind == LocationKind.GroundPile && l.OwnerOperationId == op.Id && l.Id != oldPile.Id && l.CountOf(Items.Log.Id) > 0) dropped = l;
            Assert.IsNotNull(dropped, "logs dropped as a new pile");
            Assert.AreEqual(2, dropped.CountOf(Items.Log.Id));
            Assert.Greater(dropped.Position.DistanceTo(oldPile.Position), 5000, "not at the old pile");
            Assert.Greater(dropped.Position.DistanceTo(M(110, 100)), 5000, "not at the storage");
            Assert.AreEqual(0, oldPile.CountOf(Items.Log.Id));
            Assert.AreEqual(0, oldPile.ReservedOf(Items.Log.Id));
            foreach (Reservation r in w.Inventory.Reservations) Assert.AreNotEqual(haul.Id, r.Owner, "nothing reserved by the dead haul");
            Assert.AreEqual(0, Store(w).CountOf(Items.Log.Id));

            Ox ox = w.People.FindOx(50);
            Assert.AreNotEqual(OxStateKind.Led, ox.State.Kind, "ox let go (a new haul may already hold it for tomorrow)");
            Assert.IsNull(ox.Follow);
            Assert.That(ox.X, Is.EqualTo(105).Within(0.01));
            Assert.That(ox.Z, Is.EqualTo(104).Within(0.01));
            Assert.AreEqual(0, w.OxCarrier(ox).Total);
            Assert.IsFalse(w.Tasks.IsBusy(10), "worker free to walk home");
            Assert.IsNull(w.Inventory.Audit());

            // Next day a new Haul picks up the dropped pile.
            long nextSunrise = w.WorkDay.WindowOf(w, WorkDay.DayStart(w) + 1440).Sunrise;
            while (w.Tick < nextSunrise + 600 && Store(w).CountOf(Items.Log.Id) < 2) w.Step(1);
            Assert.AreEqual(2, Store(w).CountOf(Items.Log.Id), "dropped logs reach storage the next day");
            Assert.Greater(w.Tick, nextSunrise, "not during the night");
            Assert.AreEqual(0, dropped.CountOf(Items.Log.Id));
            w.Step(150); // the ox walks back to its rest spot
            TaskRecord second = null;
            foreach (TaskRecord t in Hauls(w, op)) if (t.Id != haul.Id && t.State == TaskState.Done) second = t;
            Assert.IsNotNull(second, "a new haul did it");
            Assert.AreEqual(dropped.Id, second.Def.Steps[2].LocationId);
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void SunsetDrop_NothingLoadedYet_OxGoesBackToRest_NoPileCreated()
        {
            SimWorld w = NewWorld(12, 21, 14);
            Operation op = Setup(w, workers: 1, oxX: 105, oxZ: 104);
            LedgerLocation oldPile = w.Inventory.ProduceOnGround(M(100, 195), op.Id, Items.Log, 4);
            long end = w.WorkDay.WindowOf(w, WorkDay.DayStart(w)).End;
            // Start later so the ox is still on its way to the far pile at sunset.
            w.Step((int)(end - w.Tick) - 20);
            TaskRecord haul = Hauls(w, op)[0];
            Assert.AreEqual(TaskState.Active, haul.State);
            Assert.LessOrEqual(haul.CurrentStep, 1, "still walking to the pile");
            Ox ox = w.People.FindOx(50);
            Assert.AreEqual(OxStateKind.Led, ox.State.Kind);
            Assert.Greater(System.Math.Abs(ox.Z - 104), 3, "the ox is away from its rest spot");
            for (int i = 0; i < 300 && haul.State == TaskState.Active; i++) w.Step(1);
            Assert.AreEqual(TaskState.Cancelled, haul.State);
            Assert.AreNotEqual(OxStateKind.Led, ox.State.Kind, "ox let go (a fresh haul may already hold it for tomorrow)");
            Assert.IsNull(ox.Follow);
            Assert.That(ox.X, Is.EqualTo(105).Within(0.01), "back at its rest spot");
            Assert.That(ox.Z, Is.EqualTo(104).Within(0.01));
            Assert.AreEqual(4, oldPile.CountOf(Items.Log.Id), "logs untouched");
            foreach (Reservation r in w.Inventory.Reservations) Assert.AreNotEqual(haul.Id, r.Owner, "nothing reserved by the dead haul");
            int piles = 0;
            foreach (LedgerLocation l in w.Inventory.Locations) if (l.Kind == LocationKind.GroundPile) piles++;
            Assert.AreEqual(1, piles);
            Assert.IsNull(w.Inventory.Audit());
        }
    }
}
