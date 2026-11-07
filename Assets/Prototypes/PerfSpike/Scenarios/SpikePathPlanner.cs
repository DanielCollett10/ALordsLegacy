using System;
using System.Diagnostics;
using LordsLegacy.Prototypes.PerfSpike.Grid;
using LordsLegacy.Prototypes.PerfSpike.Nav;

namespace LordsLegacy.Prototypes.PerfSpike.Scenarios
{
    /// <summary>
    /// THROWAWAY (T-009). Runs timed A* queries for scenarios and records each into <see cref="Samples"/>
    /// by straight-line distance. Failed queries are timed and counted too.
    /// </summary>
    public sealed class SpikePathPlanner
    {
        private readonly SpikeCostGrid _grid;
        private readonly SpikeAStar _search;

        /// <summary>Creates a planner over <paramref name="grid"/>.</summary>
        public SpikePathPlanner(SpikeCostGrid grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _search = new SpikeAStar(grid);
        }

        /// <summary>The grid planned over.</summary>
        public SpikeCostGrid Grid => _grid;

        /// <summary>Query times by distance band.</summary>
        public SpikePathSamples Samples { get; } = new SpikePathSamples();

        /// <summary>Queries that found no path.</summary>
        public long Failed { get; private set; }

        /// <summary>Plans from the walker's cell to (gx,gz) and gives it the path. False if none was found.</summary>
        public bool Plan(SpikeWalker walker, int gx, int gz)
        {
            float cell = _grid.CellSize;
            int sx = Clamp((int)(walker.X / cell), _grid.Width);
            int sz = Clamp((int)(walker.Z / cell), _grid.Height);

            long t0 = Stopwatch.GetTimestamp();
            bool found = _search.TryFindPath(sx, sz, gx, gz, out SpikePath path);
            long elapsed = Stopwatch.GetTimestamp() - t0;

            float dx = (gx - sx) * cell;
            float dz = (gz - sz) * cell;
            Samples.Add((float)Math.Sqrt(dx * dx + dz * dz), elapsed * 1000.0 / Stopwatch.Frequency);
            if (!found)
            {
                Failed++;
                return false;
            }
            walker.SetPath(path.Cells);
            return true;
        }

        /// <summary>Clears the samples and the failure count.</summary>
        public void ResetStats()
        {
            Samples.Clear();
            Failed = 0;
        }

        private static int Clamp(int v, int count)
        {
            return v < 0 ? 0 : (v >= count ? count - 1 : v);
        }
    }
}
