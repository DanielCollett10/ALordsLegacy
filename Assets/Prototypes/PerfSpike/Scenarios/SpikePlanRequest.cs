using System.Collections.Generic;
using LordsLegacy.Prototypes.PerfSpike.Nav;

namespace LordsLegacy.Prototypes.PerfSpike.Scenarios
{
    /// <summary>THROWAWAY (T-010). One queued path query and its result. Reused between batches.</summary>
    public sealed class SpikePlanRequest
    {
        /// <summary>The walker that gets the path.</summary>
        public SpikeWalker Walker;
        /// <summary>Caller's own data (the crowd packs agent index and trip kind here).</summary>
        public int Tag;
        /// <summary>Start cell column.</summary>
        public int Sx;
        /// <summary>Start cell row.</summary>
        public int Sz;
        /// <summary>Goal cell column.</summary>
        public int Gx;
        /// <summary>Goal cell row.</summary>
        public int Gz;
        /// <summary>True if a path was found (or served from cache).</summary>
        public bool Found;
        /// <summary>True if the path came from the cache.</summary>
        public bool FromCache;
        /// <summary>The path cells when found.</summary>
        public IReadOnlyList<CellRef> Cells;
        /// <summary>A* wall time, ms (0 for cache hits).</summary>
        public double Ms;
    }
}
