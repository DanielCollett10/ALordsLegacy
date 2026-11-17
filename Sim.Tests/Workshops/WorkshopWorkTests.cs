using System.Linq;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Operations.Workshops;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Workshops
{
    /// <summary>Workshop work: a fixed daily rate per assigned family, one work task at a time (T-524, HW-02, PL-05).</summary>
    public class WorkshopWorkTests
    {
        private const long AdultBirth = -4_000_000;
        private const int Day = 1440;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        // Family 3 has `adults` working-age members. Stock capacities are large so tests can top them up by hand.
        private static (SimWorld w, int fp, Operation op) Make(string type, int adults = 1, int oxen = 0, SimTuning tuning = null)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            SimTuning t = tuning ?? new SimTuning { StartHour = 8 };
            t.HewingStockCapacity = 500; t.SawingStockCapacity = 500;
            var w = SimWorld.FromMapData(map, t);
            w.People.AddFamily(3, "F3", 60, 60);
            for (int i = 0; i < adults; i++) w.People.AddCitizen(30 + i, "C" + i, Sex.Male, AdultBirth, 3, 60, 60);
            for (int i = 0; i < oxen; i++) w.People.AddOx(50 + i, "O" + i, 60, 64).State = OxState.Resting(60000, 64000);
            int id = w.Footprints.Add(100, ModuleTypes.Find(type), M(60, 60), 0).Id;
            Operation op = w.Operations.Create(OperationTypes.WorkshopOperationFor(type), 0);
            w.Operations.AddModule(op.Id, id);
            WorkshopBuildings.OnBuilt(w, w.Footprints.Get(id));
            Assert.IsTrue(w.Execute(new AssignFamily(op.Id, 3)).IsAccepted);
            return (w, id, op);
        }

        // Steps `ticks`, keeping the input stock topped up, and returns what the store holds of the output.
        private static int Run(SimWorld w, int fp, ItemDef input, ItemDef output, int ticks)
        {
            LedgerLocation stock = w.Inventory.FindWorkshopStock(fp);
            for (int i = 0; i < ticks; i++)
            {
                if (stock.CountOf(input.Id) < 50) w.Inventory.Produce(stock, input, 50);
                w.Step(1);
            }
            return w.Inventory.FindGoodsStore(fp).CountOf(output.Id);
        }

        // The shift is a tuning number; the tests set it to the length of the day's daylight window so that "per day"
        // is one real working day. The family also spends time commuting, so a day makes a little under the rate.
        private static void ShiftIsTheDay(SimWorld w)
        {
            WorkDay.Window win = w.WorkDay.WindowOf(w, WorkDay.DayStart(w));
            w.Tuning.WorkshopShiftMinutes = (int)(win.End - win.Sunrise);
        }

        private static (SimWorld w, int fp, Operation op) MakeDay(string type, int adults = 1, int days = 3)
        {
            var r = Make(type, adults);
            ShiftIsTheDay(r.w);
            r.w.Tuning.HewingStoreCapacity = 500; r.w.Tuning.SawingStoreCapacity = 500;
            return r;
        }

        [Test]
        public void Hewing_LogsAvailable_MakesAboutThreeBeamsPerWorkingDay()
        {
            var (w, fp, _) = MakeDay(ModuleTypes.HewingStationId);
            int beams = Run(w, fp, Items.Log, Items.Beam, 4 * Day);
            Assert.That(beams, Is.InRange(4 * 3 - 4, 4 * 3 + 1), "about 3 a day over 4 days, give or take the partial days");
            Assert.AreEqual(beams, w.Inventory.ConsumedOf(Items.Log.Id), "one log used per beam");
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void Hewing_ThreeAdultFamily_MakesNoMoreThanOneAdultFamily()
        {
            var (w1, fp1, _) = MakeDay(ModuleTypes.HewingStationId, adults: 1);
            var (w3, fp3, _) = MakeDay(ModuleTypes.HewingStationId, adults: 3);
            int one = Run(w1, fp1, Items.Log, Items.Beam, 3 * Day);
            int three = Run(w3, fp3, Items.Log, Items.Beam, 3 * Day);
            Assert.Greater(one, 0);
            Assert.LessOrEqual(three, one, "extra adults only fetch; output is per family");
            Assert.Greater(three, 0);
            Assert.AreEqual(1, w3.Tasks.All.Count(t => t.Def.Id == WorkshopSystem.WorkTaskId && !t.IsFinished), "one work task at a time");
        }

        [Test]
        public void Sawing_BeamsAvailable_SawsThreePlanksPerBeam_AndStopsWhenStoreFull()
        {
            var (w, fp, op) = Make(ModuleTypes.SawpitId);
            ShiftIsTheDay(w);
            int planks = Run(w, fp, Items.Beam, Items.Plank, Day);
            Assert.That(planks, Is.InRange(3, 12));
            Assert.AreEqual(0, planks % 3, "3 planks from each beam");
            planks = Run(w, fp, Items.Beam, Items.Plank, 10 * Day);
            Assert.That(planks, Is.InRange(18, 20), "store holds 20; 3 do not fit in the last 2 slots");
            Assert.AreEqual(OperationState.WorkshopStoreFull, OperationStatus.Of(w, op));
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void Statuses_NoWorkers_WaitingForInput_WaitingForOx_StoreFull()
        {
            var (w, fp, op) = Make(ModuleTypes.HewingStationId, adults: 0);
            Assert.AreEqual(OperationState.NoWorkers, OperationStatus.Of(w, op), "no working-age member");
            w.People.AddCitizen(40, "A", Sex.Male, AdultBirth, 3, 60, 60);
            Assert.AreEqual(OperationState.WaitingForInput, OperationStatus.Reported(w, op), "no logs and none anywhere");

            LedgerLocation pile = w.Inventory.AddStockpile(M(20, 20));
            w.Inventory.Produce(pile, Items.Log, 4);
            Assert.AreEqual(OperationState.WaitingForOx, OperationStatus.Reported(w, op), "logs wait, no ox exists");

            w.Inventory.Consume(pile, Items.Log, 4);
            LedgerLocation stock = w.Inventory.FindWorkshopStock(fp);
            w.Inventory.Produce(stock, Items.Log, 2);
            Assert.AreEqual(OperationState.Working, OperationStatus.Reported(w, op));
            LedgerLocation store = w.Inventory.FindGoodsStore(fp);
            w.Inventory.Produce(store, Items.Beam, store.Capacity);
            Assert.AreEqual(OperationState.WorkshopStoreFull, OperationStatus.Reported(w, op));
            w.Step(Day);
            Assert.AreEqual(0, w.Tasks.All.Count(t => t.Def.Id == WorkshopSystem.WorkTaskId), "no work task while the store is full");
        }

        [Test]
        public void Hewing_HalvingTheDailyRate_HalvesTheOutput()
        {
            var (a, fa, _) = MakeDay(ModuleTypes.HewingStationId);
            a.Tuning.HewingLogsPerDay = 6;
            int full = Run(a, fa, Items.Log, Items.Beam, 4 * Day);
            var (b, fb, _) = MakeDay(ModuleTypes.HewingStationId);
            b.Tuning.HewingLogsPerDay = 3;
            int half = Run(b, fb, Items.Log, Items.Beam, 4 * Day);
            Assert.Greater(half, 0);
            Assert.That(full, Is.InRange(2 * half - 2, 2 * half + 2));
        }

        [Test]
        public void Pipeline_LogsOnGround_OxFetches_FamilyHews_BeamsInStore()
        {
            var (w, fp, op) = Make(ModuleTypes.HewingStationId, oxen: 1);
            ShiftIsTheDay(w);
            LedgerLocation pile = w.Inventory.AddStockpile(M(62, 62));
            w.Inventory.Produce(pile, Items.Log, 6);
            LedgerLocation store = w.Inventory.FindGoodsStore(fp);
            for (int i = 0; i < 3 * Day; i++) { w.Step(1); Assert.IsNull(w.Inventory.Audit()); }
            Assert.Greater(store.CountOf(Items.Beam.Id), 0, "logs became beams");
            Assert.AreEqual(6, store.CountOf(Items.Beam.Id) + w.Inventory.FindWorkshopStock(fp).CountOf(Items.Log.Id) + pile.CountOf(Items.Log.Id),
                "no log lost or made up");
        }

        [Test]
        public void SaveRoundTrip_MidWork_KeepsProgress_AndFinishesAfterLoad()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var (w, fp, _) = Make(ModuleTypes.HewingStationId);
            ShiftIsTheDay(w);
            LedgerLocation stock = w.Inventory.FindWorkshopStock(fp);
            w.Inventory.Produce(stock, Items.Log, 4);
            TaskRecord t = null;
            for (int i = 0; i < Day && (t == null || t.StepProgress < 10); i++)
            {
                w.Step(1);
                t = w.Tasks.All.FirstOrDefault(x => x.Def.Id == WorkshopSystem.WorkTaskId && !x.IsFinished);
            }
            Assert.IsNotNull(t);
            Assert.GreaterOrEqual(t.StepProgress, 10, "work progress accrued");
            int progress = t.StepProgress;

            byte[] bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(bytes, map);
            Assert.AreEqual(bytes, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash));
            TaskRecord bt = back.Tasks.Get(t.Id);
            Assert.AreEqual(WorkshopSystem.WorkTaskId, bt.Def.Id);
            Assert.AreEqual(progress, bt.StepProgress);
            Assert.AreEqual(t.Def.Steps[0].DurationTicks, bt.Def.Steps[0].DurationTicks);
            Assert.IsNull(back.Inventory.Audit());
            for (int i = 0; i < 2 * Day; i++) back.Step(1);
            Assert.Greater(back.Inventory.FindGoodsStore(fp).CountOf(Items.Beam.Id), 0, "the loaded task finishes");
            Assert.IsNull(back.Inventory.Audit());
        }

        [Test]
        public void SaveVersion_IsBumpedTo29()
        {
            Assert.GreaterOrEqual(SaveWriter.CurrentVersion, 29);
        }
    }
}
