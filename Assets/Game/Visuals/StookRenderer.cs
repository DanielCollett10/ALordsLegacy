using System.Collections.Generic;
using LordsLegacy.Sim.Inventory;
using UnityEngine;
using UnityEngine.Rendering;

namespace LordsLegacy.Game.Visuals
{
    /// <summary>
    /// Draws thatch stooks (MAT-32, MAT-39c): every record in <c>World.Curing</c> as a green cone (still curing, or due
    /// this tick), and every ground pile holding thatch as a pale dry cone, one per pile, waiting to be collected
    /// (MAT-39d). Instanced, no per-stook GameObjects. Rebuilds when the curing registry or the ledger changes. Reads the
    /// Sim only; placeholder art (docs/design/placeholder-art.md).
    /// </summary>
    public sealed class StookRenderer : MonoBehaviour
    {
        /// <summary>Height in metres of a stook.</summary>
        public const float HeightM = 1.2f;
        /// <summary>Base diameter in metres of a stook.</summary>
        public const float WidthM = 0.9f;

        [SerializeField] private SimHost host;

        private readonly List<Matrix4x4> _green = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _dry = new List<Matrix4x4>();
        private Matrix4x4[] _scratch = new Matrix4x4[InstancedDraw.MaxBatch];
        private Material _greenMat, _dryMat;
        private Mesh _mesh;
        private Bounds _bounds = new Bounds(Vector3.zero, Vector3.one);
        private int _curingVersion = -1, _inventoryVersion = -1;

        /// <summary>Green (curing) stooks drawn in the last build. For tests.</summary>
        public int GreenCount => _green.Count;

        /// <summary>Dry stooks (thatch lying in piles) drawn in the last build. For tests.</summary>
        public int DryCount => _dry.Count;

        /// <summary>Sets the SimHost (for code that adds the component at run time).</summary>
        public void Configure(SimHost simHost) { host = simHost; _curingVersion = -1; }

        private void Update() { Refresh(); }

        /// <summary>Rebuilds if the Sim changed, then draws both batches.</summary>
        public void Refresh()
        {
            if (host == null || !host.IsInitialized) return;
            var world = host.World;
            if (world.Curing.Version != _curingVersion || world.Inventory.Version != _inventoryVersion) Build(world);
            EnsureAssets();
            Draw(_green, _greenMat);
            Draw(_dry, _dryMat);
        }

        private void Build(LordsLegacy.Sim.Core.SimWorld world)
        {
            _curingVersion = world.Curing.Version;
            _inventoryVersion = world.Inventory.Version;
            _green.Clear(); _dry.Clear();
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var s in world.Curing.All)
                Add(world, _green, (float)s.Position.XMetres, (float)s.Position.ZMetres, ref min, ref max);
            foreach (var loc in world.Inventory.Locations)
                if (loc.Kind == LocationKind.GroundPile && loc.CountOf(Items.Thatch.Id) > 0)
                    Add(world, _dry, (float)loc.Position.XMetres, (float)loc.Position.ZMetres, ref min, ref max);
            _bounds = _green.Count + _dry.Count == 0 ? new Bounds(Vector3.zero, Vector3.one)
                : new Bounds((min + max) * 0.5f, (max - min) + Vector3.one * 4f);
        }

        private static void Add(LordsLegacy.Sim.Core.SimWorld world, List<Matrix4x4> list, float x, float z, ref Vector3 min, ref Vector3 max)
        {
            var p = new Vector3(x, world.Terrain.HeightAt(x, z) + HeightM * 0.5f, z);
            // The cylinder mesh is 2 m tall; a squat narrow cylinder stands in for the cone.
            list.Add(Matrix4x4.TRS(p, Quaternion.identity, new Vector3(WidthM, HeightM * 0.5f, WidthM)));
            min = Vector3.Min(min, p); max = Vector3.Max(max, p);
        }

        private void EnsureAssets()
        {
            if (_mesh == null) _mesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            if (_greenMat == null) _greenMat = MakeMaterial(new Color(0.3f, 0.65f, 0.3f));
            if (_dryMat == null) _dryMat = MakeMaterial(new Color(0.85f, 0.75f, 0.4f));
        }

        private static Material MakeMaterial(Color c)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color");
            return new Material(shader) { color = c, enableInstancing = true };
        }

        private void Draw(List<Matrix4x4> list, Material mat)
        {
            if (list.Count == 0 || mat == null || _mesh == null) return;
            var rp = new RenderParams(mat) { shadowCastingMode = ShadowCastingMode.On, receiveShadows = true, worldBounds = _bounds };
            for (int start = 0; start < list.Count; start += InstancedDraw.MaxBatch)
            {
                int n = Mathf.Min(InstancedDraw.MaxBatch, list.Count - start);
                list.CopyTo(start, _scratch, 0, n);
                Graphics.RenderMeshInstanced(rp, _mesh, 0, _scratch, n, 0);
            }
        }

        private void OnDestroy()
        {
            if (_greenMat != null) Destroy(_greenMat);
            if (_dryMat != null) Destroy(_dryMat);
        }
    }
}
