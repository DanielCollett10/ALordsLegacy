using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Placement;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Placement
{
    /// <summary>Ground piles with items block building in a 2 m circle (T-472, PL-15).</summary>
    public class PileZoneTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static MapData Map() =>
            new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);

        private static SimWorld Flat()
        {
            var w = SimWorld.FromMapData(Map(), new SimTuning());
            w.Inventory.Produce(w.Inventory.AddStockpile(M(5, 5)), Items.Log, 40);
            return w;
        }

        private static LedgerLocation Pile(SimWorld w, double x, double z, int logs = 1, int? owner = null)
        {
            LedgerLocation p = w.Inventory.GetOrAddGroundPile(M(x, z), owner);
            w.Inventory.Produce(p, Items.Log, logs);
            return p;
        }

        private static AreaShape Rect(double x, double z, double w, double h) =>
            AreaShape.Straight(new[] { M(x, z), M(x + w, z), M(x + w, z + h), M(x, z + h) });

        private static AreaRefusal Refusal(SimWorld w, AreaShape s, string type) => AreaValidator.Validate(w, s, type).Reason;

        [Test]
        public void PileZones_OneCirclePerNonEmptyPile_AnyOwner_RadiusFromTuning()
        {
            SimWorld w = Flat();
            Pile(w, 30, 30);
            Pile(w, 60, 60, 3, owner: 7);
            w.Inventory.GetOrAddGroundPile(M(80, 80), null); // empty: no circle
            var zones = PileZones.Of(w);
            Assert.AreEqual(2, zones.Count);
            Assert.AreEqual(2.0, zones[0].RadiusM);
            Assert.AreEqual(M(30, 30), zones[0].Centre);
            Assert.AreEqual(M(60, 60), zones[1].Centre);
        }

        [Test]
        public void TestBuildingArea_OverlappingCircle_RefusedItemsOnGround_ClearOneIsValid()
        {
            SimWorld w = Flat();
            Pile(w, 30, 30);
            AreaPreview p = AreaValidator.Validate(w, Rect(20, 20, 20, 20), AreaTypes.TestId);
            Assert.AreEqual(AreaRefusal.ItemsOnGround, p.Reason);
            Assert.AreEqual("Items on the ground here", p.ReasonText);
        }

        [Test]
        public void TestBuildingArea_EdgeWithinTwoMetresOfPile_Refused_EdgeBeyondIsValid()
        {
            SimWorld w = Flat();
            Pile(w, 30, 30);
            Assert.AreEqual(AreaRefusal.ItemsOnGround, Refusal(w, Rect(10, 10, 19, 40), AreaTypes.TestId)); // edge x=29, 1 m away
            Assert.IsTrue(AreaValidator.Validate(w, Rect(10, 10, 17.5, 40), AreaTypes.TestId).IsValid); // edge x=27.5, 2.5 m away
        }

        [Test]
        public void Market_OverCircle_RefusedItemsOnGround()
        {
            SimWorld w = Flat();
            Pile(w, 40, 40, 2, owner: 3);
            Assert.AreEqual(AreaRefusal.ItemsOnGround, Refusal(w, Rect(30, 30, 20, 20), AreaTypes.MarketId));
        }

        [Test]
        public void Plot_OverCircle_RefusedItemsOnGround()
        {
            SimWorld w = Flat();
            int road = w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(80, 50) }, new[] { M(20, 50), M(50, 50), M(80, 50) }).Id;
            var plot = new AreaShape(
                new[] { M(30, 51.25), M(50, 51.25), M(50, 61.25), M(30, 61.25) },
                new[] { AreaEdge.FollowsRoad(road, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            Assert.IsTrue(AreaValidator.Validate(w, plot, AreaTypes.PlotId).IsValid);
            Pile(w, 40, 56);
            Assert.AreEqual(AreaRefusal.ItemsOnGround, Refusal(w, plot, AreaTypes.PlotId));
        }

        [Test]
        public void WorkArea_OverCircle_StillValid()
        {
            SimWorld w = Flat();
            Pile(w, 40, 40);
            Assert.IsTrue(AreaValidator.Validate(w, Rect(30, 30, 20, 20), AreaTypes.WoodcuttingWorkId).IsValid);
        }

        [Test]
        public void Road_OverCircle_StillPlaceable()
        {
            SimWorld w = Flat();
            Pile(w, 40, 40);
            Assert.IsTrue(PlacementRules.CanPlaceRoadPoint(w, M(40, 40)).Ok);
            Assert.IsTrue(w.Execute(new DrawRoad(new[] { M(20, 40), M(60, 40) }, RoadTypes.DirtPathId)).IsAccepted);
        }

        [Test]
        public void VertexRules_BuildingVertexInsideCircleRefused_WorkVertexAndOutsideOk()
        {
            SimWorld w = Flat();
            Pile(w, 40, 40);
            PlacementResult r = PlacementRules.CanPlaceAreaVertex(w, AreaTypes.TestId, M(41, 40));
            Assert.AreEqual(PlacementRefusal.ItemsOnGround, r.Reason);
            Assert.AreEqual("Items on the ground here", r.Text);
            Assert.IsTrue(PlacementRules.CanPlaceAreaVertex(w, AreaTypes.TestId, M(43, 40)).Ok);
            Assert.IsTrue(PlacementRules.CanPlaceAreaVertex(w, AreaTypes.WoodcuttingWorkId, M(41, 40)).Ok);
        }

        [Test]
        public void Structure_PlotOverCircle_RefusedItemsOnGround()
        {
            SimWorld w = Flat();
            Assert.IsTrue(StructurePlacement.Preview(w, ModuleTypes.GranaryId, M(50, 50), 0).Ok);
            Pile(w, 56, 50); // inside the 5 m yard, outside the building
            StructurePlacementResult r = StructurePlacement.Preview(w, ModuleTypes.GranaryId, M(50, 50), 0);
            Assert.AreEqual(StructureRefusal.ItemsOnGround, r.Refusal);
            Assert.AreEqual("Items on the ground here", r.Text);
            Assert.IsFalse(w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(50, 50), 0)).IsAccepted);
        }

        [Test]
        public void DraftModule_OverCircle_RefusedItemsOnGround()
        {
            SimWorld w = Flat();
            var area = new[] { M(100, 100), M(120, 100), M(120, 120), M(100, 120) };
            Assert.IsTrue(DraftModulePlacement.Preview(w, OperationTypes.Woodcutting, area, null,
                ModuleTypes.WoodcuttingHutId, M(110, 110), 0, 0).Ok);
            Pile(w, 110, 110);
            Assert.AreEqual(ModuleRefusal.ItemsOnGround, DraftModulePlacement.Preview(w, OperationTypes.Woodcutting, area, null,
                ModuleTypes.WoodcuttingHutId, M(110, 110), 0, 0).Refusal);
        }

        [Test]
        public void Module_InExistingOperation_OverCircle_RefusedItemsOnGround()
        {
            SimWorld w = Flat();
            CommandResult d = w.Execute(new DesignateArea(Rect(100, 100, 30, 20), AreaTypes.WoodcuttingId));
            Assert.IsTrue(d.IsAccepted, d.Text);
            int op = w.Operations.FindByArea((int)d.Created[0]).Id;
            Assert.IsTrue(ModulePlacement.Preview(w, op, ModuleTypes.WoodcuttingHutId, M(115, 110), 0).Ok);
            Pile(w, 115, 110, 2, owner: op);
            Assert.AreEqual(ModuleRefusal.ItemsOnGround,
                ModulePlacement.Preview(w, op, ModuleTypes.WoodcuttingHutId, M(115, 110), 0).Refusal);
        }

        [Test]
        public void EmptiedPile_CircleGoes_AreaValidAgain()
        {
            SimWorld w = Flat();
            LedgerLocation pile = Pile(w, 30, 30, 2);
            Assert.AreEqual(AreaRefusal.ItemsOnGround, Refusal(w, Rect(20, 20, 20, 20), AreaTypes.TestId));
            Assert.IsTrue(w.Inventory.Consume(pile, Items.Log, 2));
            Assert.AreEqual(0, PileZones.Of(w).Count);
            Assert.IsTrue(AreaValidator.Validate(w, Rect(20, 20, 20, 20), AreaTypes.TestId).IsValid);
        }

        [Test]
        public void Tuning_PileClearanceDefaultsTwoMetres_AndSurvivesSaveRoundTrip()
        {
            MapData map = Map();
            var w = SimWorld.FromMapData(map, new SimTuning { PileClearanceM = 3.5 });
            Assert.AreEqual(2.0, new SimTuning().PileClearanceM);
            byte[] bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(bytes, map);
            Assert.AreEqual(3.5, back.Tuning.PileClearanceM);
        }
    }
}
