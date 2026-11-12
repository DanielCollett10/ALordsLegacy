using System.Collections.Generic;
using LordsLegacy.Sim.Forestry;
using UnityEngine;
using UnityEngine.Rendering;

namespace LordsLegacy.Game.Visuals
{
    /// <summary>
    /// Draws every standing tree from <c>SimHost.World.Trees</c> with the 'visual.tree.generic' definition.
    /// Trees are grouped into 64 m cells; per-cell matrix arrays are built once, cells outside the camera
    /// frustum are skipped, and visible cells are drawn with <c>Graphics.RenderMeshInstanced</c> in batches of
    /// at most 1023. There are no per-tree GameObjects and no colliders, so trees cannot be selected (CMD-11).
    /// </summary>
    public sealed class TreeRenderer : MonoBehaviour
    {
        /// <summary>Visual id of the tree definition.</summary>
        public const string TreeVisualId = "visual.tree.generic";
        /// <summary>Edge length of a batching cell in metres.</summary>
        public const float CellSizeM = 64f;
        /// <summary>Maximum instances per draw call.</summary>
        public const int MaxBatch = 1023;

        [SerializeField] private SimHost host;
        [SerializeField] private VisualRegistry registry;
        [SerializeField] private Camera cullCamera;

        private sealed class Cell
        {
            public long Key;
            public int[] Ids;
            public Matrix4x4[] Matrices;
            public float MinX, MaxX, MinY, MaxY, MinZ, MaxZ;
            public float MaxHeight;
        }

        private Mesh _builtMesh;
        private float _builtScale;

        private readonly List<Cell> _cells = new List<Cell>();
        private readonly Dictionary<long, Cell> _byKey = new Dictionary<long, Cell>();
        private readonly Plane[] _planes = new Plane[6];
        private bool _built;
        private int _builtVersion;

        /// <summary>Cells that passed the frustum test in the last drawn frame.</summary>
        public int VisibleCellCount { get; private set; }

        /// <summary>Tree instances submitted for drawing in the last drawn frame.</summary>
        public int DrawnInstanceCount { get; private set; }

        /// <summary>Total number of cells that contain trees.</summary>
        public int CellCount => _cells.Count;

        /// <summary>The instance matrix array of the cell at <paramref name="index"/> (cells are in key order). For tests.</summary>
        public Matrix4x4[] GetCellMatrices(int index) => _cells[index].Matrices;

        /// <summary>Sets the references (for code that adds the component at run time).</summary>
        public void Configure(SimHost simHost, VisualRegistry visualRegistry, Camera camera)
        {
            host = simHost;
            registry = visualRegistry;
            cullCamera = camera;
            _built = false;
        }

        /// <summary>Forces the per-cell matrices to be rebuilt from the Sim on the next draw.</summary>
        public void Invalidate() { _built = false; }

        private void Update()
        {
            Draw();
        }

        /// <summary>Builds (if needed), culls and draws. Updates the counters.</summary>
        public void Draw()
        {
            VisibleCellCount = 0;
            DrawnInstanceCount = 0;
            if (host == null || !host.IsInitialized || registry == null || cullCamera == null) return;
            // Full build on first use or Invalidate(); when only the Sim's tree set changed
            // (TreeRegistry.Version) just the cells whose trees differ are rebuilt.
            if (!_built) Build(true);
            else if (host.World.Trees.Version != _builtVersion) Build(false);

            DrawDebris();

            var def = registry.Get(TreeVisualId);
            var mesh = def.Mesh;
            var mats = def.Materials;
            if (mesh == null) return;

            if (mesh != _builtMesh || !Mathf.Approximately(def.Scale, _builtScale))
            {
                Build(true);
                mesh = def.Mesh;
            }
            GeometryUtility.CalculateFrustumPlanes(cullCamera, _planes);

            foreach (var cell in _cells)
            {
                // Inflate by the tallest tree in the cell so trees near an edge are not culled early.
                float pad = cell.MaxHeight;
                var min = new Vector3(cell.MinX - pad, cell.MinY, cell.MinZ - pad);
                var max = new Vector3(cell.MaxX + pad, cell.MaxY + pad, cell.MaxZ + pad);
                var b = new Bounds((min + max) * 0.5f, max - min);
                if (!GeometryUtility.TestPlanesAABB(_planes, b)) continue;
                VisibleCellCount++;
                DrawnInstanceCount += cell.Matrices.Length;

                int subMeshes = Mathf.Min(mesh.subMeshCount, mats.Length);
                for (int s = 0; s < subMeshes; s++)
                {
                    var mat = mats[s];
                    if (mat == null || !mat.enableInstancing) continue;
                    var rp = new RenderParams(mat)
                    {
                        shadowCastingMode = ShadowCastingMode.On,
                        receiveShadows = true,
                        worldBounds = b,
                    };
                    for (int start = 0; start < cell.Matrices.Length; start += MaxBatch)
                    {
                        int n = Mathf.Min(MaxBatch, cell.Matrices.Length - start);
                        Graphics.RenderMeshInstanced(rp, mesh, s, cell.Matrices, n, start);
                    }
                }
            }
        }

        /// <summary>Stump instances drawn (T-280).</summary>
        public int StumpCount => _stumps.Count;

        /// <summary>Felled trunk instances drawn (T-280).</summary>
        public int FelledCount => _felled.Count;

        // Stumps and felled trunks (few, so one list each, rebuilt when TreeRegistry.Version changes).
        private readonly List<Matrix4x4> _stumps = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _felled = new List<Matrix4x4>();
        private Bounds _debrisBounds;
        private int _debrisVersion = -1;

