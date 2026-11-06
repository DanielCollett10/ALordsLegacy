using LordsLegacy.Sim.Terrain;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Terrain
{
    public class HeightFieldTests
    {
        // 5x4 nodes, 2 m cells, origin (10,20): height = 0.5 * local x (ramp along x).
        private static HeightGrid Ramp()
        {
            var h = new float[5 * 4];
            for (int z = 0; z < 4; z++)
                for (int x = 0; x < 5; x++) h[z * 5 + x] = 0.5f * (x * 2f);
            return new HeightGrid(10f, 20f, 2f, 5, 4, h);
        }

        [Test]
        public void FlatGrid_HasZeroSlopeAndConstantHeight()
        {
            var g = new HeightGrid(0, 0, 2f, 3, 3, new float[] { 7, 7, 7, 7, 7, 7, 7, 7, 7 });
            Assert.AreEqual(0f, g.SlopeDegAt(1.3f, 2.7f), 1e-4);
            Assert.AreEqual(7f, g.HeightAt(1.3f, 2.7f), 1e-4);
        }

        [Test]
        public void Ramp_GivesExpectedHeightAndSlope()
        {
            var g = Ramp();
            Assert.AreEqual(2.5f, g.HeightAt(15f, 23f), 1e-4);
            float expected = (float)(System.Math.Atan(0.5) * 180 / System.Math.PI);
            Assert.AreEqual(expected, g.SlopeDegAt(15f, 23f), 1e-3);
            Assert.AreEqual(expected, g.SlopeDegAt(10f, 20f), 1e-3, "corner");
            Assert.AreEqual(expected, g.SlopeDegAt(18f, 26f), 1e-3, "far corner");
        }

        [Test]
        public void Bilinear_InterpolatesCellCentre()
        {
            var g = new HeightGrid(0, 0, 2f, 2, 2, new float[] { 0, 4, 8, 12 });
            Assert.AreEqual(6f, g.HeightAt(1f, 1f), 1e-4);
        }

        [Test]
        public void Outside_IsClampedAndNotContained()
        {
            var g = Ramp();
            Assert.IsFalse(g.Contains(9f, 25f));
            Assert.IsTrue(g.Contains(10f, 20f));
            Assert.IsTrue(g.Contains(18f, 26f));
            Assert.AreEqual(0f, g.HeightAt(-100f, 0f), 1e-4);
            Assert.AreEqual(4f, g.HeightAt(500f, 500f), 1e-4);
            Assert.AreEqual(g.SlopeDegAt(18f, 26f), g.SlopeDegAt(500f, 500f), 1e-4);
        }

        [Test]
        public void Bounds_MatchGridExtent()
        {
            var b = Ramp().Bounds;
            Assert.AreEqual(10f, b.MinX); Assert.AreEqual(18f, b.MaxX);
            Assert.AreEqual(20f, b.MinZ); Assert.AreEqual(26f, b.MaxZ);
        }

        [Test]
        public void Constructor_RejectsBadInput()
        {
            Assert.Throws<System.ArgumentException>(() => new HeightGrid(0, 0, 1f, 2, 2, new float[3]));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new HeightGrid(0, 0, 0f, 2, 2, new float[4]));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new HeightGrid(0, 0, 1f, 1, 2, new float[2]));
        }

        [Test]
        public void FlatHeightField_IsFlatEverywhere()
        {
            IHeightField f = new FlatHeightField();
            Assert.AreEqual(0f, f.HeightAt(1e6f, -1e6f));
            Assert.AreEqual(0f, f.SlopeDegAt(3, 4));
            Assert.IsTrue(f.Contains(-5e8f, 5e8f));
            Assert.IsTrue(f.Bounds.IsEmpty);
            Assert.IsFalse(f.Bounds.Contains(0, 0));
        }
    }
}
