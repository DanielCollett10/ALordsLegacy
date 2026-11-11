using System.Linq;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class GridPathfinderTests
    {
        // 4 m wide, 40 m long wall (footprint types are width x depth in mm).
        private static readonly FootprintTypeDef Wall = new FootprintTypeDef("footprint.test_wall", 4000, 40000, true, false);
        private static readonly FootprintTypeDef Slab = new FootprintTypeDef("footprint.test_slab", 60000, 4000, true, false);

        private static PointMm M(double xm, double zm) => new PointMm((long)(xm * 1000), (long)(zm * 1000));

        // 60 m x 60 m of flat ground.
        private static SimWorld NewWorld()
        {
            const int n = 31;
            var map = new MapData("path", new HeightGrid(0f, 0f, 2f, n, n, new float[n * n]), new MapTree[0]);
            return SimWorld.FromMapData(map, new SimTuning());
        }

        private static void Road(SimWorld w, double x0, double z0, double x1, double z1) =>
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(x0, z0), M(x1, z1) }, new[] { M(x0, z0), M(x1, z1) });

        private static PathResult Find(SimWorld w, double x0, double z0, double x1, double z1)
        {
            w.Nav.Sync(w);
            return GridPathfinder.Find(w.Nav, new PathPoint(x0, z0), new PathPoint(x1, z1));
        }

        private static double Length(PathResult r)
        {
            double len = 0;
            for (int i = 1; i < r.Points.Count; i++)
            {
                double dx = r.Points[i].X - r.Points[i - 1].X, dz = r.Points[i].Z - r.Points[i - 1].Z;
                len += System.Math.Sqrt(dx * dx + dz * dz);
            }
            return len;
        }

        [Test]
        public void FlatGround_StraightLine_TwoPointsAndTimeEqualsDistance()
        {
            SimWorld w = NewWorld();
            PathResult r = Find(w, 5, 5, 45, 25);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(2, r.Points.Count);
            double d = System.Math.Sqrt(40 * 40 + 20 * 20);
            Assert.AreEqual(d, Length(r), 1e-6);
            // Off-grid-diagonal line: the grid route is a little longer than straight, but never shorter.
            Assert.GreaterOrEqual(r.TravelMinutes, d - 1e-6);
            Assert.Less(r.TravelMinutes, d * 1.1);
        }

        [Test]
        public void FlatGround_AxisAlignedLine_TimeIsExactlyDistance()
        {
            PathResult r = Find(NewWorld(), 5, 5, 45, 5);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(2, r.Points.Count);
            Assert.AreEqual(40.0, r.TravelMinutes, 1e-9);
        }

        [Test]
        public void SameCell_DirectLine()
        {
            PathResult r = Find(NewWorld(), 5.2, 5.2, 5.8, 5.2);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(0.6, r.TravelMinutes, 1e-9);
        }

        [Test]
        public void SamePoint_SinglePointZeroTime()
        {
            PathResult r = Find(NewWorld(), 5, 5, 5, 5);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(1, r.Points.Count);
            Assert.AreEqual(0.0, r.TravelMinutes, 1e-12);
        }

        [Test]
        public void SteepBlock_IsWalkedAround()
        {
            const int n = 31;
            var h = new float[n * n];
            // Steep slab: x 26..34 m rises sharply for z in 10..50 (full-height plateau edge makes steep slopes).
            for (int iz = 0; iz < n; iz++)
                for (int ix = 0; ix < n; ix++)
                {
                    float x = ix * 2f, z = iz * 2f;
                    h[iz * n + ix] = (x >= 26f && x <= 34f && z >= 10f && z <= 50f) ? 20f : 0f;
                }
            var map = new MapData("steep", new HeightGrid(0f, 0f, 2f, n, n, h), new MapTree[0]);
            SimWorld w = SimWorld.FromMapData(map, new SimTuning());
            PathResult r = Find(w, 10, 30, 50, 30);
            Assert.IsTrue(r.Ok);
            Assert.Greater(r.Points.Count, 2);
            Assert.Greater(r.TravelMinutes, 40.0);
            foreach (PathPoint p in r.Points)
                Assert.IsFalse(w.Nav.IsBlocked(w.Nav.CellX(p.X), w.Nav.CellZ(p.Z)), "point " + p + " is on blocked ground");
        }

        [Test]
        public void BlockingFootprint_IsWalkedAround_ThroughNoBlockedCell()
        {
            SimWorld w = NewWorld();
            w.Footprints.Add(1, Wall, M(30, 30), 0);
            PathResult r = Find(w, 10, 30, 50, 30);
            Assert.IsTrue(r.Ok);
            Assert.Greater(r.TravelMinutes, 40.0);
            Assert.IsTrue(r.Points.Any(p => p.Z < 10 || p.Z > 50));
        }

        [Test]
        public void DiagonalSqueezeBetweenTwoBlocks_DoesNotCutCorner()
        {
            SimWorld w = NewWorld();
            var small = new FootprintTypeDef("footprint.test_small", 2000, 2000, true, false);
            w.Footprints.Add(1, small, M(21, 23), 0); // blocks cell (10,11)
            w.Footprints.Add(2, small, M(23, 21), 0); // blocks cell (11,10)
            PathResult r = Find(w, 21, 21, 23, 23);
            Assert.IsTrue(r.Ok);
            Assert.Greater(r.TravelMinutes, 2 * 1.4142135623730951 + 1e-6, "went straight through the diagonal gap");
        }

        [Test]
        public void PrefersLongerRoadRoute_WhenFaster()
        {
            // Wall x 28..32, z 7..47. Off-road the short way is round the bottom end; a road round the top is longer
            // but 1.25 x quicker.
            SimWorld plain = NewWorld();
            plain.Footprints.Add(1, Wall, M(30, 27), 0);
            PathResult off = Find(plain, 10, 25, 50, 25);

            SimWorld w = NewWorld();
            w.Footprints.Add(1, Wall, M(30, 27), 0);
            Road(w, 10, 25, 30, 49);
            Road(w, 30, 49, 50, 25);
            PathResult on = Find(w, 10, 25, 50, 25);

            Assert.IsTrue(off.Ok && on.Ok);
            Assert.IsTrue(off.Points.Any(p => p.Z < 7), "off-road should go round the bottom");
            Assert.IsTrue(on.Points.Any(p => p.Z > 47), "with the road it should go round the top");
            Assert.Greater(Length(on), Length(off));
            Assert.Less(on.TravelMinutes, off.TravelMinutes);
        }

        [Test]
        public void RoadCorner_IsKept_WhereMultiplierChanges()
        {
            SimWorld w = NewWorld();
            Road(w, 5, 5, 5, 55);
            PathResult r = Find(w, 5, 5, 5, 55);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(50.0 / 1.25, r.TravelMinutes, 1.0);
            Assert.Less(r.TravelMinutes, 50.0);
        }

        [Test]
        public void Enclosed_GoalIsNoRoute()
        {
            SimWorld w = NewWorld();
            // Ring around (30,30): two slabs (60 x 4) can't be used; build four walls from Wall/Slab pieces.
            var side = new FootprintTypeDef("footprint.test_side", 4000, 24000, true, false);
            var top = new FootprintTypeDef("footprint.test_top", 24000, 4000, true, false);
            w.Footprints.Add(1, side, M(18, 30), 0);
            w.Footprints.Add(2, side, M(42, 30), 0);
            w.Footprints.Add(3, top, M(30, 18), 0);
            w.Footprints.Add(4, top, M(30, 42), 0);
            PathResult r = Find(w, 30, 30, 5, 5);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(0, r.Points.Count);
        }

        [Test]
        public void FullWallAcrossMap_IsNoRoute()
        {
            SimWorld w = NewWorld();
            w.Footprints.Add(1, Slab, M(30, 30), 0);
            Assert.IsFalse(Find(w, 5, 5, 55, 55).Ok);
        }

        [Test]
        public void BlockedGoal_SnapsToNearestWalkableCentre()
        {
            SimWorld w = NewWorld();
            w.Footprints.Add(1, Wall, M(30, 30), 0);
            PathResult r = Find(w, 10, 30, 30, 30); // goal inside the wall
            Assert.IsTrue(r.Ok);
            PathPoint end = r.Points[r.Points.Count - 1];
            Assert.IsFalse(w.Nav.IsBlocked(w.Nav.CellX(end.X), w.Nav.CellZ(end.Z)));
            Assert.AreEqual(w.Nav.CentreX(w.Nav.CellX(end.X)), end.X, 1e-9);
            Assert.AreEqual(w.Nav.CentreZ(w.Nav.CellZ(end.Z)), end.Z, 1e-9);
            Assert.LessOrEqual(System.Math.Abs(end.X - 30), 4.5, "should snap to the wall edge");
        }

        [Test]
        public void BlockedStart_SnapsToNearestWalkableCentre()
        {
            SimWorld w = NewWorld();
            w.Footprints.Add(1, Wall, M(30, 30), 0);
            PathResult r = Find(w, 30, 30, 10, 30);
            Assert.IsTrue(r.Ok);
            PathPoint start = r.Points[0];
            Assert.IsFalse(w.Nav.IsBlocked(w.Nav.CellX(start.X), w.Nav.CellZ(start.Z)));
        }

        [Test]
        public void BlockedGoal_FurtherThan20mFromWalkable_IsNoRoute()
        {
            const int n = 31;
            var h = new float[n * n];
            // Everything x >= 20 m is a cliff top: blocked cells all the way to the edge of the map.
            for (int iz = 0; iz < n; iz++)
                for (int ix = 0; ix < n; ix++)
                    h[iz * n + ix] = ix * 2f >= 20f ? 100f + ix * 20f : 0f;
            var map = new MapData("cliff", new HeightGrid(0f, 0f, 2f, n, n, h), new MapTree[0]);
            SimWorld w = SimWorld.FromMapData(map, new SimTuning());
            Assert.IsFalse(Find(w, 5, 30, 55, 30).Ok, "goal about 35 m from walkable ground");
            Assert.IsTrue(Find(w, 5, 30, 30, 30).Ok, "goal about 10 m from walkable ground");
        }

        [Test]
        public void OutsideGrid_StartFarAway_IsNoRoute()
        {
            Assert.IsFalse(Find(NewWorld(), -100, -100, 30, 30).Ok);
        }

        [Test]
        public void SameQuery_GivesSamePath()
        {
            SimWorld w = NewWorld();
            w.Footprints.Add(1, Wall, M(30, 30), 0);
            Road(w, 10, 20, 50, 40);
            PathResult a = Find(w, 8, 12, 52, 47);
            PathResult b = Find(w, 8, 12, 52, 47);
            Assert.IsTrue(a.Ok);
            Assert.AreEqual(a.TravelMinutes, b.TravelMinutes);
            CollectionAssert.AreEqual(a.Points.ToList(), b.Points.ToList());
        }

        [Test]
        public void SymmetricDetour_TiesBreakTheSameWayEveryTime()
        {
            // Wall centred on the straight line, equal gaps both ends: either way round is equally fast.
            PathResult first = null;
            for (int i = 0; i < 3; i++)
            {
                SimWorld w = NewWorld();
                w.Footprints.Add(1, Wall, M(30, 30), 0);
                PathResult r = Find(w, 10, 30, 50, 30);
                Assert.IsTrue(r.Ok);
                if (first == null) first = r;
                else CollectionAssert.AreEqual(first.Points.ToList(), r.Points.ToList());
            }
        }

        [Test]
        public void ThroughInterface_MatchesStatic()
        {
            SimWorld w = NewWorld();
            w.Nav.Sync(w);
            IPathfinder pf = new GridPathfinder();
            PathResult r = pf.Find(w.Nav, new PathPoint(5, 5), new PathPoint(45, 5));
            Assert.AreEqual(40.0, r.TravelMinutes, 1e-9);
        }
    }
}