        private void DrawDebris()
        {
            var world = host.World;
            registry.TryGet(VisualSizes.TreeStump, out var stumpDef);
            registry.TryGet(VisualSizes.TreeFelled, out var felledDef);
            if (world.Trees.Version != _debrisVersion)
            {
                _debrisVersion = world.Trees.Version;
                _stumps.Clear(); _felled.Clear();
                var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                foreach (var t in world.Trees.Stumps)
                {
                    var p = new Vector3(t.X, world.Terrain.HeightAt(t.X, t.Z), t.Z);
                    _stumps.Add(stumpDef != null ? VisualSizes.Place(stumpDef, VisualSizes.TreeStump, p, (t.Id.Value * 137.508f) % 360f)
                        : Matrix4x4.TRS(p, Quaternion.identity, Vector3.one));
                    min = Vector3.Min(min, p); max = Vector3.Max(max, p);
                }
                foreach (var t in world.Trees.Felled)
                {
                    float yaw = (t.Id.Value * 137.508f) % 360f;
                    float len = Mathf.Max(0.5f, (float)t.TrunkLengthM);
                    float d = VisualSizes.Of(VisualSizes.TreeFelled).x;
                    var b = new Vector3(t.X, world.Terrain.HeightAt(t.X, t.Z), t.Z);
                    var c = b + LyingPiece.Direction(yaw) * (len * 0.5f) + Vector3.up * (d * 0.5f);
                    _felled.Add(felledDef != null ? LyingPiece.Place(felledDef, c, yaw, d, len)
                        : Matrix4x4.TRS(c, Quaternion.identity, Vector3.one));
                    min = Vector3.Min(min, b - Vector3.one * len); max = Vector3.Max(max, b + Vector3.one * len);
                }
                _debrisBounds = new Bounds();
                if (_stumps.Count + _felled.Count > 0) _debrisBounds.SetMinMax(min - Vector3.one, max + Vector3.one * 2f);
            }
            InstancedDraw.Draw(stumpDef, _stumps, _debrisBounds);
            InstancedDraw.Draw(felledDef, _felled, _debrisBounds);
        }

        /// <summary>
        /// Syncs the cells with the Sim. <paramref name="full"/> rebuilds every cell; otherwise a cell is rebuilt
        /// only when its standing tree ids differ from what was built (unchanged cells keep their Matrices array),
        /// and cells with no trees are dropped.
        /// </summary>
        private void Build(bool full)
        {
            _built = true;
            _cells.Clear();
            var world = host.World;
            _builtVersion = world.Trees.Version;
            var def = registry.Get(TreeVisualId);
            // Each tree is scaled so its mesh is (trunk length x definition scale) metres tall, and its base
            // sits on the terrain. Without this a 1 m placeholder mesh is invisible at forest distances.
            _builtMesh = def.Mesh;
            _builtScale = def.Scale;
            var mb = _builtMesh != null ? _builtMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            float meshHeight = Mathf.Max(0.001f, mb.size.y);
            var groups = new Dictionary<long, List<TreeRecord>>();
            foreach (var t in world.Trees.All)
            {
                if (t.State != TreeState.Standing) continue;
                int cx = Mathf.FloorToInt(t.X / CellSizeM), cz = Mathf.FloorToInt(t.Z / CellSizeM);
                long key = ((long)cx << 32) ^ (uint)cz;
                if (!groups.TryGetValue(key, out var list)) { list = new List<TreeRecord>(); groups[key] = list; }
                list.Add(t);
            }

            var keys = new List<long>(groups.Keys);
            keys.Sort();
            foreach (var key in keys)
            {
                var list = groups[key];
                if (!full && _byKey.TryGetValue(key, out var existing) && SameTrees(existing.Ids, list))
                {
                    _cells.Add(existing);
                    continue;
                }
                var cell = new Cell
                {
                    Key = key,
                    Ids = new int[list.Count],
                    Matrices = new Matrix4x4[list.Count],
                    MinX = float.MaxValue, MinY = float.MaxValue, MinZ = float.MaxValue,
                    MaxX = float.MinValue, MaxY = float.MinValue, MaxZ = float.MinValue,
                };
                for (int i = 0; i < list.Count; i++)
                {
                    var t = list[i];
                    cell.Ids[i] = t.Id.Value;
                    float y = world.Terrain.HeightAt(t.X, t.Z);
                    // Deterministic yaw from the tree id so the forest does not look like copies.
                    float yaw = (t.Id.Value * 137.508f) % 360f;
                    float height = Mathf.Max(0.1f, (float)t.TrunkLengthM) * def.Scale;
                    float s = height / meshHeight;
                    var pos = new Vector3(t.X, y - mb.min.y * s, t.Z);
                    cell.Matrices[i] = Matrix4x4.TRS(pos, Quaternion.Euler(0f, yaw, 0f), Vector3.one * s);
                    cell.MaxHeight = Mathf.Max(cell.MaxHeight, height);
                    cell.MinX = Mathf.Min(cell.MinX, t.X); cell.MaxX = Mathf.Max(cell.MaxX, t.X);
                    cell.MinZ = Mathf.Min(cell.MinZ, t.Z); cell.MaxZ = Mathf.Max(cell.MaxZ, t.Z);
                    cell.MinY = Mathf.Min(cell.MinY, y); cell.MaxY = Mathf.Max(cell.MaxY, y);
                }
                _cells.Add(cell);
            }

            _byKey.Clear();
            foreach (var c in _cells) _byKey[c.Key] = c;
        }

        private static bool SameTrees(int[] ids, List<TreeRecord> trees)
        {
            if (ids.Length != trees.Count) return false;
            for (int i = 0; i < ids.Length; i++)
                if (ids[i] != trees[i].Id.Value) return false;
            return true;
        }
    }
}
