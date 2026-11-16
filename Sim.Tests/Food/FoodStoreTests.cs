using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Homes;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Save.Sections;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Food
{
    /// <summary>Camp food store, larders and food stats (T-415, FD-02..04, FD-10).</summary>
    public class FoodStoreTests
    {
        private const long Adult = -4_000_000;
        private static PointMm P(long x, long z) => new PointMm(x, z);
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static StartLayout Layout() => new StartLayout(
            P(100000, 100000), 0, P(0, 0), P(6000, 0), P(-6000, 0), new PointMm[0],
            new[]
            {
                new StartFamily("Ash", P(-8000, -4000), new[] { new StartCitizen("A", Sex.Male, Adult), new StartCitizen("B", Sex.Female, Adult), new StartCitizen("K", Sex.Male, 0) }),
                new StartFamily("Birch", P(-8000, 4000), new[] { new StartCitizen("C", Sex.Female, Adult) }),
            }, 2);

        private static SimWorld Started()
        {
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());
            StartLayoutApplier.Apply(w, Layout());
            return w;
        }

        [Test]
        public void Start_Has400MeatInFoodStore_SeparateFromLogStockpileWith40Logs()
        {
            SimWorld w = Started();
            LedgerLocation food = w.Camp.FoodStoreLocation, logs = w.Camp.StockpileLocation;
            Assert.AreNotSame(food, logs);
            Assert.AreEqual(LocationKind.FoodStore, food.Kind);
            Assert.AreEqual(400, food.CountOf(Items.Meat.Id));
            Assert.AreEqual(0, food.CountOf(Items.Log.Id));
            Assert.AreEqual(40, logs.CountOf(Items.Log.Id));
            Assert.AreEqual(0, logs.CountOf(Items.Meat.Id));
            Assert.AreEqual(40, w.Inventory.TotalOf(Items.Log));
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void Start_EachHomelessFamilyHasItsOwnEmptyFoodOnlyLarder()
        {
            SimWorld w = Started();
            var ids = w.People.Families.Select(f => f.LarderLocationId).ToList();
            Assert.AreEqual(2, ids.Distinct().Count());
            foreach (Family f in w.People.Families)
            {
                LedgerLocation l = w.Inventory.Get(f.LarderLocationId);
                Assert.AreEqual(LocationKind.FoodStore, l.Kind);
                Assert.AreEqual(0, l.Total);
                Assert.IsFalse(l.Accepts(ItemCategory.Log));
                Assert.AreNotEqual(w.Camp.FoodStoreLocation.Id, l.Id);
            }
        }

        [Test]
        public void StartingMeatTuning_ZeroGivesEmptyStore()
        {
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning { StartingMeat = 0 });
            StartLayoutApplier.Apply(w, Layout());
            Assert.AreEqual(0, w.Camp.FoodStoreLocation.Total);
        }

        [Test]
        public void DaysLeft_StartLayout_IsRationsOverDailyNeed()
        {
            SimWorld w = Started(); // 3 adults (1 each) + 1 child (0.5) = 3.5 rations a day
            Assert.AreEqual(1200, FoodStats.TotalRations(w));
            Assert.AreEqual(3.5, FoodStats.DailyNeed(w), 1e-9);
            Assert.AreEqual(1200 / 3.5, FoodStats.DaysLeft(w), 1e-9);
        }

        [Test]
        public void DaysLeft_CountsLarderFoodToo_AndUsesTuning()
        {
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning { StartingMeat = 0, AdultRations = 2, ChildRations = 1 });
            StartLayoutApplier.Apply(w, Layout());
            Family f = w.People.Families.First();
            w.Inventory.Produce(Larders.ForFamily(w, f), Items.Berries, 5);
            w.Inventory.Produce(w.Camp.FoodStoreLocation, Items.Mushrooms, 1);
            int expected = 5 * Items.Berries.RationsPerItem + Items.Mushrooms.RationsPerItem;
            Assert.AreEqual(expected, FoodStats.TotalRations(w));
            Assert.AreEqual(expected / 7.0, FoodStats.DaysLeft(w), 1e-9);
        }

        [Test]
        public void DaysLeft_NoOneToFeed_IsInfinity()
        {
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());
            Assert.IsTrue(double.IsPositiveInfinity(FoodStats.DaysLeft(w)));
        }

        [Test]
        public void DaysLeft_ChildTurns14_CountsAsAdult()
        {
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning { StartingMeat = 0, AgeingRate = 200 });
            w.People.AddFamily(1, "F", 0, 0);
            w.People.AddCitizen(1, "Kid", Sex.Male, 0, 1, 0, 0);
            Assert.AreEqual(0.5, FoodStats.DailyNeed(w), 1e-9);
            w.Step(60 * 24 * 30); // 30 calendar days x 200 ageing rate = ~16 years (fast: keeps the suite from timing out)
            Assert.AreEqual(1.0, FoodStats.DailyNeed(w), 1e-9);
        }

        // ---- houses: designate, build, move in --------------------------------------------------

        private static SimWorld WithRoad()
        {
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(80, 50) }, new[] { M(20, 50), M(50, 50), M(80, 50) });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(60, 40)), Items.Log, 20);
            return w;
        }

        private static int PlaceHouse(SimWorld w, double x)
        {
            int road = w.Roads.Segments.First().Id;
            var shape = new AreaShape(
                new[] { M(x, 51.25), M(x + 20, 51.25), M(x + 20, 61.25), M(x, 61.25) },
                new[] { AreaEdge.FollowsRoad(road, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            CommandResult r = w.Execute(new DesignatePlot(shape));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return (int)r.Created[1];
        }

        [Test]
        public void DesignatePlot_GivesHouseItsOwnEmptyFoodOnlyLarder()
        {
            SimWorld w = WithRoad();
            int a = PlaceHouse(w, 30), b = PlaceHouse(w, 55);
            LedgerLocation la = w.Inventory.Get(w.Homes.Get(a).LarderLocationId), lb = w.Inventory.Get(w.Homes.Get(b).LarderLocationId);
            Assert.AreEqual(LocationKind.FoodStore, la.Kind);
            Assert.AreNotSame(la, lb);
            Assert.AreEqual(0, la.Total);
            Assert.IsFalse(la.Accepts(ItemCategory.Log));
        }

        [Test]
        public void MoveHouse_LarderContentsMoveToHouse_AndBackOnEviction()
        {
            SimWorld w = WithRoad();
            Family f = w.People.AddFamily(1, "F", 10, 10);
            w.People.AddCitizen(1, "C", Sex.Male, Adult, 1, 10, 10);
            w.Inventory.Produce(Larders.ForFamily(w, f), Items.Meat, 21);
            int house = PlaceHouse(w, 30);
            w.Step(2);
            Assert.IsTrue(f.IsHomeless, "unbuilt house takes nobody");
            Assert.AreEqual(21, w.Inventory.Get(f.LarderLocationId).CountOf(Items.Meat.Id));

            w.Construction.Remove(house); // built
            w.Step(1);
            Assert.AreEqual(house, f.HomeHouseId);
            Assert.AreEqual(0, w.Inventory.Get(f.LarderLocationId).Total);
            Assert.AreEqual(21, w.Inventory.Get(w.Homes.Get(house).LarderLocationId).CountOf(Items.Meat.Id));
            Assert.IsNull(w.Inventory.Audit());

            HouseRemoval.Evict(w, house);
            Assert.IsTrue(f.IsHomeless);
            Assert.AreEqual(21, w.Inventory.Get(f.LarderLocationId).CountOf(Items.Meat.Id));
            Assert.AreEqual(0, w.Inventory.Get(w.Homes.Get(house).LarderLocationId).Total);
        }

        // ---- save --------------------------------------------------------------------------------

        [Test]
        public void SaveRoundTrip_FoodStoreAndLarders_AreIdentical()
        {
            MapData map = SyntheticMap.CreateRamp();
            SimWorld w = Started();
            Family f = w.People.Families.First();
            w.Inventory.Produce(Larders.ForFamily(w, f), Items.Berries, 3);
            byte[] first = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(first, map);
            Assert.AreEqual(w.Camp.FoodStoreLocation.Id, back.Camp.FoodStoreLocation.Id);
            Assert.AreEqual(400, back.Camp.FoodStoreLocation.CountOf(Items.Meat.Id));
            Assert.AreEqual(f.LarderLocationId, back.People.FindFamily(f.Id).LarderLocationId);
            Assert.AreEqual(3, back.Inventory.Get(f.LarderLocationId).CountOf(Items.Berries.Id));
            Assert.IsNull(SaveDiff.FirstDifference(first, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash)));
        }

        [Test]
        public void SaveRoundTrip_HouseLarderAndTuning_AreIdentical()
        {
            MapData map = SyntheticMap.CreateRamp();
            SimWorld w = WithRoad();
            w.Tuning.StartingMeat = 123; w.Tuning.AdultRations = 1.5; w.Tuning.ChildRations = 0.25;
            int house = PlaceHouse(w, 30);
            w.Inventory.Produce(Larders.ForHome(w, w.Homes.Get(house)), Items.Meat, 6);
            byte[] first = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(first, map);
            Assert.AreEqual(123, back.Tuning.StartingMeat);
            Assert.AreEqual(1.5, back.Tuning.AdultRations);
            Assert.AreEqual(0.25, back.Tuning.ChildRations);
            Assert.AreEqual(w.Homes.Get(house).LarderLocationId, back.Homes.Get(house).LarderLocationId);
            Assert.AreEqual(6, back.Inventory.Get(back.Homes.Get(house).LarderLocationId).CountOf(Items.Meat.Id));
            Assert.IsNull(SaveDiff.FirstDifference(first, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash)));
        }

        [Test]
        public void FoodSection_IsRegisteredAfterHomesBeforeMovement()
        {
            var ids = SaveSections.CreateDefault().Select(s => s.Id).ToList();
            Assert.Less(ids.IndexOf(HomesSection.SectionId), ids.IndexOf(FoodSection.SectionId));
            Assert.Less(ids.IndexOf(FoodSection.SectionId), ids.IndexOf(6));
        }

        [Test]
        public void SaveVersion_IsEight_VersionFiveBytesAreRefused()
        {
            Assert.AreEqual(17, SaveWriter.CurrentVersion);
            byte[] bytes = { (byte)'L', (byte)'L', (byte)'S', (byte)'V', 5, 0, 0, 0, 0, 0, 0, 0 };
            var e = Assert.Throws<SaveFormatException>(() => SaveReader.ReadHeaderOnly(bytes));
            StringAssert.Contains("Unknown save format version 5 (this build reads version 17).", e.Message);
        }
    }
}
