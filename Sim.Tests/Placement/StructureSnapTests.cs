using System;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Placement;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Placement
{
    /// <summary>Flush snap of a fixed structure's plot (T-473, PL-14).</summary>
    public class StructureSnapTests
    {
        private const string G = ModuleTypes.GranaryId;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld Flat()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning());
            w.Inventory.Produce(w.Inventory.AddStockpile(M(5, 5)), Items.Log, 40);
            return w;
        }

        private static void AssertPlaceable(SimWorld w, StructureSnapResult s)
        {
            StructurePlacementResult r = StructurePlacement.Preview(w, G, s.Centre, s.RotationDegrees);
            Assert.IsTrue(r.Ok, r.Text);
        }

        private static double Wrap90(double deg) => Math.Abs(deg - 90 * Math.Round(deg / 90));

        [Test]
        public void NothingInReach_ReturnsInputUnchanged()
        {
            SimWorld w = Flat();
            Assert.IsTrue(w.Execute(new PlaceStructure(G, M(50, 50), 0)).IsAccepted);
            StructureSnapResult s = StructureSnap.Snap(w, G, M(150, 50), 17, 3);
            Assert.IsFalse(s.Snapped);
            Assert.AreEqual(M(150, 50), s.Centre);
            Assert.AreEqual(17.0, s.RotationDegrees);
        }

        [Test]
        public void EmptyWorld_UnknownType_BadInputs_ReturnUnchanged()
        {
            SimWorld w = Flat();
            Assert.IsFalse(StructureSnap.Snap(w, G, M(50, 50), 0, 3).Snapped);
            Assert.IsFalse(StructureSnap.Snap(w, "nope", M(50, 50), 0, 3).Snapped);
            Assert.IsFalse(StructureSnap.Snap(w, G, M(50, 50), double.NaN, 3).Snapped);
            Assert.IsFalse(StructureSnap.Snap(w, G, M(50, 50), 0, 0).Snapped);
        }

        [Test]
        public void AgainstHousePlotStylePlot_SlidesFlushAndTurnsParallel()
        {
            SimWorld w = Flat();
            Assert.IsTrue(w.Execute(new PlaceStructure(G, M(50, 50), 0)).IsAccepted); // plot x 41..59
            // Ghost 2 m inside the east edge's reach, turned 10 degrees: should turn to 0 and sit at x = 59 + 9 = 68.
            StructureSnapResult s = StructureSnap.Snap(w, G, M(66, 52), 10, 12);
            Assert.IsTrue(s.Snapped);
            Assert.AreEqual(0.0, Wrap90(s.RotationDegrees), 1e-6);
            Assert.AreEqual(68.0, s.Centre.XMetres, 0.01);
            Assert.AreEqual(52.0, s.Centre.ZMetres, 0.01);
            AssertPlaceable(w, s);
        }

        [Test]
        public void AgainstPlotFromOverlappingSide_PushesOutFlush()
        {
            SimWorld w = Flat();
            Assert.IsTrue(w.Execute(new PlaceStructure(G, M(50, 50), 0)).IsAccepted);
            StructureSnapResult s = StructureSnap.Snap(w, G, M(70, 50), 0, 12); // overlaps by 2 m
            Assert.IsTrue(s.Snapped);
            Assert.AreEqual(68.0, s.Centre.XMetres, 0.01);
            AssertPlaceable(w, s);
        }

        [Test]
        public void AgainstMarketEdge_SnapsFlush()
        {
            SimWorld w = Flat();
            var shape = AreaShape.Straight(new[] { M(20, 20), M(40, 20), M(40, 40), M(20, 40) });
            Assert.IsTrue(w.Execute(new DesignateArea(shape, AreaTypes.MarketId)).IsAccepted);
            // North edge z = 40; plot half depth 8 (rotation 0): centre z = 48. Ghost 3 m off, rotated 80 degrees.
            StructureSnapResult s = StructureSnap.Snap(w, G, M(30, 51), 80, 6);
            Assert.IsTrue(s.Snapped);
            Assert.AreEqual(0.0, Wrap90(s.RotationDegrees), 1e-6);
            AssertPlaceable(w, s);
            var fp = new Footprint(0, ModuleTypes.Granary, s.Centre, s.RotationDegrees);
            double minZ = double.MaxValue;
            foreach (PointMm p in fp.PlotPolygon) minZ = Math.Min(minZ, p.ZMetres);
            Assert.AreEqual(40.0, minZ, 0.01);
        }

        [Test]
        public void AgainstStraightRoadSide_SnapsFlushAndPlaceable()
        {
            SimWorld w = Flat();
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(10, 50), M(90, 50) }, new[] { M(10, 50), M(50, 50), M(90, 50) });
            StructureSnapResult s = StructureSnap.Snap(w, G, M(50, 62), 5, 6);
            Assert.IsTrue(s.Snapped);
            Assert.AreEqual(0.0, Wrap90(s.RotationDegrees), 1e-6);
            AssertPlaceable(w, s);
            Assert.Less(s.Centre.ZMetres, 62.0);
        }

        [Test]
        public void AgainstCurvedRoadSide_SnapsParallelToNearestPieceAndPlaceable()
        {
            SimWorld w = Flat();
            // Gentle arc (radius 300 m) bending away from the ghost: the plot sits on the outside of the curve.
            var samples = new System.Collections.Generic.List<PointMm>();
            for (int i = 0; i <= 16; i++)
            {
                double a = (-8 + i) * 0.01;
                samples.Add(M(50 + 300 * Math.Sin(a), 40 + 300 * (Math.Cos(a) - 1)));
            }
            PointMm[] pts = samples.ToArray();
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal, new[] { pts[0], pts[16] }, pts);
            // Ghost above the road (outside of a curve that bends down at its ends).
            StructureSnapResult s = StructureSnap.Snap(w, G, M(50, 48), 20, 8);
            Assert.IsTrue(s.Snapped);
            Assert.AreNotEqual(20.0, s.RotationDegrees);
            AssertPlaceable(w, s);
        }

        [Test]
        public void TwoEdgesAtCorner_SlidesFlushToBoth()
        {
            SimWorld w = Flat();
            Assert.IsTrue(w.Execute(new PlaceStructure(G, M(30, 30), 0)).IsAccepted); // plot x 21..39, z 22..38
            // A road along z = 60 (side at ~58.75); a second granary plot is not needed: corner of plot + road side.
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(10, 60), M(90, 60) }, new[] { M(10, 60), M(50, 60), M(90, 60) });
            // Ghost north-east of the plot, reaching the plot's east edge and the road's lower side.
            StructureSnapResult s = StructureSnap.Snap(w, G, M(50, 44), 0, 8);
            Assert.IsTrue(s.Snapped);
            AssertPlaceable(w, s);
            // Flush to the plot's east edge (x = 39): centre x = 48.
            Assert.AreEqual(48.0, s.Centre.XMetres, 0.01);
            // Flush to the road's south side: centre z = side - 8.
            double side = 60 - RoadTypes.DefaultCatalog.Find(RoadTypes.DirtPathId).WidthM / 2.0;
            Assert.AreEqual(side - 8.0, s.Centre.ZMetres, 0.01);
        }

        [Test]
        public void ThreeNeighbours_SnapsToNearestAndStaysClear()
        {
            SimWorld w = Flat();
            Assert.IsTrue(w.Execute(new PlaceStructure(G, M(30, 50), 0)).IsAccepted);
            Assert.IsTrue(w.Execute(new PlaceStructure(G, M(70, 50), 0)).IsAccepted); // 22 m gap between plots
            StructureSnapResult s = StructureSnap.Snap(w, G, M(51, 50), 0, 6);
            Assert.IsTrue(s.Snapped);
            AssertPlaceable(w, s);
        }
    }
}
