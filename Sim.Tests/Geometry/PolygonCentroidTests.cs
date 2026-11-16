using System;
using LordsLegacy.Sim.Geometry;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Geometry
{
    /// <summary>Polygons.Centroid: area centre, and the degenerate cases (T-457, lesson T-084).</summary>
    public class PolygonCentroidTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        [Test]
        public void Centroid_Square_IsItsCentre()
        {
            PointMm c = Polygons.Centroid(new[] { M(0, 0), M(20, 0), M(20, 20), M(0, 20) });
            Assert.AreEqual(10000, c.X);
            Assert.AreEqual(10000, c.Z);
        }

        [Test]
        public void Centroid_ClockwiseSquare_IsSameAsCounterClockwise()
        {
            PointMm c = Polygons.Centroid(new[] { M(0, 0), M(0, 20), M(20, 20), M(20, 0) });
            Assert.AreEqual(10000, c.X);
            Assert.AreEqual(10000, c.Z);
        }

        [Test]
        public void Centroid_LShape_IsAreaWeighted()
        {
            // 20x10 block (centre 10,5, area 200) plus 10x10 block (centre 5,15, area 100): (8.33, 8.33) m.
            PointMm c = Polygons.Centroid(new[] { M(0, 0), M(20, 0), M(20, 10), M(10, 10), M(10, 20), M(0, 20) });
            Assert.AreEqual(8333, c.X);
            Assert.AreEqual(8333, c.Z);
        }

        [Test]
        public void Centroid_SinglePoint_IsThatPoint()
        {
            PointMm c = Polygons.Centroid(new[] { M(7, 3) });
            Assert.AreEqual(7000, c.X);
            Assert.AreEqual(3000, c.Z);
        }

        [Test]
        public void Centroid_RepeatedPoint_IsThatPoint()
        {
            PointMm c = Polygons.Centroid(new[] { M(4, 9), M(4, 9), M(4, 9) });
            Assert.AreEqual(4000, c.X);
            Assert.AreEqual(9000, c.Z);
        }

        [Test]
        public void Centroid_TwoPoints_IsMidpoint()
        {
            PointMm c = Polygons.Centroid(new[] { M(0, 0), M(10, 4) });
            Assert.AreEqual(5000, c.X);
            Assert.AreEqual(2000, c.Z);
        }

        [Test]
        public void Centroid_CollinearPoints_IsBoundingBoxMidpoint()
        {
            PointMm c = Polygons.Centroid(new[] { M(0, 0), M(10, 0), M(4, 0) });
            Assert.AreEqual(5000, c.X);
            Assert.AreEqual(0, c.Z);
        }

        [Test]
        public void Centroid_OverlappingBackAndForthEdge_IsBoundingBoxMidpoint()
        {
            // Zero-area "polygon" that doubles back over itself.
            PointMm c = Polygons.Centroid(new[] { M(0, 0), M(10, 0), M(10, 0), M(2, 0), M(0, 0) });
            Assert.AreEqual(5000, c.X);
            Assert.AreEqual(0, c.Z);
        }

        [Test]
        public void Centroid_Empty_Throws()
        {
            Assert.Throws<ArgumentException>(() => Polygons.Centroid(new PointMm[0]));
        }
    }
}
