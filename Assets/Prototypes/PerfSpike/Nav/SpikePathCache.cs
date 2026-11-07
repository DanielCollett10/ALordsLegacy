using System.Collections.Generic;

namespace LordsLegacy.Prototypes.PerfSpike.Nav
{
    /// <summary>
    /// THROWAWAY (T-010). Path cache keyed by (start cell, end cell). No invalidation (the spike map is static).
    /// Not thread-safe: the planner only reads and writes it from the calling thread, never from workers.
    /// </summary>
    public sealed class SpikePathCache
    {
        private readonly int _width;
        private readonly Dictionary<long, IReadOnlyList<CellRef>> _paths = new Dictionary<long, IReadOnlyList<CellRef>>();

        /// <summary>Creates a cache for a grid <paramref name="gridWidth"/> cells wide.</summary>
        public SpikePathCache(int gridWidth)
        {
            _width = gridWidth;
        }

        /// <summary>Lookups that found a path.</summary>
        public long Hits { get; private set; }

        /// <summary>Lookups that found nothing.</summary>
        public long Misses { get; private set; }

        /// <summary>Cached (start, end) pairs.</summary>
        public int Count => _paths.Count;

        /// <summary>Total cells held across all cached paths.</summary>
        public long CellCount { get; private set; }

        /// <summary>Hits / (hits + misses), 0 when there were no lookups.</summary>
        public double HitRate => Hits + Misses == 0 ? 0.0 : (double)Hits / (Hits + Misses);

        /// <summary>Rough memory held by cached cells (8 bytes per cell reference, list overhead ignored).</summary>
        public long ApproxBytes => CellCount * 8;

        /// <summary>Looks up a path and counts a hit or a miss.</summary>
        public bool TryGet(int sx, int sz, int gx, int gz, out IReadOnlyList<CellRef> cells)
        {
            if (_paths.TryGetValue(Key(sx, sz, gx, gz), out cells))
            {
                Hits++;
                return true;
            }
            Misses++;
            return false;
        }

        /// <summary>Stores a path unless the pair is already cached (first writer wins).</summary>
        public void Add(int sx, int sz, int gx, int gz, IReadOnlyList<CellRef> cells)
        {
            long key = Key(sx, sz, gx, gz);
            if (_paths.ContainsKey(key)) return;
            _paths[key] = cells;
            CellCount += cells.Count;
        }

        /// <summary>Zeroes hit and miss counters; cached paths stay.</summary>
        public void ResetStats()
        {
            Hits = 0;
            Misses = 0;
        }

        private long Key(int sx, int sz, int gx, int gz)
        {
            return ((long)(sz * _width + sx) << 32) | (uint)(gz * _width + gx);
        }
    }
}
