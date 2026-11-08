using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class PolylineSpacingTests
    {
        private static PointMm P(long x, long z) => new PointMm(x, z);

        [Test]
        public void PointsEvery_StraightLine_PointsAtZeroThenEachSpacing()
        {
            // 0 m to 20 m along X, spacing 5 m: 0, 5, 10, 15, 20 m (end included when it lands exactly).
            var line = new[] { P(0, 0), P(20_000, 0) };
            var pts = Polylines.PointsEvery(line, 5);
            CollectionAssert.AreEqual(new[] { P(0, 0), P(5000, 0), P(10_000, 0), P(15_000, 0), P(20_000, 0) }, pts);
        }

        [Test]
        public void PointsEvery_NothingPastTheEnd_WhenLengthIsNotAMultiple()
        {
            // 12 m long, spacing 5 m: 0, 5, 10 only.
            var pts = Polylines.PointsEvery(new[] { P(0, 0), P(12_000, 0) }, 5);
            CollectionAssert.AreEqual(new[] { P(0, 0), P(5000, 0), P(10_000, 0) }, pts);
        }

        [Test]
        public void PointsEvery_SpacingLongerThanLine_ReturnsOnlyStart()
        {
            var pts = Polylines.PointsEvery(new[] { P(0, 0), P(3000, 0) }, 10);
            CollectionAssert.AreEqual(new[] { P(0, 0) }, pts);
        }

        [Test]
        public void PointsEvery_WalksAcrossBendsByArcLength()
        {
            // L-shape: 10 m east then 10 m north. Spacing 7 m: arc 0, 7 (on first leg), 14 (on second leg, 4 m up).
            var pts = Polylines.PointsEvery(new[] { P(0, 0), P(10_000, 0), P(10_000, 10_000) }, 7);
            CollectionAssert.AreEqual(new[] { P(0, 0), P(7000, 0), P(10_000, 4000) }, pts);
        }

        [Test]
        public void PointsEvery_ArcLengthPointsLieOnTheLine()
        {
            var line = new[] { P(0, 0), P(3000, 4000), P(3000, 9000) }; // 5 m then 5 m
            var pts = Polylines.PointsEvery(line, 2);
            Assert.AreEqual(6, pts.Count); // arc 0,2,4,6,8,10
            Assert.AreEqual(P(1200, 1600), pts[1]); // arc 2 m along the first leg
            Assert.AreEqual(P(3000, 4000 + 1000), pts[3] /* arc 6: 1 m past the bend */ );
            Assert.AreEqual(P(3000, 9000), pts[5]);
        }

        [Test]
        public void PointsEvery_RepeatedVertices_AreSkippedAndDoNotDuplicate()
        {
            var line = new[] { P(0, 0), P(0, 0), P(4000, 0), P(4000, 0), P(8000, 0) };
            var pts = Polylines.PointsEvery(line, 4);
            CollectionAssert.AreEqual(new[] { P(0, 0), P(4000, 0), P(8000, 0) }, pts);
        }

        [Test]
        public void PointsEvery_ZeroLengthPolyline_ReturnsEmpty()
        {
            Assert.IsEmpty(Polylines.PointsEvery(new[] { P(5, 5), P(5, 5), P(5, 5) }, 1));
        }

        [Test]
        public void PointsEvery_OnePointPolyline_ReturnsEmpty()
        {
            Assert.IsEmpty(Polylines.PointsEvery(new[] { P(100, 100) }, 1));
        }

        [Test]
        public void PointsEvery_EmptyPolyline_ReturnsEmpty()
        {
            Assert.IsEmpty(Polylines.PointsEvery(new PointMm[0], 1));
        }

        [Test]
        public void PointsEvery_SpacingZeroOrNegative_ThrowsArgumentException()
        {
            var line = new[] { P(0, 0), P(10_000, 0) };
            Assert.Throws<ArgumentException>(() => Polylines.PointsEvery(line, 0));
            Assert.Throws<ArgumentException>(() => Polylines.PointsEvery(line, -1));
            Assert.Throws<ArgumentException>(() => Polylines.PointsEvery(line, double.NaN));
        }

        [Test]
        public void PointsEvery_SpacingInvalidEvenForDegenerateLine_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => Polylines.PointsEvery(new[] { P(0, 0) }, 0));
        }

        [Test]
        public void PointsEvery_IsStable_SameInputSameOutput()
        {
            var line = new[] { P(0, 0), P(1234, 5678), P(-9000, 4321), P(7777, -3333) };
            var a = Polylines.PointsEvery(line, 0.7);
            var b = Polylines.PointsEvery(line, 0.7);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void PointsEvery_PositionsAreRoundedToWholeMm_NotTruncated()
        {
            // 1/3 m = 333.33 mm steps on a 1 m line: 333.33 -> 333, 666.67 -> 667, 1000 -> 1000 (end included).
            var pts = Polylines.PointsEvery(new[] { P(0, 0), P(1000, 0) }, 1.0 / 3.0);
            Assert.AreEqual(4, pts.Count);
            Assert.AreEqual(P(333, 0), pts[1]);
            Assert.AreEqual(P(667, 0), pts[2]);
            Assert.AreEqual(P(1000, 0), pts[3]);
        }

        [Test]
        public void PointsEveryWithin_ReturnsOnlyPointsInRadius_SamePositionsAsFullList()
        {
            var line = new[] { P(0, 0), P(20_000, 0) };
            var all = Polylines.PointsEvery(line, 5);
            var near = Polylines.PointsEveryWithin(line, 5, P(10_000, 0), 5.5);
            // Within 5.5 m of 10 m: 5, 10, 15 m (distance 5 m each side); 0 and 20 m are 10 m away.
            CollectionAssert.AreEqual(new[] { P(5000, 0), P(10_000, 0), P(15_000, 0) }, near);
            foreach (PointMm p in near) CollectionAssert.Contains(all, p);
        }

        [Test]
        public void PointsEveryWithin_RadiusIsInclusive()
        {
            var line = new[] { P(0, 0), P(20_000, 0) };
            var near = Polylines.PointsEveryWithin(line, 5, P(0, 0), 5);
            CollectionAssert.AreEqual(new[] { P(0, 0), P(5000, 0) }, near);
        }

        [Test]
        public void PointsEveryWithin_ZeroRadius_ReturnsOnlyExactHit()
        {
            var line = new[] { P(0, 0), P(20_000, 0) };
            CollectionAssert.AreEqual(new[] { P(10_000, 0) }, Polylines.PointsEveryWithin(line, 5, P(10_000, 0), 0));
            Assert.IsEmpty(Polylines.PointsEveryWithin(line, 5, P(10_500, 0), 0));
        }

        [Test]
        public void PointsEveryWithin_NothingInRange_ReturnsEmpty()
        {
            var line = new[] { P(0, 0), P(20_000, 0) };
            Assert.IsEmpty(Polylines.PointsEveryWithin(line, 5, P(0, 50_000), 10));
        }

        [Test]
        public void PointsEveryWithin_DegenerateInputs_ReturnEmptyOrThrow()
        {
            Assert.IsEmpty(Polylines.PointsEveryWithin(new[] { P(1, 1) }, 5, P(1, 1), 10));
            Assert.IsEmpty(Polylines.PointsEveryWithin(new[] { P(1, 1), P(1, 1) }, 5, P(1, 1), 10));
            Assert.Throws<ArgumentException>(() => Polylines.PointsEveryWithin(new[] { P(0, 0), P(10, 0) }, 0, P(0, 0), 1));
            Assert.Throws<ArgumentException>(() => Polylines.PointsEveryWithin(new[] { P(0, 0), P(10, 0) }, 1, P(0, 0), -1));
        }
    }
}
