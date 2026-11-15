using System;
using System.Linq;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Save
{
    public class LedgerFoodTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        [Test]
        public void FoodItems_HaveRations()
        {
            Assert.AreEqual(3, Items.Meat.RationsPerItem);
            Assert.AreEqual(1, Items.Berries.RationsPerItem);
            Assert.AreEqual(1, Items.Mushrooms.RationsPerItem);
            Assert.AreEqual(ItemCategory.Food, Items.Find("item.meat").Category);
            Assert.AreEqual(ItemCategory.Food, Items.Find("item.berries").Category);
            Assert.AreEqual(ItemCategory.Food, Items.Find("item.mushrooms").Category);
        }

        [Test]
        public void LogStorage_RefusesFood_WithReason()
        {
            var led = new Ledger();
            var store = led.GetOrAddStorage(1, 10);
            var ex = Assert.Throws<InvalidOperationException>(() => led.Produce(store, Items.Meat, 1));
            StringAssert.Contains("Food", ex.Message);
            Assert.AreEqual(0, led.ReserveCapacity(store, Items.Meat, 1, 1));
            Assert.IsNotNull(Ledger.RefusalReason(store, Items.Meat));
        }

        [Test]
        public void FoodStore_RefusesLogs_AcceptsFood()
        {
            var led = new Ledger();
            var larder = led.AddFoodStore(10);
            Assert.Throws<InvalidOperationException>(() => led.Produce(larder, Items.Log, 1));
            led.Produce(larder, Items.Berries, 2);
            Assert.AreEqual(2, larder.CountOf(Items.Berries.Id));
            Assert.IsNull(led.Audit());
        }

        [Test]
        public void Transfer_IntoWrongCategoryStore_Fails_AndChangesNothing()
        {
            var led = new Ledger();
            var stock = led.AddStockpile();
            led.Produce(stock, Items.Log, 3);
            led.Produce(stock, Items.Meat, 3);
            var larder = led.AddFoodStore();
            var logs = led.GetOrAddStorage(1, 10);
            Assert.IsFalse(led.Transfer(stock, larder, Items.Log, 1));
            Assert.IsFalse(led.Transfer(stock, logs, Items.Meat, 1));
            Assert.IsTrue(led.Transfer(stock, larder, Items.Meat, 2));
            Assert.IsTrue(led.Transfer(stock, logs, Items.Log, 2));
            Assert.IsNull(led.Audit());
        }

        [Test]
        public void UndeclaredKinds_AcceptAnything()
        {
            var led = new Ledger();
            var pile = led.ProduceOnGround(M(1, 1), null, Items.Meat, 1);
            var ox = led.AddCarrier(5);
            var stock = led.AddStockpile();
            led.Produce(ox, Items.Meat, 1);
            led.Produce(ox, Items.Log, 1);
            led.Produce(stock, Items.Mushrooms, 1);
            led.Produce(stock, Items.Log, 1);
            Assert.IsNotNull(pile);
        }

        [Test]
        public void LogsAndFood_RoundTripThroughSave()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
            Ledger led = w.Inventory;
            var stock = led.AddStockpile(M(10, 10));
            led.Produce(stock, Items.Log, 5);
            led.Produce(stock, Items.Meat, 4);
            var larder = led.AddFoodStore(20);
            Assert.IsTrue(led.Transfer(stock, larder, Items.Meat, 3));
            led.Produce(larder, Items.Berries, 2);
            led.Produce(larder, Items.Mushrooms, 1);

            var bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            var back = SimSave.Read(bytes, map);
            Assert.AreEqual(bytes, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash));
            Assert.IsNull(back.Inventory.Audit());
            foreach (var item in Items.All)
                Assert.AreEqual(led.TotalOf(item), back.Inventory.TotalOf(item), item.Id);
            var l2 = back.Inventory.Locations.Single(l => l.Kind == LocationKind.FoodStore);
            Assert.AreEqual(3, l2.CountOf(Items.Meat.Id));
            Assert.AreEqual(2, l2.CountOf(Items.Berries.Id));
            Assert.AreEqual(20, l2.Capacity);
            Assert.IsFalse(l2.Accepts(ItemCategory.Log));
        }
    }
}
