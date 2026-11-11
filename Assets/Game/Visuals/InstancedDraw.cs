using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LordsLegacy.Game.Visuals
{
    /// <summary>Shared helper: draws a list of matrices with a definition via <c>Graphics.RenderMeshInstanced</c>.</summary>
    public static class InstancedDraw
    {
        /// <summary>Maximum instances per draw call.</summary>
        public const int MaxBatch = 1023;

        private static Matrix4x4[] _scratch = new Matrix4x4[MaxBatch];

        /// <summary>
        /// Draws every matrix in <paramref name="matrices"/> with each sub-mesh whose material has instancing on.
        /// The whole set is drawn (no culling); <paramref name="bounds"/> must contain every instance.
        /// </summary>
        public static void Draw(VisualDefinition def, List<Matrix4x4> matrices, Bounds bounds)
        {
            if (def == null || def.Mesh == null || matrices.Count == 0) return;
            var mesh = def.Mesh;
            var mats = def.Materials;
            int subMeshes = Mathf.Min(mesh.subMeshCount, mats.Length);
            for (int s = 0; s < subMeshes; s++)
            {
                var mat = mats[s];
                if (mat == null || !mat.enableInstancing) continue;
                var rp = new RenderParams(mat)
                {
                    shadowCastingMode = ShadowCastingMode.On,
                    receiveShadows = true,
                    worldBounds = bounds,
                };
                for (int start = 0; start < matrices.Count; start += MaxBatch)
                {
                    int n = Mathf.Min(MaxBatch, matrices.Count - start);
                    matrices.CopyTo(start, _scratch, 0, n);
                    Graphics.RenderMeshInstanced(rp, mesh, s, _scratch, n, 0);
                }
            }
        }
    }
}
