using System;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Placement;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    /// <summary>Every row of docs/design/placement.md (PL-01..05, PL-07) plus boundary and degenerate cases.</summary>
    public class PlacementRulesTests
    {
        private const string T = AreaTypes.TestId;
        private const string W = AreaTypes.TestWorkId;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        // 200 x 200 m map.
        private static SimWorld World() => SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());

        private static AreaRecord AddArea(SimWorld w, string typeId, AreaShape shape)
        {
            AreaTypeDef type = w.AreaTypes.Find(typeId);
            var poly = AreaFlattener.Flatten(shape, w.Roads, w.Tuning);
            var rec = new AreaRecord(w.Areas.NextId(), type.Id, w.Areas.NextName(type), shape, poly);
            w.Areas.Add(rec);
            return rec;
        }

        private static AreaShape Square(double x, double z, double s) =>
            AreaShape.Straight(new[] { M(x, z), M(x + s, z), M(x + s, z + s), M(x, z + s) });

        // Building area 50..70 in both axes.
        private static SimWorld WithBuilding()
        {
            SimWorld w = World();
            AddArea(w, T, Square(50, 50, 20));
            return w;
        }

        // Road along +X at z = 120 (dirt path, corridor z 118.75..121.25).
        private static void AddRoad(SimWorld w) =>
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 120), M(80, 120) }, new[] { M(20, 120), M(50, 120), M(80, 120) });

        // PL-01: building-area vertex vs building areas -------------------------------------------------

        [Test]
        public void BuildingVertex_InsideOtherBuildingArea_RefusedWithAreaName()
        {
            SimWorld w = WithBuilding();
            PlacementResult r = PlacementRules.CanPlaceAreaVertex(w, T, M(60, 60));
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(PlacementRefusal.InsideArea, r.Reason);
            Assert.AreEqual("Test area 1", r.AreaName);
            Assert.AreEqual("inside Test area 1", r.Text);
        }

        [Test]
        public void BuildingVertex_OnOtherAreaEdge_Ok() =>
            Assert.IsTrue(PlacementRules.CanPlaceAreaVertex(WithBuilding(), T, M(60, 50)).Ok);

        [Test]
        public void BuildingVertex_OnOtherAreaCorner_Ok() =>
            Assert.IsTrue(PlacementRules.CanPlaceAreaVertex(WithBuilding(), T, M(50, 50)).Ok);

        [Test]
        public void BuildingVertex_4cmInsideEdge_Ok_WithinTolerance() =>
            Assert.IsTrue(PlacementRules.CanPlaceAreaVertex(WithBuilding(), T, M(60, 50.04)).Ok);

        [Test]
        public void BuildingVertex_5cmInsideEdge_Ok_ToleranceIsInclusive() =>
            Assert.IsTrue(PlacementRules.CanPlaceAreaVertex(WithBuilding(), T, M(60, 50.05)).Ok);

        [Test]
        public void BuildingVertex_6cmInsideEdge_Refused() =>
            Assert.AreEqual(PlacementRefusal.InsideArea,
                PlacementRules.CanPlaceAreaVertex(WithBuilding(), T, M(60, 50.06)).Reason);

        [Test]
        public void BuildingVertex_InsideNearCorner_6cmFromBothEdges_Refused() =>
            Assert.AreEqual(PlacementRefusal.InsideArea,
                PlacementRules.CanPlaceAreaVertex(WithBuilding(), T, M(50.06, 50.06)).Reason);

        [Test]
        public void BuildingVertex_OutsideOtherArea_Ok() =>
            Assert.IsTrue(PlacementRules.CanPlaceAreaVertex(WithBuilding(), T, M(100, 100)).Ok);

        [Test]
        public void BuildingVertex_InsideSeveralAreas_NamesTheLowestId()
        {
            SimWorld w = World();
            AddArea(w, T, Square(10, 10, 20));
            AddArea(w, T, Square(100, 100, 20));
            AddArea(w, T, Square(105, 105, 20)); // overlaps the second; both contain (110,110)
            PlacementResult r = PlacementRules.CanPlaceAreaVertex(w, T, M(110, 110));
            Assert.AreEqual("Test area 2", r.AreaName);
            Assert.AreEqual(PlacementRefusal.InsideArea, PlacementRules.CanPlaceAreaVertex(w, T, M(20, 20)).Reason);
            Assert.AreEqual("Test area 1", PlacementRules.CanPlaceAreaVertex(w, T, M(20, 20)).AreaName);
            Assert.IsTrue(PlacementRules.CanPlaceAreaVertex(w, T, M(60, 60)).Ok);
        }

        [Test]
        public void BuildingVertex_NeighbouringSeveralAreasOnSharedEdge_Ok()
        {
            SimWorld w = World();
            AddArea(w, T, Square(10, 10, 20));
            AddArea(w, T, Square(30, 10, 20)); // shares the x = 30 edge
            Assert.IsTrue(PlacementRules.CanPlaceAreaVertex(w, T, M(30, 20)).Ok);
            Assert.IsTrue(PlacementRules.CanPlaceAreaVertex(w, T, M(30, 10)).Ok);
        }

        [Test]
        public void BuildingVertex_InsideCurvedAreaBulge_Refused_AndOnCurvedEdge_Ok()
        {
            SimWorld w = World();
            var shape = new AreaShape(new[] { M(50, 50), M(70, 50), M(70, 70), M(50, 70) },
                new[] { AreaEdge.Bent(M(60, 40)), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            AreaRecord rec = AddArea(w, T, shape);
            Assert.AreEqual(PlacementRefusal.InsideArea, PlacementRules.CanPlaceAreaVertex(w, T, M(60, 60)).Reason);
            // Every flattened outline point lies on the boundary, so each is placeable.
            foreach (PointMm onOutline in rec.Polygon)
                Assert.IsTrue(PlacementRules.CanPlaceAreaVertex(w, T, onOutline).Ok, onOutline.ToString());
            // The bulge below z = 50 is part of the area: a point inside it is refused.
            Assert.AreEqual(PlacementRefusal.InsideArea, PlacementRules.CanPlaceAreaVertex(w, T, M(60, 52)).Reason);
        }

        // PL-01: building-area vertex vs roads ----------------------------------------------------------

        [Test]
        public void BuildingVertex_InsideRoadCorridor_RefusedOnRoad()
        {
            SimWorld w = World();
            AddRoad(w);
            PlacementResult r = PlacementRules.CanPlaceAreaVertex(w, T, M(50, 120));
            Assert.AreEqual(PlacementRefusal.OnRoad, r.Reason);
            Assert.IsNull(r.AreaName);
            Assert.AreEqual("on a road", r.Text);
        }

        [Test]
        public void BuildingVertex_OnRoadSideLine_Ok()
        {
            SimWorld w = World();
            AddRoad(w);
            Assert.IsTrue(PlacementRules.CanPlaceAreaVertex(w, T, M(50, 121.25)).Ok);
            Assert.IsTrue(PlacementRules.CanPlaceAreaVertex(w, T, M(50, 118.75)).Ok);
        }

        [Test]
        public void BuildingVertex_RoadSideLineToleranceEdges()
        {
            SimWorld w = World();
            AddRoad(w);
            Assert.IsTrue(PlacementRules.CanPlaceAreaVertex(w, T, M(50, 121.2)).Ok);   // 5 cm inside
            Assert.AreEqual(PlacementRefusal.OnRoad,
                PlacementRules.CanPlaceAreaVertex(w, T, M(50, 121.19)).Reason);        // 6 cm inside
            Assert.IsTrue(PlacementRules.CanPlaceAreaVertex(w, T, M(50, 121.3)).Ok);   // outside
        }

        [Test]
        public void BuildingVertex_AlongsideRoad_Ok()
        {
            SimWorld w = World();
            AddRoad(w);
            Assert.IsTrue(PlacementRules.CanPlaceAreaVertex(w, T, M(50, 130)).Ok);
        }

        [Test]
        public void BuildingVertex_OnRoadSquareEnd_Ok_AndPastEnd_Ok()
        {
            SimWorld w = World();
            AddRoad(w);
            Assert.IsTrue(PlacementRules.CanPlaceAreaVertex(w, T, M(20, 120)).Ok);
            Assert.IsTrue(PlacementRules.CanPlaceAreaVertex(w, T, M(10, 120)).Ok);
        }

        [Test]
        public void BuildingVertex_InsideCurvedRoadCorridor_Refused_AndOnSideLine_Ok()
        {
            SimWorld w = World();
            PointMm[] samples = { M(20, 150), M(40, 160), M(60, 160), M(80, 150) };
            RoadSegment seg = w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                samples, samples);
            Assert.AreEqual(PlacementRefusal.OnRoad,
                PlacementRules.CanPlaceAreaVertex(w, T, M(50, 160)).Reason);
            PointMm[] side = seg.SideLine(RoadSide.Left, w.RoadTypes);
            Assert.IsTrue(PlacementRules.CanPlaceAreaVertex(w, T, side[1]).Ok);
        }

        [Test]
        public void BuildingVertex_InsideSeveralRoads_Refused()
        {
            SimWorld w = World();
            AddRoad(w);
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(150, 20), M(150, 80) }, new[] { M(150, 20), M(150, 50), M(150, 80) });
            Assert.AreEqual(PlacementRefusal.OnRoad, PlacementRules.CanPlaceAreaVertex(w, T, M(150, 50)).Reason);
            Assert.AreEqual(PlacementRefusal.OnRoad, PlacementRules.CanPlaceAreaVertex(w, T, M(60, 120)).Reason);
        }

        // PL-01 / PL-02: work areas ----------------------------------------------------------------------

        [Test]
        public void BuildingVertex_InsideWorkArea_Ok()
        {
            SimWorld w = World();
            AddArea(w, W, Square(50, 50, 20));
            Assert.IsTrue(PlacementRules.CanPlaceAreaVertex(w, T, M(60, 60)).Ok);
        }

        [Test]
        public void WorkVertex_InsideBuildingArea_Ok() =>
            Assert.IsTrue(PlacementRules.CanPlaceAreaVertex(WithBuilding(), W, M(60, 60)).Ok);

        [Test]
        public void WorkVertex_InsideRoad_Ok()
        {
            SimWorld w = World();
            AddRoad(w);
            Assert.IsTrue(PlacementRules.CanPlaceAreaVertex(w, W, M(50, 120)).Ok);
        }

        [Test]
        public void WorkVertex_InsideWorkArea_Ok()
        {
            SimWorld w = World();
            AddArea(w, W, Square(50, 50, 20));
            Assert.IsTrue(PlacementRules.CanPlaceAreaVertex(w, W, M(60, 60)).Ok);
        }

        // PL-03: road points ---------------------------------------------------------------------------

        [Test]
        public void RoadPoint_InsideBuildingArea_RefusedWithName()
        {
            PlacementResult r = PlacementRules.CanPlaceRoadPoint(WithBuilding(), M(60, 60));
            Assert.AreEqual(PlacementRefusal.InsideArea, r.Reason);
            Assert.AreEqual("Test area 1", r.AreaName);
        }

        [Test]
        public void RoadPoint_OnBuildingAreaEdgeAndCorner_Ok()
        {
            SimWorld w = WithBuilding();
            Assert.IsTrue(PlacementRules.CanPlaceRoadPoint(w, M(60, 50)).Ok);
            Assert.IsTrue(PlacementRules.CanPlaceRoadPoint(w, M(70, 70)).Ok);
            Assert.IsTrue(PlacementRules.CanPlaceRoadPoint(w, M(60, 50.05)).Ok);
            Assert.AreEqual(PlacementRefusal.InsideArea, PlacementRules.CanPlaceRoadPoint(w, M(60, 50.06)).Reason);
        }

        [Test]
        public void RoadPoint_OnRoad_Ok()
        {
            SimWorld w = World();
            AddRoad(w);
            Assert.IsTrue(PlacementRules.CanPlaceRoadPoint(w, M(50, 120)).Ok);
            Assert.IsTrue(PlacementRules.CanPlaceRoadPoint(w, M(20, 120)).Ok);
        }

        [Test]
        public void RoadPoint_InsideWorkArea_Ok()
        {
            SimWorld w = World();
            AddArea(w, W, Square(50, 50, 20));
            Assert.IsTrue(PlacementRules.CanPlaceRoadPoint(w, M(60, 60)).Ok);
        }

        [Test]
        public void RoadPoint_EmptyWorld_Ok() =>
            Assert.IsTrue(PlacementRules.CanPlaceRoadPoint(World(), M(100, 100)).Ok);

        // PL-04: map edge ---------------------------------------------------------------------------------

        [Test]
        public void OutsideMap_RefusedForEveryKind()
        {
            SimWorld w = World();
            foreach (PointMm p in new[] { M(-1, 100), M(100, -1), M(201, 100), M(100, 201), M(-500, -500) })
            {
                Assert.AreEqual(PlacementRefusal.OutsideMap, PlacementRules.CanPlaceAreaVertex(w, T, p).Reason, p.ToString());
                Assert.AreEqual(PlacementRefusal.OutsideMap, PlacementRules.CanPlaceAreaVertex(w, W, p).Reason, p.ToString());
                Assert.AreEqual(PlacementRefusal.OutsideMap, PlacementRules.CanPlaceRoadPoint(w, p).Reason, p.ToString());
            }
            Assert.AreEqual("outside the map", PlacementRules.CanPlaceRoadPoint(w, M(-1, 1)).Text);
        }

        [Test]
        public void MapEdgeAndCorners_Ok()
        {
            SimWorld w = World();
            foreach (PointMm p in new[] { M(0, 0), M(200, 200), M(0, 200), M(100, 0) })
            {
                Assert.IsTrue(PlacementRules.CanPlaceAreaVertex(w, T, p).Ok, p.ToString());
                Assert.IsTrue(PlacementRules.CanPlaceRoadPoint(w, p).Ok, p.ToString());
            }
        }

        [Test]
        public void FlatWorldWithoutMap_EverywhereIsOnTheMap()
        {
            var w = new SimWorld(new SimTuning());
            Assert.IsTrue(PlacementRules.CanPlaceRoadPoint(w, M(-5000, 9000)).Ok);
        }

        [Test]
        public void OutsideMap_WinsOverInsideArea()
        {
            SimWorld w = World();
            AddArea(w, T, Square(-10, -10, 20)); // sticks out of the map
            Assert.AreEqual(PlacementRefusal.OutsideMap, PlacementRules.CanPlaceRoadPoint(w, M(-5, -5)).Reason);
        }

        // PL-05 / PL-07: pure, no state change ------------------------------------------------------------

        [Test]
        public void Queries_DoNotChangeWorld()
        {
            SimWorld w = WithBuilding();
            AddRoad(w);
            int areas = w.Areas.Version, roads = w.Roads.Version, treesV = w.Trees.Version;
            long tick = w.Tick;
            int log = w.CommandLog.Count;
            foreach (PointMm p in new[] { M(60, 60), M(50, 50), M(50, 120), M(-1, -1), M(100, 100) })
            {
                PlacementRules.CanPlaceAreaVertex(w, T, p);
                PlacementRules.CanPlaceAreaVertex(w, W, p);
                PlacementRules.CanPlaceRoadPoint(w, p);
            }
            Assert.AreEqual(areas, w.Areas.Version);
            Assert.AreEqual(roads, w.Roads.Version);
            Assert.AreEqual(treesV, w.Trees.Version);
            Assert.AreEqual(tick, w.Tick);
            Assert.AreEqual(log, w.CommandLog.Count);
        }

        [Test]
        public void SamePointTwice_SameAnswer()
        {
            SimWorld w = WithBuilding();
            PlacementResult a = PlacementRules.CanPlaceAreaVertex(w, T, M(60, 60));
            PlacementResult b = PlacementRules.CanPlaceAreaVertex(w, T, M(60, 60));
            Assert.AreEqual(a.Reason, b.Reason);
            Assert.AreEqual(a.AreaName, b.AreaName);
        }

        [Test]
        public void ErrorCases_UnknownTypeAndNullWorld_Throw()
        {
            Assert.Throws<ArgumentException>(() => PlacementRules.CanPlaceAreaVertex(World(), "nope", M(1, 1)));
            Assert.Throws<ArgumentNullException>(() => PlacementRules.CanPlaceAreaVertex(null, T, M(1, 1)));
            Assert.Throws<ArgumentNullException>(() => PlacementRules.CanPlaceRoadPoint(null, M(1, 1)));
        }

        [Test]
        public void OkResult_HasNoReasonAndEmptyText()
        {
            PlacementResult r = PlacementRules.CanPlaceRoadPoint(World(), M(10, 10));
            Assert.AreEqual(PlacementRefusal.None, r.Reason);
            Assert.AreEqual("", r.Text);
            Assert.IsNull(r.AreaName);
        }
    }
}
