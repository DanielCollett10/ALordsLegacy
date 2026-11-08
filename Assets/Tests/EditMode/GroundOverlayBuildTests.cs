using LordsLegacy.Game;
using NUnit.Framework;
using UnityEngine;

namespace LordsLegacy.Tests.EditMode
{
    /// <summary>Checks the GroundOverlay shader ships in builds via a Resources material.</summary>
    public class GroundOverlayBuildTests
    {
        [Test]
        public void ResourcesMaterialExistsAndUsesGroundOverlayShader()
        {
            var mat = Resources.Load<Material>(GroundOverlay.ResourcePath);
            Assert.IsNotNull(mat, "Assets/Game/Resources/GroundOverlay.mat missing");
            Assert.IsNotNull(mat.shader);
            Assert.AreEqual("LordsLegacy/GroundOverlay", mat.shader.name);
            Assert.IsTrue(mat.shader.isSupported, "shader failed to compile");
        }
    }
}
