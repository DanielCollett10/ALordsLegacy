using System;
using System.Collections.Generic;
using LordsLegacy.Prototypes.PerfSpike.Agents;
using LordsLegacy.Prototypes.PerfSpike.Grid;
using LordsLegacy.Prototypes.PerfSpike.Nav;

namespace LordsLegacy.Prototypes.PerfSpike.Scenarios
{
    /// <summary>
    /// THROWAWAY (T-009). Agents making mixed trips. There is one home/work pair per <see cref="AgentsPerPair"/>
    /// agents (so the pair count scales with the agent count; agents of a pair share a household). When an agent
    /// is idle it picks its next trip: <see cref="RepeatTripShare"/> (80%) go to the other end of their pair
    /// (home if they are not at work), the rest go to a random target 50 to 1,500 m away. After arriving the
    /// agent dwells <see cref="DwellTicks"/>. Starts are staggered over <see cref="StaggerTicks"/> so the first
    /// tick is not a burst. Optional pair-path cache: repeat trips between the two ends reuse the first path.
    /// Seeded and deterministic; timing is never read back into state.
    /// </summary>
    public sealed class SpikeTripCrowd
    {
        /// <summary>Agents sharing one home/work pair (a household).</summary>
        public const int AgentsPerPair = 5;

        /// <summary>Share of trips that are repeat home/work trips.</summary>
        public const float RepeatTripShare = 0.8f;

        /// <summary>Ticks an agent stays put after arriving.</summary>
        public const int DwellTicks = 60;

        /// <summary>Initial start delays are spread over this many ticks.</summary>
        public const int StaggerTicks = 600;

        private const int MaxSampleAttempts = 64;

        private enum Place { Home, Work, Elsewhere }

        private readonly SpikePathPlanner _planner;
        private readonly SpikeCostGrid _grid;
        private readonly SpikeRng _rng;
        private readonly bool _cachePairPaths;
        private readonly int[] _homeX, _homeZ, _workX, _workZ;
        private readonly SpikeWalker[] _walkers;
        private readonly int[] _pairOf;
        private readonly Place[] _place;
        private readonly bool[] _tripEndsAtPair;
        private readonly int[] _dwell;
        private readonly Dictionary<int, IReadOnlyList<CellRef>> _cache = new Dictionary<int, IReadOnlyList<CellRef>>();

        /// <summary>Creates the pairs and agents (all at home) from <paramref name="seed"/>.</summary>
        public SpikeTripCrowd(SpikePathPlanner planner, int agentCount, ulong seed, bool cachePairPaths = false)
        {
            if (planner == null) throw new ArgumentNullException(nameof(planner));
            if (agentCount < 1) throw new ArgumentOutOfRangeException(nameof(agentCount));

            _planner = planner;
            _grid = planner.Grid;
            _rng = new SpikeRng(seed);
            _cachePairPaths = cachePairPaths;

            PairCount = Math.Max(1, agentCount / AgentsPerPair);
            _homeX = new int[PairCount];
            _homeZ = new int[PairCount];
            _workX = new int[PairCount];
            _workZ = new int[PairCount];
            for (int p = 0; p < PairCount; p++) CreatePair(p);

            _walkers = new SpikeWalker[agentCount];
            _pairOf = new int[agentCount];
            _place = new Place[agentCount];
            _tripEndsAtPair = new bool[agentCount];
            _dwell = new int[agentCount];
            for (int i = 0; i < agentCount; i++)
            {
                int p = i % PairCount;
                _pairOf[i] = p;
                _walkers[i] = new SpikeWalker
                {
                    X = (_homeX[p] + 0.5f) * _grid.CellSize,
                    Z = (_homeZ[p] + 0.5f) * _grid.CellSize
                };
                _dwell[i] = _rng.NextInt(StaggerTicks);
            }
        }

        /// <summary>Number of home/work pairs.</summary>
        public int PairCount { get; }

        /// <summary>Number of agents.</summary>
        public int AgentCount => _walkers.Length;

        /// <summary>Trips chosen as repeat home/work trips.</summary>
        public long RepeatTrips { get; private set; }

        /// <summary>Trips chosen as random-target trips.</summary>
        public long RandomTrips { get; private set; }

        /// <summary>Repeat trips served from the pair-path cache (no A*).</summary>
        public long CacheHits { get; private set; }

        /// <summary>The agents' walkers, by agent index.</summary>
        public IReadOnlyList<SpikeWalker> Walkers => _walkers;

        /// <summary>Home cell of a pair.</summary>
        public CellRef Home(int pair) => new CellRef(_homeX[pair], _homeZ[pair]);

