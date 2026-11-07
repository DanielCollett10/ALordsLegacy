using System.Collections.Generic;
using LordsLegacy.Prototypes.PerfSpike.Grid;
using LordsLegacy.Prototypes.PerfSpike.Scenarios;
using NUnit.Framework;

namespace LordsLegacy.Prototypes.PerfSpike.Tests
{
    /// <summary>THROWAWAY (T-009). Scenario correctness on a small map, plus the explicit full-size perf runs.</summary>
    [TestFixture]
    public class ScenarioTests
    {
        private static SpikeCostGrid SmallMap()
        {
            // 1,200 m square at 2 m cells: big enough for 630 m home-to-market distances.
            return SpikeMapGenerator.Generate(3UL, 600, 600, 2f);
        }

        [Test]
        public void MixedTrips_PairCountScalesWithAgents()
        {
            var grid = SmallMap();
            Assert.That(new SpikeTripCrowd(new SpikePathPlanner(grid), 500, 1UL).PairCount, Is.EqualTo(100));
            Assert.That(new SpikeTripCrowd(new SpikePathPlanner(grid), 1000, 1UL).PairCount, Is.EqualTo(200));
        }

        [Test]
        public void MixedTrips_AboutEightyPercentRepeat()
        {
            var grid = SmallMap();
            var planner = new SpikePathPlanner(grid);
            var crowd = new SpikeTripCrowd(planner, 400, 7UL);
            for (int i = 0; i < 1500; i++) crowd.Tick();

            double total = crowd.RepeatTrips + crowd.RandomTrips;
            Assert.That(total, Is.GreaterThan(500));
            Assert.That(crowd.RepeatTrips / total, Is.InRange(0.74, 0.86));
        }

        [Test]
        public void MixedTrips_SameSeedGivesSamePositions()
        {
            var grid = SmallMap();
            var a = new SpikeTripCrowd(new SpikePathPlanner(grid), 100, 5UL);
            var b = new SpikeTripCrowd(new SpikePathPlanner(grid), 100, 5UL);
            for (int i = 0; i < 700; i++)
            {
                a.Tick();
                b.Tick();
            }
            for (int i = 0; i < 100; i++)
            {
                Assert.That(a.Walkers[i].X, Is.EqualTo(b.Walkers[i].X));
                Assert.That(a.Walkers[i].Z, Is.EqualTo(b.Walkers[i].Z));
            }
        }

        [Test]
        public void MixedTrips_PairCacheServesRepeatTrips()
        {
            var grid = SmallMap();
            var planner = new SpikePathPlanner(grid);
            var crowd = new SpikeTripCrowd(planner, 200, 2UL, cachePairPaths: true);
            for (int i = 0; i < 2000; i++) crowd.Tick();
            Assert.That(crowd.CacheHits, Is.GreaterThan(0));
        }

        [Test]
        public void SundayRush_OneRequestPerHouseholdSpreadOverWindow()
        {
            var grid = SmallMap();
            var planner = new SpikePathPlanner(grid);
            var rush = new SpikeSundayRush(planner, 500, 4UL);

            Assert.That(rush.Households, Is.EqualTo(100));
            Assert.That(rush.RequestTick(0), Is.EqualTo(0));
            Assert.That(rush.RequestTick(99), Is.LessThan(SpikeSundayRush.DefaultWindowTicks));
            Assert.That(rush.RequestTick(99), Is.GreaterThan(SpikeSundayRush.DefaultWindowTicks - 5));

            for (int t = 0; t < SpikeSundayRush.DefaultWindowTicks; t++) rush.Tick();
            Assert.That(rush.Requests, Is.EqualTo(100));
            Assert.That(planner.Samples.TotalCount, Is.EqualTo(100));
        }

