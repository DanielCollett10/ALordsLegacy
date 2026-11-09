using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Areas
{
    /// <summary>AR-34 point 5: building areas are trimmed around road corridors; a road through a plot is refused.</summary>
    public class AreaRoadTrimTests
    {
        private const string T = AreaTypes.TestId;
        private const string W = AreaTypes.TestWorkId;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);
        private static SimWorld Ramp() => SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());
        private static AreaShape Poly(params PointMm[] p) => AreaShape.Straight(p);

        // Road along +X at z = 50 from x = 20 to 80; corridor spans z 48.75..51.25.
        private static RoadSegment Road(SimWorld w) =>
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(80, 50) }, new[] { M(20, 50), M(50, 50), M(80, 50) });

        private static AreaRecord AddArea(SimWorld w, AreaShape shape)
        {
            var poly = AreaFlattener.Flatten(shape, w.Roads, w.Tuning);
            var rec = new AreaRecord(w.Areas.NextId(), T, w.Areas.NextName(w.AreaTypes.Find(T)), shape, poly);
            w.Areas.Add(rec);
            return rec;
        }

        // Plot straddling the road's end (x 80): the corridor takes a notch out of its left side.
        private static AreaShape OverRoadEnd() => Poly(M(70, 40), M(90, 40), M(90, 60), M(70, 60));

        [Test]
        public void PlotClippingRoadEnd_IsTrimmedAndFittedAroundRoad()
        {
            SimWorld w = Ramp();
            RoadSegment road = Road(w);
            AreaShape shape = OverRoadEnd();
            AreaPreview p = AreaValidator.Validate(w, shape, T);
            Assert.AreEqual(AreaRefusal.Valid, p.Reason, p.ReasonText);
            CollectionAssert.AreEqual(new[] { "road" }, p.FittedAround);
            Assert.LessOrEqual(PolygonOverlap.AreaM2(p.Polygon, road.CorridorPolygon(w.RoadTypes)), 0.1);
            Assert.Less(p.AreaM2, 400.0);
        }

        [Test]
        public void RoadRunningThroughPlot_RefusedCrossesRoad()
        {
            SimWorld w = Ramp();
            Road(w);
            AreaPreview p = AreaValidator.Validate(w, Poly(M(40, 40), M(60, 40), M(60, 60), M(40, 60)), T);
            Assert.AreEqual(AreaRefusal.CrossesRoad, p.Reason);
            Assert.AreEqual("crosses road", p.ReasonText);
            Assert.IsEmpty(p.FittedAround);
        }

        [Test]
        public void PlotVertexInsideCorridor_RefusedCrossesRoad()
        {
            SimWorld w = Ramp();
            Road(w);
            AreaPreview p = AreaValidator.Validate(w, Poly(M(40, 40), M(60, 40), M(60, 50), M(40, 50)), T);
            Assert.AreEqual(AreaRefusal.CrossesRoad, p.Reason);
        }

        [Test]
        public void PlotFlushToRoadSide_IsUnchanged()
        {
            SimWorld w = Ramp();
            Road(w);
            AreaPreview p = AreaValidator.Validate(w, Poly(M(40, 51.25), M(60, 51.25), M(60, 71.25), M(40, 71.25)), T);
            Assert.AreEqual(AreaRefusal.Valid, p.Reason, p.ReasonText);
            Assert.IsEmpty(p.FittedAround);
            Assert.AreEqual(4, p.Polygon.Count);
            Assert.AreEqual(400.0, p.AreaM2, 1e-6);
        }

        [Test]
        public void WorkArea_OverRoad_IsUntouched()
        {
            SimWorld w = Ramp();
            Road(w);
            AreaPreview p = AreaValidator.Validate(w, OverRoadEnd(), W);
            Assert.AreEqual(AreaRefusal.Valid, p.Reason, p.ReasonText);
            Assert.IsEmpty(p.FittedAround);
            Assert.AreEqual(4, p.Polygon.Count);
        }

        [Test]
        public void PlotFittedAroundNeighbourAndRoad_ListsAreaThenRoadOnce()
        {
            SimWorld w = Ramp();
            RoadSegment road = Road(w);
            // Neighbour bites the plot's right edge (x 80..100, z 52..58), keeping every plot vertex outside it.
            AreaRecord n = AddArea(w, Poly(M(80, 52), M(100, 52), M(100, 58), M(80, 58)));
            AreaPreview p = AreaValidator.Validate(w, OverRoadEnd(), T);
            Assert.AreEqual(AreaRefusal.Valid, p.Reason, p.ReasonText);
            CollectionAssert.AreEqual(new[] { n.Name, "road" }, p.FittedAround);
            Assert.LessOrEqual(PolygonOverlap.AreaM2(p.Polygon, n.Polygon), 0.1);
            Assert.LessOrEqual(PolygonOverlap.AreaM2(p.Polygon, road.CorridorPolygon(w.RoadTypes)), 0.1);
        }

        [Test]
        public void RoadSplitsPlot_AndAreaAlsoContributes_AreaRefusalWins()
        {
            SimWorld w = Ramp();
            Road(w);
            // Neighbour splits the plot into left and right pieces; the road also runs through it.
            AreaRecord n = AddArea(w, Poly(M(48, 30), M(52, 30), M(52, 70), M(48, 70)));
            AreaPreview p = AreaValidator.Validate(w, Poly(M(40, 40), M(60, 40), M(60, 60), M(40, 60)), T);
            Assert.AreEqual(AreaRefusal.OverlapsArea, p.Reason);
            Assert.AreEqual("overlaps " + n.Name, p.ReasonText);
        }
    }
}
