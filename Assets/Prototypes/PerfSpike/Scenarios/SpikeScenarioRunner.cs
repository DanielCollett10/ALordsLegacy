using System;
using System.Collections.Generic;
using System.Diagnostics;
using LordsLegacy.Prototypes.PerfSpike.Grid;

namespace LordsLegacy.Prototypes.PerfSpike.Scenarios
{
    /// <summary>
    /// THROWAWAY (T-009). Runs the perf scenarios on a cost grid and collects <see cref="SpikeScenarioResult"/>s.
    /// Each tick is timed by wrapping the whole tick in a Stopwatch; warm-up ticks are not counted.
    /// </summary>
    public static class SpikeScenarioRunner
    {
        /// <summary>Agent counts of the standard runs.</summary>
        public static readonly int[] StandardAgentCounts = { 2500, 5000, 10000 };

        /// <summary>
        /// Mixed trips: 80% repeat home/work trips, 20% random targets. <paramref name="warmupTicks"/> should cover
        /// <see cref="SpikeTripCrowd.StaggerTicks"/> so every agent is already cycling when timing starts.
        /// </summary>
        public static SpikeScenarioResult RunMixedTrips(SpikeCostGrid grid, int agents, ulong seed, int ticks,
            int warmupTicks, bool useCache = false, int parallelism = 1)
        {
            var planner = new SpikePathPlanner(grid, useCache, parallelism);
            var crowd = new SpikeTripCrowd(planner, agents, seed);
            for (int i = 0; i < warmupTicks; i++) crowd.Tick();
            planner.ResetStats();
            crowd.ResetStats();

            double[] samples = TimeTicks(ticks, crowd.Tick);

            string name = "mixed trips" + (useCache ? " +cache" : "") + (parallelism > 1 ? " +par" + parallelism : "");
            SpikeScenarioResult r = Summarise(name, grid, agents, samples, planner);
            r.Parallelism = parallelism;
            if (planner.Cache != null)
            {
                r.CacheEnabled = true;
                r.CacheHits = planner.Cache.Hits;
                r.CacheMisses = planner.Cache.Misses;
                r.CacheEntries = planner.Cache.Count;
                r.CacheBytes = planner.Cache.ApproxBytes;
            }
            int total = (int)(crowd.RepeatTrips + crowd.RandomTrips);
            r.Notes = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0} pairs; trips started: {1} repeat, {2} random ({3:F1}% repeat); A* queries: {4}; failed queries: {5}",
                crowd.PairCount, crowd.RepeatTrips, crowd.RandomTrips,
                total == 0 ? 0.0 : 100.0 * crowd.RepeatTrips / total, r.Queries, planner.Failed);
            return r;
        }

        /// <summary>
        /// The same mixed-trip run four ways: plain, cache, parallel, cache + parallel. All four end in the same
        /// agent positions (see the determinism tests); only the cost differs.
        /// </summary>
        public static List<SpikeScenarioResult> RunCacheParallelMatrix(SpikeCostGrid grid, int agents, ulong seed, int ticks,
            int warmupTicks, int parallelism)
        {
            return new List<SpikeScenarioResult>
            {
                RunMixedTrips(grid, agents, seed, ticks, warmupTicks, false, 1),
                RunMixedTrips(grid, agents, seed, ticks, warmupTicks, true, 1),
                RunMixedTrips(grid, agents, seed, ticks, warmupTicks, false, parallelism),
                RunMixedTrips(grid, agents, seed, ticks, warmupTicks, true, parallelism)
            };
        }

        /// <summary>
        /// Sunday rush: agents / 5 home-to-market requests spread over <paramref name="windowTicks"/>. With
        /// <paramref name="withBackground"/> the mixed-trip crowd (already warmed up) keeps running underneath.
        /// Timed ticks are the window ticks.
        /// </summary>
        public static SpikeScenarioResult RunSundayRush(SpikeCostGrid grid, int agents, ulong seed, bool withBackground,
            int windowTicks = SpikeSundayRush.DefaultWindowTicks, int backgroundWarmupTicks = SpikeTripCrowd.StaggerTicks)
        {
            var planner = new SpikePathPlanner(grid);
            SpikeTripCrowd background = null;
            if (withBackground)
            {
                background = new SpikeTripCrowd(planner, agents, seed);
                for (int i = 0; i < backgroundWarmupTicks; i++) background.Tick();
            }
            var rush = new SpikeSundayRush(planner, agents, seed + 1UL, windowTicks);
            planner.ResetStats();

            Action step = withBackground ? (Action)(() => { background.Tick(); rush.Tick(); }) : rush.Tick;
            double[] samples = TimeTicks(windowTicks, step);

            SpikeScenarioResult r = Summarise(withBackground ? "sunday rush + background" : "sunday rush", grid, agents, samples, planner);
            r.Notes = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0} households request home-to-market (<= {1:F0} m) over {2} ticks; requests made: {3}; failed queries: {4}",
                rush.Households, SpikeSundayRush.MaxHomeToMarketMetres, windowTicks, rush.Requests, planner.Failed);
            return r;
        }

        private static double[] TimeTicks(int ticks, Action tick)
        {
            var ms = new double[ticks];
            for (int i = 0; i < ticks; i++)
            {
                long t0 = Stopwatch.GetTimestamp();
                tick();
                ms[i] = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            }
            return ms;
        }

        private static SpikeScenarioResult Summarise(string name, SpikeCostGrid grid, int agents, double[] tickMs, SpikePathPlanner planner)
        {
            double total = 0, max = 0;
            int worst = 0;
            for (int i = 0; i < tickMs.Length; i++)
            {
                total += tickMs[i];
                if (tickMs[i] > max)
                {
                    max = tickMs[i];
                    worst = i;
                }
            }
            double[] sorted = (double[])tickMs.Clone();
            Array.Sort(sorted);

            long cells = (long)grid.Width * grid.Height;
            var r = new SpikeScenarioResult
            {
                Name = name,
                Agents = agents,
                Ticks = tickMs.Length,
                TickMeanMs = tickMs.Length == 0 ? 0 : total / tickMs.Length,
                TickMaxMs = max,
                WorstTickIndex = worst,
                TickP95Ms = sorted.Length == 0 ? 0 : sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(0.95 * sorted.Length) - 1)],
                SustainedTicksPerSecond = total <= 0 ? 0 : tickMs.Length * 1000.0 / total,
                GridBytes = cells * sizeof(float),
                // SpikeAStar: g (float), parent (int), seenGen (int), closedGen (int) per cell. The open heap is extra and grows on demand.
                SearchBytes = cells * (sizeof(float) + 3 * sizeof(int)),
                Queries = planner.Samples.TotalCount,
                FailedQueries = planner.Failed
            };
            for (int b = 0; b < planner.Samples.BandCount; b++)
            {
                r.Bands.Add(new SpikeScenarioResult.BandRow
                {
                    Label = planner.Samples.BandLabel(b),
                    Count = planner.Samples.Count(b),
                    P50Ms = planner.Samples.Percentile(b, 0.5),
                    P95Ms = planner.Samples.Percentile(b, 0.95)
                });
            }
            return r;
        }
    }
}
