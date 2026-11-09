using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    /// <summary>Polygons.Crossings and Segments.MeetingPoint (T-201, AR-32).</summary>
    public class PolygonCrossingsTests
    {
        private static PointMm P(long x, long z) => new PointMm(x, z);

        private static void AssertPoints(IReadOnlyList<PointMm> expected, IReadOnlyList<PointMm> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count, "count");
            for (int i = 0; i < expected.Count; i++)
                Assert.AreEqual(expected[i], actual[i], $"entry {i}");
        }

        [Test]
        public void Hourglass_ReportsOneCrossingAtTheCentre()
        {
            // Edges 0 and 2 are the two diagonals; they cross at (5,5). Edges 1 and 3 are on x=10 and x=0: no contact.
            PointMm[] poly = { P(0, 0), P(10, 10), P(10, 0), P(0, 10) };
            AssertPoints(new[] { P(5, 5) }, Polygons.Crossings(poly));
        }

        [Test]
        public void Hourglass_OneCrossingForEveryVertexOrder()
        {
            // Every ordering of the four corners of the same hourglass, as clicks in any order would give.
            PointMm[] corners = { P(0, 0), P(10, 10), P(10, 0), P(0, 10) };
            int[] idx = { 0, 1, 2, 3 };
            int count = 0;
            foreach (int a in idx) foreach (int b in idx) foreach (int c in idx) foreach (int d in idx)
            {
                if (a == b || a == c || a == d || b == c || b == d || c == d) continue;
                count++;
                PointMm[] poly = { corners[a], corners[b], corners[c], corners[d] };
                // Self-crossing or simple: a 4-gon with crossing diagonals has exactly one crossing; the others have none.
                int crossings = Polygons.Crossings(poly).Length;
                Assert.That(crossings == 0 || crossings == 1, $"order {a}{b}{c}{d} gave {crossings}");
                Assert.AreEqual(!Polygons.IsSimple(poly), crossings == 1, $"order {a}{b}{c}{d} disagrees with IsSimple");
            }
            Assert.AreEqual(24, count);
        }

        [Test]
        public void FigureEight_TwoCrossingsInEdgeOrder()
        {
            // Edges 0-2 cross at (20,20); edges 0-3 cross at (10,10). Both are reported, in edge order.
            PointMm[] poly = { P(0, 0), P(40, 40), P(40, 0), P(0, 40), P(20, -20) };
            AssertPoints(new[] { P(20, 20), P(10, 10) }, Polygons.Crossings(poly));
        }

        [Test]
        public void TouchingAtAVertex_ReportsTheSharedPointOncePerPair()
        {
            // Vertex 3 (10,0) lies on edge 0. Edges 2 and 3 both meet edge 0 there: two pairs, one point each.
            PointMm[] poly = { P(0, 0), P(20, 0), P(20, 20), P(10, 0), P(0, 20) };
            AssertPoints(new[] { P(10, 0), P(10, 0) }, Polygons.Crossings(poly));
        }

        [Test]
        public void SimpleSquare_ReportsNothing()
        {
            PointMm[] square = { P(0, 0), P(20, 0), P(20, 20), P(0, 20) };
            Assert.That(Polygons.Crossings(square), Is.Empty);
        }

        [Test]
        public void CollinearOverlap_ReportsTheOverlapStart()
        {
            // Edge 0 is (0,0)-(20,0); edge 3 is (30,0)-(10,0) and overlaps it on [10,20]. The overlap starts at (10,0).
            // Edge 1 also meets edge 3 at its end (20,0), which is reported as a touch in edge order after (10,0).
            PointMm[] poly = { P(0, 0), P(20, 0), P(10, 20), P(30, 0), P(10, 0) };
            AssertPoints(new[] { P(10, 0), P(20, 0) }, Polygons.Crossings(poly));
        }

        [Test]
        public void CollinearOverlap_MeetingPointIsTheLowerEndAlongTheDominantAxis()
        {
            // Overlap of (0,0)-(20,0) with (30,0)-(10,0) starts at (10,0) regardless of argument order.
            Assert.AreEqual(P(10, 0), Segments.MeetingPoint(P(0, 0), P(20, 0), P(30, 0), P(10, 0)));
            Assert.AreEqual(P(10, 0), Segments.MeetingPoint(P(30, 0), P(10, 0), P(0, 0), P(20, 0)));
        }

        [Test]
        public void RepeatedVertex_DoubledCornerReportsTheTouchItCreates()
        {
            // The doubled (10,0) makes edge 0 and edge 2 share (10,0) while not being adjacent in the list, so that
            // is one reported touch. The zero-length edge 1 touches only its neighbours, so it adds nothing.
            PointMm[] poly = { P(0, 0), P(10, 0), P(10, 0), P(10, 10), P(0, 10) };
            AssertPoints(new[] { P(10, 0) }, Polygons.Crossings(poly));
            Assert.IsFalse(Polygons.IsSimple(poly));
        }

        [Test]
        public void RepeatedVertex_NoDoubledCornerOfASquareReportsNothing()
        {
            // Control for the case above: the same square with no repeated vertex has no contact at all.
            PointMm[] poly = { P(0, 0), P(10, 0), P(10, 10), P(0, 10) };
            Assert.That(Polygons.Crossings(poly), Is.Empty);
        }

        [Test]
        public void ZeroLengthEdgeOnAnotherEdge_ReportsItsPointForEachTouchingPair()
        {
            // Edge 3 is the zero-length (10,0)-(10,0) and lies on edge 0. Edges 0-2, 0-3, 0-4 and 2-4 all meet at (10,0).
            PointMm[] poly = { P(0, 0), P(20, 0), P(20, 20), P(10, 0), P(10, 0), P(0, 20) };
            var found = Polygons.Crossings(poly);
            Assert.AreEqual(4, found.Length);
            Assert.That(found, Is.All.EqualTo(P(10, 0)));
        }

        [Test]
        public void ZeroLengthEdgeMeetingPoint_IsThePointItself()
        {
            // A point segment that lies on another segment meets it at that point; off the segment it meets nothing.
            Assert.AreEqual(P(10, 5), Segments.MeetingPoint(P(10, 5), P(10, 5), P(0, 0), P(20, 10)));
            Assert.Throws<System.ArgumentException>(() => Segments.MeetingPoint(P(3, 5), P(3, 5), P(0, 0), P(20, 10)));
        }

        [Test]
        public void TwoPoints_AndEmptyList_ReportNothing()
        {
            Assert.That(Polygons.Crossings(new[] { P(0, 0), P(10, 10) }), Is.Empty);
            Assert.That(Polygons.Crossings(new PointMm[0]), Is.Empty);
        }

        [Test]
        public void ThreePoints_AllEdgesAdjacent_ReportsNothing()
        {
            Assert.That(Polygons.Crossings(new[] { P(0, 0), P(10, 0), P(0, 10) }), Is.Empty);
        }

        [Test]
        public void MeetingPoint_CrossingRoundsToNearestMm()
        {
            // z = x meets the line from (0,10) to (3,0) at x = 30/13 = 2.31 mm, which rounds to (2,2).
            Assert.AreEqual(P(2, 2), Segments.MeetingPoint(P(0, 0), P(10, 10), P(0, 10), P(3, 0)));
        }

        [Test]
        public void MeetingPoint_ThrowsWhenSegmentsDoNotMeet()
        {
            Assert.Throws<System.ArgumentException>(() =>
                Segments.MeetingPoint(P(0, 0), P(1, 0), P(0, 5), P(1, 5)));
        }

        [Test]
        public void Crossings_CountMatchesIsSimpleForSimplePolygons()
        {
            // A simple pentagon (no contact between non-adjacent edges) reports nothing and is simple.
            PointMm[] house = { P(0, 0), P(20, 0), P(20, 20), P(10, 30), P(0, 20) };
            Assert.That(Polygons.Crossings(house), Is.Empty);
            Assert.IsTrue(Polygons.IsSimple(house));
        }
    }
}
