using System.Linq;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Inventory
{
    [TestFixture]
    public class RodBasketPanelItemTests
    {
        [Test]
        public void NewItems_AreInItemsAll_ById()
        {
            Assert.AreSame(Items.WillowRod, Items.Find("item.willow_rod"));
            Assert.AreSame(Items.HazelRod, Items.Find("item.hazel_rod"));
            Assert.AreSame(Items.Basket, Items.Find("item.basket"));
            Assert.AreSame(Items.WattlePanel, Items.Find("item.wattle_panel"));
            CollectionAssert.Contains(Items.All, Items.WillowRod);
            CollectionAssert.Contains(Items.All, Items.HazelRod);
            CollectionAssert.Contains(Items.All, Items.Basket);
            CollectionAssert.Contains(Items.All, Items.WattlePanel);
        }

        [Test]
        public void NewItems_AreGeneralCategory_NotFood()
        {
            Assert.AreEqual(ItemCategory.General, Items.WillowRod.Category);
            Assert.AreEqual(ItemCategory.General, Items.HazelRod.Category);
            Assert.AreEqual(ItemCategory.General, Items.Basket.Category);
            Assert.AreEqual(ItemCategory.General, Items.WattlePanel.Category);
            Assert.AreEqual(0, Items.Basket.RationsPerItem);
        }

        [Test]
        public void NewItemIds_AreUnique()
        {
            var ids = Items.All.Select(i => i.Id).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count());
        }

        [Test]
        public void HandLoad_WillowAndHazelRods_AreTenPerTrip()
        {
            var t = new SimTuning();
            Assert.AreEqual(10, HandLoads.For(Items.WillowRod, t));
            Assert.AreEqual(10, HandLoads.For(Items.HazelRod, t));
        }

        [Test]
        public void HandLoad_Basket_IsFivePerTrip()
        {
            Assert.AreEqual(5, HandLoads.For(Items.Basket, new SimTuning()));
        }

        [Test]
        public void HandLoad_WattlePanel_IsTwoPerTrip()
        {
            Assert.AreEqual(2, HandLoads.For(Items.WattlePanel, new SimTuning()));
        }

        [Test]
        public void HandLoad_NewItems_UseTunedValues()
        {
            var t = new SimTuning { HandCarryRods = 7, HandCarryBaskets = 3, HandCarryPanels = 4 };
            Assert.AreEqual(7, HandLoads.For(Items.WillowRod, t));
            Assert.AreEqual(7, HandLoads.For(Items.HazelRod, t));
            Assert.AreEqual(3, HandLoads.For(Items.Basket, t));
            Assert.AreEqual(4, HandLoads.For(Items.WattlePanel, t));
        }
    }

    [TestFixture]
    public class RodBasketPanelTuningDefaultsTests
    {
        [Test]
        public void Stools_MatchTuningCounts_PerKind()
        {
            var t = new SimTuning();
            Assert.AreEqual(40, t.HazelStoolCount, "MAT-15 hazel stools (tuning)");
            Assert.AreEqual(20, t.WillowStoolCount, "MAT-15 willow stools (tuning)");
        }

        [Test]
        public void Coppice_MatchesDesignNumbers()
        {
            var t = new SimTuning();
            Assert.AreEqual(10, t.CoppiceRodsPerCut, "MAT-16 rods per cut");
            Assert.AreEqual(30, t.CoppiceRegrowDays, "MAT-16 regrow days");
            Assert.AreEqual(6, t.CoppiceMinutesPerRod, "MAT-17 minutes a rod");
            Assert.AreEqual(40, t.CoppiceHutStorePerKind, "MAT-14 hut store per rod kind");
        }

        [Test]
        public void HandLoads_MatchDesignNumbers()
        {
            var t = new SimTuning();
            Assert.AreEqual(10, t.HandCarryRods, "MAT-23 rods a trip");
            Assert.AreEqual(5, t.HandCarryBaskets, "MAT-23 baskets a trip");
            Assert.AreEqual(2, t.HandCarryPanels, "MAT-23 panels a trip");
        }

        [Test]
        public void Weaver_MatchesDesignNumbers()
        {
            var t = new SimTuning();
            Assert.AreEqual(6, t.WeaverWillowPerBasket, "MAT-22 willow per basket");
            Assert.AreEqual(4, t.WeaverBasketsPerDay, "MAT-22 baskets per family-day");
            Assert.AreEqual(30, t.WeaverWillowStock, "MAT-24 weaver input");
            Assert.AreEqual(10, t.WeaverBasketStore, "MAT-24 weaver output");
        }

        [Test]
        public void WattleYard_MatchesDesignNumbers()
        {
            var t = new SimTuning();
            Assert.AreEqual(6, t.WattleHazelPerPanel, "MAT-22 hazel per panel");
            Assert.AreEqual(2, t.WattleWillowPerPanel, "MAT-22 willow per panel");
            Assert.AreEqual(5, t.WattlePanelsPerDay, "MAT-22 panels per family-day");
            Assert.AreEqual(30, t.WattleHazelStock, "MAT-24 hazel input");
            Assert.AreEqual(10, t.WattleWillowStock, "MAT-24 willow input");
            Assert.AreEqual(20, t.WattlePanelStore, "MAT-24 panel output");
        }

        [Test]
        public void KitSlots_MatchDesignNumbers()
        {
            var t = new SimTuning();
            Assert.AreEqual(5, t.ForagerHutBasketSlots, "MAT-53 forager hut baskets");
            Assert.AreEqual(2, t.GranaryBasketSlots, "MAT-53 granary baskets, one per family slot");
        }
    }
}
