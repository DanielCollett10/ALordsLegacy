using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Areas
{
    public class AreaValidatorTests
    {
        private const string T = AreaTypes.TestId;
        private const string W = AreaTypes.TestWorkId;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);
        private static SimWorld Ramp() => SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());

        // Axis-aligned square with its corner at (x, z).
        private static AreaShape Square(double x, double z, double s) =>
            AreaShape.Straight(new[] { M(x, z), M(x + s, z), M(x + s, z + s), M(x, z + s) });

        private static AreaShape Ngon(int n, double radius = 20)
        {
            var v = new PointMm[n];
            for (int i = 0; i < n; i++)
            {
                double a = 2 * System.Math.PI * i / n;
                v[i] = M(100 + radius * System.Math.Cos(a), 100 + radius * System.Math.Sin(a));
            }
            return AreaShape.Straight(v);
        }

        // Road along +X from (20,50) to (80,50); side lines at z = 51.25 (Left) and 48.75 (Right).
        private static RoadSegment Road(SimWorld w) =>
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(80, 50) }, new[] { M(20, 50), M(50, 50), M(80, 50) });

        [Test]
        public void Valid_ReturnsPolygonAreaAndPointCount()
        {
            AreaPreview p = AreaValidator.Validate(Ramp(), Square(50, 50, 20), T);
            Assert.IsTrue(p.IsValid);
            Assert.AreEqual(AreaRefusal.Valid, p.Reason);
            Assert.AreEqual(string.Empty, p.ReasonText);
            Assert.AreEqual(400.0, p.AreaM2, 1e-6);
            Assert.AreEqual(4, p.PointCount);
            Assert.AreEqual(4, p.Polygon.Count);
        }

        [Test]
        public void TooFewPoints_BothTypes()
        {
            var tri = AreaShape.Straight(new[] { M(50, 50), M(80, 50), M(80, 80) });
            Assert.AreEqual("too few points", AreaValidator.Validate(Ramp(), tri, T).ReasonText);
            Assert.AreEqual("too few points", AreaValidator.Validate(Ramp(), tri, W).ReasonText);
        }

        [Test]
        public void EmptyShape_IsTooFewPoints_WithEmptyPolygon()
        {
            AreaPreview p = AreaValidator.Validate(Ramp(), AreaShape.Straight(new PointMm[0]), T);
            Assert.AreEqual(AreaRefusal.TooFewPoints, p.Reason);
            Assert.AreEqual(0, p.Polygon.Count);
            Assert.AreEqual(0.0, p.AreaM2);
        }

        [Test]
        public void TooManyPoints_BothTypes()
        {
            Assert.AreEqual("too many points", AreaValidator.Validate(Ramp(), Ngon(9), T).ReasonText);
            Assert.AreEqual("too many points", AreaValidator.Validate(Ramp(), Ngon(9), W).ReasonText);
            Assert.IsTrue(AreaValidator.Validate(Ramp(), Ngon(8), T).IsValid);
        }

        [Test]
        public void UnknownType_IsRefused()
        {
            Assert.AreEqual(AreaRefusal.UnknownType, AreaValidator.Validate(Ramp(), Square(50, 50, 20), "area.nope").Reason);
        }

        [Test]
        public void FollowsRoad_OnSide_IsValid()
        {
            SimWorld w = Ramp();
            RoadSegment seg = Road(w);
            var shape = new AreaShape(
                new[] { M(30, 51.25), M(60, 51.25), M(60, 70), M(30, 70) },
                new[] { AreaEdge.FollowsRoad(seg.Id, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            AreaPreview p = AreaValidator.Validate(w, shape, T);
            Assert.AreEqual(AreaRefusal.Valid, p.Reason);
        }

        [Test]
        public void FollowsRoad_VertexOffByMoreThan5cm_IsNotOnRoad_BothTypes()
        {
            SimWorld w = Ramp();
            RoadSegment seg = Road(w);
            var shape = new AreaShape(
                new[] { M(30, 51.25 + 0.06), M(60, 51.25), M(60, 70), M(30, 70) },
                new[] { AreaEdge.FollowsRoad(seg.Id, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            Assert.AreEqual("not on road", AreaValidator.Validate(w, shape, T).ReasonText);
            Assert.AreEqual("not on road", AreaValidator.Validate(w, shape, W).ReasonText);
        }

        [Test]
        public void FollowsRoad_VertexOff4cm_IsWithinTolerance()
        {
            SimWorld w = Ramp();
            RoadSegment seg = Road(w);
            var shape = new AreaShape(
                new[] { M(30, 51.25 + 0.04), M(60, 51.25), M(60, 70), M(30, 70) },
                new[] { AreaEdge.FollowsRoad(seg.Id, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            Assert.AreEqual(AreaRefusal.Valid, AreaValidator.Validate(w, shape, T).Reason);
        }

        [Test]
        public void FollowsRoad_WrongSide_IsNotOnRoad()
        {
            SimWorld w = Ramp();
            RoadSegment seg = Road(w);
            var shape = new AreaShape(
                new[] { M(30, 51.25), M(60, 51.25), M(60, 70), M(30, 70) },
                new[] { AreaEdge.FollowsRoad(seg.Id, RoadSide.Right), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            Assert.AreEqual(AreaRefusal.NotOnRoad, AreaValidator.Validate(w, shape, T).Reason);
        }

        [Test]
        public void FollowsRoad_MissingSegment_IsNotOnRoad()
        {
            var shape = new AreaShape(
                new[] { M(30, 51.25), M(60, 51.25), M(60, 70), M(30, 70) },
                new[] { AreaEdge.FollowsRoad(99, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            Assert.AreEqual(AreaRefusal.NotOnRoad, AreaValidator.Validate(Ramp(), shape, T).Reason);
        }

        [Test]
        public void BowTie_EdgesCross_BothTypes()
        {
            var bow = AreaShape.Straight(new[] { M(50, 50), M(80, 80), M(80, 50), M(50, 80) });
            Assert.AreEqual("edges cross", AreaValidator.Validate(Ramp(), bow, T).ReasonText);
            Assert.AreEqual("edges cross", AreaValidator.Validate(Ramp(), bow, W).ReasonText);
        }

        // ---- T-160 / AR-11: every self-crossing outline is refused, and DesignateArea changes nothing. ----

        private static AreaShape Bent(PointMm[] v, int bentEdge, PointMm handle)
        {
            var edges = new AreaEdge[v.Length];
            for (int i = 0; i < edges.Length; i++) edges[i] = i == bentEdge ? AreaEdge.Bent(handle) : AreaEdge.Straight();
            return new AreaShape(v, edges);
        }

        private static PointMm[] Pts(double ox, double oz, params double[] xz)
        {
            var p = new PointMm[xz.Length / 2];
            for (int i = 0; i < p.Length; i++) p[i] = M(ox + xz[2 * i], oz + xz[2 * i + 1]);
            return p;
        }

        private static void AssertRefused(AreaShape shape)
        {
            SimWorld w = Ramp();
            foreach (string type in new[] { T, W })
                Assert.AreEqual(AreaRefusal.EdgesCross, AreaValidator.Validate(w, shape, type).Reason);

            int areas = w.Areas.Version, trees = w.Trees.Version, roads = w.Roads.Version;
            CommandResult r = w.Execute(new DesignateArea(shape, T));
            Assert.IsFalse(r.IsAccepted);
            Assert.AreEqual(areas, w.Areas.Version);
            Assert.AreEqual(trees, w.Trees.Version);
            Assert.AreEqual(roads, w.Roads.Version);
            Assert.AreEqual(0, System.Linq.Enumerable.Count(w.Areas.All));
        }

        [Test]
        public void OwnerRepro_FourPointsInCrossingOrder_EdgesCross() =>
            AssertRefused(AreaShape.Straight(Pts(50, 50, 0, 0, 20, 20, 20, 0, 0, 20)));

        [Test]
        public void Hourglass_OtherVertexOrder_EdgesCross() =>
            AssertRefused(AreaShape.Straight(Pts(50, 50, 0, 0, 0, 20, 20, 0, 20, 20)));

        [Test]
        public void Figure8_SixPoints_EdgesCross() =>
            AssertRefused(AreaShape.Straight(Pts(50, 50, 0, 0, 10, 12, 20, 20, 20, 0, 10, 8, 0, 20)));

        [Test]
        public void Figure8_EightPoints_EdgesCross() =>
            AssertRefused(AreaShape.Straight(Pts(60, 50, 0, 0, 10, 9, 20, 20, 30, 10, 20, 0, 10, 11, 0, 20, -10, 10)));

        [Test]
        public void BentEdgeCrossingStraightEdge_EdgesCross() =>
            AssertRefused(Bent(Pts(50, 50, 0, 0, 20, 20, 20, 0, 0, 20), 0, M(62, 58)));

        [Test]
        public void BentEdgeBulgingThroughNonAdjacentEdge_EdgesCross() =>
            AssertRefused(Bent(Pts(50, 50, 0, 0, 20, 0, 20, 20, 0, 20), 0, M(60, 90)));

        [Test]
        public void FollowsRoadEdgeCrossedByAnotherEdge_EdgesCross()
        {
            SimWorld w = Ramp();
            RoadSegment seg = Road(w);
            var shape = new AreaShape(
                new[] { M(30, 51.25), M(60, 51.25), M(40, 70), M(50, 40) },
                new[] { AreaEdge.FollowsRoad(seg.Id, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            Assert.AreEqual(AreaRefusal.EdgesCross, AreaValidator.Validate(w, shape, T).Reason);
            Assert.AreEqual(AreaRefusal.EdgesCross, AreaValidator.Validate(w, shape, W).Reason);
            int areas = w.Areas.Version, trees = w.Trees.Version;
            Assert.IsFalse(w.Execute(new DesignateArea(shape, T)).IsAccepted);
            Assert.AreEqual(areas, w.Areas.Version);
            Assert.AreEqual(trees, w.Trees.Version);
            Assert.AreEqual(0, System.Linq.Enumerable.Count(w.Areas.All));
        }

        [Test]
        public void VertexTouchingNonAdjacentEdge_EdgesCross() =>
            AssertRefused(AreaShape.Straight(Pts(0, 0, 50, 50, 70, 50, 50, 60, 70, 70, 50, 70)));

        [Test]
        public void RepeatedPoint_ZeroLengthEdge_EdgesCross()
        {
            var shape = AreaShape.Straight(new[] { M(50, 50), M(80, 50), M(80, 50), M(80, 80), M(50, 80) });
            Assert.AreEqual(AreaRefusal.EdgesCross, AreaValidator.Validate(Ramp(), shape, T).Reason);
        }

        [Test]
        public void RepeatedPointAcrossWrap_ZeroLengthEdge_EdgesCross()
        {
            var shape = AreaShape.Straight(new[] { M(50, 50), M(80, 50), M(80, 80), M(50, 80), M(50, 50) });
            Assert.AreEqual(AreaRefusal.EdgesCross, AreaValidator.Validate(Ramp(), shape, T).Reason);
        }

        [Test]
        public void CollinearPoints_ZeroArea_EdgesCross()
        {
            var shape = AreaShape.Straight(new[] { M(50, 50), M(60, 50), M(70, 50), M(80, 50) });
            Assert.AreEqual(AreaRefusal.EdgesCross, AreaValidator.Validate(Ramp(), shape, W).Reason);
        }

        [Test]
        public void PointOutsideMap_BothTypes()
        {
            Assert.AreEqual("outside the map", AreaValidator.Validate(Ramp(), Square(190, 50, 20), T).ReasonText);
            Assert.AreEqual("outside the map", AreaValidator.Validate(Ramp(), Square(-5, 50, 20), W).ReasonText);
        }

        [Test]
        public void BentEdgeBulgingOutsideMap_IsOutside()
        {
            var shape = new AreaShape(
                new[] { M(150, 50), M(190, 50), M(190, 90), M(150, 90) },
                new[] { AreaEdge.Straight(), AreaEdge.Bent(M(215, 70)), AreaEdge.Straight(), AreaEdge.Straight() });
            Assert.AreEqual(AreaRefusal.OutsideMap, AreaValidator.Validate(Ramp(), shape, T).Reason);
        }

        [Test]
        public void TooSmall_BothTypes()
        {
            Assert.AreEqual("too small", AreaValidator.Validate(Ramp(), Square(50, 50, 9), T).ReasonText);
            Assert.AreEqual("too small", AreaValidator.Validate(Ramp(), Square(50, 50, 9), W).ReasonText);
        }

        [Test]
        public void ExactMinimum_IsValid()
        {
            Assert.IsTrue(AreaValidator.Validate(Ramp(), Square(50, 50, 10), T).IsValid);
        }

        [Test]
        public void TooLarge_TestArea_Over5000()
        {
            // 80 x 80 = 6,400 m²: too large for a test area, fine for a work area.
            AreaShape s = Square(50, 50, 80);
            Assert.AreEqual("too large", AreaValidator.Validate(Ramp(), s, T).ReasonText);
            Assert.IsTrue(AreaValidator.Validate(Ramp(), s, W).IsValid);
        }

        [Test]
        public void TooLarge_TestWorkArea_Over40000()
        {
            // The ramp map is only 200 x 200 = 40,000 m², so use a flat world (unbounded).
            var w = new SimWorld(new SimTuning());
            Assert.AreEqual("too large", AreaValidator.Validate(w, Square(0, 0, 201), W).ReasonText);
            Assert.IsTrue(AreaValidator.Validate(w, Square(0, 0, 200), W).IsValid);
        }

        [Test]
        public void FirstReasonWins_PointCountBeforeEverythingElse()
        {
            // Nine points that also lie outside the map and are far too large.
            AreaShape s = Ngon(9, 500);
            Assert.AreEqual(AreaRefusal.TooManyPoints, AreaValidator.Validate(Ramp(), s, T).Reason);
        }

        [Test]
        public void FirstReasonWins_NotOnRoadBeforeEdgesCross()
        {
            SimWorld w = Ramp();
            Road(w);
            var shape = new AreaShape(
                new[] { M(50, 50), M(80, 80), M(80, 50), M(50, 80) },
                new[] { AreaEdge.FollowsRoad(0, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            Assert.AreEqual(AreaRefusal.NotOnRoad, AreaValidator.Validate(w, shape, T).Reason);
        }

        [Test]
        public void FirstReasonWins_EdgesCrossBeforeOutsideMap()
        {
            var bow = AreaShape.Straight(new[] { M(190, 50), M(230, 90), M(230, 50), M(190, 90) });
            Assert.AreEqual(AreaRefusal.EdgesCross, AreaValidator.Validate(Ramp(), bow, T).Reason);
        }

        [Test]
        public void FirstReasonWins_OutsideMapBeforeTooSmall()
        {
            Assert.AreEqual(AreaRefusal.OutsideMap, AreaValidator.Validate(Ramp(), Square(195, 50, 9), T).Reason);
        }

        [Test]
        public void Validate_DoesNotChangeWorld()
        {
            SimWorld w = Ramp();
            Road(w);
            int roadVersion = w.Roads.Version, areaVersion = w.Areas.Version, trees = w.Trees.Version;
            long tick = w.Tick;
            AreaValidator.Validate(w, Square(50, 52, 20), T);
            AreaValidator.Validate(w, Square(50, 52, 20), W);
            Assert.AreEqual(roadVersion, w.Roads.Version);
            Assert.AreEqual(areaVersion, w.Areas.Version);
            Assert.AreEqual(trees, w.Trees.Version);
            Assert.AreEqual(tick, w.Tick);
            Assert.AreEqual(0, System.Linq.Enumerable.Count(w.Areas.All));
        }
    }
}
