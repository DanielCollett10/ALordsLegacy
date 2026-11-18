using System.Collections.Generic;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Kit;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Kit
{
    /// <summary>T-563, decision 0052: kit stores of forager huts and granaries, and the claim queue.</summary>
    public class KitStoreTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);
        private static MapData Map() => new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);

        private static SimWorld World()
        {
            var w = SimWorld.FromMapData(Map(), new SimTuning { StartHour = 8, BushCount = 0 });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 80);
            w.Inventory.Produce(w.Inventory.AddGoodsStore(M(20, 22)), Items.Plank, 10);
            w.People.AddFamily(2, "F2", 40, 40);
            for (int i = 0; i < 2; i++) w.People.AddCitizen(20 + i, "C" + i, Sex.Male, AdultBirth, 2, 40, 40);
            w.People.AddFamily(3, "F3", 40, 40);
            for (int i = 0; i < 2; i++) w.People.AddCitizen(30 + i, "H" + i, Sex.Male, AdultBirth, 3, 40, 40);
            Ox ox = w.People.AddOx(50, "O", 40, 44);
            ox.State = OxState.Resting(40000, 44000);
            return w;
        }

        private static int BuildGranary(SimWorld w, double x, double z)
        {
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(x, z), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int fp = (int)r.Created[0];
            for (int i = 0; i < 6000 && !w.IsBuilt(fp); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(fp));
            return fp;
        }

        private static int BuildHut(SimWorld w, double x, double z)
        {
            var hut = new List<ModuleSpec> { new ModuleSpec(ModuleTypes.ForagerHutId, M(x, z), 0) };
            var square = AreaShape.Straight(new[] { M(x - 10, z - 10), M(x + 10, z - 10), M(x + 10, z + 10), M(x - 10, z + 10) });
            CommandResult d = w.Execute(new DesignateOperation(square, OperationTypes.ForagingId, hut));
            Assert.IsTrue(d.IsAccepted, d.Text);
            int fp = (int)d.Created[1];
            for (int i = 0; i < 6000 && !w.IsBuilt(fp); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(fp));
            return fp;
        }

        [Test]
        public void BuiltGranary_GetsKitStoreWithTwoBasketSlots_ThroughRealBuild()
        {
            SimWorld w = World();
            int fp = BuildGranary(w, 60, 60);
            LedgerLocation kit = KitStores.Find(w, fp);
            Assert.IsNotNull(kit);
            Assert.AreEqual(w.Tuning.GranaryBasketSlots, kit.Capacity);
            Assert.AreEqual(2, kit.ItemCaps[Items.Basket.Id]);
        }

        [Test]
        public void BuiltForagerHut_GetsKitStoreWithFiveBasketSlots_ThroughRealBuild()
        {
            SimWorld w = World();
            int fp = BuildHut(w, 60, 60);
            LedgerLocation kit = KitStores.Find(w, fp);
            Assert.IsNotNull(kit);
            Assert.AreEqual(5, kit.ItemCaps[Items.Basket.Id]);
            Assert.IsNotNull(w.Inventory.FindFoodStore(fp), "the hut's food store is separate");
        }

        [Test]
        public void KitStore_NotCreatedBeforeBuilt()
        {
            SimWorld w = World();
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(60, 60), 0));
            Assert.IsNull(KitStores.Find(w, (int)r.Created[0]));
        }

        [Test]
        public void KitStore_SlotLimit_SixthBasketInHutIsRefused_AndOtherItemsToo()
        {
            SimWorld w = World();
            LedgerLocation kit = KitStores.Find(w, BuildHut(w, 60, 60));
            w.Inventory.Produce(kit, Items.Basket, 5);
            Assert.Throws<System.InvalidOperationException>(() => w.Inventory.Produce(kit, Items.Basket, 1));
            Assert.Throws<System.InvalidOperationException>(() => w.Inventory.Produce(kit, Items.Plank, 1), "capacity is slots only");
        }

        [Test]
        public void KitStore_SlotLimit_GranaryHoldsTwo()
        {
            SimWorld w = World();
            LedgerLocation kit = KitStores.Find(w, BuildGranary(w, 60, 60));
            w.Inventory.Produce(kit, Items.Basket, 2);
            Assert.Throws<System.InvalidOperationException>(() => w.Inventory.Produce(kit, Items.Basket, 1));
        }

        [Test]
        public void Claims_AreOrderedOldestFirst_TiesToLowerBuildingId()
        {
            var c = new KitClaims();
            c.Claim(9, 500); c.Claim(4, 900); c.Claim(7, 500); c.Claim(2, 100);
            var ids = new List<int>();
            foreach (KitClaim k in c.Ordered()) ids.Add(k.BuildingId);
            Assert.AreEqual(new[] { 2, 7, 9, 4 }, ids);
        }

        [Test]
        public void Claim_Again_KeepsFirstClaimTick()
        {
            var c = new KitClaims();
            c.Claim(3, 100);
            c.Claim(3, 400);
            Assert.AreEqual(100, c.Get(3).FirstClaimTick);
            Assert.AreEqual(1, c.Count);
            Assert.IsTrue(c.Release(3));
            Assert.IsFalse(c.Release(3));
        }

        [Test]
        public void KitStore_IsNotAConstructionSource_ButCountsInVillageTotals()
        {
            SimWorld w = World();
            LedgerLocation kit = KitStores.Find(w, BuildHut(w, 60, 60));
            w.Inventory.Produce(kit, Items.Basket, 3);
            Assert.AreEqual(0, ConstructionSources.Available(w, Items.Basket));
            Assert.IsNull(ConstructionSources.TryReserve(w, Items.Basket, 1, M(60, 60), 99));
            Assert.AreEqual(3, GoodsStats.OfItem(w, Items.Basket));
        }

        [Test]
        public void KitStore_IsNotAWorkshopInputSource()
        {
            SimWorld w = World();
            LedgerLocation kit = KitStores.Find(w, BuildGranary(w, 60, 60));
            w.Inventory.Produce(kit, Items.Basket, 2);
            Assert.IsFalse(LordsLegacy.Sim.Operations.Workshops.InputSources.IsRodStore(w, kit));
            Assert.IsNull(LordsLegacy.Sim.Operations.Workshops.InputSources.Nearest(w, Items.Beam, M(60, 60)));
        }

        [Test]
        public void SaveRoundTrip_KitStoresAndClaims_Survive()
        {
            SimWorld w = World();
            int hut = BuildHut(w, 60, 60);
            int granary = BuildGranary(w, 80, 80);
            w.Inventory.Produce(KitStores.Find(w, hut), Items.Basket, 4);
            w.KitClaims.Release(granary);
            w.KitClaims.Release(hut);
            w.KitClaims.Claim(granary, 777);
            w.KitClaims.Claim(hut, 333);
            MapData map = Map();
            byte[] bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(bytes, map);
            LedgerLocation kit = KitStores.Find(back, hut);
            Assert.IsNotNull(kit);
            Assert.AreEqual(4, kit.CountOf(Items.Basket.Id));
            Assert.AreEqual(5, kit.ItemCaps[Items.Basket.Id]);
            Assert.IsNotNull(KitStores.Find(back, granary));
            var order = new List<int>();
            foreach (KitClaim k in back.KitClaims.Ordered()) order.Add(k.BuildingId);
            Assert.AreEqual(new[] { hut, granary }, order);
            Assert.AreEqual(777, back.KitClaims.Get(granary).FirstClaimTick);
            Assert.AreEqual(bytes, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash));
        }

        [Test]
        public void SaveVersion_IsAtLeast36()
        {
            Assert.GreaterOrEqual(SaveWriter.CurrentVersion, 36);
        }
    }
}
