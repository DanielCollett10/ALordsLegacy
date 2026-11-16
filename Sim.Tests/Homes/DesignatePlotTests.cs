using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Homes;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Homes
{
    /// <summary>DesignatePlot, HomeRegistry, move-in (HM-05, HM-10..13) and the Homes save section (T-405).</summary>
    public class DesignatePlotTests
    {
        private const long Adult = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld Make(int logs = 20)
        {
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(80, 50) }, new[] { M(20, 50), M(50, 50), M(80, 50) });
            if (logs > 0) w.Inventory.Produce(w.Inventory.AddStockpile(M(60, 40)), Items.Log, logs);
            return w;
        }

        private static AreaShape Plot(SimWorld w, double x, double width = 20, double depth = 10)
        {
            int road = w.Roads.Segments.First().Id;
            return new AreaShape(
                new[] { M(x, 51.25), M(x + width, 51.25), M(x + width, 51.25 + depth), M(x, 51.25 + depth) },
                new[] { AreaEdge.FollowsRoad(road, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
        }

        private static Family AddFamily(SimWorld w, int id, int? workplace = null, int members = 1)
        {
            Family f = w.People.AddFamily(id, "F" + id, 10, 10);
            for (int i = 0; i < members; i++) w.People.AddCitizen(id * 10 + i, "C" + id + i, Sex.Male, Adult, id, 10, 10);
            f.WorkplaceId = workplace;
            return f;
        }

        // Makes a woodcutting operation with its area centre at (x, z); returns the operation id.
        private static int Operation(SimWorld w, double x, double z)
        {
            var shape = AreaShape.Straight(new[] { M(x - 15, z - 10), M(x + 15, z - 10), M(x + 15, z + 10), M(x - 15, z + 10) });
            CommandResult r = w.Execute(new DesignateArea(shape, AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return w.Operations.FindByArea((int)r.Created[0]).Id;
        }

        private static int Place(SimWorld w, double x)
        {
            CommandResult r = w.Execute(new DesignatePlot(Plot(w, x)));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return (int)r.Created[1];
        }

        private static void Build(SimWorld w, int house) => w.Construction.Remove(house);

        [Test]
        public void Designate_CreatesPlotHouseSiteAndEmptyHome_AndReservesTenLogs()
        {
            SimWorld w = Make();
            CommandResult r = w.Execute(new DesignatePlot(Plot(w, 30)));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Assert.AreEqual(2, r.Created.Length);
            Assert.AreEqual(AreaTypes.PlotId, w.Areas.Get((int)r.Created[0]).TypeId);
            int house = (int)r.Created[1];
            Assert.AreEqual(ModuleTypes.HouseId, w.Footprints.Get(house).Type.Id);
            Assert.IsNotNull(w.Construction.Get(house));
            Assert.AreEqual(10, w.Construction.Get(house).ReservationIds.Sum(id => w.Inventory.GetReservation(id).Count));
            HomeRecord h = w.Homes.Get(house);
            Assert.AreEqual((int)r.Created[0], h.PlotAreaId);
            Assert.AreEqual(0, h.FamilyId);
        }

        [Test]
        public void Designate_ClearsTreesUnderThePlot()
        {
            var map = new MapData("flat", SyntheticMap.CreateRamp().Heights, new[] { new MapTree(new LordsLegacy.Sim.Forestry.TreeId(1), 35f, 55f, 16) });
            var w = SimWorld.FromMapData(map, new SimTuning());
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(80, 50) }, new[] { M(20, 50), M(50, 50), M(80, 50) });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(60, 40)), Items.Log, 10);
            CommandResult r = w.Execute(new DesignatePlot(Plot(w, 30)));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Assert.AreEqual(1, r.RemovedTreeIds.Length);
        }

        [Test]
        public void Designate_Unaffordable_IsRefusedAndChangesNothing()
        {
            SimWorld w = Make(9);
            CommandResult r = w.Execute(new DesignatePlot(Plot(w, 30)));
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual("Not enough logs", r.Text);
            Assert.AreEqual(0, w.Areas.All.Count());
            Assert.AreEqual(0, w.Footprints.Count);
            Assert.AreEqual(0, w.Homes.Count);
            Assert.AreEqual(0, w.Construction.Count);
        }

        [Test]
        public void Designate_SecondPlotCannotUseLogsAlreadyReserved()
        {
            SimWorld w = Make(10);
            Place(w, 30);
            Assert.IsTrue(w.Execute(new DesignatePlot(Plot(w, 55))).IsRejected);
            Assert.AreEqual(1, w.Homes.Count);
        }

        [Test]
        public void Designate_NoFrontage_IsRefused()
        {
            SimWorld w = Make();
            var s = AreaShape.Straight(new[] { M(30, 60), M(50, 60), M(50, 70), M(30, 70) });
            CommandResult r = w.Execute(new DesignatePlot(s));
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual("A plot must face a road", r.Text);
            Assert.AreEqual(0, w.Homes.Count);
        }

        [Test]
        public void MoveIn_BuiltHouse_HomelessFamilyMovesIn_HomeChangesAndHomelessClears()
        {
            SimWorld w = Make();
            Family f = AddFamily(w, 1);
            int house = Place(w, 30);
            w.Step(3);
            Assert.IsTrue(f.IsHomeless, "an unbuilt house takes nobody");
            Build(w, house);
            w.Step(1);
            Assert.IsFalse(f.IsHomeless);
            Assert.AreEqual(house, f.HomeHouseId);
            Assert.AreEqual(1, w.Homes.Get(house).FamilyId);
            Assert.AreEqual(w.Homes.Get(house), w.Homes.FindByFamily(1));
            Footprint fp = w.Footprints.Get(house);
            Assert.AreEqual(fp.AccessPoint.XMetres, f.HomeX, 1e-9);
            Assert.AreEqual(fp.AccessPoint.ZMetres, f.HomeZ, 1e-9);
        }

        [Test]
        public void MoveIn_NoHomelessFamily_HouseStaysEmpty_ThenFillsWhenOneExists()
        {
            SimWorld w = Make();
            int house = Place(w, 30);
            Build(w, house);
            w.Step(5);
            Assert.AreEqual(0, w.Homes.Get(house).FamilyId);
            Family f = AddFamily(w, 4);
            w.Step(1);
            Assert.AreEqual(4, w.Homes.Get(house).FamilyId);
            Assert.IsFalse(f.IsHomeless);
        }

        [Test]
        public void MoveIn_OneFamilyPerHouse_SecondHouseTakesNextFamily()
        {
            SimWorld w = Make();
            AddFamily(w, 1);
            AddFamily(w, 2);
            int h1 = Place(w, 30), h2 = Place(w, 55);
            Build(w, h1);
            Build(w, h2);
            w.Step(1);
            Assert.AreEqual(1, w.Homes.Get(h1).FamilyId);
            Assert.AreEqual(2, w.Homes.Get(h2).FamilyId);
        }

        [Test]
        public void MoveIn_NoWorkplaces_LowestFamilyIdWins_AndAlreadyHousedFamilyIsSkipped()
        {
            SimWorld w = Make();
            AddFamily(w, 5);
            AddFamily(w, 2);
            AddFamily(w, 9);
            int h1 = Place(w, 30), h2 = Place(w, 55);
            Build(w, h1);
            w.Step(1);
            Assert.AreEqual(2, w.Homes.Get(h1).FamilyId);
            Build(w, h2);
            w.Step(1);
            Assert.AreEqual(5, w.Homes.Get(h2).FamilyId);
        }

        [Test]
        public void MoveIn_WorkplaceFamilyBeatsLowerIdFamilyWithoutOne()
        {
            SimWorld w = Make();
            int op = Operation(w, 90, 90);
            AddFamily(w, 1);
            AddFamily(w, 7, op);
            int house = Place(w, 30);
            Build(w, house);
            w.Step(1);
            Assert.AreEqual(7, w.Homes.Get(house).FamilyId);
        }

        [Test]
        public void MoveIn_NearestWorkplaceFirst_TiesByLowestFamilyId()
        {
            SimWorld w = Make();
            int near = Operation(w, 40, 75), far = Operation(w, 120, 120);
            AddFamily(w, 1, far);
            AddFamily(w, 2, near);
            AddFamily(w, 3, near);
            int house = Place(w, 30); // house centre near (40, 55)
            Build(w, house);
            w.Step(1);
            Assert.AreEqual(2, w.Homes.Get(house).FamilyId, "nearest workplace; family 2 beats 3 on the id tie");
        }

        [Test]
        public void MoveIn_ThreeHouses_EachPicksNearestRemaining()
        {
            SimWorld w = Make();
            int opA = Operation(w, 35, 75), opB = Operation(w, 70, 75);
            AddFamily(w, 1, opB);
            AddFamily(w, 2, opA);
            int hA = Place(w, 25), hB = Place(w, 60);
            Build(w, hA);
            Build(w, hB);
            w.Step(1);
            Assert.AreEqual(2, w.Homes.Get(hA).FamilyId);
            Assert.AreEqual(1, w.Homes.Get(hB).FamilyId);
        }

        [Test]
        public void SaveRoundTrip_EmptyAndOccupiedHomes_AreIdentical()
        {
            MapData map = SyntheticMap.CreateRamp();
            SimWorld w = Make();
            AddFamily(w, 1);
            int h1 = Place(w, 30), h2 = Place(w, 55);
            Build(w, h1);
            w.Step(1);
            byte[] first = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(first, map);
            Assert.AreEqual(1, back.Homes.Get(h1).FamilyId);
            Assert.AreEqual(0, back.Homes.Get(h2).FamilyId);
            Assert.AreEqual(h1, back.People.FindFamily(1).HomeHouseId);
            Assert.AreEqual(w.Homes.Get(h2).PlotAreaId, back.Homes.Get(h2).PlotAreaId);
            byte[] second = SimSave.Write(back, "s", 1, map.MapId, map.ContentHash);
            Assert.IsNull(SaveDiff.FirstDifference(first, second));
            back.Step(60);
            w.Step(60);
            Assert.AreEqual(SimSave.Write(w, "s", 1, map.MapId, map.ContentHash),
                SimSave.Write(back, "s", 1, map.MapId, map.ContentHash));
        }

        [Test]
        public void SaveVersion_IsEight_VersionFourBytesAreRefused()
        {
            Assert.GreaterOrEqual(SaveWriter.CurrentVersion, 17);
            byte[] bytes = { (byte)'L', (byte)'L', (byte)'S', (byte)'V', 4, 0, 0, 0, 0, 0, 0, 0 };
            Assert.Throws<SaveFormatException>(() => SaveReader.ReadHeaderOnly(bytes));
        }

        [Test]
        public void HomesSection_IsRegisteredAfterPopulationAndBeforeMovement()
        {
            var ids = SaveSections.CreateDefault().Select(s => s.Id).ToList();
            Assert.Less(ids.IndexOf(5), ids.IndexOf(HomesSectionId()));
            Assert.Less(ids.IndexOf(HomesSectionId()), ids.IndexOf(6));
        }

        private static int HomesSectionId() => LordsLegacy.Sim.Save.Sections.HomesSection.SectionId;
    }
}
