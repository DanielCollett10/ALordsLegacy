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

        private static void AssertPadded(PointMm[] poly, List<StallSlot> slots)
        {
            foreach (var s in slots) foreach (var p in s.Cell)
            {
                Assert.IsTrue(Polygons.Contains(poly, p));
                for (int i = 0; i < poly.Length; i++)
                    Assert.GreaterOrEqual(Segments.DistanceToPoint(poly[i], poly[(i + 1) % poly.Length], p, out _), 1990.0);
            }
        }

        [Test] public void Rectangle_20x10_KeepsTwoMetrePadding_GivesFourSlots()
        {
            var poly = Rect(20, 10);
            var slots = StallGrid.Slots(poly, T);
            Assert.AreEqual(4, slots.Count, "usable 16 x 6 m after padding");
            AssertPadded(poly, slots);
            Assert.AreEqual(4000, Math.Abs(slots[0].Cell[1].X - slots[0].Cell[0].X));
            Assert.AreEqual(5000, Math.Abs(slots[0].Cell[2].Z - slots[0].Cell[1].Z));
        }

        [Test] public void Stall_Is3x2_CentredInCell()
        {
            var s = StallGrid.Slots(Rect(10, 9), T).Single();
            Assert.AreEqual(3000, s.Stall[1].X - s.Stall[0].X);
            Assert.AreEqual(2000, s.Stall[2].Z - s.Stall[1].Z);
            Assert.AreEqual(500, s.Stall[0].X - s.Cell[0].X);
            Assert.AreEqual(5000, s.Centre.X, "single cell is centred across the 10 m edge");
        }

        [Test] public void Grid_IsCentredInUsableArea()
        {
            var slots = StallGrid.Slots(Rect(30, 16), T);
            Assert.AreEqual(12, slots.Count);
            long minX = slots.Min(s => s.Cell.Min(p => p.X)), maxX = slots.Max(s => s.Cell.Max(p => p.X));
            Assert.AreEqual(3000, minX, "26 m usable, 24 m used: 1 m extra each side of the 2 m padding");
            Assert.AreEqual(27000, maxX);
        }

        [Test] public void LShape_KeepsOnlyCellsInsideTheL()
        {
            var poly = new[]
            {
                PointMm.FromMetres(0, 0), PointMm.FromMetres(30, 0), PointMm.FromMetres(30, 12),
                PointMm.FromMetres(14, 12), PointMm.FromMetres(14, 30), PointMm.FromMetres(0, 30),
            };
            var slots = StallGrid.Slots(poly, T);
            AssertPadded(poly, slots);
            Assert.AreEqual(14, slots.Count, "6 on the bottom row plus 2 on each of the 4 rows above");
        }

        [Test] public void CurvedEdge_DropsCellsOutsideTheArc()
        {
            var pts = new List<PointMm> { PointMm.FromMetres(0, 0), PointMm.FromMetres(40, 0) };
            for (int i = 1; i < 24; i++)
            {
                double a = Math.PI * i / 24;
                pts.Add(PointMm.FromMetres(20 + 20 * Math.Cos(a), 24 * Math.Sin(a)));
            }
            var poly = pts.ToArray();
            var slots = StallGrid.Slots(poly, T);
            Assert.Greater(slots.Count, 0);
            Assert.Less(slots.Count, 9 * 4, "arc removes corner cells of the 9 x 4 grid");
            AssertPadded(poly, slots);
        }

        [Test] public void Rotated_ProducesSameCountAsUnrotated()
        {
            double c = Math.Cos(0.6), s = Math.Sin(0.6);
            var poly = Rect(30, 16).Select(p => PointMm.FromMetres(
                p.XMetres * c - p.ZMetres * s + 30, p.XMetres * s + p.ZMetres * c + 40)).ToArray();
            var slots = StallGrid.Slots(poly, T);
            Assert.AreEqual(12, slots.Count);
            AssertPadded(poly, slots);
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
            Assert.AreEqual(10000, a[0].Centre.X, 3000);
        }

        [Test] public void CellSizeComesFromTuning()
        {
            var slots = StallGrid.Slots(Rect(30, 16), new SimTuning { StallCellWidthM = 10, StallCellDepthM = 10 });
            Assert.AreEqual(2, slots.Count);
        }
    }
}
