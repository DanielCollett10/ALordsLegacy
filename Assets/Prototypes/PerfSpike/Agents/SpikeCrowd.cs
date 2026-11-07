using System;
using System.Collections.Generic;
using System.Diagnostics;
using LordsLegacy.Prototypes.PerfSpike.Grid;
using LordsLegacy.Prototypes.PerfSpike.Nav;

namespace LordsLegacy.Prototypes.PerfSpike.Agents
{
    /// <summary>
    /// THROWAWAY (T-008). A crowd of agents walking over a <see cref="SpikeCostGrid"/>.
    /// Each tick every agent walks <see cref="WalkStepMetres"/> along its A* path (cell centres). When it reaches
    /// the end it picks a random point 50 to 1,500 m away; if that point is on the map and passable and A* can
    /// reach it, the agent takes that path. Agents start with no path, so their first tick plans one.
    /// Seeded: the same grid, agent count and seed give the same positions after any number of ticks.
    /// Timing: <see cref="TickStats"/> covers each whole tick, <see cref="MovementStats"/> the tick minus its
    /// A* queries, and <see cref="QueryStats"/> each A* call. Timing is never read back into the state.
    /// Not thread-safe; one instance per grid.
    /// </summary>
    public sealed class SpikeCrowd
    {
        /// <summary>Distance an agent walks per tick, in metres (0.87 m per game minute, decision 0004).</summary>
        public const float WalkStepMetres = 0.87f;

        /// <summary>Shortest random target distance, in metres.</summary>
        public const float MinTargetMetres = 50f;

        /// <summary>Longest random target distance, in metres.</summary>
        public const float MaxTargetMetres = 1500f;

        /// <summary>Random target points tried per request before the agent waits a tick.</summary>
        public const int MaxSampleAttempts = 16;

        /// <summary>Target requests one agent may make in a single tick (arrivals mid-step).</summary>
        public const int MaxReplansPerTick = 4;

        private const int MaxPlacementAttempts = 1000;

        private readonly SpikeCostGrid _grid;
        private readonly SpikeAStar _search;
        private readonly SpikeRng _rng;
        private readonly SpikeAgent[] _agents;
        private long _queryTicksThisTick;

        /// <summary>
        /// Creates <paramref name="agentCount"/> agents on random passable cells (cell centres), drawn from <paramref name="seed"/>.
        /// </summary>
        public SpikeCrowd(SpikeCostGrid grid, int agentCount, ulong seed)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (agentCount < 0) throw new ArgumentOutOfRangeException(nameof(agentCount));

            _grid = grid;
            _search = new SpikeAStar(grid);
            _rng = new SpikeRng(seed);
            _agents = new SpikeAgent[agentCount];
            for (int i = 0; i < agentCount; i++)
            {
                _agents[i] = CreateAgentOnPassableCell(i);
            }
        }

        /// <summary>The agents, indexed by <see cref="SpikeAgent.Id"/>.</summary>
        public IReadOnlyList<SpikeAgent> Agents => _agents;

        /// <summary>Number of agents.</summary>
        public int AgentCount => _agents.Length;

        /// <summary>Ticks run since the last <see cref="ResetStats"/> (or since creation).</summary>
        public long Ticks { get; private set; }

        /// <summary>Stopwatch time of each whole tick, including its A* queries.</summary>
        public SpikeTimingStats TickStats { get; } = new SpikeTimingStats();

        /// <summary>Stopwatch time of each tick's movement only: whole tick minus its A* queries.</summary>
        public SpikeTimingStats MovementStats { get; } = new SpikeTimingStats();

        /// <summary>Stopwatch time of each A* query, found or not.</summary>
        public SpikeTimingStats QueryStats { get; } = new SpikeTimingStats();

        /// <summary>Paths found so far since the last <see cref="ResetStats"/>.</summary>
        public long PathsFound { get; private set; }

        /// <summary>Nodes expanded by A* across all found paths since the last <see cref="ResetStats"/>.</summary>
        public long ExpandedNodes { get; private set; }

        /// <summary>Clears every counter and timing. Agent positions and paths are kept.</summary>
        public void ResetStats()
        {
            Ticks = 0;
            PathsFound = 0;
            ExpandedNodes = 0;
            TickStats.Clear();
            MovementStats.Clear();
            QueryStats.Clear();
        }

