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
            public Matrix4x4[] Matrices;
            public float MinX, MaxX, MinY, MaxY, MinZ, MaxZ;
            public float MaxHeight;
        }

        private Mesh _builtMesh;
        private float _builtScale;

        private readonly List<Cell> _cells = new List<Cell>();
        private readonly Plane[] _planes = new Plane[6];
        private bool _built;
        private int _builtVersion;

        /// <summary>Cells that passed the frustum test in the last drawn frame.</summary>
        public int VisibleCellCount { get; private set; }

        /// <summary>Tree instances submitted for drawing in the last drawn frame.</summary>
        public int DrawnInstanceCount { get; private set; }

        /// <summary>Total number of cells that contain trees.</summary>
        public int CellCount => _cells.Count;

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
            // Rebuild when the Sim's tree set changed (TreeRegistry.Version) or the view was invalidated.
            // Rebuilds all cells: the Version alone does not say which cell changed.
            if (!_built || host.World.Trees.Version != _builtVersion) Build();

            var def = registry.Get(TreeVisualId);
            var mesh = def.Mesh;
            var mats = def.Materials;
            if (mesh == null) return;

            if (mesh != _builtMesh || !Mathf.Approximately(def.Scale, _builtScale))
            {
                Build();
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

        private void Build()
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
                var cell = new Cell
                {
                    Matrices = new Matrix4x4[list.Count],
                    MinX = float.MaxValue, MinY = float.MaxValue, MinZ = float.MaxValue,
                    MaxX = float.MinValue, MaxY = float.MinValue, MaxZ = float.MinValue,
                };
                for (int i = 0; i < list.Count; i++)
                {
                    var t = list[i];
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
        }
    }
}
