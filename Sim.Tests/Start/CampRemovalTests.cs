using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Start
{
    /// <summary>Camp removal and the leftover pile (T-467, CMP-23, CMP-24, CN-11).</summary>
    public class CampRemovalTests
    {
        private const long Adult = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld Make(MapData map, int logs = 10)
        {
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
            var layout = new StartLayout(M(100, 100), 0, new PointMm(0, 0), new PointMm(6000, 0), new PointMm(-6000, 0),
                new PointMm[0], new StartFamily[0], 2);
            StartLayoutApplier.Apply(w, layout);
            if (logs > 0) w.Inventory.Produce(w.Camp.StockpileLocation, Items.Log, logs);
            return w;
        }

        private static SimWorld Make(int logs = 10) => Make(SyntheticMap.CreateRamp(), logs);

        private static Family AddFamily(SimWorld w, int id, bool housed)
        {
            Family f = w.People.AddFamily(id, "F" + id, 100, 100);
            w.People.AddCitizen(id, "C" + id, Sex.Male, Adult, id, 100, 100);
            if (housed) f.HomeHouseId = 900 + id;
            return f;
        }

        [Test]
        public void NotHandedOver_CampStays()
        {
            SimWorld w = Make();
            Assert.IsFalse(CampRemovalSystem.TryRemove(w));
            Assert.IsNotNull(w.Camp);
        }

        [Test]
        public void HomelessFamily_CampStays()
        {
            SimWorld w = Make();
            w.FoodHandedOver = true;
            AddFamily(w, 1, true);
            AddFamily(w, 2, false);
            Assert.IsFalse(CampRemovalSystem.TryRemove(w));
            Assert.IsNotNull(w.Camp);
        }

        [Test]
        public void HandedOverAndAllHoused_RemovesCampFootprintAndStores()
        {
            SimWorld w = Make();
            AddFamily(w, 1, true);
            int fp = w.Camp.FootprintId;
            int stockId = w.Camp.StockpileLocation.Id, foodId = w.Camp.FoodStoreLocation.Id;
            w.FoodHandedOver = true;
            Assert.IsTrue(CampRemovalSystem.TryRemove(w));
            Assert.IsNull(w.Camp);
            Assert.IsNull(w.Footprints.Get(fp));
            Assert.IsNull(w.Inventory.Get(stockId));
            Assert.IsNull(w.Inventory.Get(foodId));
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void NoFamiliesAtAll_HandedOver_RemovesCamp()
        {
            SimWorld w = Make();
            w.FoodHandedOver = true;
            Assert.IsTrue(CampRemovalSystem.TryRemove(w));
        }

        [Test]
        public void Leftovers_BecomeOneUnownedPileAtTheStockpileSpot()
        {
            SimWorld w = Make(logs: 7);
            int logsBefore = w.Camp.StockpileLocation.CountOf(Items.Log.Id);
            PointMm spot = w.Camp.Stockpile;
            int meat = w.Camp.FoodStoreLocation.CountOf(Items.Meat.Id);
            Assert.Greater(meat, 0);
            w.FoodHandedOver = true;
            CampRemovalSystem.TryRemove(w);
            LedgerLocation pile = w.Inventory.Locations.Single(l => l.Kind == LocationKind.GroundPile);
            Assert.IsNull(pile.OwnerOperationId);
            Assert.AreEqual(spot, pile.Position);
            Assert.AreEqual(logsBefore, pile.CountOf(Items.Log.Id));
            Assert.AreEqual(meat, pile.CountOf(Items.Meat.Id));
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void EmptyCamp_LeavesNoPile()
        {
            SimWorld w = Make(logs: 0);
            int have = w.Camp.StockpileLocation.CountOf(Items.Log.Id);
            if (have > 0) w.Inventory.Consume(w.Camp.StockpileLocation, Items.Log, have);
            w.Inventory.Consume(w.Camp.FoodStoreLocation, Items.Meat, w.Camp.FoodStoreLocation.CountOf(Items.Meat.Id));
            w.FoodHandedOver = true;
            Assert.IsTrue(CampRemovalSystem.TryRemove(w));
            Assert.IsFalse(w.Inventory.Locations.Any(l => l.Kind == LocationKind.GroundPile));
        }

        [Test]
        public void ReservedCampLogs_DelayRemovalUntilReleased()
        {
            SimWorld w = Make();
            w.FoodHandedOver = true;
            int res = w.Inventory.ReserveStock(w.Camp.StockpileLocation, Items.Log, 3, 77);
            Assert.IsFalse(CampRemovalSystem.TryRemove(w));
            Assert.IsNotNull(w.Camp);
            w.Inventory.Release(res);
            Assert.IsTrue(CampRemovalSystem.TryRemove(w));
        }

        [Test]
        public void Step_ChecksOnlyAtMidnight()
        {
            SimWorld w = Make();
            w.FoodHandedOver = true;
            w.Step(); // StartHour 8: not midnight
            Assert.IsNotNull(w.Camp);
            new CampRemovalSystem().Step(w);
            Assert.IsNotNull(w.Camp);
        }

        [Test]
        public void CampOxen_NoStand_RestWhereTheyAre_AndCampStandNoLongerOffered()
        {
            SimWorld w = Make();
            Ox ox = w.People.AddOx(50, "O", 94, 100);
            ox.State = OxState.Resting(w.Camp.OxStand.X, w.Camp.OxStand.Z);
            ox.X = 94.5; ox.Z = 100.5;
            w.FoodHandedOver = true;
            CampRemovalSystem.TryRemove(w);
            Assert.AreEqual(OxStateKind.Resting, ox.State.Kind);
            Assert.AreEqual(OxState.NoStand, ox.State.StandId);
            Assert.AreEqual(94.5, ox.HomeX);
            Assert.AreEqual(100.5, ox.HomeZ);
            Assert.AreEqual(94500, ox.State.SpotXMm);
            Assert.AreEqual(100500, ox.State.SpotZMm);
        }

        [Test]
        public void CampOxen_GoToNearestOxenStand()
        {
            SimWorld w = Make();
            CommandResult r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(30, 30), M(60, 30), M(60, 50), M(30, 50) }), AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Operation op = w.Operations.FindByArea((int)r.Created[0]);
            w.Footprints.Add(300, ModuleTypes.OxenStand, M(40, 40), 0);
            w.Operations.AddModule(op.Id, 300);
            w.Footprints.Add(301, ModuleTypes.OxenStand, M(180, 180), 0);
            w.Operations.AddModule(op.Id, 301);
            Ox ox = w.People.AddOx(50, "O", 94, 100);
            ox.State = OxState.Resting(w.Camp.OxStand.X, w.Camp.OxStand.Z);
            w.FoodHandedOver = true;
            CampRemovalSystem.TryRemove(w);
            Assert.AreEqual(300, ox.State.StandId);
            Assert.AreEqual(M(40, 40), new PointMm(ox.State.SpotXMm, ox.State.SpotZMm));
        }

        [Test]
        public void OxPool_RestSpot_WithNoCamp_DoesNotOfferTheCampStand()
        {
            SimWorld w = Make();
            PointMm stand = w.Camp.OxStand;
            Ox ox = w.People.AddOx(50, "O", 20, 20);
            w.FoodHandedOver = true;
            CampRemovalSystem.TryRemove(w);
            PointMm spot = new OxPool(w).RestSpot(ox, out int id);
            Assert.AreNotEqual(stand, spot);
            Assert.AreEqual(M(20, 20), spot);
            Assert.AreEqual(OxState.NoStand, id);
        }

        [Test]
        public void RemovedCamp_RoundTripsThroughSave()
        {
            MapData map = SyntheticMap.CreateRamp();
            SimWorld w = Make(map, 7);
            int logsBefore = w.Camp.StockpileLocation.CountOf(Items.Log.Id);
            AddFamily(w, 1, true);
            w.FoodHandedOver = true;
            Assert.IsTrue(CampRemovalSystem.TryRemove(w));
            byte[] first = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld loaded = SimSave.Read(first, map);
            Assert.IsNull(loaded.Camp);
            Assert.IsTrue(loaded.FoodHandedOver);
            Assert.AreEqual(logsBefore, loaded.Inventory.Locations.Single(l => l.Kind == LocationKind.GroundPile).CountOf(Items.Log.Id));
            Assert.IsNull(loaded.Footprints.Get(w.Footprints.Ids.DefaultIfEmpty(-1).Max() + 100));
            byte[] second = SimSave.Write(loaded, "s", 1, map.MapId, map.ContentHash);
            Assert.AreEqual(first, second);
        }

        [Test]
        public void SaveVersion_IsBumpedTo17()
        {
            Assert.AreEqual(17, SaveWriter.CurrentVersion);
        }
    }
}
