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

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>
    /// Yard collection per store type (T-562, ST-42, ST-42b): the weaver's baskets and the wattle yard's panels are
    /// collected by hand like any workshop output, the coppice hut's rods and goods never are, and the input stock
    /// of the new workshops is never read.
    /// </summary>
    public class YardWeaverWattleCollectionTests
    {
        private const long Adult = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld Make()
        {
            var w = SimWorld.FromMapData(new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]),
                new SimTuning { StartHour = 8, StartingPlanks = 0 });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 20);
            for (int fam = 2; fam <= 4; fam++)
            {
                w.People.AddFamily(fam, "F" + fam, 40, 40);
                for (int i = 0; i < 2; i++) w.People.AddCitizen(fam * 10 + i, "C" + fam + i, Sex.Male, Adult, fam, 40, 40);
            }
            Ox ox = w.People.AddOx(50, "O", 40, 44);
            ox.State = OxState.Resting(40000, 44000);
            return w;
        }

        private static Operation BuiltYard(SimWorld w, PointMm at)
        {
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.StorageYardId, at, 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int id = (int)r.Created[0];
            for (int i = 0; i < 6000 && !w.IsBuilt(id); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(id));
            return w.Operations.FindByModule(id);
        }

        /// <summary>A workshop footprint with its operation and stock, made by the workshop build hook (no Sim ticks).</summary>
        private static Footprint Workshop(SimWorld w, string type, PointMm at)
        {
            Footprint fp = w.Footprints.Add(100 + w.Operations.Count, ModuleTypes.Find(type), at, 0);
            Operation op = w.Operations.Create(OperationTypes.WorkshopOperationFor(type), 0);
            w.Operations.AddModule(op.Id, fp.Id);
            WorkshopBuildings.OnBuilt(w, fp);
            return fp;
        }

        private static LedgerLocation Output(SimWorld w, Footprint fp, ItemDef item, int count)
        {
            LedgerLocation store = w.Inventory.FindGoodsStore(fp.Id);
            if (count > 0) w.Inventory.Produce(store, item, count);
            return store;
        }

        private static YardTrip Next(SimWorld w, Operation yard) => new YardWorkshopSource().NextTrip(w, yard);

        [Test]
        public void WeaverStore_OneBasket_CollectedAtOnce_ByHand()
        {
            SimWorld w = Make();
            Operation yard = BuiltYard(w, M(100, 100));
            Footprint weaver = Workshop(w, ModuleTypes.WeaverId, M(60, 100));
            LedgerLocation store = Output(w, weaver, Items.Basket, 1);

            YardTrip t = Next(w, yard);

            Assert.IsNotNull(t, "any free basket is collected, no half-full rule (ST-42)");
            Assert.AreSame(store, t.Source);
            Assert.AreEqual(Items.Basket.Id, t.Item.Id);
            Assert.AreEqual(1, t.Count);
            Assert.IsNotNull(HandLoads.For(t.Item, w.Tuning), "baskets have a hand load, so a citizen carries them");
        }

        [Test]
        public void WeaverStore_BasketsBelowHalf_StillCollected()
        {
            SimWorld w = Make();
            Operation yard = BuiltYard(w, M(100, 100));
            Footprint weaver = Workshop(w, ModuleTypes.WeaverId, M(60, 100));
            LedgerLocation store = Output(w, weaver, Items.Basket, 2);
            Assert.Less(store.Total, store.Capacity / 2, "well under half of the 10-basket store");

            Assert.AreSame(store, Next(w, yard).Source);
        }

        [Test]
        public void WattleStore_Panels_CollectedByHand()
        {
            SimWorld w = Make();
            Operation yard = BuiltYard(w, M(100, 100));
            Footprint wattle = Workshop(w, ModuleTypes.WattleYardId, M(60, 100));
            LedgerLocation store = Output(w, wattle, Items.WattlePanel, 3);

            YardTrip t = Next(w, yard);

            Assert.IsNotNull(t);
            Assert.AreSame(store, t.Source);
            Assert.AreEqual(Items.WattlePanel.Id, t.Item.Id);
            Assert.AreEqual(3, t.Count);
            Assert.IsNotNull(HandLoads.For(t.Item, w.Tuning), "panels have a hand load (2 a trip by default)");
        }

        [Test]
        public void WattleStore_ReservedPanel_NoTrip()
        {
            SimWorld w = Make();
            Operation yard = BuiltYard(w, M(100, 100));
            Footprint wattle = Workshop(w, ModuleTypes.WattleYardId, M(60, 100));
            LedgerLocation store = Output(w, wattle, Items.WattlePanel, 1);
            Assert.AreNotEqual(0, w.Inventory.ReserveStock(store, Items.WattlePanel, 1, 999));

            Assert.IsNull(Next(w, yard), "a reserved panel is not free");
        }

        [Test]
        public void WattleYardInputStock_HazelAndWillowRods_NeverCollected()
        {
            SimWorld w = Make();
            Operation yard = BuiltYard(w, M(100, 100));
            Footprint wattle = Workshop(w, ModuleTypes.WattleYardId, M(60, 100));
            LedgerLocation hazel = w.Inventory.FindWorkshopStock(wattle.Id, Items.HazelRod.Id);
            LedgerLocation willow = w.Inventory.FindWorkshopStock(wattle.Id, Items.WillowRod.Id);
            w.Inventory.Produce(hazel, Items.HazelRod, hazel.Capacity);
            w.Inventory.Produce(willow, Items.WillowRod, willow.Capacity);

            Assert.IsNull(Next(w, yard), "only the output store is read (PL-30d)");
        }

        [Test]
        public void WeaverInputStock_WillowRods_NeverCollected()
        {
            SimWorld w = Make();
            Operation yard = BuiltYard(w, M(100, 100));
            Footprint weaver = Workshop(w, ModuleTypes.WeaverId, M(60, 100));
            LedgerLocation rods = w.Inventory.FindWorkshopStock(weaver.Id, Items.WillowRod.Id);
            w.Inventory.Produce(rods, Items.WillowRod, rods.Capacity);

            Assert.IsNull(Next(w, yard));
        }

        [Test]
        public void CoppiceHutStore_RodsAndBaskets_NeverCollected()
        {
            SimWorld w = Make();
            Operation yard = BuiltYard(w, M(100, 100));
            Footprint hut = w.Footprints.Add(300, ModuleTypes.CoppiceHut, M(60, 100), 0);
            Operation op = w.Operations.Create(OperationTypes.CoppiceId, 0);
            w.Operations.AddModule(op.Id, hut.Id);
            WorkshopBuildings.OnBuilt(w, hut);
            LedgerLocation store = w.Inventory.FindGoodsStore(hut.Id);
            w.Inventory.Produce(store, Items.HazelRod, 10);
            w.Inventory.Produce(store, Items.Basket, 1);

            Assert.IsNull(Next(w, yard), "the coppice hut has no input and is not a workshop (ST-42b)");
        }

        [Test]
        public void FullestOutputFirst_AcrossWattleAndWeaverAndCoppice_WattlePanelsWin()
        {
            SimWorld w = Make();
            Operation yard = BuiltYard(w, M(100, 100));
            Footprint weaver = Workshop(w, ModuleTypes.WeaverId, M(90, 100));
            Output(w, weaver, Items.Basket, 4); // 4 of 10
            Footprint wattle = Workshop(w, ModuleTypes.WattleYardId, M(30, 100));
            LedgerLocation panels = Output(w, wattle, Items.WattlePanel, 15); // 15 of 20, the fuller one
            Footprint hut = w.Footprints.Add(300, ModuleTypes.CoppiceHut, M(60, 60), 0);
            Operation coppice = w.Operations.Create(OperationTypes.CoppiceId, 0);
            w.Operations.AddModule(coppice.Id, hut.Id);
            WorkshopBuildings.OnBuilt(w, hut);
            w.Inventory.Produce(w.Inventory.FindGoodsStore(hut.Id), Items.Basket, 8); // never collected, however full

            YardTrip t = Next(w, yard);

            Assert.AreSame(panels, t.Source, "fullest collectable store first, not nearest");
        }

        [Test]
        public void FullYard_NoBasketTrip()
        {
            SimWorld w = Make();
            Operation yard = BuiltYard(w, M(100, 100));
            Footprint weaver = Workshop(w, ModuleTypes.WeaverId, M(60, 100));
            Output(w, weaver, Items.Basket, 3);
            LedgerLocation ys = w.Inventory.FindGoodsStore(yard.ModuleIds[0]);
            ys.Capacity = 1;
            w.Inventory.Produce(ys, Items.Plank, 1);

            Assert.IsNull(Next(w, yard), "no room in the yard for baskets either");
        }

        [TestCase(0)]
        [TestCase(1)]
        public void PlannedHaul_IsCappedAtTheHandLoad_NotTheFullFreeStock(int which)
        {
            SimWorld w = Make();
            ItemDef item = which == 0 ? Items.Basket : Items.WattlePanel;
            string type = which == 0 ? ModuleTypes.WeaverId : ModuleTypes.WattleYardId;
            Operation yard = BuiltYard(w, M(100, 100));
            Assert.IsTrue(w.Execute(new AssignFamily(yard.Id, 4)).IsAccepted);
            Footprint shop = Workshop(w, type, M(60, 100));
            w.Inventory.FindGoodsStore(shop.Id).Capacity = 30; // room for more than both workers carry
            LedgerLocation store = Output(w, shop, item, 14);
            int load = HandLoads.For(item, w.Tuning).Value;
            Assert.Less(2 * load, 14, "the store holds more than both workers can carry");

            int reserved = 0;
            for (int i = 0; i < 1440 && reserved == 0; i++)
            {
                w.Step(1);
                reserved = store.CountOf(item.Id) - store.AvailableOf(item.Id);
            }

            Assert.Greater(reserved, 0, "the yard family planned a haul");
            Assert.LessOrEqual(reserved, 2 * load, "at most one hand load per idle worker (2 adults), never the whole stock");
            Assert.AreEqual(0, reserved % load, "each planned haul is exactly one hand load");
        }

        [Test]
        public void FullPipeline_WeaverBaskets_ReachTheYard_ThroughSimTicks()
        {
            // Whole pipeline through real commands and ticks: both buildings placed and built by workers, families assigned,
            // the weaver makes baskets on its own, and the yard family carries them in. Only the weaver's rod input is
            // put into its stock directly (rods come from the coppice hut, a separate test).
            SimWorld w = Make();
            w.Tuning.WorkshopDefaultReserve = 0;
            CommandResult ry = w.Execute(new PlaceStructure(ModuleTypes.StorageYardId, M(100, 100), 0));
            CommandResult rw = w.Execute(new PlaceStructure(ModuleTypes.WeaverId, M(50, 50), 0));
            Assert.IsTrue(ry.IsAccepted && rw.IsAccepted, ry.Text + " " + rw.Text);
            int yardFp = (int)ry.Created[0], weaverFp = (int)rw.Created[0];
            for (int i = 0; i < 9000 && !(w.IsBuilt(yardFp) && w.IsBuilt(weaverFp)); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(yardFp) && w.IsBuilt(weaverFp), "both built by workers");
            Operation yardOp = w.Operations.FindByModule(yardFp), weaverOp = w.Operations.FindByModule(weaverFp);
            Assert.IsTrue(w.Execute(new AssignFamily(yardOp.Id, 4)).IsAccepted, "a family staffs the yard");
            Assert.IsTrue(w.Execute(new AssignFamily(weaverOp.Id, 3)).IsAccepted, "a family staffs the weaver");

            LedgerLocation rods = w.Inventory.FindWorkshopStock(weaverFp, Items.WillowRod.Id);
            LedgerLocation yardStore = w.Inventory.FindGoodsStore(yardFp);
            for (int i = 0; i < 2 * 1440 && yardStore.CountOf(Items.Basket.Id) == 0; i++)
            {
                if (rods.CountOf(Items.WillowRod.Id) < 6) w.Inventory.Produce(rods, Items.WillowRod, 6);
                w.Step(1);
                Assert.IsNull(w.Inventory.Audit(), "ledger audit at tick " + w.Tick);
            }

            Assert.Greater(yardStore.CountOf(Items.Basket.Id), 0, "the yard family carried a basket from the weaver to the yard");
        }
    }
}
