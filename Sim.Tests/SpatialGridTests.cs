using System;
using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Geometry;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class SpatialGridTests
    {
        // Cell size 32 m = 32 000 mm, so cell borders sit at multiples of 32 000 mm.
        private const double CellM = 32;

        private static PointMm P(long x, long z) => new PointMm(x, z);

        private static BoundsMm B(long x0, long z0, long x1, long z1) => new BoundsMm(P(x0, z0), P(x1, z1));

        private static SpatialGrid<string> NewGrid() => new SpatialGrid<string>(CellM);

        [Test]
        public void Insert_ThenQueryPointInside_ReturnsItem()
        {
            var g = NewGrid();
            g.Insert("a", B(1000, 1000, 5000, 5000));
            CollectionAssert.AreEqual(new[] { "a" }, g.QueryPoint(P(2000, 2000)));
            CollectionAssert.AreEqual(new[] { "a" }, g.QueryRect(B(0, 0, 10000, 10000)));
            Assert.AreEqual(1, g.Count);
            Assert.IsTrue(g.Contains("a"));
        }

        [Test]
        public void Remove_ExistingItem_ReturnsTrueAndItemNoLongerFound()
        {
            var g = NewGrid();
            g.Insert("a", B(1000, 1000, 5000, 5000));
            Assert.IsTrue(g.Remove("a"));
            Assert.IsEmpty(g.QueryPoint(P(2000, 2000)));
            Assert.IsEmpty(g.QueryRect(B(0, 0, 10000, 10000)));
            Assert.AreEqual(0, g.Count);
            Assert.IsFalse(g.Contains("a"));
        }

        [Test]
        public void Remove_UnknownItem_ReturnsFalseAndChangesNothing()
        {
            var g = NewGrid();
            g.Insert("a", B(0, 0, 1000, 1000));
            Assert.IsFalse(g.Remove("missing"));
            Assert.IsFalse(g.Remove(null));
            Assert.AreEqual(1, g.Count);
            CollectionAssert.AreEqual(new[] { "a" }, g.QueryPoint(P(500, 500)));
        }

        [Test]
        public void Remove_TwiceSecondCallReturnsFalse()
        {
            var g = NewGrid();
            g.Insert("a", B(0, 0, 1000, 1000));
            Assert.IsTrue(g.Remove("a"));
            Assert.IsFalse(g.Remove("a"));
        }

        [Test]
        public void Query_ItemOnCellBorder_FoundFromEitherSideOfBorder()
        {
            var g = NewGrid();
            // Straddles the x = 32 000 mm border between cell 0 and cell 1.
            g.Insert("edge", B(31000, 1000, 33000, 2000));
            CollectionAssert.AreEqual(new[] { "edge" }, g.QueryPoint(P(31500, 1500)));
            CollectionAssert.AreEqual(new[] { "edge" }, g.QueryPoint(P(32500, 1500)));
            CollectionAssert.AreEqual(new[] { "edge" }, g.QueryRect(B(30000, 0, 31200, 3000)));
            CollectionAssert.AreEqual(new[] { "edge" }, g.QueryRect(B(32800, 0, 40000, 3000)));
        }

        [Test]
        public void Query_ItemTouchingQueryEdgeExactly_IsReturned()
        {
            var g = NewGrid();
            g.Insert("a", B(0, 0, 32000, 32000));
            // Query starts exactly where the item ends (edges are inclusive).
            CollectionAssert.AreEqual(new[] { "a" }, g.QueryRect(B(32000, 0, 40000, 1000)));
            CollectionAssert.AreEqual(new[] { "a" }, g.QueryPoint(P(32000, 32000)));
            CollectionAssert.IsEmpty(g.QueryRect(B(32001, 0, 40000, 1000)));
        }

        [Test]
        public void Query_NegativeCoordinates_CellsFloorTowardNegativeInfinity()
        {
            var g = NewGrid();
            // -1 mm and -32 000 mm must land in cell -1 and cell -1 (not cell 0 by truncation).
            g.Insert("neg", B(-32000, -5, -1, -1));
            CollectionAssert.AreEqual(new[] { "neg" }, g.QueryPoint(P(-1, -1)));
            CollectionAssert.AreEqual(new[] { "neg" }, g.QueryPoint(P(-32000, -5)));
            CollectionAssert.IsEmpty(g.QueryPoint(P(0, 0)));
            CollectionAssert.IsEmpty(g.QueryPoint(P(-32001, -5)));
        }

        [Test]
        public void Query_LargeItemSpanningManyCells_ReturnedOnceFromAnyCell()
        {
            var g = NewGrid();
            // 2 km square: about 63 x 63 cells.
            g.Insert("big", B(0, 0, 2_000_000, 2_000_000));
            CollectionAssert.AreEqual(new[] { "big" }, g.QueryPoint(P(1_000_000, 1_000_000)));
            CollectionAssert.AreEqual(new[] { "big" }, g.QueryPoint(P(1, 1)));
            // A rect covering many cells of the item must still give it once.
            CollectionAssert.AreEqual(new[] { "big" }, g.QueryRect(B(0, 0, 2_000_000, 2_000_000)));
            Assert.AreEqual(1, g.QueryRect(B(-1000, -1000, 3_000_000, 3_000_000)).Count);
        }

        [Test]
        public void Remove_LargeItemSpanningManyCells_ClearsEveryCell()
        {
            var g = NewGrid();
            g.Insert("big", B(0, 0, 2_000_000, 2_000_000));
            Assert.IsTrue(g.Remove("big"));
            Assert.IsEmpty(g.QueryRect(B(0, 0, 2_000_000, 2_000_000)));
            // Re-inserting elsewhere must not see stale cell entries.
            g.Insert("small", B(10, 10, 20, 20));
            CollectionAssert.AreEqual(new[] { "small" }, g.QueryRect(B(0, 0, 2_000_000, 2_000_000)));
        }

        [Test]
        public void Query_ItemRegisteredInSeveralCellsOfRect_ReturnedOnce()
        {
            var g = NewGrid();
            // Spans cells (0..2, 0..2); the query rect covers all nine cells.
            g.Insert("wide", B(1000, 1000, 70000, 70000));
            g.Insert("other", B(5000, 5000, 6000, 6000));
            var hits = g.QueryRect(B(0, 0, 90000, 90000));
            CollectionAssert.AreEqual(new[] { "wide", "other" }, hits);
        }

        [Test]
        public void Query_ResultsInInsertionOrder_NotPositionOrder()
        {
            var g = NewGrid();
            // Inserted right-to-left and top-to-bottom, so position order would differ.
            g.Insert("first", B(90000, 90000, 91000, 91000));
            g.Insert("second", B(10000, 10000, 11000, 11000));
            g.Insert("third", B(50000, 50000, 51000, 51000));
            CollectionAssert.AreEqual(new[] { "first", "second", "third" },
                g.QueryRect(B(0, 0, 100000, 100000)));
        }

        [Test]
        public void Query_ReinsertedItemAfterRemove_GoesToEndOfOrder()
        {
            var g = NewGrid();
            g.Insert("a", B(0, 0, 1000, 1000));
            g.Insert("b", B(0, 0, 1000, 1000));
            g.Remove("a");
            g.Insert("a", B(0, 0, 1000, 1000));
            CollectionAssert.AreEqual(new[] { "b", "a" }, g.QueryPoint(P(500, 500)));
        }

        [Test]
        public void Query_EmptyGrid_ReturnsEmptyList()
        {
            var g = NewGrid();
            var hits = g.QueryRect(B(-1000, -1000, 1000, 1000));
            Assert.IsNotNull(hits);
            Assert.IsEmpty(hits);
            Assert.IsEmpty(g.QueryPoint(P(0, 0)));
        }

        [Test]
        public void Query_AwayFromAllItems_ReturnsEmptyList()
        {
            var g = NewGrid();
            g.Insert("a", B(0, 0, 1000, 1000));
            Assert.IsEmpty(g.QueryPoint(P(50000, 50000)));
            Assert.IsEmpty(g.QueryRect(B(5000, 5000, 6000, 6000)));
        }

        [Test]
        public void Query_AllItemsRemoved_ReturnsEmptyList()
        {
            var g = NewGrid();
            g.Insert("a", B(0, 0, 1000, 1000));
            g.Insert("b", B(2000, 2000, 3000, 3000));
            g.Remove("a");
            g.Remove("b");
            Assert.AreEqual(0, g.Count);
            Assert.IsEmpty(g.QueryRect(B(0, 0, 10000, 10000)));
        }

        [Test]
        public void Insert_ZeroSizeItem_IsFoundOnlyAtItsPoint()
        {
            var g = NewGrid();
            g.Insert("pin", B(4000, 4000, 4000, 4000));
            CollectionAssert.AreEqual(new[] { "pin" }, g.QueryPoint(P(4000, 4000)));
            CollectionAssert.AreEqual(new[] { "pin" }, g.QueryRect(B(3000, 3000, 4000, 4000)));
            CollectionAssert.IsEmpty(g.QueryPoint(P(4001, 4000)));
        }

        [Test]
        public void Query_ZeroSizeQueryRect_MatchesItemsContainingThatPointOnly()
        {
            var g = NewGrid();
            g.Insert("a", B(0, 0, 1000, 1000));
            g.Insert("b", B(1000, 0, 2000, 1000));
            // A degenerate rect is the same as a point query on its single point.
            CollectionAssert.AreEqual(new[] { "a", "b" }, g.QueryRect(B(1000, 500, 1000, 500)));
            CollectionAssert.AreEqual(new[] { "a" }, g.QueryRect(B(999, 500, 999, 500)));
        }

        [Test]
        public void Insert_RepeatedIdenticalBoundsForDifferentItems_BothReturnedInInsertionOrder()
        {
            var g = NewGrid();
            var same = B(100, 100, 2000, 2000);
            g.Insert("x", same);
            g.Insert("y", same);
            CollectionAssert.AreEqual(new[] { "x", "y" }, g.QueryPoint(P(500, 500)));
        }

        [Test]
        public void Query_OverlappingItems_BothReturnedWhereTheyOverlap()
        {
            var g = NewGrid();
            g.Insert("a", B(0, 0, 40000, 40000));
            g.Insert("b", B(30000, 30000, 70000, 70000));
            CollectionAssert.AreEqual(new[] { "a", "b" }, g.QueryPoint(P(31000, 31000)));
            CollectionAssert.AreEqual(new[] { "a" }, g.QueryPoint(P(1000, 1000)));
            CollectionAssert.AreEqual(new[] { "b" }, g.QueryPoint(P(60000, 60000)));
        }

        [Test]
        public void Insert_SameItemTwice_Throws()
        {
            var g = NewGrid();
            g.Insert("a", B(0, 0, 1000, 1000));
            Assert.Throws<ArgumentException>(() => g.Insert("a", B(5000, 5000, 6000, 6000)));
            Assert.AreEqual(1, g.Count);
        }

        [Test]
        public void Insert_NullItem_Throws()
        {
            var g = NewGrid();
            Assert.Throws<ArgumentNullException>(() => g.Insert(null, B(0, 0, 1, 1)));
        }

        [Test]
        public void Insert_InvertedBounds_Throws()
        {
            var g = NewGrid();
            Assert.Throws<ArgumentException>(() => g.Insert("a", B(5000, 0, 1000, 1000)));
            Assert.Throws<ArgumentException>(() => g.Insert("b", B(0, 5000, 1000, 1000)));
            Assert.AreEqual(0, g.Count);
        }

        [Test]
        public void Query_InvertedRect_Throws()
        {
            var g = NewGrid();
            Assert.Throws<ArgumentException>(() => g.QueryRect(B(5000, 0, 1000, 1000)));
        }

        [TestCase(0.0)]
        [TestCase(-5.0)]
        [TestCase(0.0004)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void Constructor_InvalidCellSize_Throws(double cellM)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialGrid<string>(cellM));
        }

        [Test]
        public void Constructor_CellSize_RoundsToWholeMillimetres()
        {
            var g = new SpatialGrid<string>(32.0004);
            Assert.AreEqual(32.0, g.CellSizeM, 1e-9);
        }

        [Test]
        public void Query_SmallCellSize_StillExactAtBorders()
        {
            var g = new SpatialGrid<string>(0.001); // 1 mm cells
            g.Insert("a", B(0, 0, 1, 1));
            g.Insert("b", B(2, 2, 3, 3));
            CollectionAssert.AreEqual(new[] { "a" }, g.QueryPoint(P(1, 1)));
            CollectionAssert.AreEqual(new[] { "a", "b" }, g.QueryRect(B(0, 0, 3, 3)));
            CollectionAssert.IsEmpty(g.QueryPoint(P(1, 2)));
        }

        [Test]
        public void Query_ManyItems_MatchesBruteForce()
        {
            var g = NewGrid();
            var rng = new Random(12345); // seeded, so the test is repeatable
            var boxes = new List<(string Name, BoundsMm B)>();
            for (int i = 0; i < 300; i++)
            {
                long x = rng.Next(-200_000, 200_000), z = rng.Next(-200_000, 200_000);
                long w = rng.Next(0, 90_000), h = rng.Next(0, 90_000);
                var box = B(x, z, x + w, z + h);
                string name = "i" + i;
                boxes.Add((name, box));
                g.Insert(name, box);
            }
            for (int q = 0; q < 100; q++)
            {
                long x = rng.Next(-220_000, 220_000), z = rng.Next(-220_000, 220_000);
                var rect = B(x, z, x + rng.Next(0, 60_000), z + rng.Next(0, 60_000));
                var expected = boxes.Where(b => b.B.Intersects(rect)).Select(b => b.Name).ToList();
                CollectionAssert.AreEqual(expected, g.QueryRect(rect));
            }
        }
    }
}
