using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Geometry;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Areas
{
    public class AreaUntanglerTests
    {
        private static PointMm P(double x, double z) { return PointMm.FromMetres(x, z); }

        [Test]
        public void HourglassBecomesSquareWithFirstPointKept()
        {
            var pts = new[] { P(40, 40), P(80, 80), P(80, 40), P(40, 80) };
            Assert.IsFalse(Polygons.IsSimple(pts));
            Assert.IsTrue(AreaUntangler.TryUntangle(pts, out var order));
            Assert.AreEqual(0, order[0]);
            var fixedPoly = new PointMm[4];
            for (int i = 0; i < 4; i++) fixedPoly[i] = pts[order[i]];
            Assert.IsTrue(Polygons.IsSimple(fixedPoly));
        }

        [Test]
        public void SimplePolygonIsLeftAlone()
        {
            var pts = new[] { P(40, 40), P(80, 40), P(80, 80), P(40, 80) };
            Assert.IsFalse(AreaUntangler.TryUntangle(pts, out var order));
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, order);
        }

        [Test]
        public void DuplicatePointsCannotBeFixed()
        {
            var pts = new[] { P(40, 40), P(40, 40), P(80, 80), P(40, 80) };
            Assert.IsFalse(AreaUntangler.TryUntangle(pts, out _));
        }
    }
}
