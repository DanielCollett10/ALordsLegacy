using System.Collections.Generic;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Areas
{
    /// <summary>HM-04: the house is placed on the longest frontage edge, long side along it, 1 m back (T-404).</summary>
    public class HousePlacementTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static AreaShape Shape(PointMm[] v, params AreaEdgeKind[] kinds)
        {
            var e = new AreaEdge[v.Length];
            for (int i = 0; i < e.Length; i++)
                e[i] = kinds[i] == AreaEdgeKind.FollowsRoad ? AreaEdge.FollowsRoad(1, RoadSide.Left) : AreaEdge.Straight();
            return new AreaShape(v, e);
        }

        private const AreaEdgeKind F = AreaEdgeKind.FollowsRoad, S = AreaEdgeKind.Straight;

        private static HousePlacementResult Run(PointMm[] v, AreaEdgeKind[] kinds, params Footprint[] fps) =>
            HousePlacement.Place(Shape(v, kinds), v, fps);

        private static PointMm[] Rect(double w, double d) => new[] { M(0, 0), M(w, 0), M(w, d), M(0, d) };

        [Test]
        public void Rectangle_HouseCentredOnFrontage_OneMetreBack_LongSideAlong()
        {
            HousePlacementResult r = Run(Rect(10, 12), new[] { F, S, S, S });
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(M(5, 4), r.Centre);
            var b = Polygons.Bounds(r.Polygon);
            Assert.AreEqual(M(1, 1), b.Min); // 8 m along the road (x), 1 m back (z)
            Assert.AreEqual(M(9, 7), b.Max);
            Assert.AreEqual(0, r.FrontageEdge);
        }

        [Test]
        public void ClockwiseShape_GivesSameHouse()
        {
            PointMm[] cw = { M(0, 0), M(0, 12), M(10, 12), M(10, 0) };
            // Edge 3 (10,0)->(0,0) is the frontage; the stored polygon is counter-clockwise.
            AreaShape shape = Shape(cw, S, S, S, F);
            PointMm[] ccw = Polygons.EnsureCounterClockwise(cw);
            HousePlacementResult r = HousePlacement.Place(shape, ccw, null);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(M(5, 4), r.Centre);
            Assert.AreEqual(3, r.FrontageEdge);
        }

        [Test]
        public void FrontageOnAnyEdge_HouseFacesIt()
        {
            // Frontage on the right edge x=10 (edge 1): house sits 1 m back from it, long side along z.
            HousePlacementResult r = Run(Rect(12, 10), new[] { S, F, S, S });
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(M(8, 5), r.Centre);
            var b = Polygons.Bounds(r.Polygon);
            Assert.AreEqual(8000, b.Max.Z - b.Min.Z);
            Assert.AreEqual(6000, b.Max.X - b.Min.X);
            Assert.AreEqual(11000, b.Max.X); // 1 m from the edge at x=12
        }

        [Test]
        public void TwoFrontageEdges_LongestChosen()
        {
            // Bottom edge 8 m, right edge 14 m, both on a road.
            PointMm[] v = { M(0, 0), M(8, 0), M(8, 14), M(0, 14) };
            HousePlacementResult r = Run(v, new[] { F, F, S, S });
            Assert.AreEqual(1, r.FrontageEdge);
        }

        [Test]
        public void EqualFrontageEdges_LowestIndexWins()
        {
            PointMm[] v = { M(0, 0), M(10, 0), M(10, 10), M(0, 10) };
            Assert.AreEqual(0, Run(v, new[] { F, F, S, S }).FrontageEdge);
        }

        [Test]
        public void NoFrontageEdge_RefusesNoFrontage()
        {
            HousePlacementResult r = Run(Rect(10, 12), new[] { S, S, S, S });
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(HousePlacementRefusal.NoFrontage, r.Refusal);
        }

        [Test]
        public void TooShallowPlot_RefusesNoRoomForTheHouse()
        {
            HousePlacementResult r = Run(Rect(10, 6), new[] { F, S, S, S }); // needs 1 + 6 m of depth
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(HousePlacementRefusal.NoRoom, r.Refusal);
            Assert.AreEqual("No room for the house", r.Text);
        }

        [Test]
        public void TooShortFrontage_RefusesNoRoom()
        {
            HousePlacementResult r = Run(Rect(7, 20), new[] { F, S, S, S }); // 8 m house along a 7 m frontage
            Assert.AreEqual(HousePlacementRefusal.NoRoom, r.Refusal);
        }

        [Test]
        public void ExactFit_IsAccepted()
        {
            HousePlacementResult r = Run(Rect(8, 7), new[] { F, S, S, S });
            Assert.IsTrue(r.Ok);
        }

        [Test]
        public void FootprintOverlap_RefusesNoRoom()
        {
            var other = new Footprint(5, ModuleTypes.LogStorage, M(5, 4), 0);
            Assert.AreEqual(HousePlacementRefusal.NoRoom, Run(Rect(10, 12), new[] { F, S, S, S }, other).Refusal);
        }

        [Test]
        public void FootprintTouchingOnly_IsAccepted()
        {
            // House spans x 6..14; log storage 8x6 at x 14..22 touches its edge without overlapping.
            var other = new Footprint(5, ModuleTypes.LogStorage, M(18, 4), 0);
            Assert.IsTrue(Run(Rect(20, 12), new[] { F, S, S, S }, other).Ok);
        }

        [Test]
        public void ConcavePlot_NotchIntoHouse_RefusesNoRoom()
        {
            // L-shape whose notch bites into where the house would sit.
            PointMm[] v = { M(0, 0), M(20, 0), M(20, 12), M(12, 12), M(12, 4), M(8, 4), M(8, 12), M(0, 12) };
            // Frontage edge 0 (20 m): house x 6..14, z 1..7 overlaps the notch top edge z=4.
            var shape = Shape(v, F, S, S, S, S, S, S, S);
            // Notch is a thin wall region: vertices (12,4),(8,4) -> the polygon interior excludes x 8..12, z>4.
            Assert.AreEqual(HousePlacementRefusal.NoRoom, HousePlacement.Place(shape, v, null).Refusal);
        }

        [Test]
        public void CurvedFrontage_HouseTangentAtMiddle()
        {
            // Frontage curve bulging inwards (up) in the middle: arc from (0,0) to (20,0) peaking at z=1 at x=10 is flat there.
            PointMm[] poly =
            {
                M(0, 0), M(5, 0.5), M(10, 0.8), M(15, 0.5), M(20, 0), M(20, 14), M(0, 14),
            };
            var shape = new AreaShape(new[] { M(0, 0), M(20, 0), M(20, 14), M(0, 14) },
                new[] { AreaEdge.FollowsRoad(1, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            HousePlacementResult r = HousePlacement.Place(shape, poly, null);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(0, r.FrontageEdge);
            // Midpoint by length is on the x=10 side of the curve at z~0.8; centre is 4 m further in.
            Assert.AreEqual(10.0, r.Centre.XMetres, 0.6);
            Assert.AreEqual(0.8 + 4.0, r.Centre.ZMetres, 0.3);
        }

        [Test]
        public void CurvedFrontage_SlopedMiddle_HouseRotated()
        {
            // Frontage whose middle segment rises 1:1; house long side follows that segment (45°).
            PointMm[] poly = { M(0, 0), M(10, 0), M(20, 10), M(30, 10), M(30, 40), M(0, 40) };
            var shape = new AreaShape(new[] { M(0, 0), M(30, 10), M(30, 40), M(0, 40) },
                new[] { AreaEdge.FollowsRoad(1, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            HousePlacementResult r = HousePlacement.Place(shape, poly, null);
            Assert.IsTrue(r.Ok);
            // Local Z (long side) runs along the segment direction: 45° off the axes.
            double rot = ((r.RotationDegrees % 90) + 90) % 90;
            Assert.AreEqual(45.0, rot, 0.001);
        }

        [Test]
        public void CurvedNeighbouringEdge_ThatCutsHouse_RefusesNoRoom()
        {
            // A bent side edge flattened so it bows into the house area.
            PointMm[] poly = { M(0, 0), M(10, 0), M(10, 5), M(7.5, 7), M(10, 9), M(10, 12), M(0, 12) };
            var shape = new AreaShape(new[] { M(0, 0), M(10, 0), M(10, 5), M(7.5, 7), M(10, 9), M(10, 12), M(0, 12) },
                new[]
                {
                    AreaEdge.FollowsRoad(1, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight(),
                    AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight(),
                });
            // House x 1..9, z 1..7; vertex (7.5,7) lies on its top edge, edges (10,5)-(7.5,7) cross x=9 inside the rect.
            Assert.AreEqual(HousePlacementRefusal.NoRoom, HousePlacement.Place(shape, poly, null).Refusal);
        }

        [Test]
        public void Deterministic_SameInputSameResult()
        {
            HousePlacementResult a = Run(Rect(10, 12), new[] { F, S, S, S });
            HousePlacementResult b = Run(Rect(10, 12), new[] { F, S, S, S });
            Assert.AreEqual(a.Centre, b.Centre);
            Assert.AreEqual(a.RotationDegrees, b.RotationDegrees);
        }

        [Test]
        public void Degenerate_EmptyOrTinyPolygon_RefusesNoRoom()
        {
            var shape = Shape(Rect(10, 12), F, S, S, S);
            Assert.AreEqual(HousePlacementRefusal.NoRoom, HousePlacement.Place(shape, new PointMm[0], null).Refusal);
            var line = new[] { M(0, 0), M(5, 0) };
            Assert.AreEqual(HousePlacementRefusal.NoRoom,
                HousePlacement.Place(new AreaShape(line, new[] { AreaEdge.FollowsRoad(1, RoadSide.Left), AreaEdge.Straight() }), line, null).Refusal);
        }

        [Test]
        public void Degenerate_ZeroLengthFrontage_RefusesNoRoom()
        {
            PointMm[] v = { M(0, 0), M(0, 0), M(10, 12), M(0, 12) };
            var shape = Shape(v, F, S, S, S);
            Assert.AreEqual(HousePlacementRefusal.NoRoom, HousePlacement.Place(shape, v, null).Refusal);
        }
    }
}