        /// <summary>Test and harness hook: moves agent <paramref name="index"/> to (x,z) metres and clears its path.</summary>
        public void PlaceAgent(int index, float x, float z)
        {
            SpikeAgent agent = _agents[index];
            agent.X = x;
            agent.Z = z;
            agent.Path = SpikeAgent.NoPath;
            agent.NextWaypoint = 0;
        }

        /// <summary>Advances every agent by one tick and records its timing.</summary>
        public void Tick()
        {
            long start = Stopwatch.GetTimestamp();
            _queryTicksThisTick = 0;
            for (int i = 0; i < _agents.Length; i++)
            {
                Advance(_agents[i]);
            }
            long total = Stopwatch.GetTimestamp() - start;

            TickStats.Add(total);
            MovementStats.Add(total - _queryTicksThisTick);
            Ticks++;
        }

        private void Advance(SpikeAgent agent)
        {
            float cell = _grid.CellSize;
            float remaining = WalkStepMetres;
            int replans = 0;

            while (remaining > 0f)
            {
                if (agent.NeedsTarget)
                {
                    if (replans++ >= MaxReplansPerTick || !RequestTarget(agent)) return;
                    continue;
                }

                CellRef next = agent.Path[agent.NextWaypoint];
                float tx = (next.X + 0.5f) * cell;
                float tz = (next.Z + 0.5f) * cell;
                float dx = tx - agent.X;
                float dz = tz - agent.Z;
                float dist = (float)Math.Sqrt(dx * dx + dz * dz);

                if (dist <= remaining)
                {
                    agent.X = tx;
                    agent.Z = tz;
                    agent.Walked += dist;
                    remaining -= dist;
                    agent.NextWaypoint++;
                }
                else
                {
                    float f = remaining / dist;
                    agent.X += dx * f;
                    agent.Z += dz * f;
                    agent.Walked += remaining;
                    remaining = 0f;
                }
            }
        }

        /// <summary>Picks a random target 50 to 1,500 m away and plans a path to it. False if no attempt succeeded.</summary>
        private bool RequestTarget(SpikeAgent agent)
        {
            float cell = _grid.CellSize;
            float sideX = _grid.Width * cell;
            float sideZ = _grid.Height * cell;
            int sx = CellOf(agent.X, _grid.Width, cell);
            int sz = CellOf(agent.Z, _grid.Height, cell);

            for (int attempt = 0; attempt < MaxSampleAttempts; attempt++)
            {
                float angle = _rng.Range(0f, (float)(2.0 * Math.PI));
                float dist = _rng.Range(MinTargetMetres, MaxTargetMetres);
                float tx = agent.X + dist * (float)Math.Cos(angle);
                float tz = agent.Z + dist * (float)Math.Sin(angle);
                if (tx < 0f || tz < 0f || tx >= sideX || tz >= sideZ) continue;

                int gx = (int)(tx / cell);
                int gz = (int)(tz / cell);
                if (!_grid.IsPassable(gx, gz)) continue;
                if (!FindPath(sx, sz, gx, gz, out SpikePath path)) continue;

                agent.Path = path.Cells;
                agent.NextWaypoint = 0;
                agent.LastTargetMetres = dist;
                agent.Replans++;
                return true;
            }
            return false;
        }

        private bool FindPath(int sx, int sz, int gx, int gz, out SpikePath path)
        {
            long t0 = Stopwatch.GetTimestamp();
            bool found = _search.TryFindPath(sx, sz, gx, gz, out path);
            long elapsed = Stopwatch.GetTimestamp() - t0;

            _queryTicksThisTick += elapsed;
            QueryStats.Add(elapsed);
            if (found)
            {
                PathsFound++;
                ExpandedNodes += path.Expanded;
            }
            return found;
        }

        private SpikeAgent CreateAgentOnPassableCell(int id)
        {
            for (int attempt = 0; attempt < MaxPlacementAttempts; attempt++)
            {
                int cx = _rng.NextInt(_grid.Width);
                int cz = _rng.NextInt(_grid.Height);
                if (_grid.IsPassable(cx, cz))
                {
                    return new SpikeAgent(id, (cx + 0.5f) * _grid.CellSize, (cz + 0.5f) * _grid.CellSize);
                }
            }
            throw new InvalidOperationException("no passable cell found for agent placement");
        }

        private static int CellOf(float metres, int count, float cell)
        {
            int c = (int)Math.Floor(metres / cell);
            if (c < 0) return 0;
            if (c >= count) return count - 1;
            return c;
        }
    }
}
