using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LordsLegacy.Game.Visuals
{
    /// <summary>
    /// Draws every forage bush from <c>SimHost.World.Bushes</c> instanced (FD-20, FD-21): a large green bush with berry or
    /// mushroom-coloured dots when it has baskets, a small brown one when empty. Two instanced batches (full and empty), no
    /// per-bush GameObjects or colliders. Rebuilds only when <c>BushRegistry.Version</c> moves. Reads the Sim only.
    /// </summary>
    public sealed class BushRenderer : MonoBehaviour
    {
        /// <summary>Diameter in metres of a full bush.</summary>
        public const float FullSizeM = 1.4f;
        /// <summary>Diameter in metres of an empty bush.</summary>
        public const float EmptySizeM = 0.7f;

        [SerializeField] private SimHost host;

        private readonly List<Matrix4x4> _full = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _empty = new List<Matrix4x4>();
        private Matrix4x4[] _scratch = new Matrix4x4[InstancedDraw.MaxBatch];
        private Mesh _mesh;
        private Material _fullMat, _emptyMat;
        private Bounds _bounds;
        private int _builtVersion = -1;

        /// <summary>Bushes drawn as full in the last build. For tests.</summary>
        public int FullCount => _full.Count;

        /// <summary>Bushes drawn as empty in the last build. For tests.</summary>
        public int EmptyCount => _empty.Count;

        /// <summary>Sets the SimHost (for code that adds the component at run time).</summary>
        public void Configure(SimHost simHost) { host = simHost; _builtVersion = -1; }

        private void Update() { Refresh(); }

        /// <summary>Rebuilds if the Sim bushes changed, then draws both batches.</summary>
        public void Refresh()
        {
            if (host == null || !host.IsInitialized) return;
            var world = host.World;
            if (world.Bushes.Version != _builtVersion) Build(world);
            EnsureAssets();
            Draw(_full, _fullMat);
            Draw(_empty, _emptyMat);
        }

        private void Build(LordsLegacy.Sim.Core.SimWorld world)
        {
            _builtVersion = world.Bushes.Version;
            _full.Clear(); _empty.Clear();
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var b in world.Bushes.All)
            {
                bool full = b.Baskets > 0;
                float size = full ? FullSizeM : EmptySizeM;
                float y = world.Terrain.HeightAt(b.X, b.Z) + size * 0.4f;
                var p = new Vector3(b.X, y, b.Z);
                (full ? _full : _empty).Add(Matrix4x4.TRS(p, Quaternion.identity, new Vector3(size, size * 0.8f, size)));
                min = Vector3.Min(min, p); max = Vector3.Max(max, p);
            }
            if (_full.Count + _empty.Count == 0) { _bounds = new Bounds(Vector3.zero, Vector3.one); return; }
            _bounds = new Bounds((min + max) * 0.5f, (max - min) + Vector3.one * 4f);
        }

        private void EnsureAssets()
        {
            if (_mesh == null) _mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            if (_fullMat == null) _fullMat = MakeMaterial(new Color(0.2f, 0.65f, 0.2f));
            if (_emptyMat == null) _emptyMat = MakeMaterial(new Color(0.45f, 0.35f, 0.2f));
        }

        private static Material MakeMaterial(Color c)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color");
            var m = new Material(shader) { color = c, enableInstancing = true };
            return m;
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
            if (_fullMat != null) Destroy(_fullMat);
            if (_emptyMat != null) Destroy(_emptyMat);
        }
    }
}
