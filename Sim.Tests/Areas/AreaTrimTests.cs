using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Areas
{
    /// <summary>AR-34: building areas are fitted around neighbouring building areas.</summary>
    public class AreaTrimTests
    {
        private const string T = AreaTypes.TestId;
        private const string W = AreaTypes.TestWorkId;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);
        private static SimWorld Ramp() => SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());

        private static AreaShape Poly(params PointMm[] p) => AreaShape.Straight(p);

        // Neighbour: square 20..40 (400 m2).
        private static AreaRecord AddNeighbour(SimWorld w, double x0 = 20, double z0 = 20, double s = 20)
        {
            AreaShape shape = Poly(M(x0, z0), M(x0 + s, z0), M(x0 + s, z0 + s), M(x0, z0 + s));
            var poly = AreaFlattener.Flatten(shape, w.Roads, w.Tuning);
            var rec = new AreaRecord(w.Areas.NextId(), T, w.Areas.NextName(w.AreaTypes.Find(T)), shape, poly);
            w.Areas.Add(rec);
            return rec;
        }

        // Point 2 (index 1) is the neighbour's top-right corner; edge 2->3 cuts the neighbour.
        private static AreaShape Screenshot7(double cornerX = 40, double cornerZ = 40) =>
            Poly(M(0, 45), M(cornerX, cornerZ), M(30, 10), M(0, 10));

        [Test]
        public void Screenshot7_EdgeCutsNeighbour_IsFittedAroundNeighbour()
        {
            SimWorld w = Ramp();
            AreaRecord n = AddNeighbour(w);
            AreaShape shape = Screenshot7();
            AreaPreview p = AreaValidator.Validate(w, shape, T);
            Assert.AreEqual(AreaRefusal.Valid, p.Reason, p.ReasonText);
            CollectionAssert.AreEqual(new[] { n.Name }, p.FittedAround);
            double overlap = PolygonOverlap.AreaM2(p.Polygon, n.Polygon);
            Assert.LessOrEqual(overlap, 0.1);
            double raw = System.Math.Abs(Polygons.SignedAreaM2(AreaFlattener.Flatten(shape, w.Roads, w.Tuning)));
            Assert.Less(p.AreaM2, raw);
            Assert.AreEqual(System.Math.Abs(Polygons.SignedAreaM2(p.Polygon)), p.AreaM2, 1e-6);
        }

        [Test]
        public void Split_PlotCrossedByNeighbour_RefusedOverlapsNeighbour()
        {
            SimWorld w = Ramp();
            AreaRecord n = AddNeighbour(w);
            AreaPreview p = AreaValidator.Validate(w, Poly(M(0, 25), M(60, 25), M(60, 35), M(0, 35)), T);
            Assert.AreEqual(AreaRefusal.OverlapsArea, p.Reason);
            Assert.AreEqual("overlaps " + n.Name, p.ReasonText);
            Assert.IsEmpty(p.FittedAround);
        }

        [Test]
        public void VertexTenCmInsideNeighbour_Refused()
        {
            SimWorld w = Ramp();
            AreaRecord n = AddNeighbour(w);
            AreaPreview p = AreaValidator.Validate(w, Screenshot7(39.9, 39.9), T);
            Assert.AreEqual(AreaRefusal.OverlapsArea, p.Reason);
            Assert.AreEqual("overlaps " + n.Name, p.ReasonText);
        }

        [Test]
        public void WorkArea_OverBuildingArea_IsNotTrimmed()
        {
            SimWorld w = Ramp();
            AddNeighbour(w);
            AreaShape shape = Screenshot7();
            AreaPreview p = AreaValidator.Validate(w, shape, W);
            Assert.AreEqual(AreaRefusal.Valid, p.Reason, p.ReasonText);
            Assert.IsEmpty(p.FittedAround);
            Assert.AreEqual(4, p.Polygon.Count);
        }

        [Test]
        public void TouchingOnly_IsUnchanged()
        {
            SimWorld w = Ramp();
            AddNeighbour(w);
            AreaPreview p = AreaValidator.Validate(w, Poly(M(40, 20), M(60, 20), M(60, 40), M(40, 40)), T);
            Assert.AreEqual(AreaRefusal.Valid, p.Reason, p.ReasonText);
            Assert.IsEmpty(p.FittedAround);
            Assert.AreEqual(4, p.Polygon.Count);
            Assert.AreEqual(400.0, p.AreaM2, 1e-6);
        }

        [Test]
        public void TwoNeighbours_BothListedInIdOrder()
        {
            SimWorld w = Ramp();
            AreaRecord a = AddNeighbour(w);
            AreaRecord b = AddNeighbour(w, -5, 20, 10); // -5..5 x 20..30, cuts the plot's left edge
            AreaPreview p = AreaValidator.Validate(w, Screenshot7(), T);
            Assert.AreEqual(AreaRefusal.Valid, p.Reason, p.ReasonText);
            CollectionAssert.AreEqual(new[] { a.Name, b.Name }, p.FittedAround);
            Assert.LessOrEqual(PolygonOverlap.AreaM2(p.Polygon, b.Polygon), 0.1);
        }

        [Test]
        public void Designate_StoresTrimmedOutline_AndKeepsTreesInTrimmedOffPart()
        {
            SimWorld w = Ramp();
            AddNeighbour(w);
            w.Trees.Add(new TreeId(9001), 36f, 32f, 5);  // in plot and neighbour: trimmed off
            w.Trees.Add(new TreeId(9002), 10f, 20f, 5);  // in plot only
            CommandResult r = w.Execute(new DesignateArea(Screenshot7(), T));
            Assert.IsTrue(r.IsAccepted, r.Text);
            AreaRecord rec = w.Areas.Get((int)r.Created[0]);
            Assert.Greater(rec.Polygon.Count, 4);
            var removed = r.RemovedTreeIds.ToList();
            CollectionAssert.Contains(removed, 9002L);
            CollectionAssert.DoesNotContain(removed, 9001L);
        }
    }
}
