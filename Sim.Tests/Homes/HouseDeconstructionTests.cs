using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Homes;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Homes
{
    /// <summary>House deconstruction and plot removal (homes.md HM-14, HM-14b; construction.md CN-58 does not apply).</summary>
    public class HouseDeconstructionTests
    {
        private const long Adult = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        // A camp at (100, 100) m (its fire is the camp fire), one road, a stock of logs, and no families.
        private static SimWorld Make(MapData map, int logs = 40)
        {
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
            StartLayoutApplier.Apply(w, new StartLayout(PointMm.FromMetres(100, 100), 0, P(0, 0), P(6, 0), P(-6, 0),
                new PointMm[0], new StartFamily[0], 0));
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(80, 50) }, new[] { M(20, 50), M(50, 50), M(80, 50) });
            if (logs > 0) w.Inventory.Produce(w.Inventory.AddStockpile(M(60, 40)), Items.Log, logs);
            return w;
        }

        private static PointMm P(long x, long z) => new PointMm(x * 1000, z * 1000);
        private static SimWorld Make(int logs = 40) => Make(SyntheticMap.CreateRamp(), logs);

        private static AreaShape Plot(SimWorld w, double x)
        {
            int road = w.Roads.Segments.First().Id;
            return new AreaShape(
                new[] { M(x, 51.25), M(x + 20, 51.25), M(x + 20, 61.25), M(x, 61.25) },
                new[] { AreaEdge.FollowsRoad(road, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
        }

        // Designates a plot at x and returns (plot area id, house footprint id).
        private static (int plot, int house) Place(SimWorld w, double x = 30)
        {
            CommandResult r = w.Execute(new DesignatePlot(Plot(w, x)));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return ((int)r.Created[0], (int)r.Created[1]);
        }

        // Adult family with one member, homeless, at the camp.
        private static Family AddFamily(SimWorld w, int id)
        {
            Family f = w.People.AddFamily(id, "F" + id, 100, 100);
            w.People.AddCitizen(id * 10, "C" + id, Sex.Male, Adult, id, 100, 100);
            return f;
        }

        // Places a plot, builds its house (CN-23 shortcut used by the other Homes tests) and lets the first family move in.
        private static (SimWorld w, int plot, int house, Family ash) BuiltWithFamily(MapData map)
        {
            SimWorld w = Make(map);
            Family ash = AddFamily(w, 1);
            AddFamily(w, 2);
            (int plot, int house) = Place(w);
            w.Construction.Remove(house);
            w.Step(1);
            Assert.AreEqual(1, w.Homes.Get(house).FamilyId, "setup: family 1 moved in");
            return (w, plot, house, ash);
        }

        private static int Unowned(SimWorld w) => w.Inventory.Locations
            .Where(l => l.Kind == LocationKind.GroundPile && l.OwnerOperationId == null).Sum(l => l.CountOf(Items.Log.Id));

        [Test]
        public void MarkBuiltHouse_MovesFamilyBackToCampTent_AndKeepsHouseAndPlot()
        {
            (SimWorld w, int plot, int house, Family ash) = BuiltWithFamily(SyntheticMap.CreateRamp());
            CommandResult r = w.Execute(new MarkForDeconstruction(house));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Assert.IsTrue(ash.IsHomeless);
            Assert.IsNull(ash.HomeHouseId);
            Assert.AreEqual(w.Camp.Fire.XMetres, ash.HomeX, 1e-9, "the camp fire stands in for the tent");
            Assert.AreEqual(w.Camp.Fire.ZMetres, ash.HomeZ, 1e-9);
            Assert.AreEqual(0, w.Homes.Get(house).FamilyId);
            Assert.IsNotNull(w.Deconstruction.Get(house));
            Assert.IsNotNull(w.Footprints.Get(house));
            Assert.IsNotNull(w.Areas.Get(plot));
        }

        [Test]
        public void MarkBuiltHouse_FamilyStaysHomeless_WhileDeconstructionRuns()
        {
            (SimWorld w, _, int house, Family ash) = BuiltWithFamily(SyntheticMap.CreateRamp());
            w.Execute(new MarkForDeconstruction(house));
            w.Step(50);
            Assert.IsTrue(ash.IsHomeless, "a house being taken down takes nobody in");
            Assert.AreEqual(0, w.Homes.Get(house).FamilyId);
        }

        [Test]
        public void DeconstructHouse_WhenFinished_RemovesFootprintHomeAndPlot_AndLeavesHalfTheLogs()
        {
            (SimWorld w, int plot, int house, _) = BuiltWithFamily(SyntheticMap.CreateRamp());
            Assert.IsTrue(w.Execute(new MarkForDeconstruction(house)).IsAccepted);
            for (int i = 0; i < 5000 && w.Footprints.Get(house) != null; i++) w.Step(1);
            Assert.IsNull(w.Footprints.Get(house), "the deconstruction tasks took the house down");
            Assert.IsNull(w.Deconstruction.Get(house));
            Assert.IsNull(w.Homes.Get(house), "HM-14b: the home goes with the house");
            Assert.IsNull(w.Areas.Get(plot), "HM-14b: the plot goes with the house");
            Assert.AreEqual(5, Unowned(w), "CN-54: half of the 10 house logs, rounded down");
        }

        [Test]
        public void CancelUnbuiltHouseSite_RemovesPlotAndHome_AndReleasesItsLogs()
        {
            SimWorld w = Make(10);
            (int plot, int house) = Place(w);
            CommandResult r = w.Execute(new MarkForDeconstruction(house));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Assert.Contains((long)plot, r.Removed.ToList(), "the plot is reported removed");
            Assert.IsNull(w.Footprints.Get(house));
            Assert.IsNull(w.Construction.Get(house));
            Assert.IsNull(w.Homes.Get(house));
            Assert.IsNull(w.Areas.Get(plot));
            Assert.IsTrue(w.Execute(new DesignatePlot(Plot(w, 30))).IsAccepted, "the 10 reserved logs were released");
        }

        [Test]
        public void CancelDeconstructionAfterEviction_HouseTakesAHomelessFamilyAgain()
        {
            (SimWorld w, _, int house, Family ash) = BuiltWithFamily(SyntheticMap.CreateRamp());
            w.Execute(new MarkForDeconstruction(house));
            Assert.IsTrue(w.Execute(new CancelDeconstruction(house)).IsAccepted);
            w.Step(1);
            Assert.AreEqual(1, w.Homes.Get(house).FamilyId, "the house is working again and takes the lowest homeless family");
            Assert.AreEqual(house, ash.HomeHouseId);
        }

        [Test]
        public void MarkHouse_IsNotRefusedBy_CN58OperationRule_ButPlotDeleteIsRefusedUntilHouseGoes()
        {
            (SimWorld w, int plot, int house, _) = BuiltWithFamily(SyntheticMap.CreateRamp());
            // A woodcutting operation nearby: CN-58 would refuse deleting an operation area with modules, not a house.
            var opShape = AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) });
            Assert.IsTrue(w.Execute(new DesignateArea(opShape, AreaTypes.WoodcuttingId)).IsAccepted);
            CommandResult mark = w.Execute(new MarkForDeconstruction(house));
            Assert.IsTrue(mark.IsAccepted, "marking a house is never refused by the operation rule: " + mark.Text);
            CommandResult del = w.Execute(new DeleteArea(plot));
            Assert.IsTrue(del.IsRejected);
            Assert.AreEqual("Deconstruct the house first", del.Text, "HM-14b refusal, not the CN-58 text");
        }

        [Test]
        public void DeleteArea_PlotWithUnbuiltHouse_IsRefused_DeconstructTheHouseFirst()
        {
            SimWorld w = Make();
            (int plot, _) = Place(w);
            CommandResult r = w.Execute(new DeleteArea(plot));
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual("Deconstruct the house first", r.Text);
            Assert.IsNotNull(w.Areas.Get(plot));
        }

        [Test]
        public void DeleteArea_PlotWithBuiltHouse_IsRefused_DeconstructTheHouseFirst()
        {
            (SimWorld w, int plot, _, _) = BuiltWithFamily(SyntheticMap.CreateRamp());
            CommandResult r = w.Execute(new DeleteArea(plot));
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual("Deconstruct the house first", r.Text);
        }

        [Test]
        public void DeleteArea_PlotWhoseHouseIsBeingDeconstructed_IsStillRefused_UntilTheHouseIsGone()
        {
            (SimWorld w, int plot, int house, _) = BuiltWithFamily(SyntheticMap.CreateRamp());
            w.Execute(new MarkForDeconstruction(house));
            Assert.AreEqual("Deconstruct the house first", w.Execute(new DeleteArea(plot)).Text);
            for (int i = 0; i < 5000 && w.Footprints.Get(house) != null; i++) w.Step(1);
            Assert.IsTrue(w.Execute(new DeleteArea(plot)).IsRejected, "the plot is already gone, so the id is unknown");
            Assert.IsNull(w.Areas.Get(plot));
        }

        [Test]
        public void SaveRoundTrip_MidDeconstruction_IsIdenticalAndContinuesIdentically()
        {
            MapData map = SyntheticMap.CreateRamp();
            (SimWorld w, _, int house, _) = BuiltWithFamily(map);
            w.Execute(new MarkForDeconstruction(house));
            w.Step(100);
            Assert.IsNotNull(w.Deconstruction.Get(house), "setup: still mid-deconstruction after 100 ticks");

            byte[] first = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(first, map);
            Assert.IsNotNull(back.Deconstruction.Get(house));
            Assert.AreEqual(0, back.Homes.Get(house).FamilyId, "the marked house is Empty after load");
            Assert.IsTrue(back.People.FindFamily(1).IsHomeless);
            Assert.AreEqual(w.Deconstruction.Get(house).ProgressMinutes, back.Deconstruction.Get(house).ProgressMinutes);
            byte[] second = SimSave.Write(back, "s", 1, map.MapId, map.ContentHash);
            Assert.IsNull(SaveDiff.FirstDifference(first, second));

            back.Step(600);
            w.Step(600);
            Assert.IsNull(w.Footprints.Get(house), "the house is down in the original");
            Assert.IsNull(back.Footprints.Get(house), "and in the loaded copy");
            Assert.AreEqual(SimSave.Write(w, "s", 1, map.MapId, map.ContentHash),
                SimSave.Write(back, "s", 1, map.MapId, map.ContentHash));
        }
    }
}
