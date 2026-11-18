using System;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Inventory
{
    /// <summary>Per-item capacity on a goods store (T-555, MAT-14).</summary>
    public class ItemCapTests
    {
        private static LedgerLocation Hut(Ledger led)
        {
            var s = led.AddGoodsStore(100);
            led.SetItemCap(s, Items.WillowRod, 40);
            led.SetItemCap(s, Items.HazelRod, 40);
            return s;
        }

        [Test]
        public void FreeSpaceFor_IsSmallerOfItemCapAndTotalRoom()
        {
            var led = new Ledger();
            var s = Hut(led);
            Assert.AreEqual(40, s.FreeSpaceFor(Items.WillowRod.Id));
            led.Produce(s, Items.WillowRod, 15);
            Assert.AreEqual(25, s.FreeSpaceFor(Items.WillowRod.Id));
            Assert.AreEqual(40, s.FreeSpaceFor(Items.HazelRod.Id));
            led.Produce(s, Items.Plank, 55); // 70 of 100 used: total room is now 30
            Assert.AreEqual(25, s.FreeSpaceFor(Items.WillowRod.Id));
            led.Produce(s, Items.Plank, 10); // 80 used: total room 20 < item room 25
            Assert.AreEqual(20, s.FreeSpaceFor(Items.WillowRod.Id));
            Assert.AreEqual(20, s.FreeSpace);
        }

        [Test]
        public void Produce_ThrowsPastItemCap_AndOtherItemStillFits()
        {
            var led = new Ledger();
            var s = Hut(led);
            led.Produce(s, Items.WillowRod, 40);
            Assert.Throws<InvalidOperationException>(() => led.Produce(s, Items.WillowRod, 1));
            led.Produce(s, Items.HazelRod, 40);
            Assert.AreEqual(80, s.Total);
            Assert.IsNull(led.Audit());
        }

        [Test]
        public void ReserveCapacity_RefusesPastItemCap_CountingIncoming()
        {
            var led = new Ledger();
            var s = Hut(led);
            led.Produce(s, Items.WillowRod, 30);
            Assert.AreNotEqual(0, led.ReserveCapacity(s, Items.WillowRod, 8, 1));
            Assert.AreEqual(0, led.ReserveCapacity(s, Items.WillowRod, 3, 2));
            Assert.AreNotEqual(0, led.ReserveCapacity(s, Items.WillowRod, 2, 2));
            Assert.AreEqual(0, led.ReserveCapacity(s, Items.WillowRod, 1, 3));
            Assert.AreNotEqual(0, led.ReserveCapacity(s, Items.HazelRod, 40, 3));
            Assert.IsNull(led.Audit());
        }

        [Test]
        public void Transfer_RefusesPastItemCap_AndLeavesStateUnchanged()
        {
            var led = new Ledger();
            var s = Hut(led);
            var pile = led.GetOrAddGroundPile(new PointMm(0, 0), null);
            led.Produce(pile, Items.WillowRod, 50);
            Assert.IsFalse(led.Transfer(pile, s, Items.WillowRod, 41));
            Assert.AreEqual(50, pile.CountOf(Items.WillowRod.Id));
            Assert.AreEqual(0, s.CountOf(Items.WillowRod.Id));
            Assert.IsTrue(led.Transfer(pile, s, Items.WillowRod, 40));
            Assert.IsFalse(led.Transfer(pile, s, Items.WillowRod, 1));
            Assert.IsNull(led.Audit());
        }

        [Test]
        public void Transfer_WithCapacityReservation_FitsExactlyToItemCap()
        {
            var led = new Ledger();
            var s = Hut(led);
            var pile = led.GetOrAddGroundPile(new PointMm(0, 0), null);
            led.Produce(pile, Items.HazelRod, 45);
            Assert.AreNotEqual(0, led.ReserveCapacity(s, Items.HazelRod, 40, 7));
            Assert.IsTrue(led.Transfer(pile, s, Items.HazelRod, 40, 7));
            Assert.IsFalse(led.Transfer(pile, s, Items.HazelRod, 1, 7));
            Assert.IsNull(led.Audit());
        }

        [Test]
        public void Audit_FlagsHoldingOverItemCap()
        {
            var led = new Ledger();
            var s = led.AddGoodsStore(100);
            led.Produce(s, Items.WillowRod, 30);
            s.ItemCaps[Items.WillowRod.Id] = 20; // forced past the cap
            StringAssert.Contains("item cap", led.Audit());
        }

        [Test]
        public void StoreWithoutItemCaps_BehavesAsBefore()
        {
            var led = new Ledger();
            var s = led.AddGoodsStore(10);
            Assert.IsFalse(s.HasItemCaps);
            Assert.AreEqual(10, s.FreeSpaceFor(Items.WillowRod.Id));
            Assert.AreEqual(s.FreeSpace, s.FreeSpaceFor(Items.Plank.Id));
            led.Produce(s, Items.WillowRod, 10);
            Assert.AreEqual(0, led.ReserveCapacity(s, Items.WillowRod, 1, 1));
            var open = led.AddGoodsStore();
            Assert.AreEqual(int.MaxValue, open.FreeSpaceFor(Items.Plank.Id));
        }

        [Test]
        public void SetItemCap_RejectsNonGoodsStoreAndBadCap()
        {
            var led = new Ledger();
            Assert.Throws<InvalidOperationException>(() => led.SetItemCap(led.AddFoodStore(5), Items.WillowRod, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => led.SetItemCap(led.AddGoodsStore(5), Items.WillowRod, 0));
        }

        [Test]
        public void ItemCaps_RoundTripThroughSave_AndVersionIsBumped()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            SimWorld w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
            var s = Hut(w.Inventory);
            w.Inventory.Produce(s, Items.WillowRod, 12);
            w.Inventory.ReserveCapacity(s, Items.HazelRod, 5, 4);

            byte[] bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(bytes, map);
            Assert.AreEqual(bytes, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash));
            LedgerLocation l = back.Inventory.Get(s.Id);
            Assert.AreEqual(40, l.ItemCapOf(Items.WillowRod.Id));
            Assert.AreEqual(40, l.ItemCapOf(Items.HazelRod.Id));
            Assert.AreEqual(int.MaxValue, l.ItemCapOf(Items.Plank.Id));
            Assert.AreEqual(28, l.FreeSpaceFor(Items.WillowRod.Id));
            Assert.IsNull(back.Inventory.Audit());

            Assert.GreaterOrEqual(SaveWriter.CurrentVersion, 34);
            byte[] old = { (byte)'L', (byte)'L', (byte)'S', (byte)'V', 33, 0, 0, 0, 0, 0, 0, 0 };
            var e = Assert.Throws<SaveFormatException>(() => SaveReader.ReadHeaderOnly(old));
            StringAssert.Contains("Unknown save format version 33", e.Message);
        }
    }
}
