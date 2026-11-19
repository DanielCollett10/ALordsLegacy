using System.Linq;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Start
{
    /// <summary>The camp General goods store: starting planks, no beams, and the camp stays while it holds goods (T-513, ST-02, ST-30, PL-08, PL-14).</summary>
    public class CampGeneralStoreTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld NewGame(MapData map, SimTuning tuning = null)
        {
            var w = SimWorld.FromMapData(map, tuning ?? new SimTuning { StartHour = 8 });
            var layout = new StartLayout(M(100, 100), 0, new PointMm(0, 0), new PointMm(6000, 0), new PointMm(-6000, 0),
                new PointMm[0], new StartFamily[0], 2);
            StartLayoutApplier.Apply(w, layout);
            return w;
        }

        private static MapData Map() => SyntheticMap.CreateRamp();

        [Test]
        public void NewGame_GeneralStoreHoldsTenPlanksAndNoBeams()
        {
            SimWorld w = NewGame(Map());
            LedgerLocation general = w.Camp.GeneralStoreLocation;
            Assert.IsNotNull(general);
            Assert.AreEqual(LocationKind.GoodsStore, general.Kind);
            Assert.AreEqual(10, general.CountOf(Items.Plank.Id));
            Assert.AreEqual(0, general.CountOf(Items.Beam.Id));
            Assert.AreEqual(10, w.Tuning.StartingPlanks);
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void NewGame_GeneralStoreHoldsNinetyThatchAndNinetyPanels()
        {
            SimWorld w = NewGame(Map());
            LedgerLocation general = w.Camp.GeneralStoreLocation;
            Assert.AreEqual(90, general.CountOf(Items.Thatch.Id));
            Assert.AreEqual(90, general.CountOf(Items.WattlePanel.Id));
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void StartingPlanks_TuningSetsTheStock()
        {
            SimWorld w = NewGame(Map(), new SimTuning { StartHour = 8, StartingPlanks = 4 });
            Assert.AreEqual(4, w.Camp.GeneralStoreLocation.CountOf(Items.Plank.Id));
        }

        [Test]
        public void GeneralStoreWithPlanks_CampStays()
        {
            SimWorld w = NewGame(Map());
            w.FoodHandedOver = true;
            Assert.IsFalse(CampRemovalSystem.TryRemove(w));
            Assert.IsNotNull(w.Camp);
            Assert.AreEqual(10, w.Camp.GeneralStoreLocation.CountOf(Items.Plank.Id));
        }

        [Test]
        public void GeneralStoreWithReservedPlanks_CampStaysUntilReleased()
        {
            SimWorld w = NewGame(Map());
            w.FoodHandedOver = true;
            int res = w.Inventory.ReserveStock(w.Camp.GeneralStoreLocation, Items.Plank, 10, 77);
            Assert.IsFalse(CampRemovalSystem.TryRemove(w));
            w.Inventory.Release(res);
            Assert.IsFalse(CampRemovalSystem.TryRemove(w), "the planks are still in the store");
            Assert.IsNotNull(w.Camp);
        }

        [Test]
        public void GeneralStoreEmpty_CampRemovedAsBefore_StoreDropped()
        {
            SimWorld w = NewGame(Map());
            LedgerLocation general = w.Camp.GeneralStoreLocation;
            int generalId = general.Id;
            int logs = w.Camp.StockpileLocation.CountOf(Items.Log.Id);
            NoCampTests.UsePlanksUp(w);
            w.FoodHandedOver = true;
            Assert.IsTrue(CampRemovalSystem.TryRemove(w));
            Assert.IsNull(w.Camp);
            Assert.IsNull(w.Inventory.Get(generalId));
            LedgerLocation pile = w.Inventory.Locations.Single(l => l.Kind == LocationKind.GroundPile);
            Assert.AreEqual(logs, pile.CountOf(Items.Log.Id), "logs still move to the leftover pile");
            Assert.AreEqual(0, pile.CountOf(Items.Plank.Id), "no planks are left behind");
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void CampWithPlanks_RoundTripsThroughSave()
        {
            MapData map = Map();
            SimWorld w = NewGame(map);
            int id = w.Camp.GeneralStoreLocation.Id;
            byte[] first = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld loaded = SimSave.Read(first, map);
            Assert.IsNotNull(loaded.Camp.GeneralStoreLocation);
            Assert.AreEqual(id, loaded.Camp.GeneralStoreLocation.Id);
            Assert.AreEqual(10, loaded.Camp.GeneralStoreLocation.CountOf(Items.Plank.Id));
            Assert.AreEqual(0, loaded.Camp.GeneralStoreLocation.CountOf(Items.Beam.Id));
            Assert.AreEqual(10, loaded.Tuning.StartingPlanks);
            byte[] second = SimSave.Write(loaded, "s", 1, map.MapId, map.ContentHash);
            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void CampWithSomePlanksUsed_RoundTripsTheCount()
        {
            MapData map = Map();
            SimWorld w = NewGame(map);
            w.Inventory.Consume(w.Camp.GeneralStoreLocation, Items.Plank, 6);
            byte[] first = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld loaded = SimSave.Read(first, map);
            Assert.AreEqual(4, loaded.Camp.GeneralStoreLocation.CountOf(Items.Plank.Id));
            Assert.IsNull(loaded.Inventory.Audit());
        }
    }
}
