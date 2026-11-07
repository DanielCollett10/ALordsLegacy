using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LordsLegacy.Prototypes.PerfSpike.Scenarios
{
    /// <summary>THROWAWAY (T-009). Metrics of one scenario run, with a plain-text table formatter.</summary>
    public sealed class SpikeScenarioResult
    {
        /// <summary>Game ticks per real second at 32x with a 15-minute day: 32 * 1440 / 900.</summary>
        public const double TargetTicksPerSecond = 51.2;

        /// <summary>Sim time allowed per 60 fps frame, ms.</summary>
        public const double FrameBudgetMs = 5.0;

        /// <summary>Scenario name.</summary>
        public string Name { get; set; }

        /// <summary>Agents in the scenario.</summary>
        public int Agents { get; set; }

        /// <summary>Timed ticks.</summary>
        public int Ticks { get; set; }

        /// <summary>Mean tick cost, ms.</summary>
        public double TickMeanMs { get; set; }

        /// <summary>Worst tick cost, ms.</summary>
        public double TickMaxMs { get; set; }

        /// <summary>Tick on which the worst tick happened (0-based, timed ticks only).</summary>
        public int WorstTickIndex { get; set; }

        /// <summary>95th percentile tick cost, ms.</summary>
        public double TickP95Ms { get; set; }

        /// <summary>Ticks the Sim could run per second of its own time (ticks / total tick time).</summary>
        public double SustainedTicksPerSecond { get; set; }

        /// <summary>Bytes of the cost grid.</summary>
        public long GridBytes { get; set; }

        /// <summary>Bytes of the A* per-cell working arrays.</summary>
        public long SearchBytes { get; set; }

        /// <summary>Path queries made in the timed ticks.</summary>
        public int Queries { get; set; }

        /// <summary>Queries that found no path.</summary>
        public long FailedQueries { get; set; }

        /// <summary>Free-text notes shown under the table (trip mix, cache hits...).</summary>
        public string Notes { get; set; } = "";

        /// <summary>Band labels, with p50 and p95 query ms and sample count per band.</summary>
        public List<BandRow> Bands { get; } = new List<BandRow>();

        /// <summary>True if the worst tick is over the 5 ms frame budget.</summary>
        public bool WorstTickOverFrameBudget => TickMaxMs > FrameBudgetMs;

        /// <summary>True if the Sim cannot sustain 51.2 ticks per second.</summary>
        public bool BelowTargetRate => SustainedTicksPerSecond < TargetTicksPerSecond;

        /// <summary>True if either budget is exceeded.</summary>
        public bool BudgetExceeded => WorstTickOverFrameBudget || BelowTargetRate;

        /// <summary>One distance band's path cost.</summary>
        public sealed class BandRow
        {
            /// <summary>Band label.</summary>
            public string Label;
            /// <summary>Queries in the band.</summary>
            public int Count;
            /// <summary>Median query ms.</summary>
            public double P50Ms;
            /// <summary>95th percentile query ms.</summary>
            public double P95Ms;
        }

        /// <summary>Formats results as one plain-text table: a summary row per result, then path cost by band.</summary>
        public static string FormatTable(IReadOnlyList<SpikeScenarioResult> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            var sb = new StringBuilder();
            CultureInfo c = CultureInfo.InvariantCulture;
            string header = string.Format(c, "{0,-28} {1,7} {2,6} {3,10} {4,10} {5,10} {6,10} {7,9} {8,9} {9,8}",
                "scenario", "agents", "ticks", "mean ms", "p95 ms", "max ms", "ticks/s", "grid MB", "A* MB", "budget");
            sb.AppendLine(header);
            sb.AppendLine(new string('-', header.Length));
            foreach (SpikeScenarioResult r in results)
            {
                sb.AppendLine(string.Format(c, "{0,-28} {1,7} {2,6} {3,10:F4} {4,10:F4} {5,10:F4} {6,10:F1} {7,9:F1} {8,9:F1} {9,8}",
                    r.Name, r.Agents, r.Ticks, r.TickMeanMs, r.TickP95Ms, r.TickMaxMs, r.SustainedTicksPerSecond,
                    r.GridBytes / 1048576.0, r.SearchBytes / 1048576.0, r.BudgetExceeded ? "EXCEEDED" : "ok"));
            }
            sb.AppendLine();
            sb.AppendLine(string.Format(c, "Budget: {0:F1} ticks/s needed (32x, 15-min day); worst tick must be <= {1:F1} ms per 60 fps frame.",
                TargetTicksPerSecond, FrameBudgetMs));
            foreach (SpikeScenarioResult r in results)
            {
                sb.AppendLine();
                sb.AppendLine(string.Format(c, "{0} ({1} agents): worst tick {2:F4} ms at tick {3}; over 5 ms frame budget: {4}; below 51.2 ticks/s: {5}",
                    r.Name, r.Agents, r.TickMaxMs, r.WorstTickIndex,
                    r.WorstTickOverFrameBudget ? "YES" : "no", r.BelowTargetRate ? "YES" : "no"));
                if (r.Notes.Length > 0) sb.AppendLine("  " + r.Notes);
                sb.AppendLine(string.Format(c, "  path cost by distance  {0,-12} {1,8} {2,10} {3,10}", "band", "queries", "p50 ms", "p95 ms"));
                foreach (BandRow b in r.Bands)
                {
                    sb.AppendLine(string.Format(c, "                         {0,-12} {1,8} {2,10:F4} {3,10:F4}", b.Label, b.Count, b.P50Ms, b.P95Ms));
                }
            }
            return sb.ToString();
        }
    }
}
