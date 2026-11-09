using LordsLegacy.Game;
using NUnit.Framework;
using UnityEngine;

namespace LordsLegacy.Tests.EditMode
{
    /// <summary>Checks the GroundOverlay material defaults for the vertical-clearance depth bias (decision 0020 Amendment).</summary>
    public class GroundOverlayTests
    {
        [Test]
        public void MaterialUsesGroundOverlayShaderWithBiasDefaults()
        {
            var mat = GroundOverlay.Material();
            Assert.IsNotNull(mat, "GroundOverlay.Material() returned null");
            Assert.AreEqual("LordsLegacy/GroundOverlay", mat.shader.name);
            Assert.AreEqual(0.15f, mat.GetFloat("_BiasConst"), 1e-6f, "_BiasConst default");
            Assert.AreEqual(0.004f, mat.GetFloat("_BiasPerDist"), 1e-6f, "_BiasPerDist default");
            Assert.AreEqual(0.1f, mat.GetFloat("_MinSin"), 1e-6f, "_MinSin default");
        }
    }
}
