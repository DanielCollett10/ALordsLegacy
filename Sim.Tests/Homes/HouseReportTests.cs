using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Homes;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Homes
{
    /// <summary>T-591: the house report (fence state and progress, level, needs, Upgrade availability) (HL-22, HL-26).</summary>
    public class HouseReportTests
    {
        private const int House = 100;
        private const int Fence = 101;
        private const long Adult = -4_000_000;
        private const int MaxTicks = 60000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld Flat()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            return SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
        }

        // A housed family with a built fence shortcut (no site) unless fenceBuilt is false.
        private static Family Housed(SimWorld w, int foodTypes, bool fenceBuilt)
        {
            Family f = w.People.AddFamily(1, "Ash", 0, 0);
            HomeRecord home = w.Homes.Add(House, 0);
            Assert.IsTrue(w.Homes.MoveIn(home, f));
            f.AllotmentFoodTypes = foodTypes;
            if (fenceBuilt)
            {
                w.Footprints.Add(Fence, ModuleTypes.Fence, PointMm.FromMetres(5, 5), 0);
                w.Homes.SetFence(home, Fence);
            }
            return f;
        }

        [Test]
        public void Of_UnknownFamily_IsNull() => Assert.IsNull(HouseReport.Of(Flat(), 99));

        [Test]
        public void Of_HomelessLevelOne_NoHouseNoFence_UpgradeBlockedByNoHouse()
        {
            SimWorld w = Flat();
            w.People.AddFamily(1, "Ash", 0, 0);
            HouseReport r = HouseReport.Of(w, 1);
            Assert.IsFalse(r.HasHouse);
            Assert.AreEqual(0, r.HouseFootprintId);
            Assert.AreEqual(FenceState.None, r.Fence);
            Assert.AreEqual("no fence", r.FenceText);
            Assert.AreEqual(1, r.Level);
            Assert.AreEqual("Household", r.LevelName);
            Assert.IsFalse(r.UpgradeAvailable);
            Assert.AreEqual("the family has no house", r.UpgradeBlocker);
        }

        [Test]
        public void Of_HousedNoFence_ReportsFenceNoneAndUnmetFencedPlotNeed()
        {
            SimWorld w = Flat();
            Housed(w, 2, fenceBuilt: false);
            HouseReport r = HouseReport.Of(w, 1);
            Assert.IsTrue(r.HasHouse);
            Assert.AreEqual(House, r.HouseFootprintId);
            Assert.AreEqual(FenceState.None, r.Fence);
            Assert.AreEqual(0.0, r.FenceProgress);
            Assert.AreEqual("no fence", r.FenceText);
            NeedStatus fenced = r.Needs.Needs.Single(n => n.Kind == NeedKind.FencedPlot);
            Assert.IsFalse(fenced.Met);
            Assert.AreEqual("no fence", fenced.Reason);
            Assert.AreEqual(4, r.Needs.Needs.Count, "level 2 has four needs (HL-21)");
            Assert.IsFalse(r.UpgradeAvailable, "the fence is missing");
            Assert.IsNull(r.UpgradeBlocker, "needs, not a blocker, stop the upgrade");
        }

        [Test]
        public void Of_HousedFenceBuiltDietAndWater_UpgradeAvailable_NoBlocker()
        {
            // Acceptance: every need of level 2 met (diverse food 2 types, fence built, the two always-met lines).
            SimWorld w = Flat();
            Housed(w, 2, fenceBuilt: true);
            HouseReport r = HouseReport.Of(w, 1);
            Assert.AreEqual(FenceState.Built, r.Fence);
            Assert.AreEqual(1.0, r.FenceProgress, "a built fence is 100%");
            Assert.AreEqual("fence built", r.FenceText);
            Assert.IsTrue(r.Needs.Needs.All(n => n.Met), "all four needs met");
            Assert.IsTrue(r.UpgradeAvailable);
            Assert.IsNull(r.UpgradeBlocker);
            Assert.AreEqual(2, r.Needs.NextLevel);
        }

        [Test]
        public void Of_HungryFamily_NeedUnmet_UpgradeNotAvailable()
        {
            SimWorld w = Flat();
            Family f = Housed(w, 2, fenceBuilt: true);
            f.IsHungry = true;
            HouseReport r = HouseReport.Of(w, 1);
            Assert.IsFalse(r.UpgradeAvailable);
            Assert.AreEqual("hungry", r.Needs.Needs.Single(n => n.Kind == NeedKind.DiverseFood).Reason);
        }

        [Test]
        public void Of_TopLevelFamily_NoUpgrade_BlockerIsHighestLevel()
        {
            SimWorld w = Flat();
            Family f = Housed(w, 2, fenceBuilt: true);
            f.RaiseLevel(2);
            HouseReport r = HouseReport.Of(w, 1);
            Assert.AreEqual(2, r.Level);
            Assert.AreEqual("Settled household", r.LevelName);
            Assert.IsFalse(r.UpgradeAvailable);
            Assert.AreEqual("already at the highest level", r.UpgradeBlocker);
        }

        // Pipeline test, no shortcut: DesignatePlot -> house built by odd-jobbers -> move in -> BuildFence command ->
        // Sim ticks until the fence is built. The report follows each step: Building with progress, then Built.
        [Test]
        public void Pipeline_DesignatedBuiltMovedIn_FenceCommandAndTicks_ReportFollowsFenceToBuilt()
        {
            SimWorld w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(80, 50) }, new[] { M(20, 50), M(50, 50), M(80, 50) });
            Support.HouseStock.Add(w, M(60, 40), 70);
            w.People.AddOx(50, "Ox", 40, 45);
            w.People.AddFamily(1, "F1", 10, 10);
            w.People.AddCitizen(10, "C1", Sex.Male, Adult, 1, 10, 10);
            int road = w.Roads.Segments.First().Id;
            var shape = new AreaShape(
                new[] { M(30, 51.25), M(50, 51.25), M(50, 61.25), M(30, 61.25) },
                new[] { AreaEdge.FollowsRoad(road, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            CommandResult plot = w.Execute(new DesignatePlot(shape));
            Assert.IsTrue(plot.IsAccepted, plot.Text);
            int house = (int)plot.Created[1];
            for (int i = 0; i < MaxTicks && !w.IsBuilt(house); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(house), "the house was built");
            for (int i = 0; i < MaxTicks && w.People.FindFamily(1).IsHomeless; i++) w.Step(1);
            Assert.IsFalse(w.People.FindFamily(1).IsHomeless, "the family moved in");

            HouseReport before = HouseReport.Of(w, 1);
            Assert.AreEqual(FenceState.None, before.Fence);
            Assert.AreEqual("no fence", before.FenceText);

            CommandResult fence = w.Execute(new BuildFence(house));
            Assert.IsTrue(fence.IsAccepted, fence.Text);
            int fenceId = (int)fence.Created[0];
            // Step until builders have started on the site (panels carried first), but before it is finished.
            ConstructionSite site = w.Construction.Get(fenceId);
            for (int i = 0; i < MaxTicks && site.ProgressMinutes == 0; i++) w.Step(1);
            Assert.Greater(site.ProgressMinutes, 0, "builders started the fence");
            Assert.IsNotNull(w.Construction.Get(fenceId), "the fence is not finished yet");
            HouseReport mid = HouseReport.Of(w, 1);
            Assert.AreEqual(FenceState.Building, mid.Fence);
            double expected = (double)site.ProgressMinutes / SiteRecipe.Of(w, w.Footprints.Get(fenceId)).BuildWorkerMinutes;
            Assert.AreEqual(expected, mid.FenceProgress, 1e-12, "progress is the site's build minutes over the recipe's");
            Assert.Greater(mid.FenceProgress, 0.0);
            Assert.Less(mid.FenceProgress, 1.0);
            Assert.IsTrue(mid.FenceText.StartsWith("fence ") && mid.FenceText.EndsWith("%"), mid.FenceText);
            Assert.IsFalse(mid.UpgradeAvailable, "needs not met yet");

            for (int i = 0; i < MaxTicks && !w.IsBuilt(fenceId); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(fenceId), "the fence was built");
            HouseReport done = HouseReport.Of(w, 1);
            Assert.AreEqual(FenceState.Built, done.Fence);
            Assert.AreEqual(1.0, done.FenceProgress);
            Assert.AreEqual("fence built", done.FenceText);
            Assert.IsTrue(done.Needs.Needs.Single(n => n.Kind == NeedKind.FencedPlot).Met, "the fenced-plot need is met");
            Assert.IsFalse(done.UpgradeAvailable, "diverse food is still missing: the report says why");
            Assert.IsNull(done.UpgradeBlocker);
        }
    }
}
