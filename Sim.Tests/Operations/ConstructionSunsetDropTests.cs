using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
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
    /// <summary>Deliveries cut short at sunset leave a site-owned pile that stays the site's (T-348, CN-25b).</summary>
    public class ConstructionSunsetDropTests
    {
        private const long AdultBirth = -4_000_000;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private sealed class Ctx
        {
            public SimWorld W;
            public Operation Op;
            public LedgerLocation Stock;
            public int SiteId;
            public ConstructionSite Site => W.Construction.Get(SiteId);
        }

        // Dec 21 (sunrise 08:00, sunset 16:00). 8 logs at a far stockpile (100,190), hut site at (110,100).
        private static Ctx Build(int startMinuteOfDay)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning
            {
                StartMonth = 12, StartDay = 21, StartHour = startMinuteOfDay / 60, StartMinute = startMinuteOfDay % 60,
                StartWeekday = Weekday.Monday, OxLoadMinutes = 2, OxUnloadMinutes = 3,
            });
            CommandResult r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) }), AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            var c = new Ctx { W = w, Op = w.Operations.FindByArea((int)r.Created[0]) };
            c.Stock = w.Inventory.AddStockpile(M(100, 190));
            w.Inventory.Produce(c.Stock, Items.Log, 8);
            w.People.AddFamily(2, "Helpers", 100, 100);
            w.People.AddCitizen(20, "H", Sex.Male, AdultBirth, 2, 100, 100);
            Ox ox = w.People.AddOx(50, "O", 100, 104);
            ox.State = OxState.Resting(100000, 104000);
            CommandResult p = w.Execute(new PlaceModule(c.Op.Id, ModuleTypes.WoodcuttingHutId, M(110, 100), 0));
            Assert.IsTrue(p.IsAccepted, p.Text);
            c.SiteId = (int)p.Created[0];
            return c;
        }

        private static List<TaskRecord> Delivers(SimWorld w) =>
            w.Tasks.All.Where(t => t.Def.Id == ConstructionTaskDefs.DeliverId).ToList();

        private static int ReservedStock(SimWorld w) =>
            w.Inventory.Reservations.Where(r => r.Kind == ReservationKind.Stock).Sum(r => r.Count);

        private static long SunsetTick(SimWorld w) => w.WorkDay.WindowOf(w, WorkDay.DayStart(w)).End;

        private static bool Carrying(SimWorld w)
        {
            Ox ox = w.People.FindOx(50);
            return ox != null && w.OxCarrier(ox).CountOf(Items.Log.Id) > 0;
        }

        // Probe: minutes from the start until an ox first carries logs.
        private static int MinutesUntilCarrying()
        {
            Ctx p = Build(12 * 60);
            for (int i = 0; i < 2000; i++)
            {
                p.W.Step(1);
                if (Carrying(p.W)) return i + 1;
            }
            Assert.Fail("never carried");
            return 0;
        }

        // A world where the first delivery is carrying logs a few minutes before sunset, then stepped to sunset - 1.
        private static Ctx CarryingNearSunset()
        {
            int d = MinutesUntilCarrying();
            Ctx c = Build(16 * 60 - d - 1);
            for (int i = 0; i < 2000 && !Carrying(c.W); i++) c.W.Step(1);
            Assert.IsTrue(Carrying(c.W), "carrying");
            Assert.Less(c.W.Tick, SunsetTick(c.W), "still before sunset");
            Assert.IsNull(c.W.Inventory.Audit());
            return c;
        }

        private static LedgerLocation SitePile(Ctx c) => c.W.Inventory.Locations.FirstOrDefault(l =>
            l.Kind == LocationKind.GroundPile && l.OwnerOperationId == c.SiteId);

        [Test]
        public void SunsetDrop_CarriedLogsGoToSiteOwnedPile_ReservedForTheSite_TotalReservedUnchanged_AuditClean()
        {
            Ctx c = CarryingNearSunset();
            Ox ox = c.W.People.FindOx(50);
            int carried = c.W.OxCarrier(ox).CountOf(Items.Log.Id);
            TaskRecord task = Delivers(c.W).First(t => t.State == TaskState.Active);
            // Logs on the ox are no longer a stock reservation (consumed at load), so count them as the site's reserved.
            int reservedBefore = ReservedStock(c.W) + carried;
            Assert.IsNull(SitePile(c));

            int guard = 0;
            while (SitePile(c) == null && guard++ < 200)
            {
                c.W.Step(1);
                Assert.IsNull(c.W.Inventory.Audit(), "audit each tick");
            }
            LedgerLocation pile = SitePile(c);
            Assert.IsNotNull(pile, "pile dropped");
            Assert.AreEqual(carried, pile.CountOf(Items.Log.Id));
            Assert.Less(c.W.Tick - SunsetTick(c.W), 10, "dropped after OxUnloadMinutes, not later");
            Assert.AreEqual(0, c.W.OxCarrier(ox).CountOf(Items.Log.Id));
            Assert.AreEqual(pile.ReservedOf(Items.Log.Id), carried, "pile reserved for the site");
            // The reservation stays on the pile: owned by the site, or already handed to the Deliver opened in the same tick.
            Assert.IsTrue(c.W.Inventory.Reservations.Any(r => r.LocationId == pile.Id && r.Count == carried &&
                (r.Owner == c.SiteId || Delivers(c.W).Any(t => t.Id == r.Owner && t.Def.Steps[2].LocationId == pile.Id))));
            Assert.AreEqual(reservedBefore, ReservedStock(c.W), "total reserved unchanged");
            Assert.AreEqual(0, c.W.Inventory.Reservations.Count(r => r.Owner == task.Id), "carrier/task reservations released");

            for (int i = 0; i < 100; i++) c.W.Step(1);
            Assert.AreEqual(TaskState.Cancelled, c.W.Tasks.Get(task.Id).State);
            Assert.IsNull(ox.Follow, "ox returned (it may already be reserved for the new Deliver)");
            Assert.IsFalse(ox.State.Kind == OxStateKind.Reserved && ox.State.TaskId == task.Id);
            Assert.IsFalse(ox.State.Kind == OxStateKind.Led);
            Assert.AreEqual(8, ReservedStock(c.W), "whole recipe still reserved");
            Assert.IsNull(c.W.Inventory.Audit());
        }

        [Test]
        public void SiteOwnedPile_IsNotAConstructionSource()
        {
            Ctx c = CarryingNearSunset();
            for (int i = 0; i < 20 && SitePile(c) == null; i++) c.W.Step(1);
            LedgerLocation pile = SitePile(c);
            Assert.IsNotNull(pile);
            int before = ConstructionSources.Available(c.W, Items.Log);
            Assert.AreEqual(c.Stock.AvailableOf(Items.Log.Id), before, "only the stockpile counts");
            c.W.Inventory.Produce(c.Stock, Items.Log, 1);
            Assert.AreEqual(before + 1, ConstructionSources.Available(c.W, Items.Log));
            Assert.AreEqual(0, pile.AvailableOf(Items.Log.Id));
        }

        [Test]
        public void NextMorning_DeliverOpensFromTheSitesOwnPileFirst_AndAuditStaysCleanToTheEnd()
        {
            Ctx c = CarryingNearSunset();
            for (int i = 0; i < 20 && SitePile(c) == null; i++) c.W.Step(1);
            LedgerLocation pile = SitePile(c);
            Assert.IsNotNull(pile);
            int onPile = pile.CountOf(Items.Log.Id);
            int oldTaskId = Delivers(c.W).Where(t => t.Def.Steps[2].LocationId != pile.Id).Max(t => t.Id);

            // The first Deliver opened after the drop is the one for the pile (own piles before other sources).
            TaskRecord first = Delivers(c.W).First(t => t.Id > oldTaskId);
            Assert.AreEqual(pile.Id, first.Def.Steps[2].LocationId);
            Assert.AreEqual(onPile, first.Def.Steps[2].Count);

            for (int i = 0; i < 3 * 24 * 60 && ConstructionProgress.Of(c.W, c.SiteId).LogsDelivered < 8; i++)
            {
                c.W.Step(1);
                Assert.IsNull(c.W.Inventory.Audit(), "audit tick " + i);
            }
            for (int i = 0; i < 200; i++) { c.W.Step(1); Assert.IsNull(c.W.Inventory.Audit()); }
            Assert.AreEqual(TaskState.Done, c.W.Tasks.Get(first.Id).State, "picked up next day");
            Assert.AreEqual(0, pile.CountOf(Items.Log.Id));
            Assert.Greater(c.W.Tick, SunsetTick(c.W) - 1);
            Assert.IsNull(c.W.Inventory.Audit());
        }
    }
}
