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
    /// <summary>Planks, beams and the General goods store (T-512, decision 0046).</summary>
    public class GoodsStoreTests
    {
        [Test]
        public void PlankAndBeam_AreGeneral_AndListedInAll()
        {
            Assert.AreEqual(ItemCategory.General, Items.Plank.Category);
            Assert.AreEqual(ItemCategory.General, Items.Beam.Category);
            CollectionAssert.Contains(Items.All, Items.Plank);
            CollectionAssert.Contains(Items.All, Items.Beam);
            Assert.AreSame(Items.Beam, Items.Find("item.beam"));
        }

        [Test]
        public void GoodsStore_RefusesLogsAndFood_AcceptsPlanksAndBeams()
        {
            var led = new Ledger();
            var store = led.AddGoodsStore(10);
            Assert.Throws<InvalidOperationException>(() => led.Produce(store, Items.Log, 1));
            Assert.Throws<InvalidOperationException>(() => led.Produce(store, Items.Meat, 1));
            Assert.AreEqual(0, led.ReserveCapacity(store, Items.Log, 1, 1));
            led.Produce(store, Items.Plank, 6);
            led.Produce(store, Items.Beam, 2);
            Assert.AreEqual(6, store.CountOf(Items.Plank.Id));
            Assert.AreEqual(8, store.Total);
            Assert.IsNull(led.Audit());
        }

        [Test]
        public void LogStorageAndFoodStore_RefusePlanks()
        {
            var led = new Ledger();
            Assert.Throws<InvalidOperationException>(() => led.Produce(led.GetOrAddStorage(1, 10), Items.Plank, 1));
            Assert.Throws<InvalidOperationException>(() => led.Produce(led.AddFoodStore(), Items.Beam, 1));
        }

        [Test]
        public void GoodsStore_ProduceThrowsPastCapacity()
        {
            var led = new Ledger();
            var store = led.AddGoodsStore(3);
            Assert.Throws<InvalidOperationException>(() => led.Produce(store, Items.Plank, 4));
        }

        [Test]
        public void GoodsStore_TransferReserveAndConsume_Work_AndAuditStaysClean()
        {
            var led = new Ledger();
            var store = led.AddGoodsStore();
            var carrier = led.AddCarrier(5);
            var stock = led.AddStockpile();
            led.Produce(store, Items.Plank, 10);

            int stockRes = led.ReserveStock(store, Items.Plank, 4, 7);
            Assert.AreNotEqual(0, stockRes);
            Assert.AreEqual(6, store.AvailableOf(Items.Plank.Id));
            Assert.AreEqual(0, led.ReserveStock(store, Items.Plank, 7, 8), "only 6 unreserved");

            Assert.IsTrue(led.Transfer(store, carrier, Items.Plank, 4, 7));
            Assert.AreEqual(6, store.CountOf(Items.Plank.Id));
            Assert.IsFalse(led.Transfer(store, stock, Items.Log, 1), "no logs held");
            Assert.IsTrue(led.Consume(carrier, Items.Plank, 3));
            Assert.AreEqual(3, led.ConsumedOf(Items.Plank.Id));
            Assert.AreEqual(7, led.TotalOf(Items.Plank));
            Assert.IsNull(led.Audit());
        }

        [Test]
        public void GroundPile_AcceptsPlanks_AndLogsStillMergeSeparately()
        {
            var led = new Ledger();
            var pile = led.ProduceOnGround(PointMm.FromMetres(1, 1), null, Items.Plank, 3);
            Assert.AreEqual(LocationKind.GroundPile, pile.Kind);
            Assert.AreEqual(3, pile.CountOf(Items.Plank.Id));
            Assert.IsTrue(pile.Accepts(ItemCategory.General));
            var store = led.AddGoodsStore();
            Assert.IsTrue(led.Transfer(pile, store, Items.Plank, 3));
            Assert.IsNull(led.Audit());
        }

        [Test]
        public void GoodsStore_RoundTripsThroughSave_WithReservationsAndCounts()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            SimWorld w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
            var store = w.Inventory.AddGoodsStore(20);
            w.Inventory.Produce(store, Items.Plank, 10);
            w.Inventory.Produce(store, Items.Beam, 2);
            w.Inventory.ReserveStock(store, Items.Plank, 3, 5);
            w.Inventory.ReserveCapacity(store, Items.Beam, 4, 5);
            w.Inventory.Consume(store, Items.Beam, 1);

            byte[] bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(bytes, map);
            Assert.AreEqual(bytes, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash));
            LedgerLocation l = back.Inventory.Get(store.Id);
            Assert.AreEqual(LocationKind.GoodsStore, l.Kind);
            Assert.AreEqual(20, l.Capacity);
            Assert.AreEqual(10, l.CountOf(Items.Plank.Id));
            Assert.AreEqual(1, l.CountOf(Items.Beam.Id));
            Assert.AreEqual(3, l.ReservedOf(Items.Plank.Id));
            Assert.AreEqual(4, l.IncomingOf(Items.Beam.Id));
            Assert.AreEqual(1, back.Inventory.ConsumedOf(Items.Beam.Id));
            Assert.IsNull(back.Inventory.Audit());
        }

        [Test]
        public void SaveVersion_IsBumpedTo23_ForGoodsStore()
        {
            Assert.GreaterOrEqual(SaveWriter.CurrentVersion, 23);
            byte[] bytes = { (byte)'L', (byte)'L', (byte)'S', (byte)'V', 22, 0, 0, 0, 0, 0, 0, 0 };
            var e = Assert.Throws<SaveFormatException>(() => SaveReader.ReadHeaderOnly(bytes));
            StringAssert.Contains("Unknown save format version 22", e.Message);
        }
    }
}
