using LordsLegacy.Prototypes.PerfSpike.Agents;
using LordsLegacy.Prototypes.PerfSpike.Grid;
using NUnit.Framework;

namespace LordsLegacy.Prototypes.PerfSpike.Tests
{
    /// <summary>
    /// THROWAWAY (T-008). Perf runs on the default 6 km map. [Explicit] so a plain `dotnet test` skips them.
    /// Run one with: dotnet test Perf.Tests/Perf.Tests.csproj --filter "TestCategory=Perf" --logger "console;verbosity=detailed"
    /// The report is written to the test output. Each run prints its report to the test output.
    /// </summary>
    [TestFixture]
    [Category("Perf")]
    public class CrowdPerfTests
    {
        [Explicit("Perf run on the 6 km map. Slow: the first tick plans every agent's path.")]
        [TestCase(1000, 600)]
        [TestCase(10000, 600)]
        public void Crowd_OnSixKmMap_TickAndQueryCost(int agents, int ticks)
        {
            SpikeCostGrid grid = SpikeMapGenerator.Generate(1);
            var crowd = new SpikeCrowd(grid, agents, 1UL);

            // One warm-up tick takes the initial burst of path plans out of the steady-state numbers.
            SpikeRunReport report = SpikeCrowdHarness.Run(crowd, ticks, 1);

            TestContext.Out.WriteLine(report.ToString());
            Assert.That(report.Ticks, Is.EqualTo(ticks));
        }
    }
}
