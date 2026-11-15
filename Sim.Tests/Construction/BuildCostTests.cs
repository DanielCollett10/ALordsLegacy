using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Construction
{
    /// <summary>Minimum cost, module-list cost and affordability queries (T-384, BT-01..03, CN-11).</summary>
    public class BuildCostTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld NewWorld() => new SimWorld(new SimTuning());

        [Test]
        public void MinimumFor_Woodcutting_IsEight()
        {
            Assert.AreEqual(8, BuildCost.MinimumFor(OperationTypes.Woodcutting));
        }

        [Test]
        public void MinimumFor_Woodcutting_EqualsHutPlusOneStorageRecipe()
        {
            int expected = ModuleTypes.WoodcuttingHut.ConstructionRecipe.Logs
                           + OperationTypes.Woodcutting.MinLogStorageCount * ModuleTypes.LogStorage.ConstructionRecipe.Logs;
            Assert.AreEqual(expected, BuildCost.MinimumFor(OperationTypes.Woodcutting));
        }

        [Test]
        public void MinimumFor_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => BuildCost.MinimumFor(null));
        }

        [Test]
        public void Of_EmptyList_IsZero()
        {
            Assert.AreEqual(0, BuildCost.Of(new string[0]));
        }

        [Test]
        public void Of_HutAndStorage_SumsRecipeLogs()
        {
            Assert.AreEqual(8, BuildCost.Of(new[] { ModuleTypes.WoodcuttingHutId, ModuleTypes.LogStorageId }));
        }

        [Test]
        public void Of_OxenStand_AddsItsOneLog()
        {
            Assert.AreEqual(9, BuildCost.Of(new[] { ModuleTypes.WoodcuttingHutId, ModuleTypes.LogStorageId, ModuleTypes.OxenStandId }));
        }

        [Test]
        public void Of_RepeatedEntries_CountEachOnce()
        {
            Assert.AreEqual(16, BuildCost.Of(new[] { ModuleTypes.WoodcuttingHutId, ModuleTypes.WoodcuttingHutId }));
        }

        [Test]
        public void Of_ModuleWithNoRecipe_CostsZero()
        {
            Assert.AreEqual(0, BuildCost.Of(new[] { ModuleTypes.LogStorageId }));
        }

        [Test]
        public void Of_UnknownId_Throws()
        {
            Assert.Throws<ArgumentException>(() => BuildCost.Of(new[] { "module.nope" }));
        }

        [Test]
        public void Of_NullList_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => BuildCost.Of(null));
        }

        [Test]
        public void CanAfford_ExactlyEightAvailable_IsTrue()
        {
            var w = NewWorld();
            w.Inventory.Produce(w.Inventory.AddStockpile(M(0, 0)), Items.Log, 8);
            Assert.IsTrue(BuildCost.CanAfford(w, BuildCost.MinimumFor(OperationTypes.Woodcutting)));
        }

        [Test]
        public void CanAfford_SevenAvailable_IsFalse()
        {
            var w = NewWorld();
            w.Inventory.Produce(w.Inventory.AddStockpile(M(0, 0)), Items.Log, 7);
            Assert.IsFalse(BuildCost.CanAfford(w, BuildCost.MinimumFor(OperationTypes.Woodcutting)));
        }

        [Test]
        public void CanAfford_NoLogs_IsFalseForAnyPositiveCost()
        {
            var w = NewWorld();
            Assert.IsFalse(BuildCost.CanAfford(w, 1));
            Assert.IsFalse(BuildCost.CanAfford(w, 8));
        }

        [Test]
        public void CanAfford_ZeroOrNegative_IsAlwaysTrue()
        {
            var w = NewWorld();
            Assert.IsTrue(BuildCost.CanAfford(w, 0));
            Assert.IsTrue(BuildCost.CanAfford(w, -3));
        }

        [Test]
        public void CanAfford_LogsReservedByAnotherSite_AreNotCounted()
        {
            var w = NewWorld();
            var sp = w.Inventory.AddStockpile(M(0, 0));
            w.Inventory.Produce(sp, Items.Log, 10);
            w.Inventory.ReserveStock(sp, Items.Log, 3, 77); // another site holds 3
            Assert.IsFalse(BuildCost.CanAfford(w, 8), "10 total − 3 reserved = 7, not enough for 8");
            Assert.IsTrue(BuildCost.CanAfford(w, 7));
        }

        [Test]
        public void CanAfford_SiteOwnedGroundPile_IsNotCounted()
        {
            var w = NewWorld();
            w.Inventory.ProduceOnGround(M(50, 50), 5, Items.Log, 9); // a site's own pile (CN-25b)
            Assert.IsFalse(BuildCost.CanAfford(w, 8));
        }

        [Test]
        public void CanAfford_UnownedGroundPileAndStockpile_AreCombined()
        {
            var w = NewWorld();
            w.Inventory.Produce(w.Inventory.AddStockpile(M(0, 0)), Items.Log, 5);
            w.Inventory.ProduceOnGround(M(10, 0), null, Items.Log, 3);
            Assert.IsTrue(BuildCost.CanAfford(w, 8));
            Assert.IsFalse(BuildCost.CanAfford(w, 9));
        }

        [Test]
        public void CanAfford_NullWorld_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => BuildCost.CanAfford(null, 1));
        }
    }
}
