using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Homes;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Scenarios
{
    /// <summary>
    /// T-592, M21 end to end: from the start layout a daub pit is built and staffed, a house is built from its daub, the
    /// house is fenced, the household is fed two food types and becomes eligible, and UpgradeHousehold makes it level 2.
    /// After the fence is built a family member leaving for work never crosses the fence line. Audited every tick; a save
    /// taken while the fence is still being built is byte-identical on a round trip. Everything runs through real commands
    /// and Sim ticks (no Build or SetState shortcut). At the end the level 2 household is saved with its built fence and
    /// loaded: the bytes match and HouseReport and HouseholdNeedsReport answer the same (T-626).
    /// </summary>
    [TestFixture]
    public class DaubFenceLevel2ScenarioTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static MapData Map() =>
            new MapData("flat", new HeightGrid(-50f, -50f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);

        private static StartCitizen Adult(string n, Sex s) => new StartCitizen(n, s, AdultBirth);

        private static SimWorld Start(MapData map)
        {
            var w = SimWorld.FromMapData(map, new SimTuning
            {
                WorldSeed = 7, StartMonth = 5, StartDay = 1, StartHour = 6, StartMinute = 0,
                BushCount = 0, HazelStoolCount = 0, WillowStoolCount = 0, ReedBedCount = 0, WorkshopDefaultReserve = 0,
            });
            var fams = new List<StartFamily>();
            for (int i = 0; i < 4; i++)
                fams.Add(new StartFamily("F" + i, M(-9 + 3 * i, 8), new[] { Adult("A" + i, Sex.Male), Adult("B" + i, Sex.Female) }));
            StartLayoutApplier.Apply(w, new StartLayout(M(0, 0), 0, M(0, 0), M(6, 0), M(-6, 0), new PointMm[0], fams, 2,
                stock: new[] { new StartStock(Items.Log, 120), new StartStock(Items.Meat, 60), new StartStock(Items.Berries, 60) }));
            LordsLegacy.Sim.Tests.Start.NoCampTests.UseHouseGoodsUp(w); // no camp daub: the house must take the pit's
            w.Inventory.Produce(w.Camp.FoodStoreLocation, Items.Berries, 200); // the camp already holds meat: a second food type
            w.Inventory.Produce(w.Camp.GeneralStoreLocation, Items.Thatch, 20);
            w.Inventory.Produce(w.Camp.GeneralStoreLocation, Items.WattlePanel, 80);
            return w;
        }

        private static void Check(SimWorld w) => Assert.IsNull(w.Inventory.Audit(), "ledger audit at tick " + w.Tick);

        private static void StepAudited(SimWorld w, int ticks, System.Func<bool> stop = null)
        {
            for (int i = 0; i < ticks && (stop == null || !stop()); i++) { w.Step(1); Check(w); }
        }

        private static byte[] Bytes(SimWorld w, MapData map) => SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);

        // The plot corners; edge 0 (corner 0 to corner 1) is the open frontage on the road, the other three sides are fenced.
        private static readonly PointMm[] PlotCorners = { M(30, 51.25), M(50, 51.25), M(50, 61.25), M(30, 61.25) };
        private static double MinX => PlotCorners.Min(p => p.XMetres);
        private static double MaxX => PlotCorners.Max(p => p.XMetres);
        private static double MinZ => PlotCorners.Min(p => p.ZMetres);
        private static double MaxZ => PlotCorners.Max(p => p.ZMetres);
        private static double FrontageZ => PlotCorners[0].ZMetres;
        private static bool InPlot(double x, double z) => x > MinX && x < MaxX && z > MinZ && z < MaxZ;

        private const double FenceClearance = 0.05; // metres: a step ending on, touching or running along a fence run also fails

        private static double PointSeg(double px, double pz, double ax, double az, double bx, double bz)
        {
            double dx = bx - ax, dz = bz - az, l2 = dx * dx + dz * dz;
            double t = l2 < 1e-12 ? 0 : System.Math.Max(0, System.Math.Min(1, ((px - ax) * dx + (pz - az) * dz) / l2));
            double qx = ax + t * dx - px, qz = az + t * dz - pz;
            return System.Math.Sqrt(qx * qx + qz * qz);
        }

        // True when the two segments cross, touch, or come within FenceClearance of each other.
        private static bool Cross(double ax, double az, double bx, double bz, double cx, double cz, double dx, double dz)
        {
            double d1 = (bx - ax) * (cz - az) - (bz - az) * (cx - ax), d2 = (bx - ax) * (dz - az) - (bz - az) * (dx - ax);
            double d3 = (dx - cx) * (az - cz) - (dz - cz) * (ax - cx), d4 = (dx - cx) * (bz - cz) - (dz - cz) * (bx - cx);
            if (d1 * d2 < 0 && d3 * d4 < 0) return true;
            return PointSeg(ax, az, cx, cz, dx, dz) < FenceClearance || PointSeg(bx, bz, cx, cz, dx, dz) < FenceClearance
                || PointSeg(cx, cz, ax, az, bx, bz) < FenceClearance || PointSeg(dx, dz, ax, az, bx, bz) < FenceClearance;
        }

        private static bool CrossesFence(FenceLine line, double ax, double az, double bx, double bz)
        {
            foreach (var run in line.Runs)
                for (int k = 0; k + 1 < run.Count; k++)
                    if (Cross(ax, az, bx, bz, run[k].XMetres, run[k].ZMetres, run[k + 1].XMetres, run[k + 1].ZMetres)) return true;
            return false;
        }

        [Test]
        public void DaubPit_House_Fence_TwoFoods_UpgradeToLevel2_AuditedEveryTick_SaveMidFenceByteIdentical()
        {
            MapData map = Map();
            SimWorld w = Start(map);

            // 1. A daub pit, built by command and staffed by family 1.
            CommandResult p = w.Execute(new PlaceStructure(ModuleTypes.DaubPitId, M(60, 20), 0));
            Assert.IsTrue(p.IsAccepted, p.Text);
            int pit = (int)p.Created[0];
            StepAudited(w, 6 * 1440, () => w.IsBuilt(pit));
            Assert.IsTrue(w.IsBuilt(pit), "pit built");
            Operation op = w.Operations.FindByModule(pit);
            Assert.IsTrue(w.Execute(new AssignFamily(op.Id, 1)).IsAccepted);
            LedgerLocation pitStore = w.Inventory.FindGoodsStore(pit);
            StepAudited(w, 4 * 1440, () => pitStore.CountOf(Items.Daub.Id) >= 10);
            Assert.GreaterOrEqual(pitStore.CountOf(Items.Daub.Id), 10, "the pit made daub");

            // 2. A house on a plot fronting the road, built from the pit's daub.
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(80, 50) }, new[] { M(20, 50), M(50, 50), M(80, 50) });
            int road = w.Roads.Segments.First().Id;
            var shape = new AreaShape(
                PlotCorners,
                new[] { AreaEdge.FollowsRoad(road, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            CommandResult hr = w.Execute(new DesignatePlot(shape));
            Assert.IsTrue(hr.IsAccepted, hr.Text);
            int house = (int)hr.Created[1];
            StepAudited(w, 8 * 1440, () => w.IsBuilt(house));
            Assert.IsTrue(w.IsBuilt(house), "house built");
            StepAudited(w, 2 * 1440, () => w.Homes.Get(house).FamilyId != 0);
            HomeRecord home = w.Homes.Get(house);
            Assert.AreNotEqual(0, home.FamilyId, "a family moved in");
            Family fam = w.People.Families.First(f => f.Id == home.FamilyId);
            Assert.AreEqual(1, fam.Level);

            // Not eligible yet: no fence.
            Assert.IsFalse(HouseholdNeedsReport.Of(w, fam.Id).Eligible);
            Assert.IsFalse(w.Execute(new UpgradeHousehold(fam.Id)).IsAccepted, "refused without fence");

            // 3. Fence it; save while it is still being built.
            CommandResult fr = w.Execute(new BuildFence(house));
            Assert.IsTrue(fr.IsAccepted, fr.Text);
            int fence = (int)fr.Created[0];
            StepAudited(w, 200);
            Assert.IsFalse(w.IsBuilt(fence), "fence still going up");
            byte[] first = Bytes(w, map);
            SimWorld back = SimSave.Read(first, map);
            CollectionAssert.AreEqual(first, Bytes(back, map), "save mid-fence is byte-identical");
            Assert.AreEqual(FenceState.Building, HouseReport.Of(back, fam.Id).Fence, "fence still under construction after load");
            Assert.AreEqual(Report(w, fam.Id), Report(back, fam.Id), "reports agree after loading a mid-fence save");
            for (int i = 0; i < 300; i++) { w.Step(1); back.Step(1); Check(w); Check(back); }
            CollectionAssert.AreEqual(Bytes(w, map), Bytes(back, map), "original and loaded stay identical");

            StepAudited(w, 8 * 1440, () => w.IsBuilt(fence));
            Assert.IsTrue(w.IsBuilt(fence), "fence built");
            Assert.AreEqual(FenceState.Built, HouseFence.StateOf(w, w.Homes.Get(house)));

            // 4. A family member leaving for work (the pit) from inside the plot never crosses the fence line.
            FenceLine line = FenceLine.Of(w.Areas.Get(w.Homes.Get(house).PlotAreaId));
            if (fam.Id != 1) Assert.IsTrue(w.Execute(new AssignFamily(op.Id, fam.Id)).IsAccepted);
            var members = w.People.Citizens.Where(c => c.FamilyId == fam.Id).ToList();
            var last = members.ToDictionary(c => c.Id, c => (c.X, c.Z));
            int moved = 0, exits = 0;
            Assert.IsTrue(members.Any(c => InPlot(c.X, c.Z)), "a family member starts inside the plot");
            for (int i = 0; i < 3 * 1440; i++)
            {
                w.Step(1); Check(w);
                foreach (Citizen c in members)
                {
                    var (px, pz) = last[c.Id];
                    if (px != c.X || pz != c.Z)
                    {
                        moved++;
                        Assert.IsFalse(CrossesFence(line, px, pz, c.X, c.Z), "member " + c.Id + " crossed or touched the fence at tick " + w.Tick);
                        if (InPlot(px, pz) && !InPlot(c.X, c.Z))
                        {
                            // The step leaves the plot: it must cross the open frontage edge (corner 0 to corner 1, between the fenced sides).
                            double t = (FrontageZ - pz) / (c.Z - pz);
                            double xAt = px + t * (c.X - px);
                            Assert.IsTrue(c.Z <= FrontageZ && t >= 0 && t <= 1 && xAt > MinX && xAt < MaxX,
                                "member " + c.Id + " left the plot at x=" + xAt + " tick " + w.Tick + ", not through the frontage");
                            exits++;
                        }
                    }
                    last[c.Id] = (c.X, c.Z);
                }
            }
            Assert.Greater(moved, 20, "the family actually walked");
            Assert.Greater(exits, 0, "a member left the plot through the frontage");

            // 5. Two food types by the Sunday allotment, then the upgrade.
            for (int i = 0; i < 8 * 1440 && !(w.Now.IsSunday && fam.AllotmentFoodTypes >= 2); i++) { w.Step(1); Check(w); }
            Assert.GreaterOrEqual(fam.AllotmentFoodTypes, w.Tuning.DiverseFoodTypes, "two food types allotted");
            // The Sunday food trip brings the ration home; the household stops being hungry once it is eaten from the larder.
            StepAudited(w, 2 * 1440, () => !fam.IsHungry);
            HouseholdNeedsReport rep = HouseholdNeedsReport.Of(w, fam.Id);
            Assert.IsTrue(rep.Eligible, string.Join("; ", rep.Needs.Where(n => !n.Met).Select(n => n.Reason)));
            CommandResult up = w.Execute(new UpgradeHousehold(fam.Id));
            Assert.IsTrue(up.IsAccepted, up.Text);
            Assert.AreEqual(2, fam.Level);
            Check(w);

            // 6. The level 2 household with a built fence and its recorded food types saves and loads byte-exactly; the
            // HouseReport and HouseholdNeedsReport answers are the same after load.
            Assert.GreaterOrEqual(fam.AllotmentFoodTypes, 2, "food types recorded before the save");
            string beforeReports = Report(w, fam.Id);
            byte[] done = Bytes(w, map);
            SimWorld loaded = SimSave.Read(done, map);
            Check(loaded);
            CollectionAssert.AreEqual(done, Bytes(loaded, map), "level 2 save with a built fence is byte-identical");
            Family loadedFam = loaded.People.FindFamily(fam.Id);
            Assert.AreEqual(2, loadedFam.Level, "level survives the load");
            Assert.AreEqual(fam.AllotmentFoodTypes, loadedFam.AllotmentFoodTypes, "allotment food types survive the load");
            Assert.AreEqual(home.FenceFootprintId, loaded.Homes.Get(house).FenceFootprintId, "fence link survives the load");
            Assert.AreEqual(FenceState.Built, HouseFence.StateOf(loaded, loaded.Homes.Get(house)));
            Assert.AreEqual(beforeReports, Report(loaded, fam.Id), "HouseReport and HouseholdNeedsReport agree after load");
        }

        // One line per field of HouseReport and HouseholdNeedsReport, so a load that changes any answer fails the test.
        private static string Report(SimWorld w, int familyId)
        {
            HouseReport h = HouseReport.Of(w, familyId);
            HouseholdNeedsReport n = HouseholdNeedsReport.Of(w, familyId);
            var parts = new List<string>
            {
                h.FamilyId.ToString(), h.HouseFootprintId.ToString(), h.Level.ToString(), h.LevelName, h.Fence.ToString(),
                h.FenceProgress.ToString("R"), h.FenceText, h.UpgradeAvailable.ToString(), h.UpgradeBlocker ?? "",
                n.FamilyId.ToString(), n.Level.ToString(), n.NextLevel.ToString(), n.Eligible.ToString(), n.Blocker ?? "",
            };
            parts.AddRange(n.Needs.Select(x => x.Kind + "|" + x.Label + "|" + x.Met + "|" + x.Reason));
            return string.Join("\n", parts);
        }
    }
}
