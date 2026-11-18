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

        /// <summary>Height in metres of a hazel stool with rods (placeholder, T-568).</summary>
        public const float HazelFullHeightM = 1.6f;
        /// <summary>Height in metres of a willow stool with rods.</summary>
        public const float WillowFullHeightM = 2.0f;
        /// <summary>Height in metres of a cut stool (either kind) while it regrows.</summary>
        public const float CutHeightM = 0.35f;

        // Batch slots: forage full / empty, then hazel and willow stools with rods / cut.
        private const int SlotFull = 0, SlotEmpty = 1, SlotHazelFull = 2, SlotHazelCut = 3, SlotWillowFull = 4, SlotWillowCut = 5, SlotCount = 6;

        private readonly List<Matrix4x4>[] _lists = MakeLists();
        private readonly Material[] _mats = new Material[SlotCount];
        private Matrix4x4[] _scratch = new Matrix4x4[InstancedDraw.MaxBatch];
        private Mesh _mesh, _stoolMesh;
        private Bounds _bounds;
        private int _builtVersion = -1;

        private static List<Matrix4x4>[] MakeLists()
        {
            var l = new List<Matrix4x4>[SlotCount];
            for (int i = 0; i < SlotCount; i++) l[i] = new List<Matrix4x4>();
            return l;
        }

        /// <summary>Forage bushes drawn as full in the last build (stools not counted). For tests.</summary>
        public int FullCount => _lists[SlotFull].Count;

        /// <summary>Forage bushes drawn as empty in the last build (stools not counted). For tests.</summary>
        public int EmptyCount => _lists[SlotEmpty].Count;

        /// <summary>Stools of this kind drawn with rods on them in the last build. For tests.</summary>
        public int StoolFullCount(LordsLegacy.Sim.Foraging.BushKind kind)
            => _lists[kind == LordsLegacy.Sim.Foraging.BushKind.WillowStool ? SlotWillowFull : SlotHazelFull].Count;

        /// <summary>Stools of this kind drawn cut (regrowing) in the last build. For tests.</summary>
        public int StoolCutCount(LordsLegacy.Sim.Foraging.BushKind kind)
            => _lists[kind == LordsLegacy.Sim.Foraging.BushKind.WillowStool ? SlotWillowCut : SlotHazelCut].Count;

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
            for (int i = 0; i < SlotCount; i++) Draw(_lists[i], _mats[i], i >= SlotHazelFull ? _stoolMesh : _mesh);
        }

        private void Build(LordsLegacy.Sim.Core.SimWorld world)
        {
            _builtVersion = world.Bushes.Version;
            foreach (var l in _lists) l.Clear();
            int total = 0;
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var b in world.Bushes.All)
            {
                bool full = b.Baskets > 0;
                Matrix4x4 m;
                Vector3 p;
                int slot;
                if (LordsLegacy.Sim.Foraging.BushKinds.IsStool(b.Kind))
                {
                    // Coppice stool: a cylinder of rods; a short stump while cut and regrowing.
                    bool willow = b.Kind == LordsLegacy.Sim.Foraging.BushKind.WillowStool;
                    float h = full ? (willow ? WillowFullHeightM : HazelFullHeightM) : CutHeightM;
                    float w = full ? (willow ? 1.0f : 0.9f) : 0.7f;
                    p = new Vector3(b.X, world.Terrain.HeightAt(b.X, b.Z) + h * 0.5f, b.Z);
                    // The cylinder mesh is 2 m tall, so half-height scale.
                    m = Matrix4x4.TRS(p, Quaternion.identity, new Vector3(w, h * 0.5f, w));
                    slot = willow ? (full ? SlotWillowFull : SlotWillowCut) : (full ? SlotHazelFull : SlotHazelCut);
                }
                else
                {
                    float size = full ? FullSizeM : EmptySizeM;
                    p = new Vector3(b.X, world.Terrain.HeightAt(b.X, b.Z) + size * 0.4f, b.Z);
                    m = Matrix4x4.TRS(p, Quaternion.identity, new Vector3(size, size * 0.8f, size));
                    slot = full ? SlotFull : SlotEmpty;
                }
                _lists[slot].Add(m);
                total++;
                min = Vector3.Min(min, p); max = Vector3.Max(max, p);
            }
            if (total == 0) { _bounds = new Bounds(Vector3.zero, Vector3.one); return; }
            _bounds = new Bounds((min + max) * 0.5f, (max - min) + Vector3.one * 4f);
        }

        private void EnsureAssets()
        {
            if (_mesh == null) _mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            if (_stoolMesh == null) _stoolMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            if (_mats[SlotFull] == null) _mats[SlotFull] = MakeMaterial(new Color(0.2f, 0.65f, 0.2f));
            if (_mats[SlotEmpty] == null) _mats[SlotEmpty] = MakeMaterial(new Color(0.45f, 0.35f, 0.2f));
            if (_mats[SlotHazelFull] == null) _mats[SlotHazelFull] = MakeMaterial(new Color(0.45f, 0.5f, 0.15f));
            if (_mats[SlotHazelCut] == null) _mats[SlotHazelCut] = MakeMaterial(new Color(0.4f, 0.28f, 0.14f));
            if (_mats[SlotWillowFull] == null) _mats[SlotWillowFull] = MakeMaterial(new Color(0.75f, 0.7f, 0.25f));
            if (_mats[SlotWillowCut] == null) _mats[SlotWillowCut] = MakeMaterial(new Color(0.7f, 0.6f, 0.4f));
        }

        private static Material MakeMaterial(Color c)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color");
            var m = new Material(shader) { color = c, enableInstancing = true };
            return m;
        }

        private void Draw(List<Matrix4x4> list, Material mat, Mesh mesh)
        {
            if (list.Count == 0 || mat == null || mesh == null) return;
            var rp = new RenderParams(mat) { shadowCastingMode = ShadowCastingMode.On, receiveShadows = true, worldBounds = _bounds };
            for (int start = 0; start < list.Count; start += InstancedDraw.MaxBatch)
            {
                int n = Mathf.Min(InstancedDraw.MaxBatch, list.Count - start);
                list.CopyTo(start, _scratch, 0, n);
                Graphics.RenderMeshInstanced(rp, mesh, 0, _scratch, n, 0);
            }
        }

        private void OnDestroy()
        {
            foreach (var m in _mats) if (m != null) Destroy(m);
        }
    }
}
