using System.Collections.Generic;
using LordsLegacy.Sim.Footprints;
using UnityEngine;

namespace LordsLegacy.Game.Visuals
{
    /// <summary>
    /// Draws the modules placed in operations (hut, log storage, oxen stand) with their <c>visual.module.*</c>
    /// definitions, stretched to the footprint, via <c>Graphics.RenderMeshInstanced</c> (no GameObjects, no colliders,
    /// so they never block ground picking). Rebuilt only when <c>FootprintRegistry.Version</c> changes. Placeholder art.
    /// </summary>
    public sealed class ModuleRenderer : MonoBehaviour
    {
        [SerializeField] private SimHost host;
        [SerializeField] private VisualRegistry registry;

        private readonly Dictionary<string, List<Matrix4x4>> _byVisual = new Dictionary<string, List<Matrix4x4>>();
        private int _count;
        private int _builtVersion = -1;
        private Bounds _bounds;

        /// <summary>Modules currently drawn. For tests.</summary>
        public int BoxCount => _count;

        /// <summary>Sets the SimHost and, optionally, the visual registry (for code that adds the component at run time).</summary>
        public void Configure(SimHost simHost, VisualRegistry visualRegistry = null)
        {
            host = simHost;
            registry = visualRegistry;
            _builtVersion = -1;
        }

        /// <summary>The visual id used for a module type id.</summary>
        public static string VisualIdOf(string moduleTypeId)
        {
            switch (moduleTypeId)
            {
                case ModuleTypes.WoodcuttingHutId: return VisualSizes.ModuleHut;
                case ModuleTypes.OxenStandId: return VisualSizes.ModuleOxenStand;
                default: return VisualSizes.ModuleLogStorage;
            }
        }

        private void Update() { Refresh(); }

        /// <summary>Rebuilds if the Sim footprints changed, then draws.</summary>
        public void Refresh()
        {
            if (host == null || !host.IsInitialized) return;
            var world = host.World;
            if (world.Footprints.Version != _builtVersion) Build(world);
            if (registry == null) return;
            foreach (var kv in _byVisual)
                if (registry.TryGet(kv.Key, out var def)) InstancedDraw.Draw(def, kv.Value, _bounds);
        }

        private void Build(LordsLegacy.Sim.Core.SimWorld world)
        {
            _builtVersion = world.Footprints.Version;
            _byVisual.Clear();
            _count = 0;
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var op in world.Operations.All)
                foreach (int id in op.ModuleIds)
                {
                    var fp = world.Footprints.Get(id);
                    if (fp == null) continue;
                    string vid = VisualIdOf(fp.Type.Id);
                    var box = VisualSizes.Of(vid);
                    float x = (float)fp.Centre.XMetres, z = (float)fp.Centre.ZMetres;
                    var p = new Vector3(x, world.Terrain.HeightAt(x, z), z);
                    // Footprint rotation is counter-clockwise (x towards z); a Unity yaw turns z towards x, hence the sign.
                    float yaw = -(float)fp.RotationDegrees;
                    Matrix4x4 m;
                    if (registry != null && registry.TryGet(vid, out var def))
                    {
                        var mb = def.Mesh != null ? def.Mesh.bounds : new Bounds(Vector3.zero, Vector3.one);
                        var s = new Vector3(fp.Type.WidthMm / 1000f / Mathf.Max(0.001f, mb.size.x),
                            box.y * def.Scale / Mathf.Max(0.001f, mb.size.y),
                            fp.Type.DepthMm / 1000f / Mathf.Max(0.001f, mb.size.z));
                        m = Matrix4x4.TRS(new Vector3(p.x, p.y - mb.min.y * s.y, p.z), Quaternion.Euler(0f, yaw, 0f), s);
                    }
                    else m = Matrix4x4.identity;
                    if (!_byVisual.TryGetValue(vid, out var list)) { list = new List<Matrix4x4>(); _byVisual[vid] = list; }
                    list.Add(m);
                    _count++;
                    float r = Mathf.Max(fp.Type.WidthMm, fp.Type.DepthMm) / 1000f;
                    min = Vector3.Min(min, p - new Vector3(r, 1f, r));
                    max = Vector3.Max(max, p + new Vector3(r, box.y + 1f, r));
                }
            _bounds = new Bounds();
            if (_count > 0) _bounds.SetMinMax(min, max);
        }
    }
}
