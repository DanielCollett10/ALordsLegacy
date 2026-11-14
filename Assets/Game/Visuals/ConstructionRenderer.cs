using System.Collections.Generic;
using LordsLegacy.Sim.Construction;
using UnityEngine;

namespace LordsLegacy.Game.Visuals
{
    /// <summary>
    /// Draws a placeholder scaffold (<c>visual.construction.scaffold</c>) for every unbuilt footprint (CN-22, CN-42),
    /// stretched to the footprint, with a height that grows with <see cref="ConstructionProgress.Of"/>. Instanced, no
    /// GameObjects, no colliders. Rebuilt every frame (few sites). Built modules are drawn by <see cref="ModuleRenderer"/>.
    /// </summary>
    public sealed class ConstructionRenderer : MonoBehaviour
    {
        /// <summary>Scaffold height as a fraction of full height with no progress.</summary>
        public const float MinHeightFraction = 0.15f;

        [SerializeField] private SimHost host;
        [SerializeField] private VisualRegistry registry;

        private readonly List<Matrix4x4> _matrices = new List<Matrix4x4>();
        private readonly Dictionary<int, Vector3> _sizes = new Dictionary<int, Vector3>();
        private Bounds _bounds;

        /// <summary>Scaffolds currently drawn. For tests.</summary>
        public int ScaffoldCount => _sizes.Count;

        /// <summary>Drawn size (width x, height y, depth z, metres) of a footprint's scaffold; false if none. For tests.</summary>
        public bool TryGetSize(int footprintId, out Vector3 size) => _sizes.TryGetValue(footprintId, out size);

        /// <summary>Sets the references (for code that adds the component at run time).</summary>
        public void Configure(SimHost simHost, VisualRegistry visualRegistry)
        {
            host = simHost;
            registry = visualRegistry;
        }

        private void Update() { Refresh(); }

        /// <summary>Rebuilds from the Sim, then draws.</summary>
        public void Refresh()
        {
            if (host == null || !host.IsInitialized) return;
            var world = host.World;
            _matrices.Clear();
            _sizes.Clear();
            VisualDefinition def = null;
            if (registry != null) registry.TryGet(VisualSizes.ConstructionScaffold, out def);
            float fullHeight = VisualSizes.Of(VisualSizes.ConstructionScaffold).y * (def != null ? def.Scale : 1f);
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var site in world.Construction.Sites)
            {
                var fp = world.Footprints.Get(site.FootprintId);
                var progress = ConstructionProgress.Of(world, site.FootprintId);
                if (fp == null || progress == null) continue;
                float h = fullHeight * (MinHeightFraction + (1f - MinHeightFraction) * (float)progress.Fraction);
                var size = new Vector3(fp.Type.WidthMm / 1000f, h, fp.Type.DepthMm / 1000f);
                float x = (float)fp.Centre.XMetres, z = (float)fp.Centre.ZMetres;
                var p = new Vector3(x, world.Terrain.HeightAt(x, z), z);
                // Footprint rotation is counter-clockwise (x towards z); a Unity yaw turns z towards x, hence the sign.
                float yaw = -(float)fp.RotationDegrees;
                var mb = def != null && def.Mesh != null ? def.Mesh.bounds : new Bounds(Vector3.zero, Vector3.one);
                var s = new Vector3(size.x / Mathf.Max(0.001f, mb.size.x), size.y / Mathf.Max(0.001f, mb.size.y),
                    size.z / Mathf.Max(0.001f, mb.size.z));
                _matrices.Add(Matrix4x4.TRS(new Vector3(p.x, p.y - mb.min.y * s.y, p.z), Quaternion.Euler(0f, yaw, 0f), s));
                _sizes[site.FootprintId] = size;
                float r = Mathf.Max(size.x, size.z);
                min = Vector3.Min(min, p - new Vector3(r, 1f, r));
                max = Vector3.Max(max, p + new Vector3(r, size.y + 1f, r));
            }
            _bounds = new Bounds();
            if (_matrices.Count > 0) _bounds.SetMinMax(min, max);
            if (def != null) InstancedDraw.Draw(def, _matrices, _bounds);
        }
    }
}