        /// <summary>Work cell of a pair.</summary>
        public CellRef Work(int pair) => new CellRef(_workX[pair], _workZ[pair]);

        /// <summary>Clears trip counters (not agent state).</summary>
        public void ResetStats()
        {
            RepeatTrips = 0;
            RandomTrips = 0;
            CacheHits = 0;
        }

        /// <summary>Advances every agent one tick.</summary>
        public void Tick()
        {
            float cell = _grid.CellSize;
            for (int i = 0; i < _walkers.Length; i++)
            {
                SpikeWalker w = _walkers[i];
                if (w.HasPath)
                {
                    w.Walk(cell);
                    if (!w.HasPath)
                    {
                        _dwell[i] = DwellTicks;
                        if (!_tripEndsAtPair[i]) _place[i] = Place.Elsewhere;
                    }
                    continue;
                }
                if (_dwell[i] > 0)
                {
                    _dwell[i]--;
                    continue;
                }
                if (!StartTrip(i, w)) _dwell[i] = 1;
            }
        }

        private bool StartTrip(int i, SpikeWalker w)
        {
            int p = _pairOf[i];
            if (_rng.NextFloat() < RepeatTripShare)
            {
                bool toWork = _place[i] == Place.Home;
                int gx = toWork ? _workX[p] : _homeX[p];
                int gz = toWork ? _workZ[p] : _homeZ[p];
                if (!PlanRepeat(i, w, p, toWork, gx, gz)) return false;
                RepeatTrips++;
                _place[i] = toWork ? Place.Work : Place.Home;
                _tripEndsAtPair[i] = true;
                return true;
            }

            if (!PickRandomTarget(w, out int rx, out int rz)) return false;
            if (!_planner.Plan(w, rx, rz)) return false;
            RandomTrips++;
            _tripEndsAtPair[i] = false;
            return true;
        }

        private bool PlanRepeat(int i, SpikeWalker w, int pair, bool toWork, int gx, int gz)
        {
            bool atPairEnd = _place[i] != Place.Elsewhere;
            int key = pair * 2 + (toWork ? 1 : 0);
            if (_cachePairPaths && atPairEnd && _cache.TryGetValue(key, out IReadOnlyList<CellRef> cached))
            {
                w.SetPath(cached);
                CacheHits++;
                return true;
            }
            if (!_planner.Plan(w, gx, gz)) return false;
            if (_cachePairPaths && atPairEnd) _cache[key] = w.Path;
            return true;
        }

        private bool PickRandomTarget(SpikeWalker w, out int gx, out int gz)
        {
            float cell = _grid.CellSize;
            float sideX = _grid.Width * cell;
            float sideZ = _grid.Height * cell;
            for (int attempt = 0; attempt < MaxSampleAttempts; attempt++)
            {
                float angle = _rng.Range(0f, (float)(2.0 * Math.PI));
                float dist = _rng.Range(SpikeCrowd.MinTargetMetres, SpikeCrowd.MaxTargetMetres);
                float tx = w.X + dist * (float)Math.Cos(angle);
                float tz = w.Z + dist * (float)Math.Sin(angle);
                if (tx < 0f || tz < 0f || tx >= sideX || tz >= sideZ) continue;
                gx = (int)(tx / cell);
                gz = (int)(tz / cell);
                if (_grid.IsPassable(gx, gz)) return true;
            }
            gx = 0;
            gz = 0;
            return false;
        }

        private void CreatePair(int p)
        {
            float cell = _grid.CellSize;
            int hx = 0, hz = 0;
            for (int attempt = 0; attempt < 1000; attempt++)
            {
                hx = _rng.NextInt(_grid.Width);
                hz = _rng.NextInt(_grid.Height);
                if (_grid.IsPassable(hx, hz)) break;
            }
            _homeX[p] = hx;
            _homeZ[p] = hz;

            // Work is 50-1,500 m from home where the map allows; otherwise fall back to the home cell's own spot.
            _workX[p] = hx;
            _workZ[p] = hz;
            for (int attempt = 0; attempt < 1000; attempt++)
            {
                float angle = _rng.Range(0f, (float)(2.0 * Math.PI));
                float dist = _rng.Range(SpikeCrowd.MinTargetMetres, SpikeCrowd.MaxTargetMetres);
                int wx = (int)(((hx + 0.5f) * cell + dist * (float)Math.Cos(angle)) / cell);
                int wz = (int)(((hz + 0.5f) * cell + dist * (float)Math.Sin(angle)) / cell);
                if (!_grid.IsPassable(wx, wz)) continue;
                _workX[p] = wx;
                _workZ[p] = wz;
                break;
            }
        }
    }
}
