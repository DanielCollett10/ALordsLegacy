using System;
using LordsLegacy.Sim.Core;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class SimRandomTests
    {
        [Test]
        public void SameSeed_GivesSameSequence()
        {
            var a = new SimRandom(12345);
            var b = new SimRandom(12345);
            for (int i = 0; i < 1000; i++) Assert.AreEqual(a.NextUInt(), b.NextUInt(), "draw " + i);
        }

        [Test]
        public void DifferentSeeds_GiveDifferentSequences()
        {
            var a = new SimRandom(1);
            var b = new SimRandom(2);
            int same = 0;
            for (int i = 0; i < 100; i++) if (a.NextUInt() == b.NextUInt()) same++;
            Assert.That(same, Is.LessThan(3));
        }

        [Test]
        public void ZeroSeed_IsValidAndNotStuck()
        {
            var r = new SimRandom(0);
            uint first = r.NextUInt();
            uint second = r.NextUInt();
            Assert.AreNotEqual(first, second, "a zero seed must still advance");
        }

        [Test]
        public void KnownSequence_IsPinnedForSeed12345()
        {
            // Regression pin: the sequence must not change between builds, or saves and replays break (CLK-07).
            var r = new SimRandom(12345);
            var got = new uint[PinnedFirstDraws.Length];
            for (int i = 0; i < got.Length; i++) got[i] = r.NextUInt();
            CollectionAssert.AreEqual(PinnedFirstDraws, got);
        }

        // First NextUInt values of seed 12345, recorded from this implementation.
        private static readonly uint[] PinnedFirstDraws = { 1411482639u, 3165192603u, 3360792183u, 2433038347u };

        [Test]
        public void NextInt_StaysInRange_HalfOpen()
        {
            var r = new SimRandom(7);
            for (int i = 0; i < 10000; i++)
            {
                int v = r.NextInt(-5, 10);
                Assert.That(v, Is.InRange(-5, 9));
            }
        }

        [Test]
        public void NextInt_ReachesBothEnds()
        {
            var r = new SimRandom(99);
            bool sawMin = false, sawLastBelowMax = false;
            for (int i = 0; i < 10000; i++)
            {
                int v = r.NextInt(0, 6);
                if (v == 0) sawMin = true;
                if (v == 5) sawLastBelowMax = true;
            }
            Assert.IsTrue(sawMin, "min is inclusive");
            Assert.IsTrue(sawLastBelowMax, "max - 1 is reachable");
        }

        [Test]
        public void NextInt_SingleValueRange_AlwaysReturnsMin()
        {
            var r = new SimRandom(3);
            for (int i = 0; i < 100; i++) Assert.AreEqual(5, r.NextInt(5, 6));
        }

        [Test]
        public void NextInt_MaxNotGreaterThanMin_Throws()
        {
            var r = new SimRandom(3);
            Assert.Throws<ArgumentOutOfRangeException>(() => r.NextInt(5, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => r.NextInt(5, 4));
        }

        [Test]
        public void NextInt_FullIntRange_DoesNotOverflow()
        {
            var r = new SimRandom(11);
            bool sawNegative = false, sawPositive = false;
            for (int i = 0; i < 2000; i++)
            {
                int v = r.NextInt(int.MinValue, int.MaxValue);
                Assert.That(v, Is.InRange(int.MinValue, int.MaxValue - 1));
                if (v < 0) sawNegative = true; else sawPositive = true;
            }
            Assert.IsTrue(sawNegative && sawPositive);
        }

        [Test]
        public void NextInt_ReturnsSameSequenceForSameSeed()
        {
            var a = new SimRandom(12345);
            var b = new SimRandom(12345);
            for (int i = 0; i < 500; i++) Assert.AreEqual(a.NextInt(0, 1000), b.NextInt(0, 1000));
        }

        [Test]
        public void NextDouble_IsInHalfOpenUnitInterval()
        {
            var r = new SimRandom(5);
            for (int i = 0; i < 10000; i++)
            {
                double d = r.NextDouble();
                Assert.That(d, Is.GreaterThanOrEqualTo(0.0));
                Assert.That(d, Is.LessThan(1.0));
            }
        }

        [Test]
        public void State_RestoreResumesSameSequence()
        {
            // The save path: read State, draw on, restore into a fresh generator, draw again.
            var r = new SimRandom(12345);
            for (int i = 0; i < 17; i++) r.NextUInt();
            ulong saved = r.State;
            uint[] expected = new uint[20];
            for (int i = 0; i < expected.Length; i++) expected[i] = r.NextUInt();

            var restored = new SimRandom(0);
            restored.Restore(saved);
            for (int i = 0; i < expected.Length; i++) Assert.AreEqual(expected[i], restored.NextUInt());
        }

        [Test]
        public void State_IsTheSameForTheSameSeed_AndDiffersForAnother()
        {
            Assert.AreEqual(new SimRandom(12345).State, new SimRandom(12345).State);
            Assert.AreNotEqual(new SimRandom(12345).State, new SimRandom(12346).State);
        }

        [Test]
        public void State_ChangesEveryDraw()
        {
            var r = new SimRandom(1);
            ulong before = r.State;
            r.NextUInt();
            Assert.AreNotEqual(before, r.State);
        }
    }
}
