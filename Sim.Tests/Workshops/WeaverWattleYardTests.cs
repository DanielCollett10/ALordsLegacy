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
    /// <summary>The weaver and the wattle yard as data, placed, built, worked and deconstructed (T-561, MAT-20..24, PL-30b).</summary>
    public class WeaverWattleYardTests
    {
        private const long AdultBirth = -4_000_000;
        private const int Day = 1440;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld Make(int logs)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
            if (logs > 0) w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, logs);
            w.People.AddFamily(3, "F3", 40, 40);
            for (int i = 0; i < 2; i++) w.People.AddCitizen(30 + i, "C" + i, Sex.Male, AdultBirth, 3, 40, 40);
            w.People.AddOx(50, "O", 40, 44).State = OxState.Resting(40000, 44000);
            return w;
        }

        private static int PlaceAndBuild(SimWorld w, string type)
        {
            CommandResult r = w.Execute(new PlaceStructure(type, M(50, 50), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int id = (int)r.Created[0];
            for (int i = 0; i < 6000 && !w.IsBuilt(id); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(id), "built by workers through Sim ticks only");
            return id;
        }

        [Test]
        public void WeaverDef_SixWillowRodsToOneBasket_FourPerDay_InputThirty_StoreTen()
        {
            WorkshopDef d = WorkshopDef.Weaving(new SimTuning());
            Assert.AreEqual(1, d.Inputs.Count);
            Assert.AreEqual(Items.WillowRod.Id, d.Inputs[0].Item.Id);
            Assert.AreEqual(6, d.Inputs[0].Count);
            Assert.AreEqual(CarryClass.Hand, d.Inputs[0].Carry);
            Assert.AreEqual(Items.Basket.Id, d.Output.Id);
            Assert.AreEqual(1, d.Outputs[0].Count);
            Assert.AreEqual(4, d.BatchesPerFamilyDay);
            Assert.AreEqual(30, d.StockCapacityOf(d.Inputs[0]));
            Assert.AreEqual(10, d.OutputCapacity);
            Assert.AreEqual(5, new SimTuning().WorkshopDefaultReserve);
        }

        [Test]
        public void WattleYardDef_SixHazelTwoWillowToOnePanel_FivePerDay_Inputs30And10_StoreTwenty()
        {
            WorkshopDef d = WorkshopDef.Wattling(new SimTuning());
            Assert.AreEqual(2, d.Inputs.Count);
            Assert.AreEqual(Items.HazelRod.Id, d.Inputs[0].Item.Id);
            Assert.AreEqual(6, d.Inputs[0].Count);
            Assert.AreEqual(Items.WillowRod.Id, d.Inputs[1].Item.Id);
            Assert.AreEqual(2, d.Inputs[1].Count);
            Assert.AreEqual(Items.WattlePanel.Id, d.Output.Id);
            Assert.AreEqual(1, d.Outputs[0].Count);
            Assert.AreEqual(5, d.BatchesPerFamilyDay);
            Assert.AreEqual(30, d.StockCapacityOf(d.Inputs[0]));
            Assert.AreEqual(10, d.StockCapacityOf(d.Inputs[1]));
            Assert.AreEqual(20, d.OutputCapacity);
        }

        [TestCase(ModuleTypes.WeaverId, OperationTypes.WeavingId)]
        [TestCase(ModuleTypes.WattleYardId, OperationTypes.WattlingId)]
        public void Types_AreSixByFour_SixLogs480Min_OneFamilySlot_InCatalogues(string type, string opType)
        {
            FootprintTypeDef t = ModuleTypes.Find(type);
            Assert.AreEqual(6000, t.WidthMm);
            Assert.AreEqual(4000, t.DepthMm);
            Assert.AreEqual(6, t.ConstructionRecipe.Logs);
            Assert.AreEqual(480, t.ConstructionRecipe.BuildWorkerMinutes);
            Assert.AreEqual(opType, OperationTypes.WorkshopOperationFor(type));
            Assert.AreEqual(1, OperationTypes.Find(opType).FamilySlots);
            Assert.AreEqual(type, OperationTypes.Find(opType).HutTypeId);
            Assert.IsNotNull(OperationTypes.WorkshopOf(opType, new SimTuning()));
        }

        [TestCase(ModuleTypes.WeaverId, OperationTypes.WeavingId)]
        [TestCase(ModuleTypes.WattleYardId, OperationTypes.WattlingId)]
        public void PlaceBuildAssignDeconstruct_LikeTheHewingStation(string type, string opType)
        {
            SimWorld w = Make(6);
            CommandResult r = w.Execute(new PlaceStructure(type, M(50, 50), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int id = (int)r.Created[0];
            Operation op = w.Operations.All.Single();
            Assert.AreEqual(opType, op.TypeId);
            Assert.IsNull(w.Inventory.FindGoodsStore(id), "no store until built");
            Assert.AreEqual("Hut not built yet", w.Execute(new AssignFamily(op.Id, 3)).Text);

            for (int i = 0; i < 6000 && !w.IsBuilt(id); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(id));
            Assert.IsTrue(w.Execute(new AssignFamily(op.Id, 3)).IsAccepted);
            WorkshopDef def = OperationTypes.WorkshopOf(opType, w.Tuning);
            foreach (RecipeInput input in def.Inputs)
                Assert.AreEqual(def.StockCapacityOf(input), w.Inventory.FindWorkshopStock(id, input.Item.Id).Capacity);
            Assert.AreEqual(def.OutputCapacity, w.Inventory.FindGoodsStore(id).Capacity);

            w.Tuning.DeconstructTimeShare = 0;
            Assert.IsTrue(w.Execute(new MarkForDeconstruction(id)).IsAccepted);
            Assert.IsNull(w.Footprints.Get(id));
            Assert.IsNull(w.Operations.Get(op.Id));
            Assert.IsNull(w.Inventory.FindGoodsStore(id));
            Assert.IsNull(w.Inventory.FindWorkshopStock(id, def.Inputs[0].Item.Id));
            Assert.IsNull(w.Inventory.Audit());
        }

        [TestCase(ModuleTypes.WeaverId)]
        [TestCase(ModuleTypes.WattleYardId)]
        public void Place_RefusedWithFiveLogs(string type)
        {
            SimWorld w = Make(5);
            Assert.IsTrue(w.Execute(new PlaceStructure(type, M(50, 50), 0)).IsRejected);
            Assert.AreEqual(0, w.Operations.Count);
        }

        [Test]
        public void Weaver_WorkingDay_MakesBasketsFromSixRodsEach()
        {
            SimWorld w = Make(6);
            w.Tuning.WorkshopDefaultReserve = 0;
            int id = PlaceAndBuild(w, ModuleTypes.WeaverId);
            Assert.IsTrue(w.Execute(new AssignFamily(w.Operations.FindByModule(id).Id, 3)).IsAccepted);
            LedgerLocation stock = w.Inventory.FindWorkshopStock(id, Items.WillowRod.Id);
            for (int i = 0; i < 2 * Day; i++)
            {
                if (stock.CountOf(Items.WillowRod.Id) < 30) w.Inventory.Produce(stock, Items.WillowRod, 6);
                w.Step(1);
            }
            int baskets = w.Inventory.FindGoodsStore(id).CountOf(Items.Basket.Id);
            Assert.That(baskets, Is.InRange(2, 8), "up to 4 a day over two days, less the commute");
            Assert.AreEqual(baskets * 6, w.Inventory.ConsumedOf(Items.WillowRod.Id), "6 rods per basket");
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void WattleYard_WorkingDay_MakesPanelsFromSixHazelAndTwoWillow()
        {
            SimWorld w = Make(6);
            w.Tuning.WorkshopDefaultReserve = 0;
            int id = PlaceAndBuild(w, ModuleTypes.WattleYardId);
            Assert.IsTrue(w.Execute(new AssignFamily(w.Operations.FindByModule(id).Id, 3)).IsAccepted);
            LedgerLocation hazel = w.Inventory.FindWorkshopStock(id, Items.HazelRod.Id);
            LedgerLocation willow = w.Inventory.FindWorkshopStock(id, Items.WillowRod.Id);
            for (int i = 0; i < 2 * Day; i++)
            {
                if (hazel.CountOf(Items.HazelRod.Id) < 30) w.Inventory.Produce(hazel, Items.HazelRod, 6);
                if (willow.CountOf(Items.WillowRod.Id) < 10) w.Inventory.Produce(willow, Items.WillowRod, 2);
                w.Step(1);
            }
            int panels = w.Inventory.FindGoodsStore(id).CountOf(Items.WattlePanel.Id);
            Assert.That(panels, Is.InRange(2, 10), "up to 5 a day over two days, less the commute");
            Assert.AreEqual(panels * 6, w.Inventory.ConsumedOf(Items.HazelRod.Id), "6 hazel per panel");
            Assert.AreEqual(panels * 2, w.Inventory.ConsumedOf(Items.WillowRod.Id), "2 willow per panel");
            Assert.IsNull(w.Inventory.Audit());
        }
    }
}
