using System;

namespace LordsLegacy.Prototypes.PerfSpike.Grid
{
    /// <summary>
    /// THROWAWAY (T-007). Regular grid of movement costs. Cell (x,z) covers metres
    /// [x*CellSize, (x+1)*CellSize) x [z*CellSize, (z+1)*CellSize). Cost is the price of entering a cell
    /// per metre travelled: flat ground ~1, steeper ground more, roads less. Blocked cells hold
    /// <see cref="float.PositiveInfinity"/>. Costs are stored row-major (index = z*Width + x).
    /// </summary>
    public sealed class SpikeCostGrid
    {
        /// <summary>Blocked-cell cost.</summary>
        public const float Blocked = float.PositiveInfinity;

        private readonly float[] _costs;

        /// <summary>Cells along x.</summary>
        public int Width { get; }
        /// <summary>Cells along z.</summary>
        public int Height { get; }
        /// <summary>Cell edge length in metres.</summary>
        public float CellSize { get; }
        /// <summary>
        /// Lower bound on every passable cell's cost, so it is safe as an A* heuristic multiplier.
        /// It can only fall (<see cref="SetCost"/>), so it may sit below the true minimum but never above it.
        /// </summary>
        public float MinCost { get; private set; }

        /// <summary>Creates a grid from row-major costs (copied). Every cost must be &gt; 0 (or blocked).</summary>
        public SpikeCostGrid(int width, int height, float cellSize, float[] costs)
        {
            if (width < 1) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 1) throw new ArgumentOutOfRangeException(nameof(height));
            if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));
            if (costs == null) throw new ArgumentNullException(nameof(costs));
            if (costs.Length != width * height) throw new ArgumentException("costs length must be width*height", nameof(costs));

            Width = width;
            Height = height;
            CellSize = cellSize;
            _costs = (float[])costs.Clone();

            float min = Blocked;
            for (int i = 0; i < _costs.Length; i++)
            {
                float c = _costs[i];
                if (!(c > 0f)) throw new ArgumentException("costs must be > 0 or blocked", nameof(costs));
                if (c < min) min = c;
            }
            MinCost = float.IsPositiveInfinity(min) ? 1f : min;
        }

        /// <summary>Cost of entering cell (x,z). Blocked cells return <see cref="Blocked"/>.</summary>
        public float CostAt(int x, int z)
        {
            return _costs[z * Width + x];
        }

        /// <summary>True if (x,z) is inside the grid.</summary>
        public bool InBounds(int x, int z)
        {
            return (uint)x < (uint)Width && (uint)z < (uint)Height;
        }

        /// <summary>True if (x,z) is inside the grid and not blocked.</summary>
        public bool IsPassable(int x, int z)
        {
            return InBounds(x, z) && !float.IsPositiveInfinity(_costs[z * Width + x]);
        }

        /// <summary>Sets a cell's cost. Must be &gt; 0, or <see cref="Blocked"/>.</summary>
        public void SetCost(int x, int z, float cost)
        {
            if (!InBounds(x, z)) throw new ArgumentOutOfRangeException(nameof(x));
            if (!(cost > 0f)) throw new ArgumentOutOfRangeException(nameof(cost));
            _costs[z * Width + x] = cost;
            if (cost < MinCost) MinCost = cost;
        }

        /// <summary>Copy of the row-major cost array (for tests and comparisons).</summary>
        public float[] CopyCosts()
        {
            return (float[])_costs.Clone();
        }
    }
}
