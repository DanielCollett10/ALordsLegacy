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
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Workshops
{
    /// <summary>Multi-input workshops (T-558, decision 0051): one stock per input, a batch needs every input.</summary>
    public class MultiInputWorkshopTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        // Two logs and one beam make three planks. Logs hold 10, beams hold 4 (own capacities).
        private static WorkshopDef TwoInput() => new WorkshopDef("workshop.two",
            new[] { new RecipeInput(Items.Log, 2, CarryClass.Ox, 10), new RecipeInput(Items.Beam, 1, CarryClass.Ox, 4) },
            new[] { new RecipeOutput(Items.Plank, 3) }, 3, 20, 100, 1);

        [SetUp]
        public void SetUp() => OperationTypes.TestDefs = new Dictionary<string, WorkshopDef> { { OperationTypes.HewingId, TwoInput() } };

        [TearDown]
        public void TearDown() => OperationTypes.TestDefs = null;

        private static MapData Map() => new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);

        private static (SimWorld w, int fp, Operation op) Make(int oxen = 0)
        {
            var t = new SimTuning { StartHour = 8, WorkshopDefaultReserve = 0 };
            var w = SimWorld.FromMapData(Map(), t);
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

        [Test]
        public void OnBuilt_CreatesOneStockPerInput_EachWithItsOwnCapacity()
        {
            var (w, fp, _) = Make();
            Assert.AreEqual(2, w.Inventory.WorkshopStocks(fp).Count);
            Assert.AreEqual(10, w.Inventory.FindWorkshopStock(fp, Items.Log.Id).Capacity);
            Assert.AreEqual(4, w.Inventory.FindWorkshopStock(fp, Items.Beam.Id).Capacity);
        }

        [Test]
        public void OnlyOneInputInStock_NoWorkStarts_StatusWaiting()
        {
            var (w, fp, op) = Make();
            w.Inventory.Produce(w.Inventory.FindWorkshopStock(fp, Items.Log.Id), Items.Log, 6);
            w.Step(5);
            Assert.AreEqual(0, WorkshopSystem.OpenWorkCount(w, op));
            Assert.AreEqual(OperationState.WaitingForInput, WorkshopStatus.Of(w, op, TwoInput()));
        }

        [Test]
        public void ShortOfOneInputCount_NoWorkStarts()
        {
            var (w, fp, op) = Make();
            w.Inventory.Produce(w.Inventory.FindWorkshopStock(fp, Items.Log.Id), Items.Log, 1); // needs 2
            w.Inventory.Produce(w.Inventory.FindWorkshopStock(fp, Items.Beam.Id), Items.Beam, 1);
            w.Step(5);
            Assert.AreEqual(0, WorkshopSystem.OpenWorkCount(w, op));
        }

        [Test]
        public void AllInputsPresent_StartReservesEveryInputAndTheOutputRoom()
        {
            var (w, fp, op) = Make();
            LedgerLocation logs = w.Inventory.FindWorkshopStock(fp, Items.Log.Id);
            LedgerLocation beams = w.Inventory.FindWorkshopStock(fp, Items.Beam.Id);
            w.Inventory.Produce(logs, Items.Log, 2);
            w.Inventory.Produce(beams, Items.Beam, 1);
            w.Step(1);
            Assert.AreEqual(1, WorkshopSystem.OpenWorkCount(w, op));
            Assert.AreEqual(0, logs.AvailableOf(Items.Log.Id), "log batch reserved");
            Assert.AreEqual(0, beams.AvailableOf(Items.Beam.Id), "beam batch reserved");
            Assert.AreEqual(100 - 3, w.Inventory.FindGoodsStore(fp).FreeSpace, "room for the batch reserved");
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void StoreWithoutRoomForOneBatch_NoWorkStarts()
        {
            var (w, fp, op) = Make();
            LedgerLocation store = w.Inventory.FindGoodsStore(fp);
            w.Inventory.Produce(store, Items.Plank, store.FreeSpace - 2); // room for 2, a batch makes 3
            w.Inventory.Produce(w.Inventory.FindWorkshopStock(fp, Items.Log.Id), Items.Log, 2);
            w.Inventory.Produce(w.Inventory.FindWorkshopStock(fp, Items.Beam.Id), Items.Beam, 1);
            w.Step(3);
            Assert.AreEqual(0, WorkshopSystem.OpenWorkCount(w, op));
            Assert.AreEqual(OperationState.WorkshopStoreFull, WorkshopStatus.Of(w, op, TwoInput()));
        }

        [Test]
        public void Finish_ConsumesEveryInputBatch_AndProducesOutputs()
        {
            var (w, fp, _) = Make();
            LedgerLocation logs = w.Inventory.FindWorkshopStock(fp, Items.Log.Id);
            LedgerLocation beams = w.Inventory.FindWorkshopStock(fp, Items.Beam.Id);
            w.Inventory.Produce(logs, Items.Log, 2);
            w.Inventory.Produce(beams, Items.Beam, 1);
            for (int i = 0; i < 3000 && w.Inventory.FindGoodsStore(fp).CountOf(Items.Plank.Id) == 0; i++) w.Step(1);
            Assert.AreEqual(3, w.Inventory.FindGoodsStore(fp).CountOf(Items.Plank.Id));
            Assert.AreEqual(0, logs.CountOf(Items.Log.Id));
            Assert.AreEqual(0, beams.CountOf(Items.Beam.Id));
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void Pipeline_OxFetchesBothInputs_FamilyWorks_PlanksInStore()
        {
            var (w, fp, _) = Make(oxen: 1);
            LedgerLocation pile = w.Inventory.AddStockpile(M(62, 62));
            w.Inventory.Produce(pile, Items.Log, 4);
            // Beams come from another building's goods store (a second built station), not a ground pile.
            int other = w.Footprints.Add(101, ModuleTypes.Find(ModuleTypes.HewingStationId), M(70, 60), 0).Id;
            WorkshopBuildings.OnBuilt(w, w.Footprints.Get(other));
            w.Inventory.Produce(w.Inventory.FindGoodsStore(other), Items.Beam, 2);
            LedgerLocation store = w.Inventory.FindGoodsStore(fp);
            for (int i = 0; i < 4 * 1440; i++) { w.Step(1); Assert.IsNull(w.Inventory.Audit()); }
            Assert.Greater(store.CountOf(Items.Plank.Id), 0, "both inputs were fetched and a batch finished: logs " + w.Inventory.FindWorkshopStock(fp, Items.Log.Id).CountOf(Items.Log.Id)
                + " beams " + w.Inventory.FindWorkshopStock(fp, Items.Beam.Id).CountOf(Items.Beam.Id) + " pile " + pile.CountOf(Items.Log.Id) + "/" + pile.CountOf(Items.Beam.Id));
        }

        [Test]
        public void SaveRoundTrip_TwoInputWorkshopMidBatch_FinishesAfterLoad()
        {
            MapData map = Map();
            var (w, fp, op) = Make();
            w.Inventory.Produce(w.Inventory.FindWorkshopStock(fp, Items.Log.Id), Items.Log, 5);
            w.Inventory.Produce(w.Inventory.FindWorkshopStock(fp, Items.Beam.Id), Items.Beam, 2);
            w.Step(10);
            Assert.AreEqual(1, WorkshopSystem.OpenWorkCount(w, op), "mid-batch");
            byte[] bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(bytes, map);
            Assert.AreEqual(2, back.Inventory.WorkshopStocks(fp).Count);
            Assert.AreEqual(4, back.Inventory.FindWorkshopStock(fp, Items.Beam.Id).Capacity);
            Assert.AreEqual(3, back.Inventory.FindWorkshopStock(fp, Items.Log.Id).AvailableOf(Items.Log.Id), "5 logs, 2 reserved");
            Assert.IsNull(back.Inventory.Audit());
            for (int i = 0; i < 3000 && back.Inventory.FindGoodsStore(fp).CountOf(Items.Plank.Id) < 3; i++) back.Step(1);
            Assert.GreaterOrEqual(back.Inventory.FindGoodsStore(fp).CountOf(Items.Plank.Id), 3);
        }
    }
}
