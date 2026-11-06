using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class SimWorldTests
    {
        private static SimWorld New() => new SimWorld(new SimTuning());

        [Test]
        public void StepAdvancesTick()
        {
            var world = New();
            world.Step(4);
            Assert.That(world.Tick, Is.EqualTo(4));
        }

        [Test]
        public void TuningIsExposed()
        {
            var t = new SimTuning();
            Assert.That(new SimWorld(t).Tuning, Is.SameAs(t));
        }

        [Test]
        public void TickZeroIsTuningStartTime()
        {
            var t = new SimTuning();
            var now = New().Now;
            Assert.That(now.Month, Is.EqualTo(t.StartMonth));
            Assert.That(now.DayOfMonth, Is.EqualTo(t.StartDay));
            Assert.That(now.Hour, Is.EqualTo(t.StartHour));
            Assert.That(now.Minute, Is.EqualTo(t.StartMinute));
            Assert.That(now.Weekday, Is.EqualTo(t.StartWeekday));
            Assert.That(now.Year, Is.EqualTo(1));
        }

        [Test]
        public void DifferentStartIsReflectedInNow()
        {
            var world = new SimWorld(new SimTuning
            {
                StartMonth = 9, StartDay = 15, StartHour = 8, StartMinute = 30, StartWeekday = Weekday.Thursday
            });
            var now = world.Now;
            Assert.That(now.Month, Is.EqualTo(9));
            Assert.That(now.DayOfMonth, Is.EqualTo(15));
            Assert.That(now.Hour, Is.EqualTo(8));
            Assert.That(now.Minute, Is.EqualTo(30));
            Assert.That(now.Weekday, Is.EqualTo(Weekday.Thursday));
        }

        [Test]
        public void Step60AdvancesOneGameHour()
        {
            var world = New();
            var start = world.Now;
            world.Step(60);
            Assert.That(world.Now.Hour, Is.EqualTo(start.Hour + 1));
            Assert.That(world.Now.Minute, Is.EqualTo(start.Minute));
            Assert.That(world.Now.DayOfMonth, Is.EqualTo(start.DayOfMonth));
        }

        [Test]
        public void NewWorldIsNotPaused()
        {
            Assert.That(New().Paused, Is.False);
        }

        [Test]
        public void PauseSetsPaused()
        {
            var world = New();
            world.Pause();
            world.Pause();
            Assert.That(world.Paused, Is.True);
        }

        [Test]
        public void StepWhilePausedLeavesTickAndNowUnchanged()
        {
            var world = New();
            world.Step(2);
            var before = world.Now;
            world.Pause();
            world.Step(500);
            Assert.That(world.Tick, Is.EqualTo(2));
            Assert.That(world.Now.Format(), Is.EqualTo(before.Format()));
        }

        [Test]
        public void ResumeAllowsSteppingAgain()
        {
            var world = New();
            world.Pause();
            world.Resume();
            Assert.That(world.Paused, Is.False);
            world.Step(3);
            Assert.That(world.Tick, Is.EqualTo(3));
        }

        [Test]
        public void NegativeStepIsRejectedEvenWhilePaused()
        {
            var world = New();
            world.Pause();
            Assert.Throws<System.ArgumentOutOfRangeException>(() => world.Step(-1));
        }

        [Test]
        public void NullTuningIsRejected()
        {
            Assert.Throws<System.ArgumentNullException>(() => new SimWorld(null));
        }
    }
}
