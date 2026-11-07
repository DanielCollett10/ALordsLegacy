using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class PolygonOverlapTests
    {
        private static PointMm[] Rect(double x0, double z0, double x1, double z1) => new[]
        {
            PointMm.FromMetres(x0, z0), PointMm.FromMetres(x1, z0), PointMm.FromMetres(x1, z1), PointMm.FromMetres(x0, z1),
        };

        private static PointMm[] L(double ox) => new[]
        {
            PointMm.FromMetres(ox, 0), PointMm.FromMetres(ox + 10, 0), PointMm.FromMetres(ox + 10, 4),
            PointMm.FromMetres(ox + 4, 4), PointMm.FromMetres(ox + 4, 10), PointMm.FromMetres(ox, 10),
        };

        [Test] public void Disjoint_IsZero() =>
            Assert.AreEqual(0.0, PolygonOverlap.AreaM2(Rect(0, 0, 5, 5), Rect(20, 20, 25, 25)));

        [Test] public void PartialOverlap_KnownArea() =>
            Assert.AreEqual(25.0, PolygonOverlap.AreaM2(Rect(0, 0, 10, 10), Rect(5, 5, 15, 15)), 1e-6);

        [Test] public void OneInsideOther_IsInnerArea()
        {
            Assert.AreEqual(16.0, PolygonOverlap.AreaM2(Rect(0, 0, 10, 10), Rect(3, 3, 7, 7)), 1e-6);
            Assert.AreEqual(16.0, PolygonOverlap.AreaM2(Rect(3, 3, 7, 7), Rect(0, 0, 10, 10)), 1e-6);
        }

        [Test] public void ConcaveVsConcave_KnownArea()
        {
            // a = [0,10]x[0,4] + [0,4]x[0,10]; b = same shifted 2 m east.
            // overlap = [2,10]x[0,4] (32) + [2,4]x[4,10] (12) = 44
            Assert.AreEqual(44.0, PolygonOverlap.AreaM2(L(0), L(2)), 1e-6);
        }

        [Test] public void SharedFullEdge_UnderTolerance() =>
            Assert.Less(PolygonOverlap.AreaM2(Rect(0, 0, 10, 10), Rect(10, 0, 20, 10)), 0.001);

        [Test] public void SharedPartialEdge_UnderTolerance() =>
            Assert.Less(PolygonOverlap.AreaM2(Rect(0, 0, 10, 10), Rect(10, 5, 20, 15)), 0.001);

        [Test] public void TouchingSingleVertex_UnderTolerance() =>
            Assert.Less(PolygonOverlap.AreaM2(Rect(0, 0, 10, 10), Rect(10, 10, 20, 20)), 0.001);

        [Test] public void SameFlattenedCurve_ShareNoArea()
        {
            var curve = new List<PointMm>();
            for (int i = 0; i <= 20; i++)
                curve.Add(PointMm.FromMetres(i, 3.0 * System.Math.Sin(i / 3.0)));
            var below = new List<PointMm>(curve) { PointMm.FromMetres(20, -20), PointMm.FromMetres(0, -20) };
            var above = new List<PointMm>(curve) { PointMm.FromMetres(20, 20), PointMm.FromMetres(0, 20) };
            Assert.Less(PolygonOverlap.AreaM2(below, above), 0.001);
            Assert.IsFalse(PolygonOverlap.Overlaps(below, above, 0.1));
        }

        [Test] public void Overlaps_TrueOnlyWhenAreaOverTolerance()
        {
            var a = Rect(0, 0, 10, 10);
            Assert.IsTrue(PolygonOverlap.Overlaps(a, Rect(5, 5, 15, 15), 24.9));
            Assert.IsFalse(PolygonOverlap.Overlaps(a, Rect(5, 5, 15, 15), 25.1));
        }

        [Test] public void DegenerateInputs_AreZero()
        {
            var a = Rect(0, 0, 10, 10);
            var flat = new[] { new PointMm(0, 0), new PointMm(5000, 5000), new PointMm(10000, 10000) };
            Assert.AreEqual(0.0, PolygonOverlap.AreaM2(a, new PointMm[0]));
            Assert.AreEqual(0.0, PolygonOverlap.AreaM2(a, new[] { new PointMm(1, 1) }));
            Assert.AreEqual(0.0, PolygonOverlap.AreaM2(a, new[] { new PointMm(0, 0), new PointMm(5000, 5000) }));
            Assert.AreEqual(0.0, PolygonOverlap.AreaM2(a, flat));
            Assert.AreEqual(0.0, PolygonOverlap.AreaM2(a, null));
            Assert.AreEqual(0.0, PolygonOverlap.AreaM2(flat, flat));
        }

        [Test] public void IdenticalPolygons_GiveFullArea() =>
            Assert.AreEqual(100.0, PolygonOverlap.AreaM2(Rect(0, 0, 10, 10), Rect(0, 0, 10, 10)), 1e-6);

        [Test] public void SameInputs_BitIdentical()
        {
            var a = L(0); var b = L(2.37);
            double r1 = PolygonOverlap.AreaM2(a, b), r2 = PolygonOverlap.AreaM2(a, b);
            Assert.AreEqual(System.BitConverter.DoubleToInt64Bits(r1), System.BitConverter.DoubleToInt64Bits(r2));
        }
    }
}