        [Test]
        public void SundayRush_HomesAreWithinMarketRange()
        {
            var grid = SmallMap();
            var rush = new SpikeSundayRush(new SpikePathPlanner(grid), 500, 4UL);
            for (int h = 0; h < rush.Households; h++)
            {
                float dx = (rush.Home(h).X - rush.MarketX) * grid.CellSize;
                float dz = (rush.Home(h).Z - rush.MarketZ) * grid.CellSize;
                Assert.That(dx * dx + dz * dz, Is.LessThanOrEqualTo((SpikeSundayRush.MaxHomeToMarketMetres + 2f) * (SpikeSundayRush.MaxHomeToMarketMetres + 2f)));
            }
        }

        [Test]
        public void Samples_BandsAndPercentiles()
        {
            Assert.That(SpikePathSamples.BandOf(50f), Is.EqualTo(0));
            Assert.That(SpikePathSamples.BandOf(100f), Is.EqualTo(1));
            Assert.That(SpikePathSamples.BandOf(999f), Is.EqualTo(3));
            Assert.That(SpikePathSamples.BandOf(1500f), Is.EqualTo(4));

            var s = new SpikePathSamples();
            for (int i = 1; i <= 100; i++) s.Add(300f, i);
            Assert.That(s.Count(2), Is.EqualTo(100));
            Assert.That(s.Percentile(2, 0.5), Is.EqualTo(50.0));
            Assert.That(s.Percentile(2, 0.95), Is.EqualTo(95.0));
            Assert.That(s.Percentile(0, 0.5), Is.EqualTo(0.0));
        }

        [Test]
        public void Runner_ProducesTableWithAllMetrics()
        {
            var grid = SmallMap();
            var results = new List<SpikeScenarioResult>
            {
                SpikeScenarioRunner.RunMixedTrips(grid, 300, 1UL, 200, 700),
                SpikeScenarioRunner.RunSundayRush(grid, 300, 1UL, false),
                SpikeScenarioRunner.RunSundayRush(grid, 300, 1UL, true, 240, 700)
            };
            string table = SpikeScenarioResult.FormatTable(results);
            TestContext.Out.WriteLine(table);

            Assert.That(results[0].Ticks, Is.EqualTo(200));
            Assert.That(results[0].GridBytes, Is.EqualTo(600L * 600 * 4));
            Assert.That(results[1].Ticks, Is.EqualTo(240));
            Assert.That(results[1].Queries, Is.EqualTo(60));
            Assert.That(table, Does.Contain("mean ms").And.Contain("max ms").And.Contain("ticks/s").And.Contain("p50 ms").And.Contain("p95 ms").And.Contain("grid MB"));
            Assert.That(table, Does.Contain("sunday rush"));
        }

        /// <summary>Full-size runs on the 6 km map. Run with: dotnet test Perf.Tests/Perf.Tests.csproj --filter "Name~FullSize" --logger "console;verbosity=detailed"</summary>
        [Explicit("Full-size scenario runs on the 6 km map. Takes minutes.")]
        [Category("Perf")]
        [Test]
        public void FullSize_AllScenarios_PrintTable()
        {
            SpikeCostGrid grid = SpikeMapGenerator.Generate(1);
            var results = new List<SpikeScenarioResult>();
            foreach (int agents in SpikeScenarioRunner.StandardAgentCounts)
            {
                results.Add(SpikeScenarioRunner.RunMixedTrips(grid, agents, 1UL, 1200, SpikeTripCrowd.StaggerTicks + 100));
                results.Add(SpikeScenarioRunner.RunSundayRush(grid, agents, 1UL, true));
            }
            results.Add(SpikeScenarioRunner.RunMixedTrips(grid, 10000, 1UL, 1200, SpikeTripCrowd.StaggerTicks + 100, cachePairPaths: true));

            string table = SpikeScenarioResult.FormatTable(results);
            TestContext.Out.WriteLine(table);
            System.IO.File.WriteAllText(System.IO.Path.Combine(TestContext.CurrentContext.WorkDirectory, "perf-scenarios.txt"), table);
            Assert.That(results.Count, Is.EqualTo(7));
        }
    }
}
