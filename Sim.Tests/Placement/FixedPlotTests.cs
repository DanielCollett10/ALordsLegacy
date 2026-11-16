using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Placement;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Placement
{
    /// <summary>Fixed-structure plot outlines: overlap checks, vertex rules, snaps (T-471, PL-10..13).</summary>
    public class FixedPlotTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        // Granary at (50,50), rot 0: plot x 41..59, z 42..58.
        private static (SimWorld w, MapData map, int id) Make(double gx = 50, double gz = 50)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 8);
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(gx, gz), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return (w, map, (int)r.Created[0]);
        }

        private static AreaShape Rect(double x0, double z0, double x1, double z1) =>
            AreaShape.Straight(new[] { M(x0, z0), M(x1, z0), M(x1, z1), M(x0, z1) });

        [Test]
        public void HousePlotOverGranaryYard_IsRefused_OverlapsGranaryWithId()
        {
            var (w, _, id) = Make();
            AreaPreview p = AreaValidator.Validate(w, Rect(55, 40, 75, 60), AreaTypes.TestId);
            Assert.AreEqual(AreaRefusal.OverlapsArea, p.Reason);
            Assert.AreEqual("overlaps Granary " + id, p.ReasonText);
            CommandResult r = w.Execute(new DesignateArea(Rect(55, 40, 75, 60), AreaTypes.TestId));
            Assert.IsTrue(r.IsRejected);
        }

        [Test]
        public void AreaTouchingGranaryPlotEdge_IsAllowed_NotTrimmed()
        {
            var (w, _, _) = Make();
            AreaPreview p = AreaValidator.Validate(w, Rect(59, 40, 79, 60), AreaTypes.TestId);
            Assert.AreEqual(AreaRefusal.Valid, p.Reason, p.ReasonText);
            Assert.AreEqual(0, p.FittedAround.Count);
        }

        [Test]
        public void AreaEnclosingTheWholeGranaryPlot_IsRefused()
        {
            var (w, _, _) = Make();
            Assert.AreEqual(AreaRefusal.OverlapsArea, AreaValidator.Validate(w, Rect(30, 30, 70, 70), AreaTypes.TestId).Reason);
        }

        [Test]
        public void AreaInsideTheYardOnly_IsRefused()
        {
            var (w, _, _) = Make();
            Assert.AreEqual(AreaRefusal.OverlapsArea, AreaValidator.Validate(w, Rect(42, 43, 47, 48), AreaTypes.TestId).Reason);
        }

        [Test]
        public void RemovedGranary_NoLongerBlocksAreas()
        {
            var (w, _, id) = Make();
            Assert.IsTrue(w.Execute(new RemoveModule(id)).IsAccepted);
            Assert.IsNull(FixedPlots.FindOverlapping(w, new[] { M(55, 40), M(75, 40), M(75, 60), M(55, 60) }));
            Assert.AreEqual(AreaRefusal.Valid, AreaValidator.Validate(w, Rect(55, 40, 75, 60), AreaTypes.TestId).Reason);
        }

        [Test]
        public void VertexInsideYard_IsRefused_OnYardBoundaryAndOutside_Allowed()
        {
            var (w, _, id) = Make();
            PlacementResult inside = PlacementRules.CanPlaceAreaVertex(w, AreaTypes.TestId, M(50, 50));
            Assert.AreEqual(PlacementRefusal.InsideArea, inside.Reason);
            StringAssert.Contains("Granary " + id, inside.AreaName);
            Assert.AreEqual(PlacementRefusal.InsideArea, PlacementRules.CanPlaceAreaVertex(w, AreaTypes.TestId, M(45, 45)).Reason);
            Assert.IsTrue(PlacementRules.CanPlaceAreaVertex(w, AreaTypes.TestId, M(59, 50)).Ok, "on the boundary");
            Assert.IsTrue(PlacementRules.CanPlaceAreaVertex(w, AreaTypes.TestId, M(65, 50)).Ok);
        }

        [Test]
        public void SnapQuery_FindsPlotCornerThenEdge_AndCombinedQueryIncludesIt()
        {
            var (w, _, id) = Make();
            AreaSnapHit? corner = FixedPlots.FindNearestSnap(w, M(59.2, 58.3), 1000);
            Assert.IsTrue(corner.HasValue);
            Assert.AreEqual(AreaSnapKind.Vertex, corner.Value.Kind);
            Assert.IsTrue(corner.Value.IsFixedPlot);
            Assert.AreEqual(id, corner.Value.AreaId);
            Assert.AreEqual(M(59, 58), corner.Value.Point);

            AreaSnapHit? edge = FixedPlots.FindNearestSnapIncludingAreas(w, M(59.4, 50), 1000);
            Assert.AreEqual(AreaSnapKind.Edge, edge.Value.Kind);
            Assert.AreEqual(M(59, 50), edge.Value.Point);
            Assert.IsNull(FixedPlots.FindNearestSnapIncludingAreas(w, M(80, 80), 1000));
        }

        [Test]
        public void SnapQuery_WithAnAreaNearby_VertexBeatsEdge()
        {
            var (w, _, _) = Make();
            Assert.IsTrue(w.Execute(new DesignateArea(Rect(59, 20, 79, 40), AreaTypes.TestId)).IsAccepted);
            AreaSnapHit? hit = FixedPlots.FindNearestSnapIncludingAreas(w, M(59.1, 39.9), 1000);
            Assert.IsFalse(hit.Value.IsFixedPlot);
            Assert.AreEqual(AreaSnapKind.Vertex, hit.Value.Kind);
        }

        [Test]
        public void SecondAreaNextToTheGranaryAndAnotherArea_AllNeighboursChecked()
        {
            var (w, _, _) = Make();
            Assert.IsTrue(w.Execute(new DesignateArea(Rect(59, 20, 79, 41), AreaTypes.TestId)).IsAccepted);
            Assert.AreEqual(AreaRefusal.OverlapsArea, AreaValidator.Validate(w, Rect(59, 38, 79, 60), AreaTypes.TestId).Reason);
        }

        [Test]
        public void RoundTrip_KeepsGranaryOutline_AndOlderVersionIsRefused()
        {
            var (w, map, id) = Make();
            byte[] bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(bytes, map);
            Footprint f = back.Footprints.Get(id);
            Assert.AreEqual(w.Footprints.Get(id).PlotPolygon, f.PlotPolygon);
            Assert.AreEqual(id, FixedPlots.FindOverlapping(back, new[] { M(55, 40), M(75, 40), M(75, 60), M(55, 60) }).Id);
            Assert.AreEqual(bytes, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash));
            byte[] old = { (byte)'L', (byte)'L', (byte)'S', (byte)'V', 17, 0, 0, 0, 0, 0, 0, 0 };
            Assert.Throws<SaveFormatException>(() => SaveReader.ReadHeaderOnly(old));
        }
    }
}
