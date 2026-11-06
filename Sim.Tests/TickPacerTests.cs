using System;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class TickPacerTests
    {
        // A 24-minute day at 1x gives exactly 1 game minute per real second, so tests can use whole numbers.
        private static SimTuning ExactTuning(int maxTicks = 1000) =>
            new SimTuning { RealMinutesPerGameDayAt1x = 24, MaxTicksPerFrame = maxTicks };

        [Test]
        public void Speeds_AreTheTuningSpeeds_AndStartAtOneX()
        {
            var pacer = new TickPacer(new SimTuning());
            CollectionAssert.AreEqual(new[] { 1, 2, 4, 8, 16, 32 }, pacer.Speeds);
            Assert.AreEqual(0, pacer.CurrentSpeedIndex);
            Assert.AreEqual(1, pacer.CurrentSpeed);
            Assert.IsFalse(pacer.SlowedDown);
            Assert.AreEqual(0, pacer.Fraction);
        }

        [Test]
        public void Constructor_RejectsNullOrBadSpeeds()
        {
            Assert.Throws<ArgumentNullException>(() => new TickPacer(null));
            Assert.Throws<ArgumentException>(() => new TickPacer(new SimTuning { Speeds = new int[0] }));
            Assert.Throws<ArgumentException>(() => new TickPacer(new SimTuning { Speeds = new[] { 1, 0 } }));
        }

        [Test]
        public void EachSpeed_RunsThatManyTimesRealTime()
        {
            var pacer = new TickPacer(ExactTuning());
            for (int i = 0; i < pacer.Speeds.Count; i++)
            {
                pacer.SetSpeed(i);
                Assert.AreEqual(pacer.Speeds[i], pacer.CurrentSpeed);
                pacer.Reset();
                Assert.AreEqual(pacer.Speeds[i], pacer.Advance(1.0), "speed index " + i);
                Assert.AreEqual(0, pacer.Fraction, 1e-9);
            }
        }

        [Test]
        public void SetSpeed_KeepsIndexAndSpeedInStep()
        {
            var pacer = new TickPacer(ExactTuning());
            pacer.SetSpeed(4);
            Assert.AreEqual(4, pacer.CurrentSpeedIndex);
            Assert.AreEqual(16, pacer.CurrentSpeed);
            Assert.AreEqual(16.0, pacer.GameMinutesPerRealSecond, 1e-9);
        }

        [Test]
        public void SetSpeed_OutOfRange_Throws_AndKeepsCurrentSpeed()
        {
            var pacer = new TickPacer(ExactTuning());
            pacer.SetSpeed(2);
            Assert.Throws<ArgumentOutOfRangeException>(() => pacer.SetSpeed(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => pacer.SetSpeed(6));
            Assert.Throws<ArgumentOutOfRangeException>(() => pacer.SetSpeed(int.MaxValue));
            Assert.AreEqual(2, pacer.CurrentSpeedIndex);
            Assert.AreEqual(4, pacer.CurrentSpeed);
        }

        [Test]
        public void Advance_DefaultTuning_CarriesFractionAtOneX()
        {
            // 1.6 game minutes per real second at 1x (15-minute day).
            var pacer = new TickPacer(new SimTuning());
            Assert.AreEqual(1, pacer.Advance(1.0));
            Assert.AreEqual(0.6, pacer.Fraction, 1e-9);
            Assert.AreEqual(2, pacer.Advance(1.0)); // 0.6 + 1.6 = 2.2
            Assert.AreEqual(0.2, pacer.Fraction, 1e-9);
        }

        [Test]
        public void Advance_CarriesFractionsAcrossCalls()
        {
            var pacer = new TickPacer(ExactTuning());
            Assert.AreEqual(0, pacer.Advance(0.4));
            Assert.AreEqual(0.4, pacer.Fraction, 1e-9);
            Assert.AreEqual(0, pacer.Advance(0.4));
            Assert.AreEqual(0.8, pacer.Fraction, 1e-9);
            Assert.AreEqual(1, pacer.Advance(0.4));
            Assert.AreEqual(0.2, pacer.Fraction, 1e-9);
        }

        [Test]
        public void Advance_ManySmallFrames_ConserveTotalTicks()
        {
            var pacer = new TickPacer(new SimTuning()); // 1.6 ticks per real second
            long ticks = 0;
            for (int i = 0; i < 1000; i++) ticks += pacer.Advance(0.1); // 100 real seconds
            Assert.That(ticks, Is.EqualTo(160).Or.EqualTo(159));
            Assert.That(pacer.Fraction, Is.GreaterThanOrEqualTo(0).And.LessThan(1));
        }

        [Test]
        public void Fraction_IsAlwaysBelowOne()
        {
            var pacer = new TickPacer(new SimTuning());
            for (int i = 1; i <= 50; i++)
            {
                pacer.Advance(0.137 * i);
                Assert.That(pacer.Fraction, Is.GreaterThanOrEqualTo(0).And.LessThan(1));
            }
        }

        [Test]
        public void Advance_ZeroSeconds_ReturnsZero_AndKeepsFraction()
        {
            var pacer = new TickPacer(new SimTuning());
            pacer.Advance(1.0);
            Assert.AreEqual(0, pacer.Advance(0));
            Assert.AreEqual(0.6, pacer.Fraction, 1e-9);
        }

        [Test]
        public void Advance_RejectsNegativeNaNAndInfinity()
        {
            var pacer = new TickPacer(new SimTuning());
            Assert.Throws<ArgumentOutOfRangeException>(() => pacer.Advance(-0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => pacer.Advance(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => pacer.Advance(double.PositiveInfinity));
        }

        [Test]
        public void Reset_ClearsCarryOver()
        {
            var pacer = new TickPacer(ExactTuning());
            pacer.Advance(0.6);
            Assert.AreEqual(0.6, pacer.Fraction, 1e-9);
            pacer.Reset();
            Assert.AreEqual(0, pacer.Fraction);
            Assert.AreEqual(0, pacer.Advance(0.6)); // without the reset this would be 1 tick
        }

        [Test]
        public void Reset_KeepsSelectedSpeed()
        {
            var pacer = new TickPacer(ExactTuning());
            pacer.SetSpeed(3);
            pacer.Reset();
            Assert.AreEqual(3, pacer.CurrentSpeedIndex);
        }

        [Test]
        public void Cap_ReturnsMaxTicks_SetsSlowedDown_AndDropsExcess()
        {
            var pacer = new TickPacer(new SimTuning()); // cap 8, 1.6 ticks per real second at 1x
            Assert.AreEqual(8, pacer.Advance(10)); // 16 due
            Assert.IsTrue(pacer.SlowedDown);
            Assert.AreEqual(0, pacer.Fraction);

            // The excess is gone, not owed later.
            Assert.AreEqual(0, pacer.Advance(0.1));
            Assert.IsFalse(pacer.SlowedDown);
        }

        [Test]
        public void Cap_AtHighSpeed_SlowsDown()
        {
            var pacer = new TickPacer(new SimTuning());
            pacer.SetSpeed(5); // 32x = 51.2 ticks per real second
            Assert.AreEqual(8, pacer.Advance(1.0));
            Assert.IsTrue(pacer.SlowedDown);
        }

        [Test]
        public void Cap_ExactlyAtMax_IsNotSlowedDown()
        {
            var pacer = new TickPacer(ExactTuning(maxTicks: 8));
            Assert.AreEqual(8, pacer.Advance(8.0));
            Assert.IsFalse(pacer.SlowedDown);
        }

        [Test]
        public void Cap_UsesTuningMaxTicksPerFrame()
        {
            var pacer = new TickPacer(ExactTuning(maxTicks: 3));
            Assert.AreEqual(3, pacer.Advance(5.0));
            Assert.IsTrue(pacer.SlowedDown);
        }

        [Test]
        public void DayLength_TenMinutes_ChangesRateWithoutCodeChange()
        {
            // 10-minute day: 2.4 game minutes per real second at 1x.
            var pacer = new TickPacer(new SimTuning { RealMinutesPerGameDayAt1x = 10 });
            Assert.AreEqual(2.4, pacer.GameMinutesPerRealSecond, 1e-9);
            Assert.AreEqual(2, pacer.Advance(1.0));
            Assert.AreEqual(0.4, pacer.Fraction, 1e-9);
        }

        [Test]
        public void DayLength_TwentyMinutes_ChangesRateWithoutCodeChange()
        {
            // 20-minute day: 1.2 game minutes per real second at 1x.
            var pacer = new TickPacer(new SimTuning { RealMinutesPerGameDayAt1x = 20 });
            Assert.AreEqual(1.2, pacer.GameMinutesPerRealSecond, 1e-9);
            Assert.AreEqual(1, pacer.Advance(1.0));
            Assert.AreEqual(0.2, pacer.Fraction, 1e-9);
        }

        [Test]
        public void DayLength_ScalesWithSpeed()
        {
            var pacer = new TickPacer(new SimTuning { RealMinutesPerGameDayAt1x = 20, MaxTicksPerFrame = 1000 });
            pacer.SetSpeed(2); // 4x
            Assert.AreEqual(4.8, pacer.GameMinutesPerRealSecond, 1e-9);
            Assert.AreEqual(4, pacer.Advance(1.0));
        }

        [Test]
        public void CustomSpeeds_AreUsed()
        {
            var pacer = new TickPacer(new SimTuning { Speeds = new[] { 1, 3 }, MaxTicksPerFrame = 1000, RealMinutesPerGameDayAt1x = 24 });
            CollectionAssert.AreEqual(new[] { 1, 3 }, pacer.Speeds);
            pacer.SetSpeed(1);
            Assert.AreEqual(3, pacer.Advance(1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => pacer.SetSpeed(2));
        }
    }
}
