using LordsLegacy.Sim.Core;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class SimWorldTests
    {
        [Test]
        public void StepAdvancesTickAndTime()
        {
            var world = new SimWorld(secondsPerTick: 0.5);
            world.Step(4);
            Assert.That(world.Tick, Is.EqualTo(4));
            Assert.That(world.SimulatedSeconds, Is.EqualTo(2.0).Within(1e-9));
        }

        [Test]
        public void NewWorldIsNotPaused()
        {
            Assert.That(new SimWorld().IsPaused, Is.False);
        }

        [Test]
        public void PauseSetsIsPaused()
        {
            var world = new SimWorld();
            world.Pause();
            world.Pause();
            Assert.That(world.IsPaused, Is.True);
        }

        [Test]
        public void StepWhilePausedDoesNotAdvance()
        {
            var world = new SimWorld();
            world.Step(2);
            world.Pause();
            world.Step(5);
            Assert.That(world.Tick, Is.EqualTo(2));
            Assert.That(world.SimulatedSeconds, Is.EqualTo(2 * world.SecondsPerTick).Within(1e-9));
        }

        [Test]
        public void ResumeAllowsSteppingAgain()
        {
            var world = new SimWorld();
            world.Pause();
            world.Resume();
            Assert.That(world.IsPaused, Is.False);
            world.Step(3);
            Assert.That(world.Tick, Is.EqualTo(3));
        }

        [Test]
        public void NegativeStepIsRejectedEvenWhilePaused()
        {
            var world = new SimWorld();
            world.Pause();
            Assert.Throws<System.ArgumentOutOfRangeException>(() => world.Step(-1));
        }

        [Test]
        public void NegativeStepIsRejected()
        {
            var world = new SimWorld();
            Assert.Throws<System.ArgumentOutOfRangeException>(() => world.Step(-1));
        }
    }
}
