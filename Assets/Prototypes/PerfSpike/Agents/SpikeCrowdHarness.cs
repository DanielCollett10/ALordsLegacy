using System;
using System.Globalization;

namespace LordsLegacy.Prototypes.PerfSpike.Agents
{
    /// <summary>
    /// THROWAWAY (T-008). Runs a <see cref="SpikeCrowd"/> for a number of ticks and reports the timings.
    /// Warm-up ticks run first and are excluded: the stats are reset before the timed ticks start.
    /// </summary>
    public static class SpikeCrowdHarness
    {
        /// <summary>Runs <paramref name="warmupTicks"/> untimed ticks, resets the stats, runs <paramref name="ticks"/> timed ticks and reports.</summary>
        public static SpikeRunReport Run(SpikeCrowd crowd, int ticks, int warmupTicks = 0)
        {
            if (crowd == null) throw new ArgumentNullException(nameof(crowd));
            if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
            if (warmupTicks < 0) throw new ArgumentOutOfRangeException(nameof(warmupTicks));

            for (int i = 0; i < warmupTicks; i++) crowd.Tick();
            crowd.ResetStats();
            for (int i = 0; i < ticks; i++) crowd.Tick();
            return Report(crowd);
        }

        /// <summary>Snapshot of the crowd's counters and timings since its last reset.</summary>
        public static SpikeRunReport Report(SpikeCrowd crowd)
        {
            if (crowd == null) throw new ArgumentNullException(nameof(crowd));
            return new SpikeRunReport(
                crowd.AgentCount,
                crowd.Ticks,
                crowd.MovementStats.MeanMs,
                crowd.MovementStats.MaxMs,
                crowd.TickStats.MeanMs,
                crowd.TickStats.MaxMs,
                crowd.QueryStats.Count,
                crowd.QueryStats.MeanMs,
                crowd.QueryStats.MaxMs,
                crowd.PathsFound == 0 ? 0.0 : (double)crowd.ExpandedNodes / crowd.PathsFound);
        }
    }

    /// <summary>
    /// THROWAWAY (T-008). Result of a harness run. Movement is the tick minus its A* queries; the tick
    /// figures include the queries. Query figures cover every A* call, found or not. Expanded nodes are
    /// averaged over found paths only.
    /// </summary>
    public sealed class SpikeRunReport
    {
        /// <summary>Creates a report from already-measured values.</summary>
        public SpikeRunReport(int agents, long ticks, double movementMeanMs, double movementMaxMs,
            double tickMeanMs, double tickMaxMs, long queries, double queryMeanMs, double queryMaxMs,
            double expandedPerPathMean)
        {
            Agents = agents;
            Ticks = ticks;
            MovementMeanMs = movementMeanMs;
            MovementMaxMs = movementMaxMs;
            TickMeanMs = tickMeanMs;
            TickMaxMs = tickMaxMs;
            Queries = queries;
            QueryMeanMs = queryMeanMs;
            QueryMaxMs = queryMaxMs;
            ExpandedPerPathMean = expandedPerPathMean;
        }

        /// <summary>Number of agents in the crowd.</summary>
        public int Agents { get; }

        /// <summary>Timed ticks.</summary>
        public long Ticks { get; }

        /// <summary>Mean movement cost per tick (whole tick minus A* queries), ms.</summary>
        public double MovementMeanMs { get; }

        /// <summary>Largest movement cost of one tick, ms.</summary>
        public double MovementMaxMs { get; }

        /// <summary>Mean whole-tick cost, ms, including queries.</summary>
        public double TickMeanMs { get; }

        /// <summary>Largest whole-tick cost, ms, including queries.</summary>
        public double TickMaxMs { get; }

        /// <summary>A* queries made during the timed ticks.</summary>
        public long Queries { get; }

        /// <summary>Mean A* cost per query, ms.</summary>
        public double QueryMeanMs { get; }

        /// <summary>Largest A* cost of one query, ms.</summary>
        public double QueryMaxMs { get; }

        /// <summary>Mean nodes expanded per found path.</summary>
        public double ExpandedPerPathMean { get; }

        /// <summary>One-line summary with invariant-culture numbers, for logs and test output.</summary>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "agents={0} ticks={1} movement mean={2:F4} ms max={3:F4} ms | tick mean={4:F4} ms max={5:F4} ms | " +
                "queries={6} mean={7:F4} ms max={8:F4} ms expanded/path={9:F1}",
                Agents, Ticks, MovementMeanMs, MovementMaxMs, TickMeanMs, TickMaxMs,
                Queries, QueryMeanMs, QueryMaxMs, ExpandedPerPathMean);
        }
    }
}
