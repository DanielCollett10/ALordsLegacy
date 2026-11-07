using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Prototypes.PerfSpike.Grid;
using LordsLegacy.Prototypes.PerfSpike.Nav;
using NUnit.Framework;

namespace LordsLegacy.Prototypes.PerfSpike.Tests
{
    /// <summary>THROWAWAY (T-007). Tests for the grid A* on small hand-made grids.</summary>
    [TestFixture]
    public class AStarTests
    {
        private static SpikeCostGrid Flat(int width, int height, float cost = 1f)
        {
            var costs = new float[width * height];
            for (int i = 0; i < costs.Length; i++) costs[i] = cost;
            return new SpikeCostGrid(width, height, 2f, costs);
        }

        [Test]
        public void StraightPath_OnFlatGround_IsDirectAndCostsSteps()
        {
            var search = new SpikeAStar(Flat(50, 50));

            bool found = search.TryFindPath(3, 5, 3, 45, out SpikePath path);

            Assert.That(found, Is.True);
            Assert.That(path.Cells.Count, Is.EqualTo(41));
            Assert.That(path.Cells.First().X, Is.EqualTo(3));
            Assert.That(path.Cells.First().Z, Is.EqualTo(5));
            Assert.That(path.Cells.Last().Z, Is.EqualTo(45));
            Assert.That(path.Cells.All(c => c.X == 3), Is.True, "no sideways detour on flat ground");
            Assert.That(path.Cost, Is.EqualTo(40f).Within(1e-3f));
        }

        [Test]
        public void DiagonalPath_OnFlatGround_UsesDiagonals()
        {
            var search = new SpikeAStar(Flat(20, 20));

            bool found = search.TryFindPath(0, 0, 10, 10, out SpikePath path);

            Assert.That(found, Is.True);
            Assert.That(path.Cells.Count, Is.EqualTo(11));
            Assert.That(path.Cost, Is.EqualTo(10f * 1.41421356f).Within(1e-3f));
        }

        [Test]
        public void PathPrefersRoad_WhenRoadIsCheaper()
        {
            // 41 x 41 cells, all expensive (5) except the road along z = 0 (cost 1).
            // Straight across from (0,10) to (40,10) costs 200; the road route costs about 161.4.
            var costs = new float[41 * 41];
            for (int i = 0; i < costs.Length; i++) costs[i] = 5f;
            for (int x = 0; x < 41; x++) costs[x] = 1f;
            var search = new SpikeAStar(new SpikeCostGrid(41, 41, 2f, costs));

            bool found = search.TryFindPath(0, 10, 40, 10, out SpikePath path);

            Assert.That(found, Is.True);
            Assert.That(path.Cost, Is.LessThan(200f));
            int roadCells = path.Cells.Count(c => c.Z == 0);
            Assert.That(roadCells, Is.GreaterThanOrEqualTo(15), "route should run along the road");
        }

        [Test]
        public void UnreachableTarget_ReturnsNone()
        {
            // Goal (10,10) fully ringed by blocked cells (all 8 neighbours).
            var costs = new float[20 * 20];
            for (int i = 0; i < costs.Length; i++) costs[i] = 1f;
            var grid = new SpikeCostGrid(20, 20, 2f, costs);
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                    if (dx != 0 || dz != 0) grid.SetCost(10 + dx, 10 + dz, SpikeCostGrid.Blocked);
            var search = new SpikeAStar(grid);

            bool found = search.TryFindPath(0, 0, 10, 10, out SpikePath path);

            Assert.That(found, Is.False);
            Assert.That(path, Is.Null);
        }

        [Test]
        public void BlockedOrOutOfBoundsEndpoints_ReturnNone()
        {
            var grid = Flat(10, 10);
            grid.SetCost(5, 5, SpikeCostGrid.Blocked);
            var search = new SpikeAStar(grid);

            Assert.That(search.TryFindPath(5, 5, 0, 0, out _), Is.False, "blocked start");
            Assert.That(search.TryFindPath(0, 0, 5, 5, out _), Is.False, "blocked goal");
            Assert.That(search.TryFindPath(0, 0, 10, 0, out _), Is.False, "goal off the grid");
        }

        [Test]
        public void Detour_GoesThroughGapInWall()
        {
            // Wall across column 5 with a gap at z = 9.
            var grid = Flat(11, 11);
            for (int z = 0; z < 11; z++)
                if (z != 9) grid.SetCost(5, z, SpikeCostGrid.Blocked);
            var search = new SpikeAStar(grid);

            bool found = search.TryFindPath(0, 0, 10, 0, out SpikePath path);

            Assert.That(found, Is.True);
            Assert.That(path.Cells.Any(c => c.X == 5 && c.Z == 9), Is.True);
            Assert.That(path.Cells.Any(c => c.X == 5 && c.Z != 9), Is.False);
        }

        [Test]
        public void DiagonalStep_CannotCutBlockedCorner()
        {
            // Two blocked cells touching only at a corner: (1,0) and (0,1). Going (0,0)->(1,1) would cut the corner.
            var grid = Flat(2, 2);
            grid.SetCost(1, 0, SpikeCostGrid.Blocked);
            grid.SetCost(0, 1, SpikeCostGrid.Blocked);
            var search = new SpikeAStar(grid);

            Assert.That(search.TryFindPath(0, 0, 1, 1, out _), Is.False);
        }

        [Test]
        public void SameStartAndGoal_ReturnsSingleCell()
        {
            var search = new SpikeAStar(Flat(5, 5));

            bool found = search.TryFindPath(2, 2, 2, 2, out SpikePath path);

            Assert.That(found, Is.True);
            Assert.That(path.Cells.Count, Is.EqualTo(1));
            Assert.That(path.Cost, Is.EqualTo(0f));
        }

        [Test]
        public void SearchesCanBeRepeated_OnTheSameInstance()
        {
            var search = new SpikeAStar(Flat(30, 30));

            search.TryFindPath(0, 0, 29, 29, out SpikePath first);
            search.TryFindPath(0, 29, 29, 0, out SpikePath second);

            Assert.That(first.Cells.Last().X, Is.EqualTo(29));
            Assert.That(second.Cells.Last().Z, Is.EqualTo(0));
            Assert.That(second.Cells.First().Z, Is.EqualTo(29));
        }

        [Test]
        public void HeapPopsInAscendingOrder()
        {
            var heap = new SpikeMinHeap(2);
            var keys = new List<float> { 5f, 1f, 9f, 3f, 7f, 2f, 8f, 0f, 4f, 6f };
            for (int i = 0; i < keys.Count; i++) heap.Push(keys[i], i);

            var popped = new List<float>();
            while (heap.Count > 0)
            {
                heap.Pop(out float k, out _);
                popped.Add(k);
            }

            CollectionAssert.AreEqual(keys.OrderBy(k => k).ToList(), popped);
        }
    }
}
