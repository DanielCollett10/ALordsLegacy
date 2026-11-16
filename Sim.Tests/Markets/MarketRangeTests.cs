using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Markets;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Markets
{
    /// <summary>Houses in walking range of a market centre, and the market centre (T-457, MK-34, MK-41).</summary>
    public class MarketRangeTests
    {
        // 4 m wide, 40 m long wall (Z-long), blocks movement.
        private static readonly FootprintTypeDef Wall = new FootprintTypeDef("footprint.test_wall", 4000, 40000, true, false);
        private static readonly FootprintTypeDef Side = new FootprintTypeDef("footprint.test_side", 4000, 24000, true, false);
        private static readonly FootprintTypeDef Top = new FootprintTypeDef("footprint.test_top", 24000, 4000, true, false);

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld World(double rangeM = 50)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            return SimWorld.FromMapData(map, new SimTuning { MarketRangeM = rangeM });
        }

        /// <summary>A house whose access point is 5 m south of its centre (house is 8 m deep, access 1 m out).</summary>
        private static void House(SimWorld w, int id, double x, double z) => w.Footprints.Add(id, ModuleTypes.House, M(x, z), 0);

        private static HouseRangeResult Range(SimWorld w, double x, double z) => MarketRange.HousesInRange(w, M(x, z));

        private static AreaShape Rect(double x, double z, double width, double height) =>
            AreaShape.Straight(new[] { M(x, z), M(x + width, z), M(x + width, z + height), M(x, z + height) });

        [Test]
        public void InRange_HouseWithinWalkingRange_IsListedIn()
        {
            SimWorld w = World(50);
            House(w, 1, 30, 50); // access (30, 45): 15 m from the centre
            HouseRangeResult r = Range(w, 30, 30);
            CollectionAssert.AreEqual(new[] { 1 }, r.InRange);
            CollectionAssert.IsEmpty(r.OutOfRange);
        }

        [Test]
        public void OutOfRange_HouseBeyondRange_IsListedOut()
        {
            SimWorld w = World(50);
            House(w, 1, 30, 120); // access (30, 115): 85 m away
            HouseRangeResult r = Range(w, 30, 30);
            CollectionAssert.IsEmpty(r.InRange);
            CollectionAssert.AreEqual(new[] { 1 }, r.OutOfRange);
        }

        /// <summary>A wall (z 10..50) between centre (10, 30) and a house at (50, 30), access (50, 25).</summary>
        private static SimWorld DetourWorld(double rangeM)
        {
            SimWorld w = World(rangeM);
            w.Footprints.Add(1, Wall, M(30, 30), 0);
            House(w, 2, 50, 30);
            w.Nav.Sync(w);
            return w;
        }

        [Test]
        public void OutOfRange_DetourLongerThanRange_IsOutEvenThoughStraightLineIsWithin()
        {
            SimWorld w = DetourWorld(50);
            double straight = System.Math.Sqrt(40 * 40 + 5 * 5);
            double walk = AllotmentSystem.WalkingDistanceM(w, new PathPoint(10, 30), new PathPoint(50, 25));
            Assert.Less(straight, 50, "precondition: straight line is in range");
            Assert.Greater(walk, 50, "precondition: the walk round the wall is out of range");

            HouseRangeResult r = Range(w, 10, 30);
            CollectionAssert.IsEmpty(r.InRange);
            CollectionAssert.AreEqual(new[] { 2 }, r.OutOfRange);
        }

        [Test]
        public void InRange_DetourWithinRange_IsIn()
        {
            SimWorld w = DetourWorld(80); // same walk, range now covers the detour
            CollectionAssert.AreEqual(new[] { 2 }, Range(w, 10, 30).InRange);
        }

        [Test]
        public void BlockedPath_HouseInsideAWallRing_IsOutFromOutsideTheRing()
        {
            SimWorld w = World(630); // default-sized range: only the blocked route can put it out
            w.Footprints.Add(1, Side, M(18, 30), 0);
            w.Footprints.Add(2, Side, M(42, 30), 0);
            w.Footprints.Add(3, Top, M(30, 18), 0);
            w.Footprints.Add(4, Top, M(30, 42), 0);
            House(w, 5, 30, 30); // inside the ring; access (30, 25)

            HouseRangeResult outside = Range(w, 5, 5);
            CollectionAssert.IsEmpty(outside.InRange);
            CollectionAssert.AreEqual(new[] { 5 }, outside.OutOfRange, "no walking route counts as out of range");
        }

        [Test]
        public void BlockedPath_CentreInsideTheRing_ReachesTheHouseInside()
        {
            SimWorld w = World(630);
            w.Footprints.Add(1, Side, M(18, 30), 0);
            w.Footprints.Add(2, Side, M(42, 30), 0);
            w.Footprints.Add(3, Top, M(30, 18), 0);
            w.Footprints.Add(4, Top, M(30, 42), 0);
            House(w, 5, 30, 30);
            CollectionAssert.AreEqual(new[] { 5 }, Range(w, 30, 37).InRange);
        }

        [Test]
        public void ZeroLengthWalk_CentreOnTheAccessPoint_IsInEvenWithZeroRange()
        {
            SimWorld w = World(0);
            House(w, 1, 30, 50); // access (30, 45)
            CollectionAssert.AreEqual(new[] { 1 }, Range(w, 30, 45).InRange);
        }

        [Test]
        public void DraftCentre_UsesThePointGiven_NotAMarket()
        {
            SimWorld w = World(50);
            House(w, 1, 30, 50);  // access (30, 45)
            House(w, 2, 30, 120); // access (30, 115)
            Assert.AreEqual(0, w.Markets.Count, "precondition: no market is drawn");

            HouseRangeResult near1 = Range(w, 30, 40);
            CollectionAssert.AreEqual(new[] { 1 }, near1.InRange);
            CollectionAssert.AreEqual(new[] { 2 }, near1.OutOfRange);

            HouseRangeResult near2 = Range(w, 30, 110);
            CollectionAssert.AreEqual(new[] { 2 }, near2.InRange);
            CollectionAssert.AreEqual(new[] { 1 }, near2.OutOfRange);
        }

        [Test]
        public void DraftCentre_OnAMarketArea_UsesAreaCentreFromCentreOf()
        {
            SimWorld w = World(50);
            Assert.IsTrue(w.Execute(new DesignateArea(Rect(30, 30, 20, 20), AreaTypes.MarketId)).IsAccepted);
            Market m = w.Markets.Current;
            PointMm centre = MarketRange.CentreOf(w, m);
            House(w, 1, 40, 60); // access (40, 55): 15 m from the centre (40, 40)
            House(w, 2, 150, 40); // access (150, 35): far out
            HouseRangeResult r = MarketRange.HousesInRange(w, centre);
            CollectionAssert.AreEqual(new[] { 1 }, r.InRange);
            CollectionAssert.AreEqual(new[] { 2 }, r.OutOfRange);
        }

        [Test]
        public void CentreOf_RectangularMarket_IsItsMidpoint()
        {
            SimWorld w = World();
            Assert.IsTrue(w.Execute(new DesignateArea(Rect(30, 30, 20, 20), AreaTypes.MarketId)).IsAccepted);
            PointMm c = MarketRange.CentreOf(w, w.Markets.Current);
            Assert.AreEqual(40000, c.X);
            Assert.AreEqual(40000, c.Z);
        }

        [Test]
        public void CentreOf_MarketWithMissingArea_Throws()
        {
            SimWorld w = World();
            var m = w.Markets.Add(999); // no such area
            Assert.Throws<System.InvalidOperationException>(() => MarketRange.CentreOf(w, m));
        }

        [Test]
        public void HousesInRange_ThreeHousesAtMixedDistances_SplitsEachOne()
        {
            SimWorld w = World(50);
            House(w, 3, 30, 60);   // 25 m out
            House(w, 1, 30, 40);   // 5 m out (access at z 35, centre z 30)
            House(w, 2, 30, 100);  // 65 m out, out of range
            HouseRangeResult r = Range(w, 30, 30);
            CollectionAssert.AreEqual(new[] { 1, 3 }, r.InRange);
            CollectionAssert.AreEqual(new[] { 2 }, r.OutOfRange);
        }

        [Test]
        public void SameMeasureAsAllotment_RangeExactlyTheWalk_IsIn_AndJustUnderIsOut()
        {
            SimWorld probe = World(50);
            House(probe, 1, 30, 50);
            probe.Nav.Sync(probe);
            double d = AllotmentSystem.WalkingDistanceM(probe, new PathPoint(30, 30), new PathPoint(30, 45));
            Assert.Greater(d, 0);

            SimWorld atEdge = World(d);
            House(atEdge, 1, 30, 50);
            CollectionAssert.AreEqual(new[] { 1 }, Range(atEdge, 30, 30).InRange, "range is inclusive");

            SimWorld justUnder = World(d - 0.01);
            House(justUnder, 1, 30, 50);
            CollectionAssert.AreEqual(new[] { 1 }, Range(justUnder, 30, 30).OutOfRange);
        }

        [Test]
        public void Deterministic_SameWorldAndCentre_GiveSameLists()
        {
            SimWorld w = World(50);
            House(w, 1, 30, 50);
            House(w, 2, 30, 120);
            HouseRangeResult a = Range(w, 30, 30), b = Range(w, 30, 30);
            CollectionAssert.AreEqual(a.InRange, b.InRange);
            CollectionAssert.AreEqual(a.OutOfRange, b.OutOfRange);
        }
    }
}
