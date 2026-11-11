using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class SimWorldRandomTests
    {
        [Test]
        public void Random_IsNeverNull()
        {
            Assert.That(new SimWorld(new SimTuning()).Random, Is.Not.Null);
        }

        [Test]
        public void Random_IsTheSameInstanceEveryTime()
        {
            var world = new SimWorld(new SimTuning());
            Assert.That(world.Random, Is.SameAs(world.Random));
        }

        [Test]
        public void Random_DefaultSeedIs12345()
        {
            Assert.AreEqual(12345, new SimTuning().WorldSeed);
            Assert.AreEqual(new SimRandom(12345).State, new SimWorld(new SimTuning()).Random.State);
        }

        [Test]
        public void Random_SameSeed_GivesSameSequence()
        {
            var a = new SimWorld(new SimTuning { WorldSeed = 777 });
            var b = new SimWorld(new SimTuning { WorldSeed = 777 });
            for (int i = 0; i < 200; i++)
            {
                Assert.AreEqual(a.Random.NextInt(0, 100), b.Random.NextInt(0, 100), "draw " + i);
            }
        }

        [Test]
        public void Random_DifferentSeed_GivesDifferentSequence()
        {
            var a = new SimWorld(new SimTuning { WorldSeed = 1 });
            var b = new SimWorld(new SimTuning { WorldSeed = 2 });
            Assert.AreNotEqual(a.Random.NextUInt(), b.Random.NextUInt());
        }

        [Test]
        public void Random_NegativeSeed_IsAccepted()
        {
            var a = new SimWorld(new SimTuning { WorldSeed = -1 });
            var b = new SimWorld(new SimTuning { WorldSeed = -1 });
            Assert.AreEqual(a.Random.NextUInt(), b.Random.NextUInt());
        }

        [Test]
        public void Random_NotChangedByStepping()
        {
            // Time alone does not draw from the source; only routine choices do.
            var world = new SimWorld(new SimTuning());
            ulong before = world.Random.State;
            world.Step(50);
            Assert.AreEqual(before, world.Random.State);
        }
    }
}
