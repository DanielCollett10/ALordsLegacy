using System;
using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Terrain;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class TreeClearingTests
    {
        private static PointMm[] Ring(params (double x, double z)[] pts) =>
            pts.Select(p => PointMm.FromMetres(p.x, p.z)).ToArray();

        private static PointMm[] Path(params (double x, double z)[] pts) =>
            pts.Select(p => PointMm.FromMetres(p.x, p.z)).ToArray();

        private static int[] Ids(IEnumerable<TreeId> ids) => ids.Select(i => i.Value).ToArray();

        private static TreeRegistry Square10Trees()
        {
            // Square 0..10 m. 1 inside, 2 on the left edge, 3 on a corner, 4 outside east, 5 outside west.
            var r = new TreeRegistry(16f);
            r.Add(new TreeId(1), 5, 5, 4);
            r.Add(new TreeId(2), 0, 5, 4);
            r.Add(new TreeId(3), 10, 10, 4);
            r.Add(new TreeId(4), 11, 5, 4);
            r.Add(new TreeId(5), -0.5f, 5, 4);
            return r;
        }

        [Test]
        public void PolygonRemovesTreesInsideAndOnBoundaryAndReturnsIdsSorted()
        {
            var r = Square10Trees();
            var removed = TreeClearing.RemoveInPolygon(r, Ring((0, 0), (10, 0), (10, 10), (0, 10)));
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, Ids(removed));
            CollectionAssert.AreEqual(new[] { 4, 5 }, r.All.Select(t => t.Id.Value).ToArray());
            Assert.IsFalse(r.TryGet(new TreeId(1), out _));
        }

        [Test]
        public void PolygonWithRepeatedVertexGivesSameResult()
        {
            var r = Square10Trees();
            var removed = TreeClearing.RemoveInPolygon(r, Ring((0, 0), (10, 0), (10, 0), (10, 10), (0, 10)));
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, Ids(removed));
        }

        [Test]
        public void PolygonWithFewerThanThreeVerticesThrows()
        {
            var r = Square10Trees();
            Assert.Throws<ArgumentException>(() => TreeClearing.RemoveInPolygon(r, Ring((0, 0), (10, 10))));
            Assert.Throws<ArgumentException>(() => TreeClearing.RemoveInPolygon(r, Ring()));
            Assert.AreEqual(5, r.Count);
        }

        [Test]
        public void CollinearPolygonRemovesOnlyTreesOnItsEdges()
        {
            // Zero-area "polygon" along the x axis: trees exactly on the segment go, the tree 0.5 m off does not.
            var r = new TreeRegistry();
            r.Add(new TreeId(1), 5, 0, 4);
            r.Add(new TreeId(2), 5, 0.5f, 4);
            var removed = TreeClearing.RemoveInPolygon(r, Ring((0, 0), (10, 0), (5, 0)));
            CollectionAssert.AreEqual(new[] { 1 }, Ids(removed));
            Assert.AreEqual(1, r.Count);
        }

        [Test]
        public void PolygonWithNoTreesInsideRemovesNothingAndKeepsVersion()
        {
            var r = Square10Trees();
            int v = r.Version;
            var removed = TreeClearing.RemoveInPolygon(r, Ring((20, 20), (30, 20), (30, 30)));
            Assert.AreEqual(0, removed.Count);
            Assert.AreEqual(v, r.Version);
            Assert.AreEqual(5, r.Count);
        }

        [Test]
        public void PolylineRemovesTreesWithinDistanceInclusive()
        {
            // Line along x from 0 to 10 m, distance 2 m.
            var r = new TreeRegistry(16f);
            r.Add(new TreeId(1), 5, 1.5f, 4);   // inside
            r.Add(new TreeId(2), 5, 2, 4);      // exactly 2 m: removed
            r.Add(new TreeId(3), 5, 2.5f, 4);   // too far: kept
            r.Add(new TreeId(4), -1, 0, 4);     // beyond the start, 1 m from the end point: removed
            r.Add(new TreeId(5), 20, 0, 4);     // far along the line: kept
            var removed = TreeClearing.RemoveNearPolyline(r, Path((0, 0), (10, 0)), 2.0);
            CollectionAssert.AreEqual(new[] { 1, 2, 4 }, Ids(removed));
            CollectionAssert.AreEqual(new[] { 3, 5 }, r.All.Select(t => t.Id.Value).ToArray());
        }

        [Test]
        public void ZeroDistanceRemovesOnlyTreesOnTheLine()
        {
            var r = new TreeRegistry();
            r.Add(new TreeId(1), 5, 0, 4);
            r.Add(new TreeId(2), 5, 0.001f, 4); // 1 mm off the line
            var removed = TreeClearing.RemoveNearPolyline(r, Path((0, 0), (10, 0)), 0);
            CollectionAssert.AreEqual(new[] { 1 }, Ids(removed));
        }

        [Test]
        public void SinglePointPolylineIsADisc()
        {
            var r = new TreeRegistry();
            r.Add(new TreeId(1), 5, 8, 4);      // exactly 3 m from (5,5): removed
            r.Add(new TreeId(2), 5, 8.01f, 4);  // 3.01 m: kept
            var removed = TreeClearing.RemoveNearPolyline(r, Path((5, 5)), 3.0);
            CollectionAssert.AreEqual(new[] { 1 }, Ids(removed));
            Assert.AreEqual(1, r.Count);
        }

        [Test]
        public void RepeatedPointsPolylineGivesSameResultAsSegment()
        {
            var r = new TreeRegistry();
            r.Add(new TreeId(1), 5, 0.5f, 4);
            r.Add(new TreeId(2), 5, 1.5f, 4);
            var removed = TreeClearing.RemoveNearPolyline(r, Path((0, 0), (0, 0), (10, 0), (10, 0)), 1.0);
            CollectionAssert.AreEqual(new[] { 1 }, Ids(removed));
        }

        [Test]
        public void AllRepeatedPointPolylineIsADisc()
        {
            var r = new TreeRegistry();
            r.Add(new TreeId(1), 2, 0, 4);
            r.Add(new TreeId(2), 4, 0, 4);
            var removed = TreeClearing.RemoveNearPolyline(r, Path((0, 0), (0, 0), (0, 0)), 2.5);
            CollectionAssert.AreEqual(new[] { 1 }, Ids(removed));
        }

        [Test]
        public void EmptyPolylineThrows()
        {
            var r = Square10Trees();
            Assert.Throws<ArgumentException>(() => TreeClearing.RemoveNearPolyline(r, new PointMm[0], 1.0));
            Assert.AreEqual(5, r.Count);
        }

        [Test]
        public void NegativeNaNOrInfiniteDistanceThrows()
        {
            var r = Square10Trees();
            var line = Path((0, 0), (10, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => TreeClearing.RemoveNearPolyline(r, line, -1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => TreeClearing.RemoveNearPolyline(r, line, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => TreeClearing.RemoveNearPolyline(r, line, double.PositiveInfinity));
            Assert.AreEqual(5, r.Count);
        }

        [Test]
        public void PolylineRemovingNothingKeepsVersion()
        {
            var r = Square10Trees();
            int v = r.Version;
            var removed = TreeClearing.RemoveNearPolyline(r, Path((50, 50), (60, 50)), 1.0);
            Assert.AreEqual(0, removed.Count);
            Assert.AreEqual(v, r.Version);
        }

        [Test]
        public void TreesRemovedByHelpersAreGoneFromQueries()
        {
            var r = Square10Trees();
            TreeClearing.RemoveInPolygon(r, Ring((0, 0), (10, 0), (10, 10), (0, 10)));
            Assert.AreEqual(0, r.QueryRect(new Bounds2(-1, -1, 11, 11)).Count(t => t.Id.Value <= 3));
            Assert.AreEqual(0, r.QueryRadius(5, 5, 20f).Count(t => t.Id.Value <= 3));
            Assert.AreEqual(2, r.Count);
        }
    }
}
