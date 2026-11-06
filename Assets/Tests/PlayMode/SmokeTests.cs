using LordsLegacy.Game;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>Fails now if an assembly reference is missing.</summary>
    public class SmokeTests
    {
        [Test]
        public void SimAndGameAssembliesAreReferenced()
        {
            var world = new SimWorld(new SimTuning());
            Assert.AreEqual(0, world.Tick);
            Assert.AreEqual("LordsLegacy.Game", GameAssemblyMarker.AssemblyName);
        }
    }
}
