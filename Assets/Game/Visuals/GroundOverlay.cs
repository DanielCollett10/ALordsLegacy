using System.Collections.Generic;
using UnityEngine;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Material source for everything drawn on the ground (roads, area outlines/fills, drafts, markers).
    /// Uses shader <c>LordsLegacy/GroundOverlay</c> (decision 0020), which biases depth towards the camera
    /// in proportion to view distance. The shader ships via the material <c>Resources/GroundOverlay</c>.
    /// </summary>
    public static class GroundOverlay
    {
        /// <summary>Shader name.</summary>
        public const string ShaderName = "LordsLegacy/GroundOverlay";

        /// <summary>Resources path of the base material (no extension).</summary>
        public const string ResourcePath = "GroundOverlay";

        static Material _base;
        static readonly Dictionary<Texture, Material> Cache = new Dictionary<Texture, Material>();

        /// <summary>Returns a cached overlay material, optionally with a main texture. Do not destroy it; tint per vertex colour.</summary>
        public static Material Material(Texture texture = null)
        {
            if (_base == null)
            {
                _base = Resources.Load<Material>(ResourcePath);
                if (_base == null)
                {
                    var shader = Shader.Find(ShaderName);
                    if (shader == null) return null;
                    _base = new Material(shader) { name = "GroundOverlay" };
                }
            }
            if (texture == null) return _base;

            // Drop entries whose texture was destroyed.
            if (Cache.TryGetValue(texture, out var m) && m != null) return m;
            m = new Material(_base) { name = "GroundOverlay_" + texture.name, mainTexture = texture };
            Cache[texture] = m;
            return m;
        }
    }
}
