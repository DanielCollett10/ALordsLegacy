using System.Linq;
using LordsLegacy.Prototypes.PerfSpike.Agents;
using LordsLegacy.Prototypes.PerfSpike.Grid;
using NUnit.Framework;

namespace LordsLegacy.Prototypes.PerfSpike.Tests
{
    /// <summary>THROWAWAY (T-008). Fast tests for the walking agents and the timing harness. Perf runs are in CrowdPerfTests.</summary>
    [TestFixture]
    public class CrowdTests
    {
        private static SpikeCostGrid Flat(int width, int height)
        {
            var costs = new float[width * height];
            for (int i = 0; i < costs.Length; i++) costs[i] = 1f;
            return new SpikeCostGrid(width, height, 2f, costs);
        }

        [Test]
        public void AgentCount_IsTheParameter()
        {
            var crowd = new SpikeCrowd(Flat(100, 100), 37, 1UL);

            Assert.That(crowd.AgentCount, Is.EqualTo(37));
            Assert.That(crowd.Agents.Count, Is.EqualTo(37));
        }

        [Test]
        public void EachTick_WalksExactly0_87Metres_OnceAgentHasAPath()
        {
            // 2 km flat map; the agent starts near the middle, so a random target 50-1,500 m away is almost always on the map.
            var crowd = new SpikeCrowd(Flat(1000, 1000), 1, 5UL);
            crowd.PlaceAgent(0, 1000f, 1000f);

            crowd.Tick();
            Assert.That(crowd.Agents[0].Walked, Is.EqualTo(SpikeCrowd.WalkStepMetres).Within(1e-4f), "first tick plans and walks");

            for (int i = 0; i < 9; i++) crowd.Tick();
            Assert.That(crowd.Agents[0].Walked, Is.EqualTo(10 * SpikeCrowd.WalkStepMetres).Within(1e-3f), "10 ticks = 8.7 m");
        }

        [Test]
        public void WalkStep_IsZeroPointEightSevenMetres()
        {
            Assert.That(SpikeCrowd.WalkStepMetres, Is.EqualTo(0.87f).Within(1e-6f));
        }

        [Test]
        public void ArrivalAtTarget_RequestsANewTarget()
        {
            var crowd = new SpikeCrowd(Flat(1000, 1000), 1, 5UL);
            crowd.PlaceAgent(0, 1000f, 1000f);
            SpikeAgent agent = crowd.Agents[0];

            // Targets are at most 1,500 m away, so the agent arrives within about 1,725 ticks.
            for (int t = 0; t < 10000 && agent.Replans < 2; t++) crowd.Tick();

            Assert.That(agent.Replans, Is.GreaterThanOrEqualTo(2), "a second target is requested on arrival");
            Assert.That(agent.LastTargetMetres, Is.InRange(SpikeCrowd.MinTargetMetres, SpikeCrowd.MaxTargetMetres));
        }

        [Test]
        public void RandomTargets_AreBetween50And1500Metres()
        {
            var crowd = new SpikeCrowd(Flat(1000, 1000), 50, 9UL);
            crowd.Tick();

            var planned = crowd.Agents.Where(a => a.Replans > 0).ToList();
            Assert.That(planned.Count, Is.GreaterThan(0), "most agents find a target on a 2 km flat map");
            Assert.That(planned.All(a => a.LastTargetMetres >= SpikeCrowd.MinTargetMetres
                                      && a.LastTargetMetres <= SpikeCrowd.MaxTargetMetres), Is.True);
        }

        [Test]
        public void SameSeed_GivesSamePositionsAfterNTicks()
        {
            SpikeCostGrid grid = SpikeMapGenerator.Generate(7, 500, 500, 2f);
            var a = new SpikeCrowd(grid, 20, 99UL);
            var b = new SpikeCrowd(grid, 20, 99UL);

            for (int t = 0; t < 200; t++)
            {
                a.Tick();
                b.Tick();
            }

            Assert.That(a.Agents.Any(agent => agent.Walked > 0f), Is.True, "agents actually moved");
            for (int i = 0; i < a.AgentCount; i++)
            {
                Assert.That(b.Agents[i].X, Is.EqualTo(a.Agents[i].X), "agent {0} X", i);
                Assert.That(b.Agents[i].Z, Is.EqualTo(a.Agents[i].Z), "agent {0} Z", i);
            }
        }

        [Test]
        public void DifferentSeed_GivesDifferentPositions()
        {
            SpikeCostGrid grid = SpikeMapGenerator.Generate(7, 500, 500, 2f);
            var a = new SpikeCrowd(grid, 10, 99UL);
            var b = new SpikeCrowd(grid, 10, 100UL);

            for (int t = 0; t < 100; t++)
            {
                a.Tick();
                b.Tick();
            }

            bool anyDifferent = Enumerable.Range(0, a.AgentCount)
                .Any(i => a.Agents[i].X != b.Agents[i].X || a.Agents[i].Z != b.Agents[i].Z);
            Assert.That(anyDifferent, Is.True);
        }

        [Test]
        public void Harness_RunsTheTicksAndReportsCountsAndTimings()
        {
            var crowd = new SpikeCrowd(Flat(1000, 1000), 5, 3UL);

            // No warm-up: every agent plans its first path inside the timed ticks, so queries are counted.
            SpikeRunReport report = SpikeCrowdHarness.Run(crowd, 50, 0);

            Assert.That(report.Agents, Is.EqualTo(5));
            Assert.That(report.Ticks, Is.EqualTo(50));
            Assert.That(crowd.TickStats.Count, Is.EqualTo(50));
            Assert.That(report.Queries, Is.GreaterThanOrEqualTo(1));
            Assert.That(report.MovementMaxMs, Is.GreaterThanOrEqualTo(report.MovementMeanMs));
            Assert.That(report.TickMaxMs, Is.GreaterThanOrEqualTo(report.TickMeanMs));
            Assert.That(report.MovementMeanMs, Is.LessThanOrEqualTo(report.TickMeanMs + 1e-9),
                "movement is the tick minus its queries");
            Assert.That(report.QueryMeanMs, Is.GreaterThanOrEqualTo(0.0));
            Assert.That(report.ExpandedPerPathMean, Is.GreaterThan(0.0));
            Assert.That(report.ToString(), Does.Contain("agents=5"));
        }

        [Test]
        public void Harness_WarmupTicks_AreNotCounted()
        {
            var crowd = new SpikeCrowd(Flat(1000, 1000), 3, 4UL);

            SpikeRunReport report = SpikeCrowdHarness.Run(crowd, 10, 5);

            Assert.That(report.Ticks, Is.EqualTo(10));
            Assert.That(crowd.Ticks, Is.EqualTo(10));
        }
    }
}
