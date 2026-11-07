using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Areas
{
    public class AreaTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        // Road along +X from (0,0) to (20,0) with a middle sample at (10,0); width 2.5 so sides are z = +-1.25.
        private static RoadSegment MakeRoad(RoadNetwork n) =>
            n.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(0, 0), M(20, 0) }, new[] { M(0, 0), M(10, 0), M(20, 0) });

        private static PointMm[] Square() => new[] { M(0, 0), M(10, 0), M(10, 10), M(0, 10) };

        [Test]
        public void AreaTypes_MatchTuningDoc()
        {
            AreaTypeDef t = AreaTypes.Find("area.test");
            Assert.AreEqual(AreaKind.Building, t.Kind);
            Assert.AreEqual(4, t.MinPoints);
            Assert.AreEqual(8, t.MaxPoints);
            Assert.AreEqual(100, t.MinAreaM2);
            Assert.AreEqual(5000, t.MaxAreaM2);
            Assert.AreEqual(10.0, t.MaxSlopeDegrees);
            Assert.IsTrue(t.ClearsTrees);
            Assert.IsTrue(t.BlocksRoads);
            Assert.AreEqual("Test area", t.NamePrefix);

            AreaTypeDef w = AreaTypes.Find("area.test_work");
            Assert.AreEqual(AreaKind.Work, w.Kind);
            Assert.AreEqual(4, w.MinPoints);
            Assert.AreEqual(8, w.MaxPoints);
            Assert.AreEqual(100, w.MinAreaM2);
            Assert.AreEqual(40000, w.MaxAreaM2);
            Assert.IsNull(w.MaxSlopeDegrees);
            Assert.IsFalse(w.ClearsTrees);
            Assert.IsFalse(w.BlocksRoads);
            Assert.IsNull(AreaTypes.Find("area.nope"));
        }

        [Test]
        public void AreaShape_MismatchedEdgeCount_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => new AreaShape(Square(), new[] { AreaEdge.Straight() }));
        }

        [Test]
        public void SideLine_LeftAndRight_AreOffsetHalfWidth()
        {
            RoadSegment seg = MakeRoad(new RoadNetwork());
            Assert.AreEqual(new[] { M(0, 1.25), M(10, 1.25), M(20, 1.25) }, seg.SideLine(RoadSide.Left));
            Assert.AreEqual(new[] { M(0, -1.25), M(10, -1.25), M(20, -1.25) }, seg.SideLine(RoadSide.Right));
        }

        [Test]
        public void CorridorPolygon_IsCounterClockwiseWithRoadArea()
        {
            PointMm[] poly = MakeRoad(new RoadNetwork()).CorridorPolygon();
            Assert.IsTrue(Polygons.IsCounterClockwise(poly));
            Assert.AreEqual(50.0, Polygons.SignedAreaM2(poly), 1e-6);
        }

        [Test]
        public void SideLine_BentRoad_StaysHalfWidthFromCentreLine()
        {
            var n = new RoadNetwork();
            RoadSegment seg = n.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(0, 0), M(10, 10) }, new[] { M(0, 0), M(10, 0), M(10, 10) });
            foreach (PointMm p in seg.SideLine(RoadSide.Left).Concat(seg.SideLine(RoadSide.Right)))
                Assert.That(Polylines.Nearest(seg.Samples, p).DistanceMm, Is.GreaterThanOrEqualTo(1250 - 1));
        }

        [Test]
        public void Flatten_StraightEdges_ReturnsVerticesUnchanged()
        {
            PointMm[] r = AreaFlattener.Flatten(AreaShape.Straight(Square()), new RoadNetwork());
            Assert.AreEqual(Square(), r);
        }

        [Test]
        public void Flatten_ClockwiseInput_ReturnsCounterClockwise()
        {
            PointMm[] cw = Square().Reverse().ToArray();
            PointMm[] r = AreaFlattener.Flatten(AreaShape.Straight(cw), new RoadNetwork());
            Assert.IsTrue(Polygons.IsCounterClockwise(r));
            Assert.AreEqual(4, r.Length);
            Assert.AreEqual(100.0, Polygons.SignedAreaM2(r), 1e-6);
        }

        [Test]
        public void Flatten_BentEdge_PassesThroughHandleAndSamplesEveryMetre()
        {
            var edges = new[] { AreaEdge.Bent(M(5, -3)), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() };
            PointMm[] r = AreaFlattener.Flatten(new AreaShape(Square(), edges), new RoadNetwork());
            Assert.IsTrue(Polygons.IsCounterClockwise(r));
            Assert.Greater(r.Length, 14);
            var closed = r.Concat(new[] { r[0] }).ToArray();
            Assert.Less(Polylines.Nearest(closed, M(5, -3)).DistanceMm, 100);
            // The bulge adds 2/3 * chord * height = 2/3 * 10 * 3 = 20 m² to the square.
            Assert.AreEqual(120.0, Polygons.SignedAreaM2(r), 0.5);
            int start = System.Array.IndexOf(r, M(0, 0));
            for (int i = start; i < start + 10; i++) Assert.LessOrEqual(r[i % r.Length].DistanceTo(r[(i + 1) % r.Length]), 1500);
            Assert.IsTrue(Polygons.IsSimple(r));
        }

        [Test]
        public void Flatten_BentEdge_CustomSpacing_ChangesPointCount()
        {
            var edges = new[] { AreaEdge.Bent(M(5, -3)), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() };
            var shape = new AreaShape(Square(), edges);
            int fine = AreaFlattener.Flatten(shape, new RoadNetwork(), new SimTuning { CurveFlattenSpacingM = 0.5 }).Length;
            int coarse = AreaFlattener.Flatten(shape, new RoadNetwork(), new SimTuning { CurveFlattenSpacingM = 2 }).Length;
            Assert.Greater(fine, coarse);
        }

        [Test]
        public void Flatten_BentEdgeWithHandleAtMidpoint_AddsNoPoints()
        {
            var edges = new[] { AreaEdge.Bent(M(5, 0)), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() };
            Assert.AreEqual(Square(), AreaFlattener.Flatten(new AreaShape(Square(), edges), new RoadNetwork()));
        }

        [Test]
        public void Flatten_BentEdgeOnZeroLengthChord_DoesNotThrowAndHasNoDuplicates()
        {
            var verts = new[] { M(0, 0), M(0, 0), M(10, 10), M(0, 10) };
            var edges = new[] { AreaEdge.Bent(M(0, -2)), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() };
            PointMm[] r = AreaFlattener.Flatten(new AreaShape(verts, edges), new RoadNetwork());
            for (int i = 0; i < r.Length; i++) Assert.AreNotEqual(r[i], r[(i + 1) % r.Length]);
        }

        [Test]
        public void Flatten_FollowsRoad_CopiesSideLinePointBetweenVertices()
        {
            var n = new RoadNetwork();
            RoadSegment seg = MakeRoad(n);
            var verts = new[] { M(5, 1.25), M(15, 1.25), M(15, 20), M(5, 20) };
            var edges = new[] { AreaEdge.FollowsRoad(seg.Id, RoadSide.Left), AreaEdge.Straight(),
                                AreaEdge.Straight(), AreaEdge.Straight() };
            PointMm[] r = AreaFlattener.Flatten(new AreaShape(verts, edges), n);
            Assert.AreEqual(new[] { M(5, 1.25), M(10, 1.25), M(15, 1.25), M(15, 20), M(5, 20) }, r);
        }

        [Test]
        public void Flatten_FollowsRoad_VerticesAgainstTravelDirection_ReversesSamples()
        {
            var n = new RoadNetwork();
            RoadSegment seg = MakeRoad(n);
            // Clockwise input; edge 0 runs from x=18 back to x=2 along the left side, passing (10, 1.25).
            var verts = new[] { M(18, 1.25), M(2, 1.25), M(2, 20), M(18, 20) };
            var edges = new[] { AreaEdge.FollowsRoad(seg.Id, RoadSide.Left), AreaEdge.Straight(),
                                AreaEdge.Straight(), AreaEdge.Straight() };
            PointMm[] r = AreaFlattener.Flatten(new AreaShape(verts, edges), n);
            Assert.IsTrue(Polygons.IsCounterClockwise(r));
            Assert.Contains(M(10, 1.25), r);
            Assert.AreEqual(5, r.Length);
        }

        [Test]
        public void Flatten_FollowsRoad_MissingSegment_FallsBackToStraight()
        {
            var verts = new[] { M(5, 1.25), M(15, 1.25), M(15, 20), M(5, 20) };
            var edges = new[] { AreaEdge.FollowsRoad(999, RoadSide.Left), AreaEdge.Straight(),
                                AreaEdge.Straight(), AreaEdge.Straight() };
            var shape = new AreaShape(verts, edges);
            Assert.AreEqual(verts, AreaFlattener.Flatten(shape, new RoadNetwork()));
            Assert.AreEqual(verts, AreaFlattener.Flatten(shape, null));
        }

        [Test]
        public void Flatten_FollowsRoad_SegmentDeletedLater_FallsBackToStraight()
        {
            var n = new RoadNetwork();
            RoadSegment seg = MakeRoad(n);
            var verts = new[] { M(5, 1.25), M(15, 1.25), M(15, 20), M(5, 20) };
            var edges = new[] { AreaEdge.FollowsRoad(seg.Id, RoadSide.Left), AreaEdge.Straight(),
                                AreaEdge.Straight(), AreaEdge.Straight() };
            n.RemoveSegment(seg.Id);
            Assert.AreEqual(verts, AreaFlattener.Flatten(new AreaShape(verts, edges), n));
        }

        [Test]
        public void Flatten_EmptyShape_ReturnsEmpty()
        {
            Assert.IsEmpty(AreaFlattener.Flatten(AreaShape.Straight(new PointMm[0]), new RoadNetwork()));
        }

        [Test]
        public void Flatten_RepeatedVertex_IsDropped()
        {
            var verts = new[] { M(0, 0), M(10, 0), M(10, 0), M(10, 10), M(0, 10) };
            PointMm[] r = AreaFlattener.Flatten(AreaShape.Straight(verts), new RoadNetwork());
            Assert.AreEqual(Square(), r);
        }
    }
}
