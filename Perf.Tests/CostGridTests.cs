using System.Linq;
using LordsLegacy.Prototypes.PerfSpike.Grid;
using NUnit.Framework;

namespace LordsLegacy.Prototypes.PerfSpike.Tests
{
    /// <summary>THROWAWAY (T-007). Tests for the seeded synthetic cost grid.</summary>
    [TestFixture]
    public class CostGridTests
    {
        [Test]
        public void DefaultMap_Is3000SquareAt2mCells()
        {
            SpikeCostGrid grid = SpikeMapGenerator.Generate(1);

            Assert.That(grid.Width, Is.EqualTo(3000));
            Assert.That(grid.Height, Is.EqualTo(3000));
            Assert.That(grid.CellSize, Is.EqualTo(2f));
            Assert.That(grid.Width * grid.CellSize, Is.EqualTo(6000f), "6 km side");
        }

        [Test]
        public void SameSeed_GivesIdenticalGrid()
        {
            SpikeCostGrid a = SpikeMapGenerator.Generate(42, 200, 200, 2f);
            SpikeCostGrid b = SpikeMapGenerator.Generate(42, 200, 200, 2f);

            CollectionAssert.AreEqual(a.CopyCosts(), b.CopyCosts());
            Assert.That(a.MinCost, Is.EqualTo(b.MinCost));
        }

        [Test]
        public void DifferentSeed_GivesDifferentGrid()
        {
            float[] a = SpikeMapGenerator.Generate(42, 200, 200, 2f).CopyCosts();
            float[] b = SpikeMapGenerator.Generate(43, 200, 200, 2f).CopyCosts();

            Assert.That(a.SequenceEqual(b), Is.False);
        }

        [Test]
        public void Costs_RiseWithSlope_AndRoadsAreCheap()
        {
            SpikeCostGrid grid = SpikeMapGenerator.Generate(7, 300, 300, 2f);
            float[] costs = grid.CopyCosts();

            Assert.That(costs.Max(), Is.GreaterThan(1.5f), "hills should make some ground dearer than flat");
            Assert.That(costs.Any(c => c == SpikeMapGenerator.RoadCost), Is.True, "at least one road cell");
            Assert.That(grid.MinCost, Is.LessThanOrEqualTo(SpikeMapGenerator.RoadCost));
            Assert.That(costs.All(c => c > 0f), Is.True);
        }

        [Test]
        public void SetCost_RejectsNonPositive_AndLowersMinCost()
        {
            var grid = new SpikeCostGrid(3, 3, 1f, new float[9] { 2, 2, 2, 2, 2, 2, 2, 2, 2 });

            Assert.Throws<System.ArgumentOutOfRangeException>(() => grid.SetCost(0, 0, 0f));
            grid.SetCost(1, 1, 0.5f);
            Assert.That(grid.MinCost, Is.EqualTo(0.5f));
            grid.SetCost(1, 1, SpikeCostGrid.Blocked);
            Assert.That(grid.IsPassable(1, 1), Is.False);
        }
    }
}
