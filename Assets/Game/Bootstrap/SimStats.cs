using System.Collections.Generic;
using System.Diagnostics;

namespace LordsLegacy.Game
{
    /// <summary>Measures Sim cost: last Step time in ms and a rolling 1-second ticks-per-second rate.</summary>
    public sealed class SimStats
    {
        private const double WindowSeconds = 1.0;
        private readonly Queue<KeyValuePair<double, int>> _samples = new Queue<KeyValuePair<double, int>>();
        private int _windowTicks;

        /// <summary>Milliseconds the last Step call took.</summary>
        public double LastStepMs { get; private set; }

        /// <summary>Ticks run in the last second of real time.</summary>
        public double TicksPerSecond => _windowTicks / WindowSeconds;

        /// <summary>Records a Tick call: elapsed Stopwatch ticks of Step, ticks run, and the real time now.</summary>
        public void Record(long stopwatchTicks, int ticksRun, double nowSeconds)
        {
            LastStepMs = stopwatchTicks * 1000.0 / Stopwatch.Frequency;
            if (ticksRun > 0)
            {
                _samples.Enqueue(new KeyValuePair<double, int>(nowSeconds, ticksRun));
                _windowTicks += ticksRun;
            }
            Prune(nowSeconds);
        }

        /// <summary>Drops samples older than one second relative to <paramref name="nowSeconds"/>.</summary>
        public void Prune(double nowSeconds)
        {
            while (_samples.Count > 0 && nowSeconds - _samples.Peek().Key >= WindowSeconds)
                _windowTicks -= _samples.Dequeue().Value;
        }
    }
}
