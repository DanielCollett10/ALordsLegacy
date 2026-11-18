using System.Collections.Generic;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Operations.Workshops;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Workshops
{
    /// <summary>T-559, decision 0051, PL-30: shortest-first fetch, per-input reserve, zero-input workshops, status text.</summary>
    public class WorkshopFetchChoiceTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        // Logs hold 10, beams hold 4; both fetched by ox, one item per trip.
        private static WorkshopDef TwoInput() => new WorkshopDef("workshop.two",
            new[] { new RecipeInput(Items.Log, 2, CarryClass.Ox, 10), new RecipeInput(Items.Beam, 1, CarryClass.Ox, 4) },
            new[] { new RecipeOutput(Items.Plank, 3) }, 3, 20, 100, 1);

        private static WorkshopDef ZeroInput() => new WorkshopDef("workshop.zero",
            new RecipeInput[0], new[] { new RecipeOutput(Items.Plank, 2) }, 4, 10, 6, 1);

        [TearDown]
        public void TearDown() => OperationTypes.TestDefs = null;

        private static (SimWorld w, int fp, Operation op) Make(WorkshopDef def, int oxen)
        {
            OperationTypes.TestDefs = new Dictionary<string, WorkshopDef> { { OperationTypes.HewingId, def } };
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8, WorkshopDefaultReserve = 0 });
            w.People.AddFamily(3, "F3", 60, 60);
            w.People.AddCitizen(30, "C", Sex.Male, AdultBirth, 3, 60, 60);
            for (int i = 0; i < oxen; i++) w.People.AddOx(50 + i, "O" + i, 60, 64).State = OxState.Resting(60000, 64000);
            int id = w.Footprints.Add(100, ModuleTypes.Find(ModuleTypes.HewingStationId), M(60, 60), 0).Id;
            Operation op = w.Operations.Create(OperationTypes.HewingId, 0);
            w.Operations.AddModule(op.Id, id);
            WorkshopBuildings.OnBuilt(w, w.Footprints.Get(id));
            Assert.IsTrue(w.Execute(new AssignFamily(op.Id, 3)).IsAccepted);
            return (w, id, op);
        }

        // Logs on a stockpile and beams in another station's store, so both inputs have a source.
        private static void AddSources(SimWorld w, int logs, int beams)
        {
            w.Inventory.Produce(w.Inventory.AddStockpile(M(62, 62)), Items.Log, logs);
            int other = w.Footprints.Add(101, ModuleTypes.Find(ModuleTypes.HewingStationId), M(70, 60), 0).Id;
            WorkshopBuildings.OnBuilt(w, w.Footprints.Get(other));
            w.Inventory.Produce(w.Inventory.FindGoodsStore(other), Items.Beam, beams);
        }

        private static void Stock(SimWorld w, int fp, ItemDef item, int n) =>
            w.Inventory.Produce(w.Inventory.FindWorkshopStock(fp, item.Id), item, n);

        [Test]
        public void Fetch_PicksInputWithLowestShare_BeamsWhenBeamsShorter()
        {
            var (w, fp, _) = Make(TwoInput(), 1);
            AddSources(w, 20, 20);
            Stock(w, fp, Items.Log, 5); // 5 / 10 = 0.5
            Stock(w, fp, Items.Beam, 1); // 1 / 4 = 0.25
            w.Step(1);
            Assert.AreEqual(1, InputReserve.OnTheWay(w, Items.Beam));
            Assert.AreEqual(0, InputReserve.OnTheWay(w, Items.Log));
        }

        [Test]
        public void Fetch_PicksInputWithLowestShare_LogsWhenLogsShorter()
        {
            var (w, fp, _) = Make(TwoInput(), 1);
            AddSources(w, 20, 20);
            Stock(w, fp, Items.Log, 1); // 0.1
            Stock(w, fp, Items.Beam, 3); // 0.75
            w.Step(1);
            Assert.AreEqual(1, InputReserve.OnTheWay(w, Items.Log));
            Assert.AreEqual(0, InputReserve.OnTheWay(w, Items.Beam));
        }

        [Test]
        public void Fetch_EqualShares_KeepsRecipeOrder()
        {
            var (w, fp, _) = Make(TwoInput(), 1);
            AddSources(w, 20, 20);
            w.Step(1);
            Assert.AreEqual(1, InputReserve.OnTheWay(w, Items.Log), "both empty: the first input goes first");
            Assert.AreEqual(0, InputReserve.OnTheWay(w, Items.Beam));
        }

        [Test]
        public void Fetch_SkipsInputBlockedByItsOwnReserve()
        {
            var (w, fp, op) = Make(TwoInput(), 1);
            AddSources(w, 20, 20);
            Stock(w, fp, Items.Log, 5);
            Stock(w, fp, Items.Beam, 1); // beams are shorter...
            Assert.IsTrue(w.Execute(new SetWorkshopReserve(op.Id, Items.Beam.Id, 100)).IsAccepted); // ...but their reserve blocks them
            w.Step(1);
            Assert.AreEqual(0, InputReserve.OnTheWay(w, Items.Beam));
            Assert.AreEqual(1, InputReserve.OnTheWay(w, Items.Log), "logs have their own reserve (0) and are fetched");
        }

        [Test]
        public void Fetch_NothingFetchedWhenEveryInputIsBlockedByReserve()
        {
            var (w, _, op) = Make(TwoInput(), 1);
            AddSources(w, 20, 20);
            Assert.IsTrue(w.Execute(new SetWorkshopReserve(op.Id, Items.Beam.Id, 100)).IsAccepted);
            Assert.IsTrue(w.Execute(new SetWorkshopReserve(op.Id, Items.Log.Id, 100)).IsAccepted);
            w.Step(3);
            Assert.AreEqual(0, InputReserve.OnTheWay(w, Items.Beam) + InputReserve.OnTheWay(w, Items.Log));
        }

        [Test]
        public void ZeroInputWorkshop_MakesOutputUntilStoreFull_NeverFetches()
        {
            var (w, fp, op) = Make(ZeroInput(), 1);
            AddSources(w, 20, 3);
            LedgerLocation store = w.Inventory.FindGoodsStore(fp);
            Assert.AreEqual(OperationState.Working, WorkshopStatus.Of(w, op, ZeroInput()));
            for (int i = 0; i < 6 * 1440 && store.CountOf(Items.Plank.Id) < 6; i++)
            {
                w.Step(1);
                Assert.AreEqual(0, InputReserve.OnTheWay(w, Items.Log) + InputReserve.OnTheWay(w, Items.Beam));
                Assert.IsNull(w.Inventory.Audit());
            }
            Assert.AreEqual(6, store.CountOf(Items.Plank.Id), "filled to capacity in batches of 2");
            Assert.AreEqual(0, WorkshopSystem.OpenWorkCount(w, op));
            Assert.AreEqual(0, w.Inventory.WorkshopStocks(fp).Count, "no input stock");
            Assert.AreEqual("Store full", WorkshopReport.Of(w, fp).StatusText);
        }

        [Test]
        public void ZeroInputWorkshop_StatusIsOnlyWorkingNoWorkersOrStoreFull()
        {
            var (w, fp, op) = Make(ZeroInput(), 0);
            Assert.AreEqual("Working", WorkshopReport.Of(w, fp).StatusText);
            w.Execute(new UnassignFamily(op.Id, 3));
            Assert.AreEqual("No workers", WorkshopReport.Of(w, fp).StatusText);
        }

        [Test]
        public void Status_WaitingNamesFirstMissingInput()
        {
            var (w, fp, _) = Make(TwoInput(), 0);
            Assert.AreEqual("Waiting for logs", WorkshopReport.Of(w, fp).StatusText, "both missing: first in recipe order");
            Stock(w, fp, Items.Log, 2);
            Assert.AreEqual("Waiting for beams", WorkshopReport.Of(w, fp).StatusText);
        }

        [Test]
        public void Status_ReserveBlock_NamesTheItemItWaitsOn()
        {
            var (w, fp, op) = Make(TwoInput(), 1);
            w.Inventory.Produce(w.Inventory.AddStockpile(M(62, 62)), Items.Log, 3);
            Assert.IsTrue(w.Execute(new SetWorkshopReserve(op.Id, Items.Log.Id, 10)).IsAccepted);
            w.Step(2);
            Assert.AreEqual("Waiting: 3 / 10 logs in storage", WorkshopReport.Of(w, fp).StatusText);

            // Logs fine, beams missing and blocked by the beam reserve: the status names beams.
            Stock(w, fp, Items.Log, 2);
            int other = w.Footprints.Add(101, ModuleTypes.Find(ModuleTypes.HewingStationId), M(70, 60), 0).Id;
            WorkshopBuildings.OnBuilt(w, w.Footprints.Get(other));
            w.Inventory.Produce(w.Inventory.FindGoodsStore(other), Items.Beam, 2);
            Assert.IsTrue(w.Execute(new SetWorkshopReserve(op.Id, Items.Beam.Id, 5)).IsAccepted);
            Assert.AreEqual("Waiting: 2 / 5 beams in storage", WorkshopReport.Of(w, fp).StatusText);
        }
    }
}
