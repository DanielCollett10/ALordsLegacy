using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Homes;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Navigation
{
    /// <summary>T-587: built house fences block the nav cells their FenceLine crosses (decision 0053, HL-12, HL-12c).</summary>
    public class FenceNavTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld Make()
        {
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(80, 50) }, new[] { M(20, 50), M(50, 50), M(80, 50) });
            Support.HouseStock.Add(w, M(60, 40), 100);
            return w;
        }

        // Plot 20 x 10 m with its frontage on the road edge (z = 51.25); the other three sides are fenced.
        private static int BuiltHouse(SimWorld w, double x)
        {
            int road = w.Roads.Segments.First().Id;
            var shape = new AreaShape(
                new[] { M(x, 51.25), M(x + 20, 51.25), M(x + 20, 61.25), M(x, 61.25) },
                new[] { AreaEdge.FollowsRoad(road, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            CommandResult r = w.Execute(new DesignatePlot(shape));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int house = (int)r.Created[1];
            w.Construction.Remove(house);
            return house;
        }

        private static int BuiltFence(SimWorld w, int house)
        {
            CommandResult r = w.Execute(new BuildFence(house));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int fence = (int)r.Created[0];
            w.Construction.Remove(fence);
            w.Step();
            return fence;
        }

        private static IEnumerable<PointMm> Samples(SimWorld w, int house)
        {
            FenceLine line = FenceLine.Of(w.Areas.Get(w.Homes.Get(house).PlotAreaId));
            foreach (var run in line.Runs)
                for (int k = 0; k + 1 < run.Count; k++)
                {
                    double len = run[k].DistanceTo(run[k + 1]);
                    int n = (int)(len / 250) + 1;
                    for (int i = 0; i <= n; i++)
                        yield return new PointMm(run[k].X + (run[k + 1].X - run[k].X) * i / n, run[k].Z + (run[k + 1].Z - run[k].Z) * i / n);
                }
        }

        private static bool Blocked(SimWorld w, PointMm p) => w.Nav.IsBlocked(w.Nav.CellX(p.XMetres), w.Nav.CellZ(p.ZMetres));

        private static int BlockedCount(SimWorld w)
        {
            int n = 0;
            for (int z = 0; z < w.Nav.Depth; z++) for (int x = 0; x < w.Nav.Width; x++) if (w.Nav.IsBlocked(x, z)) n++;
            return n;
        }

        private static bool Cross(double ax, double az, double bx, double bz, double cx, double cz, double dx, double dz)
        {
            double d1 = (bx - ax) * (cz - az) - (bz - az) * (cx - ax), d2 = (bx - ax) * (dz - az) - (bz - az) * (dx - ax);
            double d3 = (dx - cx) * (az - cz) - (dz - cz) * (ax - cx), d4 = (dx - cx) * (bz - cz) - (dz - cz) * (bx - cx);
            return d1 * d2 < 0 && d3 * d4 < 0;
        }

        private static bool PathCrossesFence(SimWorld w, int house, PathResult path)
        {
            FenceLine line = FenceLine.Of(w.Areas.Get(w.Homes.Get(house).PlotAreaId));
            for (int i = 0; i + 1 < path.Points.Count; i++)
                foreach (var run in line.Runs)
                    for (int k = 0; k + 1 < run.Count; k++)
                        if (Cross(path.Points[i].X, path.Points[i].Z, path.Points[i + 1].X, path.Points[i + 1].Z,
                                run[k].XMetres, run[k].ZMetres, run[k + 1].XMetres, run[k + 1].ZMetres)) return true;
            return false;
        }

        [Test]
        public void FenceUnderConstruction_BlocksNothing()
        {
            SimWorld w = Make();
            int house = BuiltHouse(w, 30);
            w.Step();
            int before = BlockedCount(w);
            CommandResult r = w.Execute(new BuildFence(house));
            Assert.IsTrue(r.IsAccepted, r.Text);
            w.Step();
            Assert.AreEqual(before, BlockedCount(w));
        }

        [Test]
        public void BuiltFence_BlocksEveryCellOfItsLine_AndRemovalFreesThem()
        {
            SimWorld w = Make();
            int house = BuiltHouse(w, 30);
            w.Step();
            int before = BlockedCount(w);
            int version = w.Nav.Version;
            BuiltFence(w, house);
            Assert.Greater(w.Nav.Version, version);
            foreach (PointMm p in Samples(w, house)) Assert.IsTrue(Blocked(w, p), "gap at " + p.XMetres + "," + p.ZMetres);
            Assert.Greater(BlockedCount(w), before);
            // The open frontage stays walkable midway along the road edge.
            Assert.IsFalse(Blocked(w, M(40, 51.5)), "frontage mouth");

            HouseFence.Remove(w, w.Homes.Get(house));
            w.Step();
            Assert.AreEqual(before, BlockedCount(w));
        }

        [Test]
        public void DiagonalFenceLine_HasNoGapsBetweenCells()
        {
            SimWorld w = Make();
            int road = w.Roads.Segments.First().Id;
            // A diamond-ish plot whose sides run diagonally; only the first edge is frontage.
            var shape = new AreaShape(
                new[] { M(40, 51.25), M(50, 51.25), M(62, 63), M(50, 75), M(38, 63) },
                new[] { AreaEdge.FollowsRoad(road, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            CommandResult r = w.Execute(new DesignatePlot(shape));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int house = (int)r.Created[1];
            w.Construction.Remove(house);
            BuiltFence(w, house);
            foreach (PointMm p in Samples(w, house)) Assert.IsTrue(Blocked(w, p), "gap at " + p.XMetres + "," + p.ZMetres);
            // Diagonal cell steps are 4-connected: a path may not slip between two diagonal blocked cells.
            PathResult path = GridPathfinder.Find(w.Nav, new PathPoint(50, 63), new PathPoint(50, 90));
            if (path.Ok) Assert.IsFalse(PathCrossesFence(w, house, path));
        }

        [Test]
        public void CitizenAtBackOfFencedPlot_PathsOutThroughFrontage_NeverAcrossFence()
        {
            SimWorld w = Make();
            int house = BuiltHouse(w, 30);
            BuiltFence(w, house);
            // Back-left corner of the plot, inside the fence; the goal is just outside the west fence (8 m straight).
            PathResult path = GridPathfinder.Find(w.Nav, new PathPoint(33, 59), new PathPoint(25, 59));
            Assert.IsTrue(path.Ok);
            Assert.IsFalse(PathCrossesFence(w, house, path), "path crosses the fence");
            Assert.Greater(path.Points.Count, 2, "goes round via the frontage");
            // The detour out through the frontage must be far longer than the 8 m straight line.
            double len = 0;
            for (int i = 0; i + 1 < path.Points.Count; i++)
                len += System.Math.Sqrt(System.Math.Pow(path.Points[i + 1].X - path.Points[i].X, 2) + System.Math.Pow(path.Points[i + 1].Z - path.Points[i].Z, 2));
            Assert.Greater(len, 20.0, "len " + len + " pts " + string.Join(";", path.Points.Select(q => q.X + "," + q.Z)));
        }

        [Test]
        public void TwoFencedPlotsSideBySide_ShareOneStrip_AndBothHousesReachableFromRoad()
        {
            SimWorld w = Make();
            int a = BuiltHouse(w, 30), b = BuiltHouse(w, 50);
            int fa = BuiltFence(w, a), fb = BuiltFence(w, b);
            Assert.AreNotEqual(fa, fb);
            // The shared side is one strip of cells: each cell on plot A's east side is blocked, and so is its neighbour set.
            int shared = w.Nav.CellX(50.0);
            for (double z = 53; z < 61; z += 0.5) Assert.IsTrue(w.Nav.IsBlocked(shared, w.Nav.CellZ(z)));
            int strip = 0;
            for (int x = shared - 3; x <= shared + 3; x++) if (w.Nav.IsBlocked(x, w.Nav.CellZ(57))) strip++;
            Assert.LessOrEqual(strip, 2, "shared side is a single strip, not two");

            foreach (int house in new[] { a, b })
            {
                double x = w.Homes.Get(house) != null ? w.Footprints.Get(house).Centre.XMetres : 0;
                PathResult path = GridPathfinder.Find(w.Nav, new PathPoint(x, 48), new PathPoint(x, 54));
                Assert.IsTrue(path.Ok, "house " + house + " unreachable");
                Assert.IsFalse(PathCrossesFence(w, a, path));
                Assert.IsFalse(PathCrossesFence(w, b, path));
            }
        }

        [Test]
        public void CitizenOnNewlyBlockedFenceCell_IsMovedToWalkablePoint()
        {
            SimWorld w = Make();
            int house = BuiltHouse(w, 30);
            w.Step();
            w.People.AddFamily(1, "F", 30.1, 58);
            Citizen c = w.People.AddCitizen(1, "C", Sex.Male, -4_000_000, 1, 30.1, 58);
            BuiltFence(w, house);
            w.Step(2);
            Assert.IsFalse(w.Nav.IsBlocked(w.Nav.CellX(c.X), w.Nav.CellZ(c.Z)));
        }

        [Test]
        public void SaveLoad_WithBuiltFence_GivesSameCellsAndPaths()
        {
            MapData map = SyntheticMap.CreateRamp();
            SimWorld w = Make();
            int house = BuiltHouse(w, 30);
            BuiltFence(w, house);
            byte[] bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(bytes, map);
            back.Step();
            for (int z = 0; z < w.Nav.Depth; z++)
                for (int x = 0; x < w.Nav.Width; x++)
                    Assert.AreEqual(w.Nav.IsBlocked(x, z), back.Nav.IsBlocked(x, z), x + "," + z);
            PathResult p1 = GridPathfinder.Find(w.Nav, new PathPoint(32, 60), new PathPoint(32, 45));
            PathResult p2 = GridPathfinder.Find(back.Nav, new PathPoint(32, 60), new PathPoint(32, 45));
            Assert.AreEqual(p1.Points.Count, p2.Points.Count);
            Assert.AreEqual(p1.TravelMinutes, p2.TravelMinutes, 1e-9);
        }
    }
}
