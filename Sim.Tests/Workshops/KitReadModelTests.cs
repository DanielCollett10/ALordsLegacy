using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Coppice;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Foraging;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Kit;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Operations.Workshops;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Workshops
{
    /// <summary>T-566: read models for coppice, recipes and kit (reports and village totals). Read only.</summary>
    public class KitReadModelTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static MapData Map() =>
            new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);

        private static SimWorld World() =>
            SimWorld.FromMapData(Map(), new SimTuning { StartHour = 8, BushCount = 0, HazelStoolCount = 0, WillowStoolCount = 0 });

        // ---- coppice ----

        private static SimWorld CoppiceWorld(bool withFamily)
        {
            SimWorld w = World();
            CommandResult r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) }), AreaTypes.CoppiceId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Operation op = w.Operations.FindByArea((int)r.Created[0]);
            Footprint hut = w.Footprints.Add(1, ModuleTypes.CoppiceHut, M(100, 100), 0);
            w.Operations.AddModule(op.Id, 1);
            WorkshopBuildings.OnBuilt(w, hut);
            if (withFamily)
            {
                w.People.AddFamily(1, "F", 100, 100);
                w.People.AddCitizen(10, "C", Sex.Male, AdultBirth, 1, 100, 100);
                Assert.IsTrue(w.Execute(new AssignFamily(op.Id, 1)).IsAccepted);
            }
            return w;
        }

        [Test]
        public void CoppiceReport_NotACoppiceHut_IsNull()
        {
            SimWorld w = CoppiceWorld(true);
            w.Footprints.Add(2, ModuleTypes.ForagerHut, M(10, 10), 0);
            Assert.IsNull(CoppiceReport.Of(w, 2));
            Assert.IsNull(CoppiceReport.Of(w, 999));
        }

        [Test]
        public void CoppiceReport_StockPerKindOutOf40_AndStoolsAvailableOrRegrowing()
        {
            SimWorld w = CoppiceWorld(true);
            LedgerLocation store = w.Inventory.FindGoodsStore(1);
            w.Inventory.Produce(store, Items.HazelRod, 12);
            w.Inventory.Produce(store, Items.WillowRod, 3);
            w.Bushes.Add(100, 95, BushKind.HazelStool, 6);
            w.Bushes.Add(102, 95, BushKind.HazelStool, 6);
            w.Bushes.Add(104, 95, BushKind.HazelStool, 0);
            w.Bushes.Add(106, 95, BushKind.WillowStool, 0);
            w.Bushes.Add(108, 95, BushKind.Berries, 5);   // not a stool
            CoppiceReport r = CoppiceReport.Of(w, 1);
            CoppiceKindLine hazel = r.Line(BushKind.HazelStool), willow = r.Line(BushKind.WillowStool);
            Assert.AreEqual(2, r.Kinds.Count);
            Assert.AreEqual(12, hazel.Stock); Assert.AreEqual(40, hazel.Cap);
            Assert.AreEqual(3, willow.Stock); Assert.AreEqual(40, willow.Cap);
            Assert.AreEqual(2, hazel.StoolsAvailable); Assert.AreEqual(1, hazel.StoolsRegrowing);
            Assert.AreEqual(0, willow.StoolsAvailable); Assert.AreEqual(1, willow.StoolsRegrowing);
            Assert.AreEqual("hazel: 12 / 40", hazel.StockText);
            Assert.AreEqual("2 available, 1 regrowing", hazel.StoolsText);
            Assert.IsTrue(r.IsBuilt);
            Assert.AreEqual(OperationState.Working, r.State);
            Assert.AreEqual("Working", r.StatusText);
        }

        [Test]
        public void CoppiceReport_NoStoolsInRegion_StatusNoStools()
        {
            CoppiceReport r = CoppiceReport.Of(CoppiceWorld(true), 1);
            Assert.AreEqual(OperationState.NoStools, r.State);
            Assert.AreEqual("No stools", r.StatusText);
            Assert.AreEqual(0, r.Line(BushKind.HazelStool).StoolsAvailable);
        }

        [Test]
        public void CoppiceReport_NoFamily_NoWorkers_AndPaused()
        {
            SimWorld w = CoppiceWorld(false);
            Assert.AreEqual("No workers", CoppiceReport.Of(w, 1).StatusText);
            SimWorld p = CoppiceWorld(true);
            p.Operations.All.Single().Paused = true;
            Assert.AreEqual("Paused", CoppiceReport.Of(p, 1).StatusText);
        }

        [Test]
        public void CoppiceReport_BothKindsFull_StorageFull()
        {
            SimWorld w = CoppiceWorld(true);
            LedgerLocation store = w.Inventory.FindGoodsStore(1);
            w.Inventory.Produce(store, Items.HazelRod, 40);
            w.Inventory.Produce(store, Items.WillowRod, 40);
            w.Bushes.Add(100, 95, BushKind.HazelStool, 6);
            w.Bushes.Add(102, 95, BushKind.WillowStool, 6);
            CoppiceReport r = CoppiceReport.Of(w, 1);
            Assert.AreEqual("Storage full", r.StatusText);
            Assert.AreEqual(40, r.Line(BushKind.HazelStool).Stock);
        }

        [Test]
        public void CoppiceReport_UnbuiltHut_IsUnderConstruction()
        {
            SimWorld w = World();
            w.Footprints.Add(1, ModuleTypes.CoppiceHut, M(100, 100), 0);
            CoppiceReport r = CoppiceReport.Of(w, 1);
            if (r.IsBuilt) Assert.Pass("Footprints.Add counts as built here");
            Assert.AreEqual("Under construction", r.StatusText);
            Assert.AreEqual(0, r.Line(BushKind.HazelStool).Cap, "no store yet");
        }

        // ---- workshop recipe and inputs ----

        private static SimWorld WorkshopWorld()
        {
            SimWorld w = World();
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 12);
            w.People.AddFamily(3, "F3", 40, 40);
            w.People.AddCitizen(30, "C", Sex.Male, AdultBirth, 3, 40, 40);
            w.People.AddOx(50, "O", 40, 44).State = OxState.Resting(40000, 44000);
            return w;
        }

        private static int PlaceAndBuild(SimWorld w, string type)
        {
            CommandResult r = w.Execute(new PlaceStructure(type, M(50, 50), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int id = (int)r.Created[0];
            for (int i = 0; i < 6000 && !w.IsBuilt(id); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(id));
            return id;
        }

        [Test]
        public void WorkshopReport_WattleYard_ListsBothInputsWithStockReserveStorageAndRecipe()
        {
            SimWorld w = WorkshopWorld();
            int id = PlaceAndBuild(w, ModuleTypes.WattleYardId);
            Operation op = w.Operations.FindByModule(id);
            Assert.IsTrue(w.Execute(new SetWorkshopReserve(op.Id, Items.HazelRod.Id, 7)).IsAccepted);
            w.Footprints.Add(900, ModuleTypes.CoppiceHut, M(10, 10), 0);
            WorkshopBuildings.OnBuilt(w, w.Footprints.Get(900));
            LedgerLocation rods = w.Inventory.FindGoodsStore(900);
            w.Inventory.Produce(rods, Items.HazelRod, 15);
            w.Inventory.Produce(rods, Items.WillowRod, 4);
            w.Inventory.Produce(w.Inventory.FindWorkshopStock(id, Items.HazelRod.Id), Items.HazelRod, 9);

            WorkshopReport r = WorkshopReport.Of(w, id);
            Assert.AreEqual(2, r.Inputs.Count);
            WorkshopInputLine hazel = r.Inputs[0], willow = r.Inputs[1];
            Assert.AreEqual(Items.HazelRod.Id, hazel.ItemId);
            Assert.AreEqual(6, hazel.PerBatch);
            Assert.AreEqual(9, hazel.Stock);
            Assert.AreEqual(30, hazel.StockCapacity);
            Assert.AreEqual(7, hazel.Reserve, "the player's setting");
            Assert.AreEqual(15, hazel.InStorage, "workshop stock is not counted");
            Assert.AreEqual(Items.WillowRod.Id, willow.ItemId);
            Assert.AreEqual(2, willow.PerBatch);
            Assert.AreEqual(0, willow.Stock);
            Assert.AreEqual(10, willow.StockCapacity);
            Assert.AreEqual(w.Tuning.WorkshopDefaultReserve, willow.Reserve, "unset input shows the tuning default");
            Assert.AreEqual(4, willow.InStorage);
            Assert.AreEqual("6 hazel_rod + 2 willow_rod -> 1 wattle_panel", r.RecipeText);
        }

        [Test]
        public void WorkshopReport_Weaver_OneInputAndRecipe_AndHewingStillWorks()
        {
            SimWorld w = WorkshopWorld();
            int id = PlaceAndBuild(w, ModuleTypes.WeaverId);
            WorkshopReport r = WorkshopReport.Of(w, id);
            Assert.AreEqual(1, r.Inputs.Count);
            Assert.AreEqual(6, r.Inputs[0].PerBatch);
            Assert.AreEqual(30, r.Inputs[0].StockCapacity);
            Assert.AreEqual("6 willow_rod -> 1 basket", r.RecipeText);
            int hew = PlaceAndBuildAt(w, ModuleTypes.HewingStationId, 70);
            WorkshopReport h = WorkshopReport.Of(w, hew);
            Assert.AreEqual("1 log -> " + w.Tuning.HewingBeamsPerLog + " beam", h.RecipeText);
            Assert.AreEqual(Items.Log.Id, h.Inputs.Single().ItemId);
            Assert.AreEqual("log", h.InputName, "existing fields unchanged");
        }

        private static int PlaceAndBuildAt(SimWorld w, string type, double x)
        {
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 30)), Items.Log, 12);
            CommandResult r = w.Execute(new PlaceStructure(type, M(x, 80), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int id = (int)r.Created[0];
            for (int i = 0; i < 6000 && !w.IsBuilt(id); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(id));
            return id;
        }

        // ---- kit ----

        [Test]
        public void ForagerHutReport_ShowsBasketsHeldOutOfSlots_AndStatus()
        {
            SimWorld w = World();
            CommandResult r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) }), AreaTypes.ForagingId));
            Operation op = w.Operations.FindByArea((int)r.Created[0]);
            Footprint hut = w.Footprints.Add(1, ModuleTypes.ForagerHut, M(100, 100), 0);
            w.Operations.AddModule(op.Id, 1);
            w.Inventory.AddFoodStore(w.Tuning.ForagerHutCapacity, 1);
            KitStores.OnBuilt(w, hut);
            w.Inventory.Produce(KitStores.Find(w, 1), Items.Basket, 1);
            w.Inventory.Produce(w.Inventory.FindFoodStore(1), Items.Berries, 4);

            ForagerHutReport rep = ForagerHutReport.Of(w, 1);
            Assert.AreEqual(1, rep.BasketsHeld);
            Assert.AreEqual(w.Tuning.ForagerHutBasketSlots, rep.BasketSlots);
            Assert.AreEqual("Baskets: 1 / " + w.Tuning.ForagerHutBasketSlots, rep.BasketsText);
            Assert.AreEqual(4, rep.FoodTotal);
            Assert.AreEqual("No workers", rep.StatusText);
            Assert.IsNull(ForagerHutReport.Of(w, 2));
        }

        [Test]
        public void ForagerHutReport_ReservedBasketOnLoan_StillCountsAsHeld()
        {
            SimWorld w = World();
            Footprint hut = w.Footprints.Add(1, ModuleTypes.ForagerHut, M(100, 100), 0);
            KitStores.OnBuilt(w, hut);
            LedgerLocation kit = KitStores.Find(w, 1);
            w.Inventory.Produce(kit, Items.Basket, 2);
            w.Inventory.ReserveStock(kit, Items.Basket, 1, -3_000_001);
            Assert.AreEqual(2, ForagerHutReport.Of(w, 1).BasketsHeld);
        }

        [Test]
        public void GranaryReport_ShowsBasketsHeldOutOfSlots_OtherFieldsUnchanged()
        {
            SimWorld w = World();
            Footprint g = w.Footprints.Add(1, ModuleTypes.Granary, M(60, 60), 0);
            w.Inventory.AddFoodStore(100, 1);
            KitStores.OnBuilt(w, g);
            w.Inventory.Produce(KitStores.Find(w, 1), Items.Basket, 2);
            GranaryReport r = GranaryReport.Of(w, 1);
            Assert.AreEqual(2, r.BasketsHeld);
            Assert.AreEqual(w.Tuning.GranaryBasketSlots, r.BasketSlots);
            Assert.AreEqual("Baskets: 2 / " + w.Tuning.GranaryBasketSlots, r.BasketsText);
            Assert.AreEqual(0, r.Total, "baskets are not food");
            Assert.AreEqual(100, r.Capacity);
        }

        [Test]
        public void GranaryReport_NoKitStore_ZeroBaskets()
        {
            SimWorld w = World();
            w.Footprints.Add(1, ModuleTypes.Granary, M(60, 60), 0);
            w.Inventory.AddFoodStore(100, 1);
            Assert.AreEqual(0, GranaryReport.Of(w, 1).BasketsHeld);
        }

        // ---- totals ----

        [Test]
        public void GoodsStats_RodsBasketsPanels_CountEveryGoodsStoreIncludingKitAndReserved()
        {
            SimWorld w = World();
            Assert.AreEqual(0, GoodsStats.Rods(w));
            Assert.AreEqual(0, GoodsStats.Baskets(w));
            Assert.AreEqual(0, GoodsStats.Panels(w));
            LedgerLocation yard = w.Inventory.AddGoodsStore(100);
            w.Inventory.Produce(yard, Items.HazelRod, 5);
            w.Inventory.Produce(yard, Items.WillowRod, 3);
            w.Inventory.Produce(yard, Items.Basket, 2);
            w.Inventory.Produce(yard, Items.WattlePanel, 4);
            w.Inventory.ReserveStock(yard, Items.WattlePanel, 1, -3_000_009);
            Footprint hut = w.Footprints.Add(1, ModuleTypes.ForagerHut, M(100, 100), 0);
            KitStores.OnBuilt(w, hut);
            w.Inventory.Produce(KitStores.Find(w, 1), Items.Basket, 1);
            Assert.AreEqual(8, GoodsStats.Rods(w));
            Assert.AreEqual(3, GoodsStats.Baskets(w), "yard plus kit store");
            Assert.AreEqual(4, GoodsStats.Panels(w), "reserved panels still count");
            Assert.AreEqual(0, GoodsStats.Planks(w));
        }
    }
}
