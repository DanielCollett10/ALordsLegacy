using System;
using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Geometry;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class TriangulatorTests
    {
        private static PointMm P(long x, long z) => new PointMm(x, z);

        private static void AssertValid(PointMm[] poly)
        {
            var tris = Triangulator.EarClip(poly);
            Assert.AreEqual(poly.Length - 2, tris.Count, "triangle count");
            Assert.AreEqual(Math.Abs(Polygons.SignedArea2Mm(poly)), tris.Sum(t => t.Area2Mm), "total area");
            Assert.IsTrue(tris.All(t => t.Area2Mm >= 0), "all triangles non-negative");
        }

        [Test] public void Convex_Square() => AssertValid(new[] { P(0, 0), P(10, 0), P(10, 10), P(0, 10) });

        [Test] public void Convex_Hexagon() =>
            AssertValid(new[] { P(10, 0), P(20, 0), P(30, 10), P(20, 20), P(10, 20), P(0, 10) });

        [Test] public void Concave_L() =>
            AssertValid(new[] { P(0, 0), P(20, 0), P(20, 10), P(10, 10), P(10, 20), P(0, 20) });

        [Test] public void Concave_U() =>
            AssertValid(new[] { P(0, 0), P(30, 0), P(30, 20), P(20, 20), P(20, 5), P(10, 5), P(10, 20), P(0, 20) });

        [Test]
        public void Concave_Star()
        {
            var pts = new List<PointMm>();
            for (int i = 0; i < 10; i++)
            {
                double r = i % 2 == 0 ? 100000 : 40000, a = Math.PI * 2 * i / 10;
                pts.Add(P((long)Math.Round(r * Math.Cos(a)), (long)Math.Round(r * Math.Sin(a))));
            }
            AssertValid(pts.ToArray());
        }

        [Test] public void Clockwise_InputIsNormalised() =>
            AssertValid(new[] { P(0, 0), P(0, 20), P(10, 20), P(10, 10), P(20, 10), P(20, 0) });

        [Test] public void CollinearPoints_OnEdges() =>
            AssertValid(new[] { P(0, 0), P(5, 0), P(10, 0), P(10, 5), P(10, 10), P(5, 10), P(0, 10), P(0, 5) });

        [Test] public void CollinearPoints_OnReflexSide() =>
            AssertValid(new[] { P(0, 0), P(10, 0), P(20, 0), P(20, 10), P(10, 10), P(10, 20), P(10, 30), P(0, 30), P(0, 15) });

        [Test] public void VeryThinEar_Sliver() =>
            AssertValid(new[] { P(0, 0), P(1000000, 0), P(1000000, 1), P(0, 1) });

        [Test] public void VeryThinTriangle_NearlyCollinear() =>
            AssertValid(new[] { P(0, 0), P(1000000, 1), P(2000000, 0) });

        [Test] public void Triangle_ReturnsOne() => AssertValid(new[] { P(0, 0), P(4, 0), P(0, 3) });

        [Test] public void ZeroAreaPolygon_ReturnsEmpty() =>
            Assert.AreEqual(0, Triangulator.EarClip(new[] { P(0, 0), P(5, 0), P(10, 0) }).Count);

        [Test] public void FewerThanThreePoints_Throws() =>
            Assert.Throws<ArgumentException>(() => Triangulator.EarClip(new[] { P(0, 0), P(1, 1) }));

        [Test]
        public void FlattenedCurve_200Points()
        {
            // Upper wavy curve left to right (100 pts), lower curve right to left (100 pts).
            var pts = new List<PointMm>();
            for (int i = 0; i < 100; i++)
            {
                double t = i / 99.0;
                pts.Add(P((long)Math.Round(t * 50000), (long)Math.Round(10000 + 8000 * Math.Sin(t * Math.PI * 2))));
            }
            for (int i = 99; i >= 0; i--)
            {
                double t = i / 99.0;
                pts.Add(P((long)Math.Round(t * 50000), (long)Math.Round(2000 * Math.Sin(t * Math.PI * 3))));
            }
            var arr = pts.ToArray();
            Assert.AreEqual(200, arr.Length);
            Assert.IsTrue(Polygons.IsSimple(arr));
            AssertValid(arr);
        }
    }
}
