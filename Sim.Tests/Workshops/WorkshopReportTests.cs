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
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Workshops
{
    /// <summary>WorkshopReport and GoodsStats: what panels and the top bar read (T-525).</summary>
    public class WorkshopReportTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static (SimWorld w, int fp, Operation op) Make(string type, bool assign = true)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
            w.People.AddFamily(3, "Smith", 60, 60);
            w.People.AddCitizen(30, "C", Sex.Male, AdultBirth, 3, 60, 60);
            int id = w.Footprints.Add(100, ModuleTypes.Find(type), M(60, 60), 0).Id;
            Operation op = w.Operations.Create(OperationTypes.WorkshopOperationFor(type), 0);
            w.Operations.AddModule(op.Id, id);
            WorkshopBuildings.OnBuilt(w, w.Footprints.Get(id));
            if (assign) Assert.IsTrue(w.Execute(new AssignFamily(op.Id, 3)).IsAccepted);
            return (w, id, op);
        }

        [Test]
        public void Report_NotAWorkshop_IsNull()
        {
            var (w, _, _) = Make(ModuleTypes.SawpitId);
            Assert.IsNull(WorkshopReport.Of(w, 9999));
            Assert.IsNull(WorkshopReport.Of(w, w.Footprints.Add(101, ModuleTypes.Granary, M(20, 20), 0).Id));
            Assert.IsNull(WorkshopReport.ConfirmText(w, ModuleTypes.GranaryId));
        }

        [Test]
        public void Report_Hewing_NamesStockStoreFamilyAndStatus()
        {
            var (w, fp, _) = Make(ModuleTypes.HewingStationId);
            w.Inventory.Produce(w.Inventory.FindWorkshopStock(fp), Items.Log, 4);
            w.Inventory.Produce(w.Inventory.FindGoodsStore(fp), Items.Beam, 7);
            WorkshopReport r = WorkshopReport.Of(w, fp);
            Assert.AreEqual("Hewing station", r.Name);
            Assert.IsTrue(r.IsBuilt);
            Assert.AreEqual(4, r.InputCount);
            Assert.AreEqual(w.Tuning.HewingStockCapacity, r.InputCapacity);
            Assert.AreEqual(7, r.OutputCount);
            Assert.AreEqual(w.Tuning.HewingStoreCapacity, r.OutputCapacity);
            Assert.AreEqual(3, r.FamilyId);
            Assert.AreEqual("Smith", r.FamilyName);
            Assert.AreEqual("Working", r.StatusText);
            StringAssert.StartsWith("Input: 4 / ", r.InputText);
            StringAssert.EndsWith(" logs", r.InputText);
            StringAssert.StartsWith("Output: 7 / ", r.OutputText);
            StringAssert.EndsWith(" beams", r.OutputText);
        }

        [Test]
        public void Report_Statuses_NoWorkers_WaitingForInput_StoreFull_Paused()
        {
            var (w, fp, op) = Make(ModuleTypes.SawpitId, assign: false);
            Assert.AreEqual("No workers", WorkshopReport.Of(w, fp).StatusText);
            Assert.IsNull(WorkshopReport.Of(w, fp).FamilyName);
            Assert.IsTrue(w.Execute(new AssignFamily(op.Id, 3)).IsAccepted);
            Assert.AreEqual("Waiting for beams", WorkshopReport.Of(w, fp).StatusText);
            LedgerLocation store = w.Inventory.FindGoodsStore(fp);
            w.Inventory.Produce(store, Items.Plank, store.Capacity);
            Assert.AreEqual("Store full", WorkshopReport.Of(w, fp).StatusText);
            op.Paused = true;
            Assert.AreEqual("Paused", WorkshopReport.Of(w, fp).StatusText);
        }

        [Test]
        public void Report_UnbuiltWorkshop_SaysUnderConstruction()
        {
            var (w, _, _) = Make(ModuleTypes.SawpitId);
            w.Inventory.AddStockpile(M(20, 20));
            w.Inventory.Produce(w.Inventory.AddStockpile(M(21, 21)), Items.Log, 6);
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.HewingStationId, M(30, 30), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            WorkshopReport rep = WorkshopReport.Of(w, (int)r.Created[0]);
            Assert.IsFalse(rep.IsBuilt);
            Assert.AreEqual("Under construction", rep.StatusText);
        }

        [Test]
        public void ConfirmText_NamesCostAndRates()
        {
            var (w, _, _) = Make(ModuleTypes.SawpitId);
            Assert.AreEqual("Build a hewing station?\nCosts 6 logs.\nTurns 3 logs a day into 3 beams. One family works it.",
                WorkshopReport.ConfirmText(w, ModuleTypes.HewingStationId));
            Assert.AreEqual("Build a sawpit?\nCosts 6 logs.\nTurns 3 beams a day into 9 planks. One family works it.",
                WorkshopReport.ConfirmText(w, ModuleTypes.SawpitId));
        }

        // ---- GoodsStats ----

        [Test]
        public void GoodsStats_ReservedLog_StillCounts()
        {
            var (w, _, _) = Make(ModuleTypes.SawpitId);
            LedgerLocation pile = w.Inventory.AddStockpile(M(20, 20));
            w.Inventory.Produce(pile, Items.Log, 5);
            Assert.AreEqual(5, GoodsStats.Logs(w));
            Assert.AreEqual(2, w.Inventory.ReserveStock(pile, Items.Log, 2, 77) == 0 ? 0 : 2);
            Assert.AreEqual(3, pile.AvailableOf(Items.Log.Id));
            Assert.AreEqual(5, GoodsStats.Logs(w), "reserved stock is still in the village (decision 0046)");
        }

        [Test]
        public void GoodsStats_LogOnAnOx_AndInAWorkshopStock_AreNotCounted()
        {
            var (w, fp, _) = Make(ModuleTypes.HewingStationId);
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 5);
            w.Inventory.Produce(w.Inventory.FindWorkshopStock(fp), Items.Log, 4);
            LedgerLocation carrier = w.Inventory.AddCarrier(10);
            w.Inventory.Produce(carrier, Items.Log, 2);
            Assert.AreEqual(5, GoodsStats.Logs(w), "5 in the stockpile only");
        }

        [Test]
        public void GoodsStats_LogSources_BuiltStorageAndUnownedPilesCount_OperationPileDoesNot()
        {
            var (w, _, op) = Make(ModuleTypes.HewingStationId);
            w.Inventory.Produce(w.Inventory.GetOrAddGroundPile(M(10, 10), null), Items.Log, 3);
            w.Inventory.Produce(w.Inventory.GetOrAddGroundPile(M(30, 30), op.Id), Items.Log, 4);
            Assert.AreEqual(3, GoodsStats.Logs(w));
        }

        [Test]
        public void GoodsStats_BeamsAndPlanks_CountEveryGoodsStore_NotWorkshopStock()
        {
            var (hw, hewing, _) = Make(ModuleTypes.HewingStationId);
            hw.Inventory.Produce(hw.Inventory.FindGoodsStore(hewing), Items.Beam, 5);
            hw.Inventory.Produce(hw.Inventory.AddGoodsStore(M(10, 10)), Items.Beam, 2);
            hw.Inventory.Produce(hw.Inventory.AddGoodsStore(M(11, 11)), Items.Plank, 10);
            LedgerLocation goods = hw.Inventory.Locations.First(l => l.Kind == LocationKind.GoodsStore && l.Position.X != 0);
            hw.Inventory.ReserveStock(goods, Items.Beam, 1, 5);
            Assert.AreEqual(7, GoodsStats.Beams(hw), "reserved beam still counts");
            Assert.AreEqual(10, GoodsStats.Planks(hw));

            var (sw, sawpit, _) = Make(ModuleTypes.SawpitId);
            sw.Inventory.Produce(sw.Inventory.FindWorkshopStock(sawpit), Items.Beam, 6);
            Assert.AreEqual(0, GoodsStats.Beams(sw), "beams waiting in a sawpit's stock are committed, not village stock");
            sw.Inventory.Produce(sw.Inventory.FindGoodsStore(sawpit), Items.Plank, 9);
            Assert.AreEqual(9, GoodsStats.Planks(sw));
        }
    }
}
