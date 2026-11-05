using NetSim.Sim.Core;
using NUnit.Framework;

namespace NetSim.Sim.Tests
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
        public void NegativeStepIsRejected()
        {
            var world = new SimWorld();
            Assert.Throws<System.ArgumentOutOfRangeException>(() => world.Step(-1));
        }
    }
}
