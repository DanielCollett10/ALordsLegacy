using System.Collections.Generic;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    /// <summary>Multi-item reservation for construction sites (T-514, decision 0046).</summary>
    public class SiteMaterialsTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld NewWorld() => new SimWorld(new SimTuning());

        private static ConstructionRecipe Recipe(int logs, int planks) =>
            new ConstructionRecipe(new Dictionary<string, int> { { Items.Log.Id, logs }, { Items.Plank.Id, planks } }, 10);

        [Test]
        public void TryReserveAll_LogsAndPlanks_ReservesBoth()
        {
            var w = NewWorld();
            LedgerLocation stock = w.Inventory.AddStockpile(M(0, 0));
            LedgerLocation store = w.Inventory.AddGoodsStore(M(5, 0));
            w.Inventory.Produce(stock, Items.Log, 10);
            w.Inventory.Produce(store, Items.Plank, 10);

            List<int> ids = SiteMaterials.TryReserveAll(w, Recipe(4, 6), M(0, 0), 1);

            Assert.IsNotNull(ids);
            Assert.AreEqual(6, stock.AvailableOf(Items.Log.Id));
            Assert.AreEqual(4, store.AvailableOf(Items.Plank.Id));
        }

        [Test]
        public void TryReserveAll_NearestStoreFirstPerItem()
        {
            var w = NewWorld();
            LedgerLocation far = w.Inventory.AddGoodsStore(M(100, 0));
            LedgerLocation near = w.Inventory.AddGoodsStore(M(10, 0));
            w.Inventory.Produce(far, Items.Plank, 5);
            w.Inventory.Produce(near, Items.Plank, 5);

            Assert.IsNotNull(SiteMaterials.TryReserveAll(w, Recipe(0, 7), M(0, 0), 1));
            Assert.AreEqual(0, near.AvailableOf(Items.Plank.Id));
            Assert.AreEqual(3, far.AvailableOf(Items.Plank.Id));
        }

        [Test]
        public void TryReserveAll_SecondItemShort_ReleasesFirst()
        {
            var w = NewWorld();
            LedgerLocation stock = w.Inventory.AddStockpile(M(0, 0));
            LedgerLocation store = w.Inventory.AddGoodsStore(M(5, 0));
            w.Inventory.Produce(stock, Items.Log, 10);
            w.Inventory.Produce(store, Items.Plank, 4);

            Assert.IsNull(SiteMaterials.TryReserveAll(w, Recipe(4, 6), M(0, 0), 1));
            Assert.AreEqual(10, stock.AvailableOf(Items.Log.Id));
            Assert.AreEqual(4, store.AvailableOf(Items.Plank.Id));
        }

        [Test]
        public void TryReserveAll_ReservedPlanksStayInTotalButLeaveAvailable()
        {
            var w = NewWorld();
            LedgerLocation store = w.Inventory.AddGoodsStore(M(5, 0));
            w.Inventory.Produce(store, Items.Plank, 10);

            Assert.IsNotNull(SiteMaterials.TryReserveAll(w, Recipe(0, 6), M(0, 0), 1));
            Assert.AreEqual(10, store.CountOf(Items.Plank.Id));
            Assert.AreEqual(10, w.Inventory.TotalOf(Items.Plank));
            Assert.AreEqual(4, store.AvailableOf(Items.Plank.Id));
        }

        [Test]
        public void Shortfall_PlanksShort_NamesPlanks()
        {
            var w = NewWorld();
            w.Inventory.Produce(w.Inventory.AddStockpile(M(0, 0)), Items.Log, 10);
            w.Inventory.Produce(w.Inventory.AddGoodsStore(M(5, 0)), Items.Plank, 4);
            Assert.AreEqual("Not enough planks (have 4, need 6)", SiteMaterials.Shortfall(w, Recipe(4, 6)));
        }

        [Test]
        public void Shortfall_LogsShort_UsesLogsText()
        {
            var w = NewWorld();
            w.Inventory.Produce(w.Inventory.AddStockpile(M(0, 0)), Items.Log, 2);
            Assert.AreEqual("Not enough logs (have 2, need 5)", SiteMaterials.Shortfall(w, Recipe(5, 0)));
        }

        [Test]
        public void Shortfall_NothingShort_ReturnsNull()
        {
            var w = NewWorld();
            Assert.IsNull(SiteMaterials.Shortfall(w, ConstructionRecipe.Empty));
        }

        [Test]
        public void TryReserveAll_EmptyRecipe_ReturnsEmptyList()
        {
            var w = NewWorld();
            Assert.AreEqual(0, SiteMaterials.TryReserveAll(w, ConstructionRecipe.Empty, M(0, 0), 1).Count);
        }
    }
}
