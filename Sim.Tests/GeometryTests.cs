using LordsLegacy.Sim.Geometry;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class GeometryTests
    {
        private static PointMm P(long x, long z) => new PointMm(x, z);

        private static PointMm[] Square(long s = 10) => new[] { P(0, 0), P(s, 0), P(s, s), P(0, s) };

        [Test]
        public void PointMm_MetresRoundTrip()
        {
            var p = PointMm.FromMetres(1.2345, -2.5);
            Assert.AreEqual(1235, p.X);
            Assert.AreEqual(-2500, p.Z);
            Assert.AreEqual(-2.5, p.ToMetres().Z, 1e-12);
            Assert.AreEqual(P(1, 2), P(1, 2));
        }

        [Test]
        public void Segments_Crossing() =>
            Assert.AreEqual(SegmentRelation.Crossing, Segments.Classify(P(0, 0), P(10, 10), P(0, 10), P(10, 0)));

        [Test]
        public void Segments_SharedVertexAndTJoinTouch()
        {
            Assert.AreEqual(SegmentRelation.Touching, Segments.Classify(P(0, 0), P(5, 5), P(5, 5), P(9, 0)));
            Assert.AreEqual(SegmentRelation.Touching, Segments.Classify(P(0, 0), P(10, 0), P(5, 0), P(5, 7)));
        }

        [Test]
        public void Segments_CollinearCases()
        {
            Assert.AreEqual(SegmentRelation.CollinearOverlap, Segments.Classify(P(0, 0), P(10, 0), P(5, 0), P(20, 0)));
            Assert.AreEqual(SegmentRelation.Touching, Segments.Classify(P(0, 0), P(10, 0), P(10, 0), P(20, 0)));
            Assert.AreEqual(SegmentRelation.None, Segments.Classify(P(0, 0), P(10, 0), P(11, 0), P(20, 0)));
            Assert.AreEqual(SegmentRelation.CollinearOverlap, Segments.Classify(P(0, 0), P(0, 10), P(0, 10), P(0, 5)));
        }

        [Test]
        public void Segments_DisjointAndNearMiss()
        {
            Assert.AreEqual(SegmentRelation.None, Segments.Classify(P(0, 0), P(10, 0), P(0, 1), P(10, 1)));
            Assert.AreEqual(SegmentRelation.None,
                Segments.Classify(P(0, 0), P(25_000_000, 25_000_000), P(0, 1), P(12_000_000, 12_000_001)));
        }

        [Test]
        public void Segments_ZeroLengthSegments()
        {
            Assert.AreEqual(SegmentRelation.None, Segments.Classify(P(5, 5), P(5, 5), P(0, 0), P(10, 0)));
            Assert.AreEqual(SegmentRelation.None, Segments.Classify(P(0, 0), P(10, 0), P(5, 5), P(5, 5)));
            Assert.AreEqual(SegmentRelation.Touching, Segments.Classify(P(5, 0), P(5, 0), P(0, 0), P(10, 0)));
            Assert.AreEqual(SegmentRelation.Touching, Segments.Classify(P(0, 0), P(10, 0), P(5, 0), P(5, 0)));
            Assert.AreEqual(SegmentRelation.Touching, Segments.Classify(P(1, 1), P(1, 1), P(1, 1), P(1, 1)));
            Assert.AreEqual(SegmentRelation.None, Segments.Classify(P(1, 1), P(1, 1), P(2, 2), P(2, 2)));
            Assert.AreEqual(SegmentRelation.None, Segments.Classify(P(11, 0), P(11, 0), P(0, 0), P(10, 0)));
        }

        [Test]
        public void Polyline_MinDistance_RepeatedPoint()
        {
            var a = new[] { P(0, 0), P(10, 0) };
            var b = new[] { P(5, 5), P(5, 5), P(8, 5) };
            Assert.AreEqual(5, Polylines.MinDistanceMm(a, b), 1e-9);
            Assert.AreEqual(5, Polylines.MinDistanceMm(b, a), 1e-9);
        }

        [Test]
        public void Segments_Distance()
        {
            Assert.AreEqual(5, Segments.Distance(P(0, 0), P(10, 0), P(3, 5), P(8, 5)), 1e-9);
            Assert.AreEqual(0, Segments.Distance(P(0, 0), P(10, 10), P(0, 10), P(10, 0)), 1e-9);
        }

        [Test]
        public void Polygon_AreaWindingAndCcw()
        {
            var sq = Square(2000);
            Assert.AreEqual(4.0, Polygons.SignedAreaM2(sq), 1e-9);
            Assert.IsTrue(Polygons.IsCounterClockwise(sq));
            var cw = new[] { sq[3], sq[2], sq[1], sq[0] };
            Assert.AreEqual(-4.0, Polygons.SignedAreaM2(cw), 1e-9);
            Assert.IsFalse(Polygons.IsCounterClockwise(cw));
            var fixedPoly = Polygons.EnsureCounterClockwise(cw);
            Assert.AreEqual(4.0, Polygons.SignedAreaM2(fixedPoly), 1e-9);
            Assert.AreEqual(Polygons.SignedAreaM2(sq), Polygons.SignedAreaM2(Polygons.EnsureCounterClockwise(sq)));
        }

        [Test]
        public void Polygon_IsSimple()
        {
            Assert.IsTrue(Polygons.IsSimple(Square()));
            Assert.IsTrue(Polygons.IsSimple(new[] { P(0, 0), P(4, 0), P(4, 4), P(2, 1), P(0, 4) }));
            Assert.IsFalse(Polygons.IsSimple(new[] { P(0, 0), P(10, 10), P(10, 0), P(0, 10) }));
            Assert.IsFalse(Polygons.IsSimple(new[] { P(0, 0), P(10, 0) }));
            Assert.IsFalse(Polygons.IsSimple(new[] { P(0, 0), P(0, 0), P(10, 0), P(10, 10) }));
            Assert.IsFalse(Polygons.IsSimple(new[] { P(0, 0), P(10, 0), P(10, 10), P(0, 0) }));
            Assert.IsFalse(Polygons.IsSimple(new[] { P(0, 0), P(5, 0), P(10, 0) }));
            Assert.IsFalse(Polygons.IsSimple(new[] { P(0, 0), P(10, 0), P(10, 10), P(5, 0), P(5, 10), P(0, 10) }));
            Assert.IsFalse(Polygons.IsSimple(new[] { P(0, 0), P(10, 0), P(4, 0), P(4, 5) }));
        }

        [Test]
        public void Polygon_PointInPolygon()
        {
            var sq = Square();
            Assert.AreEqual(PointLocation.Inside, Polygons.Locate(sq, P(5, 5)));
            Assert.AreEqual(PointLocation.Outside, Polygons.Locate(sq, P(11, 5)));
            Assert.AreEqual(PointLocation.Outside, Polygons.Locate(sq, P(-1, 5)));
            Assert.AreEqual(PointLocation.Boundary, Polygons.Locate(sq, P(10, 5)));
            Assert.AreEqual(PointLocation.Boundary, Polygons.Locate(sq, P(0, 0)));
            Assert.IsTrue(Polygons.Contains(sq, P(10, 10)));
            var c = new[] { P(0, 0), P(4, 0), P(4, 4), P(2, 1), P(0, 4) };
            Assert.AreEqual(PointLocation.Outside, Polygons.Locate(c, P(2, 3)));
            Assert.AreEqual(PointLocation.Inside, Polygons.Locate(c, P(1, 1)));
            Assert.AreEqual(PointLocation.Outside, Polygons.Locate(c, P(-3, 1)));
            Assert.AreEqual(PointLocation.Outside, Polygons.Locate(c, P(5, 1))); // ray level with vertex (2,1)
            var cw = new[] { P(0, 0), P(0, 10), P(10, 10), P(10, 0) };
            Assert.AreEqual(PointLocation.Inside, Polygons.Locate(cw, P(5, 5)));
        }

        [Test]
        public void Polygon_Bounds()
        {
            var b = Polygons.Bounds(new[] { P(3, -2), P(-4, 7), P(0, 0) });
            Assert.AreEqual(P(-4, -2), b.Min);
            Assert.AreEqual(P(3, 7), b.Max);
            Assert.IsTrue(b.Contains(P(3, 7)));
            Assert.IsFalse(b.Contains(P(4, 0)));
            Assert.IsTrue(b.Intersects(new BoundsMm(P(3, 7), P(9, 9))));
            Assert.IsFalse(b.Intersects(new BoundsMm(P(4, 8), P(9, 9))));
            Assert.Throws<System.ArgumentException>(() => Polygons.Bounds(new PointMm[0]));
        }

        [Test]
        public void Polyline_Length()
        {
            var l = new[] { P(0, 0), P(3000, 0), P(3000, 4000) };
            Assert.AreEqual(7000, Polylines.LengthMm(l), 1e-9);
            Assert.AreEqual(7.0, Polylines.LengthMetres(l), 1e-9);
            Assert.AreEqual(0, Polylines.LengthMm(new[] { P(1, 1) }));
        }

        [Test]
        public void Polyline_Nearest()
        {
            var l = new[] { P(0, 0), P(10, 0), P(10, 10) };
            var h = Polylines.Nearest(l, P(4, 3));
            Assert.AreEqual(3, h.DistanceMm, 1e-9);
            Assert.AreEqual(4, h.ArcLengthMm, 1e-9);
            Assert.AreEqual(4, h.X, 1e-9);
            Assert.AreEqual(0, h.Z, 1e-9);
            Assert.AreEqual(0, h.SegmentIndex);

            h = Polylines.Nearest(l, P(13, 6));
            Assert.AreEqual(3, h.DistanceMm, 1e-9);
            Assert.AreEqual(16, h.ArcLengthMm, 1e-9);
            Assert.AreEqual(1, h.SegmentIndex);

            h = Polylines.Nearest(l, P(-5, -5));
            Assert.AreEqual(0, h.ArcLengthMm, 1e-9);
            h = Polylines.Nearest(l, P(10, 30));
            Assert.AreEqual(20, h.ArcLengthMm, 1e-9);
            Assert.AreEqual(20, h.DistanceMm, 1e-9);
        }

        [Test]
        public void Polyline_MinDistance()
        {
            var a = new[] { P(0, 0), P(10, 0) };
            Assert.AreEqual(5, Polylines.MinDistanceMm(a, new[] { P(0, 5), P(10, 5) }), 1e-9);
            Assert.AreEqual(0, Polylines.MinDistanceMm(a, new[] { P(5, -5), P(5, 5) }), 1e-9);
            Assert.AreEqual(0, Polylines.MinDistanceMm(a, new[] { P(10, 0), P(15, 3) }), 1e-9);
            Assert.AreEqual(0, Polylines.MinDistanceMm(a, new[] { P(5, 0), P(20, 0) }), 1e-9);
            Assert.AreEqual(5, Polylines.MinDistanceMm(a, new[] { P(15, 0), P(20, 0) }), 1e-9);
            Assert.AreEqual(3, Polylines.MinDistanceMm(a, new[] { P(4, 3) }), 1e-9);
            Assert.AreEqual(5, Polylines.MinDistanceMm(new[] { P(0, 5), P(10, 5) }, a), 1e-9);
        }
    }
}
