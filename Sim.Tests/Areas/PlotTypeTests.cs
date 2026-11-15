using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Areas
{
    /// <summary>The plot area type (HM-02, HM-03) and the house footprint type (HM-04, HM-06) (T-403).</summary>
    public class PlotTypeTests
    {
        private const string P = AreaTypes.PlotId;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);
        private static SimWorld Ramp() => SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());

        // Road along +X at z=50; Left side line at z=51.25.
        private static RoadSegment Road(SimWorld w) =>
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(80, 50) }, new[] { M(20, 50), M(50, 50), M(80, 50) });

        // Plot of width x depth with its first edge flush to the road's Left side.
        private static AreaShape Facing(RoadSegment seg, double x, double width, double depth) =>
            new AreaShape(
                new[] { M(x, 51.25), M(x + width, 51.25), M(x + width, 51.25 + depth), M(x, 51.25 + depth) },
                new[] { AreaEdge.FollowsRoad(seg.Id, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });

        [Test]
        public void Plot_IsInDefaultCatalog_WithSpecValues()
        {
            AreaTypeDef t = Ramp().AreaTypes.Find(P);
            Assert.IsNotNull(t);
            Assert.AreEqual(AreaKind.Building, t.Kind);
            Assert.AreEqual(60.0, t.MinAreaM2);
            Assert.AreEqual(400.0, t.MaxAreaM2);
            Assert.IsTrue(t.RequiresRoadFrontage);
            Assert.IsFalse(AreaTypes.Test.RequiresRoadFrontage);
        }

        [Test]
        public void Plot_FacingRoad_200m2_IsValid()
        {
            SimWorld w = Ramp();
            Assert.AreEqual(AreaRefusal.Valid, AreaValidator.Validate(w, Facing(Road(w), 30, 20, 10), P).Reason);
        }

        [Test]
        public void Plot_NoRoadInWorld_StraightEdges_IsRefusedWithMustFaceARoad()
        {
            AreaShape s = AreaShape.Straight(new[] { M(30, 60), M(50, 60), M(50, 70), M(30, 70) });
            AreaPreview p = AreaValidator.Validate(Ramp(), s, P);
            Assert.AreEqual(AreaRefusal.NoFrontage, p.Reason);
            Assert.AreEqual("A plot must face a road", p.ReasonText);
        }

        [Test]
        public void Plot_NextToRoadButNoEdgeFlush_IsRefusedWithMustFaceARoad()
        {
            SimWorld w = Ramp();
            Road(w);
            AreaShape s = AreaShape.Straight(new[] { M(30, 51.25), M(50, 51.25), M(50, 61.25), M(30, 61.25) });
            Assert.AreEqual(AreaRefusal.NoFrontage, AreaValidator.Validate(w, s, P).Reason);
        }

        [Test]
        public void Plot_SameShapeAsTestType_WithoutFrontage_IsValidForTestType()
        {
            AreaShape s = AreaShape.Straight(new[] { M(30, 60), M(50, 60), M(50, 80), M(30, 80) });
            Assert.AreEqual(AreaRefusal.Valid, AreaValidator.Validate(Ramp(), s, AreaTypes.TestId).Reason);
        }

        [Test]
        public void Plot_UnderMinimum50m2_IsTooSmallForAHouse()
        {
            SimWorld w = Ramp();
            AreaPreview p = AreaValidator.Validate(w, Facing(Road(w), 30, 5, 10), P);
            Assert.AreEqual(AreaRefusal.PlotTooSmall, p.Reason);
            Assert.AreEqual("Too small for a house", p.ReasonText);
        }

        [Test]
        public void Plot_Exactly60m2_IsValid()
        {
            SimWorld w = Ramp();
            Assert.AreEqual(AreaRefusal.Valid, AreaValidator.Validate(w, Facing(Road(w), 30, 6, 10), P).Reason);
        }

        [Test]
        public void Plot_Exactly400m2_IsValid()
        {
            SimWorld w = Ramp();
            Assert.AreEqual(AreaRefusal.Valid, AreaValidator.Validate(w, Facing(Road(w), 30, 20, 20), P).Reason);
        }

        [Test]
        public void Plot_Over400m2_IsTooLarge()
        {
            SimWorld w = Ramp();
            Assert.AreEqual(AreaRefusal.TooLarge, AreaValidator.Validate(w, Facing(Road(w), 30, 21, 20), P).Reason);
        }

        [Test]
        public void Plot_TinyAndNoFrontage_ReportsFrontageFirst()
        {
            AreaShape s = AreaShape.Straight(new[] { M(30, 60), M(32, 60), M(32, 62), M(30, 62) });
            Assert.AreEqual(AreaRefusal.NoFrontage, AreaValidator.Validate(Ramp(), s, P).Reason);
        }

        [Test]
        public void House_IsSixByEightMetres_BlocksMovement_Recipe10Logs720Minutes()
        {
            FootprintTypeDef h = ModuleTypes.Find(ModuleTypes.HouseId);
            Assert.AreSame(ModuleTypes.House, h);
            Assert.AreEqual(6000, h.WidthMm);
            Assert.AreEqual(8000, h.DepthMm);
            Assert.IsTrue(h.BlocksMovement);
            Assert.AreEqual(10, h.ConstructionRecipe.Logs);
            Assert.AreEqual(720, h.ConstructionRecipe.BuildWorkerMinutes);
            Assert.AreEqual("House", ModuleTypes.DisplayName(ModuleTypes.HouseId));
        }

        [Test]
        public void RoundTrip_WorldWithAPlot_IsIdentical()
        {
            MapData map = SyntheticMap.CreateRamp();
            SimWorld w = SimWorld.FromMapData(map, new SimTuning());
            Assert.IsTrue(w.Execute(new DesignateArea(Facing(Road(w), 30, 20, 10), P)).IsAccepted);
            byte[] first = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(first, map);
            Assert.IsNotNull(back.AreaTypes.Find(P));
            byte[] second = SimSave.Write(back, "s", 1, map.MapId, map.ContentHash);
            Assert.IsNull(SaveDiff.FirstDifference(first, second));
            Assert.AreEqual(first, second);
        }
    }
}
