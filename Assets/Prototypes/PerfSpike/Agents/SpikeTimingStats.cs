using System.Diagnostics;

namespace LordsLegacy.Prototypes.PerfSpike.Agents
{
    /// <summary>
    /// THROWAWAY (T-008). Count, total and maximum of timed durations, kept in
    /// <see cref="Stopwatch"/> ticks and reported in milliseconds.
    /// </summary>
    public sealed class SpikeTimingStats
    {
        /// <summary>Number of samples added.</summary>
        public long Count { get; private set; }

        /// <summary>Sum of samples, in Stopwatch ticks.</summary>
        public long TotalTicks { get; private set; }

        /// <summary>Largest sample, in Stopwatch ticks.</summary>
        public long MaxTicks { get; private set; }

        /// <summary>Adds one sample of <paramref name="elapsedTicks"/> Stopwatch ticks.</summary>
        public void Add(long elapsedTicks)
        {
            Count++;
            TotalTicks += elapsedTicks;
            if (elapsedTicks > MaxTicks) MaxTicks = elapsedTicks;
        }

        /// <summary>Removes all samples.</summary>
        public void Clear()
        {
            Count = 0;
            TotalTicks = 0;
            MaxTicks = 0;
        }

        /// <summary>Sum of samples in milliseconds.</summary>
        public double TotalMs => TicksToMs(TotalTicks);

        /// <summary>Mean sample in milliseconds, or 0 when there are none.</summary>
        public double MeanMs => Count == 0 ? 0.0 : TicksToMs(TotalTicks) / Count;

        /// <summary>Largest sample in milliseconds.</summary>
        public double MaxMs => TicksToMs(MaxTicks);

        private static double TicksToMs(long ticks)
        {
            return ticks * 1000.0 / Stopwatch.Frequency;
        }
    }
}
