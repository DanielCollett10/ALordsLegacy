using System;
using System.Collections.Generic;
using LordsLegacy.Prototypes.PerfSpike.Agents;
using LordsLegacy.Prototypes.PerfSpike.Grid;

namespace LordsLegacy.Prototypes.PerfSpike.Scenarios
{
    /// <summary>
    /// THROWAWAY (T-009). The 'Sunday rush' (FOOD-A7): one agent per household (agents / 5) asks for a path from
    /// home to the market. Homes lie 20 m up to <see cref="MaxHomeToMarketMetres"/> from the market. Request
    /// <c>i</c> is made at tick <c>i * WindowTicks / households</c>, so the requests spread evenly over
    /// <see cref="WindowTicks"/> (240 ticks = 4 game hours). Agents walk their path afterwards.
    /// </summary>
    public sealed class SpikeSundayRush
    {
        /// <summary>Longest straight-line home-to-market distance, metres.</summary>
        public const float MaxHomeToMarketMetres = 630f;

        /// <summary>Default request window: 4 game hours at one tick per game minute.</summary>
        public const int DefaultWindowTicks = 240;

        private readonly SpikePathPlanner _planner;
        private readonly SpikeWalker[] _walkers;
        private readonly int[] _homeX, _homeZ;
        private readonly List<SpikeWalker> _active = new List<SpikeWalker>();
        private int _next;
        private int _tick;

        /// <summary>Builds the households' homes and the market from <paramref name="seed"/>.</summary>
        public SpikeSundayRush(SpikePathPlanner planner, int agentCount, ulong seed, int windowTicks = DefaultWindowTicks)
        {
            if (planner == null) throw new ArgumentNullException(nameof(planner));
            if (agentCount < 1) throw new ArgumentOutOfRangeException(nameof(agentCount));
            if (windowTicks < 1) throw new ArgumentOutOfRangeException(nameof(windowTicks));

            _planner = planner;
            WindowTicks = windowTicks;
            Households = Math.Max(1, agentCount / SpikeTripCrowd.AgentsPerPair);

            SpikeCostGrid grid = planner.Grid;
            var rng = new SpikeRng(seed);
            FindMarket(grid, out int mx, out int mz);
            MarketX = mx;
            MarketZ = mz;

            _homeX = new int[Households];
            _homeZ = new int[Households];
            _walkers = new SpikeWalker[Households];
            for (int h = 0; h < Households; h++)
            {
                PlaceHome(grid, rng, h);
                _walkers[h] = new SpikeWalker
                {
                    X = (_homeX[h] + 0.5f) * grid.CellSize,
                    Z = (_homeZ[h] + 0.5f) * grid.CellSize
                };
            }
        }

        /// <summary>Households, which is also the number of requests.</summary>
        public int Households { get; }

        /// <summary>Ticks over which the requests are spread.</summary>
        public int WindowTicks { get; }

        /// <summary>Market cell column.</summary>
        public int MarketX { get; }

        /// <summary>Market cell row.</summary>
        public int MarketZ { get; }

        /// <summary>Requests made so far.</summary>
        public int Requests => _next;

        /// <summary>Tick on which request <paramref name="household"/> is made.</summary>
        public int RequestTick(int household)
        {
            return (int)((long)household * WindowTicks / Households);
        }

        /// <summary>Home cell of a household.</summary>
        public Nav.CellRef Home(int household) => new Nav.CellRef(_homeX[household], _homeZ[household]);

        /// <summary>Makes the requests due this tick, then walks every agent that has a path.</summary>
        public void Tick()
        {
            while (_next < Households && RequestTick(_next) <= _tick)
            {
                SpikeWalker w = _walkers[_next];
                if (_planner.Plan(w, MarketX, MarketZ)) _active.Add(w);
                _next++;
            }

            float cell = _planner.Grid.CellSize;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                SpikeWalker w = _active[i];
                w.Walk(cell);
                if (!w.HasPath)
                {
                    _active[i] = _active[_active.Count - 1];
                    _active.RemoveAt(_active.Count - 1);
                }
            }
            _tick++;
        }

        private static void FindMarket(SpikeCostGrid grid, out int mx, out int mz)
        {
            int cx = grid.Width / 2;
            int cz = grid.Height / 2;
            for (int r = 0; r < Math.Max(grid.Width, grid.Height); r++)
            {
                for (int dz = -r; dz <= r; dz++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != r) continue;
                        if (!grid.IsPassable(cx + dx, cz + dz)) continue;
                        mx = cx + dx;
                        mz = cz + dz;
                        return;
                    }
                }
            }
            throw new InvalidOperationException("no passable cell for the market");
        }

        private void PlaceHome(SpikeCostGrid grid, SpikeRng rng, int h)
        {
            float cell = grid.CellSize;
            float mxm = (MarketX + 0.5f) * cell;
            float mzm = (MarketZ + 0.5f) * cell;
            for (int attempt = 0; attempt < 1000; attempt++)
            {
                float angle = rng.Range(0f, (float)(2.0 * Math.PI));
                float dist = rng.Range(20f, MaxHomeToMarketMetres);
                float x = mxm + dist * (float)Math.Cos(angle);
                float z = mzm + dist * (float)Math.Sin(angle);
                if (x < 0f || z < 0f) continue;
                int gx = (int)(x / cell);
                int gz = (int)(z / cell);
                if (!grid.IsPassable(gx, gz)) continue;
                _homeX[h] = gx;
                _homeZ[h] = gz;
                return;
            }
            _homeX[h] = MarketX;
            _homeZ[h] = MarketZ;
        }
    }
}
