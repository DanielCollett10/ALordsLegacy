using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using LordsLegacy.Prototypes.PerfSpike.Grid;
using LordsLegacy.Prototypes.PerfSpike.Scenarios;

namespace LordsLegacy.Prototypes.PerfSpike.Unity
{
    /// <summary>
    /// THROWAWAY (T-012). Runs the standard perf scenarios (2,500 / 5,000 / 10,000 agents: mixed trips and
    /// Sunday rush with background) on the 6 km map and returns the report as one string. No engine or Editor APIs,
    /// so the same code runs in the Editor, in a player, or under dotnet. The caller supplies the environment line.
    /// </summary>
    public static class BenchmarkRunner
    {
        /// <summary>Timed ticks for mixed trips (same as the dotnet FullSize run).</summary>
        public const int MeasuredTicks = 1200;

        /// <summary>Seed for the map, the crowds and the rush (same as the dotnet FullSize run).</summary>
        public const ulong Seed = 1UL;

        /// <summary>
        /// Runs every standard scenario and returns the formatted report. Blocks the calling thread until done.
        /// </summary>
        /// <param name="environment">One line describing the runtime (Unity version, Mono version, platform).</param>
        /// <param name="progress">Optional; called with a short line before each scenario and when the map is ready.</param>
        public static string Run(string environment, Action<string> progress = null)
        {
            var total = Stopwatch.StartNew();
            var sb = new StringBuilder();
            sb.AppendLine("Environment: " + (environment ?? "unknown"));
            sb.AppendLine("Logical processors: " + Environment.ProcessorCount);
            sb.AppendLine("Map: 6 km x 6 km at 2 m cells (3000 x 3000), seed " + Seed);
            sb.AppendLine("Measured ticks per mixed-trip run: " + MeasuredTicks + "; warm-up: " + (SpikeTripCrowd.StaggerTicks + 100));
            sb.AppendLine();

            Report(progress, "building 6 km map");
            SpikeCostGrid grid = SpikeMapGenerator.Generate(Seed);
            Report(progress, "map ready in " + total.ElapsedMilliseconds + " ms");

            var results = new List<SpikeScenarioResult>();
            foreach (int agents in SpikeScenarioRunner.StandardAgentCounts)
            {
                Report(progress, "mixed trips, " + agents + " agents");
                results.Add(SpikeScenarioRunner.RunMixedTrips(grid, agents, Seed, MeasuredTicks,
                    SpikeTripCrowd.StaggerTicks + 100));

                Report(progress, "sunday rush + background, " + agents + " agents");
                results.Add(SpikeScenarioRunner.RunSundayRush(grid, agents, Seed, true));
            }

            sb.Append(SpikeScenarioResult.FormatTable(results));
            sb.AppendLine();
            sb.AppendLine("Wall time for the whole benchmark: " + total.ElapsedMilliseconds + " ms");
            return sb.ToString();
        }

        private static void Report(Action<string> progress, string line)
        {
            if (progress != null) progress(line);
        }
    }
}
