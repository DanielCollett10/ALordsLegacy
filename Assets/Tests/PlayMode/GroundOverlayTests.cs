using LordsLegacy.Game;
using NUnit.Framework;
using UnityEngine;

namespace LordsLegacy.Tests.PlayMode
{
    public class GroundOverlayTests
    {
        [Test]
        public void MaterialLoadsWithGroundOverlayShader()
        {
            var mat = GroundOverlay.Material();
            Assert.IsNotNull(mat);
            Assert.AreEqual("LordsLegacy/GroundOverlay", mat.shader.name);
            Assert.AreNotEqual("Hidden/InternalErrorShader", mat.shader.name);
            Assert.IsTrue(mat.shader.isSupported);
            Assert.AreSame(mat, GroundOverlay.Material(), "material should be cached");
        }

        [Test]
        public void TexturedMaterialIsCachedAndKeepsShader()
        {
            var tex = new Texture2D(2, 2);
            try
            {
                var a = GroundOverlay.Material(tex);
                Assert.AreSame(a, GroundOverlay.Material(tex));
                Assert.AreSame(tex, a.mainTexture);
                Assert.AreEqual("LordsLegacy/GroundOverlay", a.shader.name);
            }
            finally { Object.DestroyImmediate(tex); }
        }
    }
}
