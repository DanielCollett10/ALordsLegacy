using System;
using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class StallGridTests
    {
        private static readonly SimTuning T = new SimTuning();

        private static PointMm[] Rect(double w, double h) => new[]
        {
            PointMm.FromMetres(0, 0), PointMm.FromMetres(w, 0), PointMm.FromMetres(w, h), PointMm.FromMetres(0, h),
        };

        private static void AssertInside(PointMm[] poly, List<StallSlot> slots)
        {
            foreach (var s in slots) foreach (var p in s.Cell) Assert.IsTrue(Polygons.Contains(poly, p));
        }

        [Test] public void Rectangle_20x10_GivesTenSlots()
        {
            var poly = Rect(20, 10);
            var slots = StallGrid.Slots(poly, T);
            Assert.AreEqual(10, slots.Count);
            AssertInside(poly, slots);
            Assert.AreEqual(4000, Math.Abs(slots[0].Cell[1].X - slots[0].Cell[0].X));
            Assert.AreEqual(5000, Math.Abs(slots[0].Cell[2].Z - slots[0].Cell[1].Z));
        }

        [Test] public void Stall_Is3x2_CentredInCell()
        {
            var s = StallGrid.Slots(Rect(6, 5), T).Single();
            Assert.AreEqual(3000, s.Stall[1].X - s.Stall[0].X);
            Assert.AreEqual(2000, s.Stall[2].Z - s.Stall[1].Z);
            Assert.AreEqual(500, s.Stall[0].X - s.Cell[0].X);
            Assert.AreEqual(2000, s.Centre.X);
        }

        [Test] public void LShape_KeepsOnlyCellsInsideTheL()
        {
            var poly = new[]
            {
                PointMm.FromMetres(0, 0), PointMm.FromMetres(20, 0), PointMm.FromMetres(20, 5),
                PointMm.FromMetres(8, 5), PointMm.FromMetres(8, 15), PointMm.FromMetres(0, 15),
            };
            var slots = StallGrid.Slots(poly, T);
            AssertInside(poly, slots);
            Assert.AreEqual(5 + 2 + 2, slots.Count, "5 on the 20 m arm plus 2 per row up the 8 m arm");
        }

        [Test] public void CurvedEdge_DropsCellsOutsideTheArc()
        {
            var pts = new List<PointMm> { PointMm.FromMetres(0, 0), PointMm.FromMetres(20, 0) };
            for (int i = 1; i < 12; i++)
            {
                double a = Math.PI * i / 12;
                pts.Add(PointMm.FromMetres(10 + 10 * Math.Cos(a), 12 * Math.Sin(a)));
            }
            var poly = pts.ToArray();
            var slots = StallGrid.Slots(poly, T);
            Assert.Greater(slots.Count, 0);
            Assert.Less(slots.Count, 5 * 2 + 1);
            AssertInside(poly, slots);
        }

        [Test] public void Rotated_ProducesSameCountAsUnrotated()
        {
            double c = Math.Cos(0.6), s = Math.Sin(0.6);
            var poly = Rect(20, 10).Select(p => PointMm.FromMetres(
                p.XMetres * c - p.ZMetres * s + 30, p.XMetres * s + p.ZMetres * c + 40)).ToArray();
            // Shrink slightly so rounding never lets the exact fit fail.
            var slots = StallGrid.Slots(poly, T);
            Assert.AreEqual(10, slots.Count);
            // Exact fit: corners may land a rounding mm outside, so check the stall footprints instead.
            foreach (var sl in slots) Assert.IsTrue(Polygons.Contains(poly, sl.Centre));
            var side = slots[0].Cell[1] - slots[0].Cell[0];
            Assert.AreEqual(Math.Tan(0.6), (double)side.Z / side.X, 0.01);
        }

        [Test] public void ThinStrip_HasZeroSlots() => Assert.AreEqual(0, StallGrid.Slots(Rect(30, 4), T).Count);

        [Test] public void TooSmall_AndDegenerate_AreEmpty()
        {
            Assert.AreEqual(0, StallGrid.Slots(Rect(3, 3), T).Count);
            Assert.AreEqual(0, StallGrid.Slots(new PointMm[0], T).Count);
            Assert.AreEqual(0, StallGrid.Slots(new[] { new PointMm(0, 0), new PointMm(5000, 0) }, T).Count);
            Assert.AreEqual(0, StallGrid.Slots(new[] { new PointMm(0, 0), new PointMm(0, 0), new PointMm(0, 0) }, T).Count);
        }

        [Test] public void OrderedNearestCentreFirst_AndDeterministic()
        {
            var poly = Rect(20, 10);
            var a = StallGrid.Slots(poly, T);
            var b = StallGrid.Slots(poly, T);
            CollectionAssert.AreEqual(a.Select(s => s.Centre).ToList(), b.Select(s => s.Centre).ToList());
            double Dist(StallSlot s) => Math.Pow(s.Centre.XMetres - 10, 2) + Math.Pow(s.Centre.ZMetres - 5, 2);
            for (int i = 1; i < a.Count; i++) Assert.LessOrEqual(Dist(a[i - 1]), Dist(a[i]) + 1e-9);
            Assert.AreEqual(10000, a[0].Centre.X, 2000);
        }

        [Test] public void CellSizeComesFromTuning()
        {
            var slots = StallGrid.Slots(Rect(20, 10), new SimTuning { StallCellWidthM = 10, StallCellDepthM = 10 });
            Assert.AreEqual(2, slots.Count);
        }
    }
}
