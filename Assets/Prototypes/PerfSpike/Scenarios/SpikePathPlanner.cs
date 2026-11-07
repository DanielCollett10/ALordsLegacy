using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LordsLegacy.Prototypes.PerfSpike.Grid;
using LordsLegacy.Prototypes.PerfSpike.Nav;

namespace LordsLegacy.Prototypes.PerfSpike.Scenarios
{
    /// <summary>
    /// THROWAWAY (T-009, T-010). Runs timed A* queries for scenarios and records each into <see cref="Samples"/>
    /// by straight-line distance. Failed queries are timed and counted too.
    /// Queries are queued with <see cref="Add"/> and run by <see cref="Execute"/>. Order of work:
    /// (1) cache lookups, in queue order, on the calling thread; (2) the misses run on 1..N searchers (N =
    /// <see cref="Parallelism"/>, each with its own <see cref="SpikeAStar"/>; workers pull queries from a shared
    /// counter and write only to their own request slot); (3) results are applied in queue order on the calling
    /// thread (walker paths, samples, cache inserts). Phases 1 and 3 never depend on thread timing, so the
    /// outcome is identical for any Parallelism and with or without the cache (A* is deterministic).
    /// </summary>
    public sealed class SpikePathPlanner
    {
        private readonly SpikeCostGrid _grid;
        private readonly SpikeAStar[] _searches;
        private readonly SpikePathCache _cache;
        private readonly List<SpikePlanRequest> _pool = new List<SpikePlanRequest>();
        private readonly List<SpikePlanRequest> _pending = new List<SpikePlanRequest>();
        private int _count;
        private int _nextPending;

        /// <summary>Creates a planner over <paramref name="grid"/> with <paramref name="parallelism"/> searchers.</summary>
        public SpikePathPlanner(SpikeCostGrid grid, bool useCache = false, int parallelism = 1)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            if (parallelism < 1) throw new ArgumentOutOfRangeException(nameof(parallelism));
            Parallelism = parallelism;
            _searches = new SpikeAStar[parallelism];
            for (int i = 0; i < parallelism; i++) _searches[i] = new SpikeAStar(grid);
            if (useCache) _cache = new SpikePathCache(grid.Width);
        }

        /// <summary>The grid planned over.</summary>
        public SpikeCostGrid Grid => _grid;

        /// <summary>Searchers (threads) used for a batch of cache misses.</summary>
        public int Parallelism { get; }

        /// <summary>The path cache, or null when caching is off.</summary>
        public SpikePathCache Cache => _cache;

        /// <summary>Query times by distance band (A* runs only; cache hits are not sampled).</summary>
        public SpikePathSamples Samples { get; } = new SpikePathSamples();

        /// <summary>Queries that found no path.</summary>
        public long Failed { get; private set; }

        /// <summary>Requests queued since <see cref="BeginBatch"/>, in queue order. Valid after <see cref="Execute"/>.</summary>
        public int BatchCount => _count;

        /// <summary>The i-th queued request.</summary>
        public SpikePlanRequest Request(int i) => _pool[i];

        /// <summary>Plans from the walker's cell to (gx,gz) and gives it the path. False if none was found.</summary>
        public bool Plan(SpikeWalker walker, int gx, int gz)
        {
            BeginBatch();
            Add(walker, gx, gz, 0);
            Execute();
            return _pool[0].Found;
        }

        /// <summary>Empties the queue.</summary>
        public void BeginBatch()
        {
            _count = 0;
        }

        /// <summary>Queues a query from the walker's current cell to (gx,gz).</summary>
        public void Add(SpikeWalker walker, int gx, int gz, int tag)
        {
            if (_count == _pool.Count) _pool.Add(new SpikePlanRequest());
            SpikePlanRequest r = _pool[_count++];
            float cell = _grid.CellSize;
            r.Walker = walker;
            r.Tag = tag;
            r.Sx = Clamp((int)(walker.X / cell), _grid.Width);
            r.Sz = Clamp((int)(walker.Z / cell), _grid.Height);
            r.Gx = gx;
            r.Gz = gz;
            r.Found = false;
            r.FromCache = false;
            r.Cells = null;
            r.Ms = 0;
        }

        /// <summary>Runs the queued queries and applies their results in queue order.</summary>
        public void Execute()
        {
            _pending.Clear();
            for (int i = 0; i < _count; i++)
            {
                SpikePlanRequest r = _pool[i];
                if (_cache != null && _cache.TryGet(r.Sx, r.Sz, r.Gx, r.Gz, out IReadOnlyList<CellRef> cached))
                {
                    r.Found = true;
                    r.FromCache = true;
                    r.Cells = cached;
                }
                else
                {
                    _pending.Add(r);
                }
            }

            RunPending();

            float cell = _grid.CellSize;
            for (int i = 0; i < _count; i++)
            {
                SpikePlanRequest r = _pool[i];
                if (!r.FromCache)
                {
                    float dx = (r.Gx - r.Sx) * cell;
                    float dz = (r.Gz - r.Sz) * cell;
                    Samples.Add((float)Math.Sqrt(dx * dx + dz * dz), r.Ms);
                    if (!r.Found) Failed++;
                    else _cache?.Add(r.Sx, r.Sz, r.Gx, r.Gz, r.Cells);
                }
                if (r.Found) r.Walker.SetPath(r.Cells);
            }
        }

        /// <summary>Clears the samples, the failure count and the cache hit/miss counters (cached paths stay).</summary>
        public void ResetStats()
        {
            Samples.Clear();
            Failed = 0;
            _cache?.ResetStats();
        }

        private void RunPending()
        {
            int workers = Math.Min(Parallelism, _pending.Count);
            if (workers <= 1)
            {
                for (int i = 0; i < _pending.Count; i++) Search(_searches[0], _pending[i]);
                return;
            }

            _nextPending = -1;
            var tasks = new Task[workers - 1];
            for (int w = 1; w < workers; w++)
            {
                SpikeAStar search = _searches[w];
                tasks[w - 1] = Task.Run(() => Work(search));
            }
            Work(_searches[0]);
            Task.WaitAll(tasks);
        }

        private void Work(SpikeAStar search)
        {
            while (true)
            {
                int i = Interlocked.Increment(ref _nextPending);
                if (i >= _pending.Count) return;
                Search(search, _pending[i]);
            }
        }

        private static void Search(SpikeAStar search, SpikePlanRequest r)
        {
            long t0 = Stopwatch.GetTimestamp();
            r.Found = search.TryFindPath(r.Sx, r.Sz, r.Gx, r.Gz, out SpikePath path);
            r.Ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            r.Cells = r.Found ? path.Cells : null;
        }

        private static int Clamp(int v, int count)
        {
            return v < 0 ? 0 : (v >= count ? count - 1 : v);
        }
    }
}
