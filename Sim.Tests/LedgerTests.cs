using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class LedgerTests
    {
        private static PointMm P(long x, long z) => new PointMm(x, z);

        [Test]
        public void LogItem_HasIdAndCategory()
        {
            Assert.AreEqual("item.log", Items.Log.Id);
            Assert.AreEqual(ItemCategory.Log, Items.Log.Category);
            Assert.AreSame(Items.Log, Items.Find("item.log"));
            Assert.IsNull(Items.Find("item.nope"));
        }

        [Test]
        public void World_HasEmptyLedger()
        {
            var w = new SimWorld(new SimTuning());
            Assert.AreEqual(0, w.Inventory.TotalOf(Items.Log));
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void Produce_AddsToLocationTotalsAndBumpsVersion()
        {
            var l = new Ledger();
            var pile = l.GetOrAddGroundPile(P(0, 0), null);
            int v = l.Version;
            l.Produce(pile, Items.Log, 3);
            Assert.AreEqual(3, pile.CountOf("item.log"));
            Assert.AreEqual(3, pile.Total);
            Assert.AreEqual(3, l.TotalOf(Items.Log));
            Assert.Greater(l.Version, v);
        }

        [Test]
        public void Produce_ZeroOrNegative_Throws()
        {
            var l = new Ledger();
            var pile = l.GetOrAddGroundPile(P(0, 0), null);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => l.Produce(pile, Items.Log, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => l.Produce(pile, Items.Log, -1));
            Assert.AreEqual(0, l.TotalOf(Items.Log));
        }

        [Test]
        public void Produce_AtForeignLocation_Throws()
        {
            var a = new Ledger();
            var b = new Ledger();
            var pile = a.GetOrAddGroundPile(P(0, 0), null);
            b.GetOrAddGroundPile(P(5000, 0), null);
            Assert.Throws<System.ArgumentException>(() => b.Produce(pile, Items.Log, 1));
        }

        [Test]
        public void GroundPiles_WithinOneMetreSameOwner_Merge()
        {
            var l = new Ledger();
            var a = l.ProduceOnGround(P(0, 0), 7, Items.Log, 1);
            var b = l.ProduceOnGround(P(600, 800), 7, Items.Log, 2); // exactly 1000 mm away
            Assert.AreSame(a, b);
            Assert.AreEqual(3, a.Total);
            Assert.AreEqual(1, CountLocations(l));
        }

        [Test]
        public void GroundPiles_BeyondOneMetre_DoNotMerge()
        {
            var l = new Ledger();
            var a = l.ProduceOnGround(P(0, 0), 7, Items.Log, 1);
            var b = l.ProduceOnGround(P(1001, 0), 7, Items.Log, 1);
            Assert.AreNotSame(a, b);
            Assert.AreEqual(2, CountLocations(l));
        }

        [Test]
        public void GroundPiles_DifferentOwner_DoNotMerge()
        {
            var l = new Ledger();
            var a = l.ProduceOnGround(P(0, 0), 7, Items.Log, 1);
            var b = l.ProduceOnGround(P(0, 0), 8, Items.Log, 1);
            var c = l.ProduceOnGround(P(0, 0), null, Items.Log, 1);
            Assert.AreNotSame(a, b);
            Assert.AreNotSame(a, c);
            Assert.AreNotSame(b, c);
            Assert.AreEqual(3, l.TotalOf(Items.Log));
        }

        [Test]
        public void GroundPile_NearestOfSeveralWins()
        {
            var l = new Ledger();
            var far = l.ProduceOnGround(P(0, 0), null, Items.Log, 1);
            var near = l.ProduceOnGround(P(1900, 0), null, Items.Log, 1);
            Assert.AreNotSame(far, near);
            var hit = l.ProduceOnGround(P(1200, 0), null, Items.Log, 1); // 1200 from far, 700 from near
            Assert.AreSame(near, hit);
        }

        [Test]
        public void Storage_BoundToModule_IsReused()
        {
            var l = new Ledger();
            var s1 = l.GetOrAddStorage(4);
            Assert.AreSame(s1, l.GetOrAddStorage(4));
            Assert.AreNotSame(s1, l.GetOrAddStorage(5));
            Assert.AreEqual(LocationKind.Storage, s1.Kind);
            Assert.AreEqual(4, s1.ModuleId);
            l.Produce(s1, Items.Log, 2);
            l.ProduceOnGround(P(0, 0), null, Items.Log, 3);
            Assert.AreEqual(5, l.TotalOf(Items.Log));
            Assert.AreEqual(2, s1.Total);
        }

        [Test]
        public void Audit_AfterOperations_IsClean()
        {
            var l = new Ledger();
            l.ProduceOnGround(P(0, 0), 1, Items.Log, 4);
            l.Produce(l.GetOrAddStorage(2), Items.Log, 6);
            Assert.IsNull(l.Audit());
        }

        [Test]
        public void Audit_CountChangedOutsideOperations_ReportsFault()
        {
            var l = new Ledger();
            var pile = l.ProduceOnGround(P(0, 0), 1, Items.Log, 4);
            pile.Counts["item.log"] = 5; // tamper, bypassing Produce
            Assert.IsNotNull(l.Audit());
            pile.Counts["item.log"] = 4;
            Assert.IsNull(l.Audit());
            pile.Counts["item.log"] = 0;
            Assert.IsNotNull(l.Audit());
        }

        private static int CountLocations(Ledger l)
        {
            int n = 0;
            foreach (var _ in l.Locations) n++;
            return n;
        }
    }
}
