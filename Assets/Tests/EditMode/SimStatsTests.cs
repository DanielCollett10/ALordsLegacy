using LordsLegacy.Game;
using NUnit.Framework;

namespace LordsLegacy.Tests.EditMode
{
    public class SimStatsTests
    {
        [Test]
        public void RollingRateCountsOnlyTheLastSecond()
        {
            var s = new SimStats();
            s.Record(0, 5, 0.0);
            s.Record(0, 3, 0.5);
            Assert.AreEqual(8.0, s.TicksPerSecond, 1e-9);
            s.Record(0, 2, 1.2); // first sample expired
            Assert.AreEqual(5.0, s.TicksPerSecond, 1e-9);
            s.Prune(3.0);
            Assert.AreEqual(0.0, s.TicksPerSecond, 1e-9);
        }

        [Test]
        public void LastStepMsConvertsStopwatchTicks()
        {
            var s = new SimStats();
            s.Record(System.Diagnostics.Stopwatch.Frequency / 1000, 1, 0);
            Assert.AreEqual(1.0, s.LastStepMs, 1e-6);
        }
    }
}
