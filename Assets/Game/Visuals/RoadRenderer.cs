using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;
using UnityEngine;

namespace LordsLegacy.Game.Visuals
{
    /// <summary>
    /// Draws confirmed roads as flat strips that follow the terrain: one mesh (one child object) per
    /// <see cref="RoadSegment"/>, built from its samples, its road type's width and the Sim terrain height plus
    /// <see cref="HeightOffsetM"/>. Rebuilds when <c>RoadNetwork.Version</c> changes. Also draws one draft road
    /// (<see cref="ShowPreview"/>) with the too-steep sample ranges coloured red. No colliders, so roads cannot
    /// block picking.
    /// </summary>
    public sealed class RoadRenderer : MonoBehaviour
    {
        /// <summary>Height of the strips above the terrain, in metres.</summary>
        public const float HeightOffsetM = 0.05f;
        /// <summary>Extra lift of the draft road so it draws over a road beneath it.</summary>
        public const float PreviewLiftM = 0.03f;
        /// <summary>Colour of the invalid stretches of a draft road.</summary>
        public static readonly Color InvalidColor = new Color(0.9f, 0.1f, 0.1f, 1f);
        /// <summary>Colour of the valid stretches of a draft road.</summary>
        public static readonly Color ValidPreviewColor = new Color(0.95f, 0.85f, 0.6f, 1f);

        [SerializeField] private SimHost host;
        [SerializeField] private VisualRegistry registry;

        private readonly Dictionary<int, GameObject> _segments = new Dictionary<int, GameObject>();
        private int _builtVersion = -1;
        private Material _fallbackMaterial;
        private Material _previewMaterial;
        private GameObject _previewObject;
        private Mesh _previewMesh;

        /// <summary>Number of segment meshes currently built.</summary>
        public int SegmentMeshCount => _segments.Count;

        /// <summary>The mesh built for a segment id, or null when none.</summary>
        public Mesh GetSegmentMesh(int segmentId) =>
            _segments.TryGetValue(segmentId, out var go) ? go.GetComponent<MeshFilter>().sharedMesh : null;

        /// <summary>The draft road mesh, or null while no preview is shown.</summary>
        public Mesh PreviewMesh => _previewObject != null && _previewObject.activeSelf ? _previewMesh : null;

        /// <summary>Sets the references (for code that adds the component at run time).</summary>
        public void Configure(SimHost simHost, VisualRegistry visualRegistry)
        {
            host = simHost;
            registry = visualRegistry;
            _builtVersion = -1;
        }

        private void Update() { Sync(); }

        /// <summary>Brings the segment meshes in line with the Sim network if its version changed.</summary>
        public void Sync()
        {
            if (host == null || !host.IsInitialized) return;
            var roads = host.World.Roads;
            if (roads.Version == _builtVersion) return;
            _builtVersion = roads.Version;

            var live = new HashSet<int>();
            foreach (var seg in roads.Segments) live.Add(seg.Id);
            var gone = new List<int>();
            foreach (var id in _segments.Keys) if (!live.Contains(id)) gone.Add(id);
            foreach (var id in gone) Discard(_segments[id], true, id);

            foreach (var seg in roads.Segments)
            {
                if (_segments.ContainsKey(seg.Id)) continue; // segments are immutable, ids never reused
                var def = RoadTypes.Find(seg.TypeId);
                float width = def != null ? (float)def.WidthM : 2.5f;
                var go = new GameObject("Road " + seg.Id);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = BuildMesh(seg.Samples, width, HeightOffsetM, null, "Road " + seg.Id);
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = RoadMaterial(def != null ? def.VisualId : "visual.road.dirt_path");
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _segments[seg.Id] = go;
            }
        }

        /// <summary>Draws a draft road. Samples inside <c>InvalidRanges</c> (inclusive) get red vertices.</summary>
        public void ShowPreview(RoadPreview preview, float width)
        {
            if (preview == null || host == null || !host.IsInitialized || preview.Samples.Count < 2) { HidePreview(); return; }
            if (_previewObject == null)
            {
                _previewObject = new GameObject("Road preview");
                _previewObject.transform.SetParent(transform, false);
                _previewObject.AddComponent<MeshFilter>();
                var mr = _previewObject.AddComponent<MeshRenderer>();
                mr.sharedMaterial = PreviewMaterial();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var colors = new Color[preview.Samples.Count];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = InRanges(preview.InvalidRanges, i) ? InvalidColor : ValidPreviewColor;
            if (_previewMesh != null) Destroy(_previewMesh);
            _previewMesh = BuildMesh(preview.Samples, width, HeightOffsetM + PreviewLiftM, colors, "Road preview");
            _previewObject.GetComponent<MeshFilter>().sharedMesh = _previewMesh;
            _previewObject.SetActive(true);
        }

        /// <summary>Hides the draft road.</summary>
        public void HidePreview()
        {
            if (_previewObject != null) _previewObject.SetActive(false);
        }

        private static bool InRanges(IReadOnlyList<InvalidRange> ranges, int index)
        {
            for (int r = 0; r < ranges.Count; r++)
                if (index >= ranges[r].StartIndex && index <= ranges[r].EndIndex) return true;
            return false;
        }

        private Mesh BuildMesh(IReadOnlyList<PointMm> samples, float width, float lift, Color[] sampleColors, string name)
        {
            int n = samples.Count;
            var terrain = host.World.Terrain;
            var verts = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            var cols = sampleColors != null ? new Color[n * 2] : null;
            float half = width * 0.5f;
            float along = 0f;
            for (int i = 0; i < n; i++)
            {
                float x = (float)samples[i].XMetres, z = (float)samples[i].ZMetres;
                int a = Mathf.Max(0, i - 1), b = Mathf.Min(n - 1, i + 1);
                var d = new Vector2((float)(samples[b].XMetres - samples[a].XMetres), (float)(samples[b].ZMetres - samples[a].ZMetres));
                d = d.sqrMagnitude > 1e-12f ? d.normalized : Vector2.right;
                var right = new Vector2(d.y, -d.x);
                if (i > 0)
                    along += (float)samples[i].DistanceTo(samples[i - 1]) / 1000f;
                // Height is sampled at the edge vertices so the strip hugs a cross slope.
                float lx = x - right.x * half, lz = z - right.y * half;
                float rx = x + right.x * half, rz = z + right.y * half;
                verts[i * 2] = new Vector3(lx, terrain.HeightAt(lx, lz) + lift, lz);
                verts[i * 2 + 1] = new Vector3(rx, terrain.HeightAt(rx, rz) + lift, rz);
                uvs[i * 2] = new Vector2(0f, along);
                uvs[i * 2 + 1] = new Vector2(1f, along);
                if (cols != null) { cols[i * 2] = sampleColors[i]; cols[i * 2 + 1] = sampleColors[i]; }
            }
            var tris = new int[(n - 1) * 6];
            for (int i = 0; i < n - 1; i++)
            {
                int l0 = i * 2, r0 = l0 + 1, l1 = l0 + 2, r1 = l0 + 3;
                int t = i * 6;
                tris[t] = l0; tris[t + 1] = l1; tris[t + 2] = r0;
                tris[t + 3] = r0; tris[t + 4] = l1; tris[t + 5] = r1;
            }
            var mesh = new Mesh { name = name };
            mesh.vertices = verts;
            mesh.uv = uvs;
            if (cols != null) mesh.colors = cols;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private Material RoadMaterial(string visualId)
        {
            if (registry != null && registry.TryGet(visualId, out var def) && def.Materials.Length > 0 && def.Materials[0] != null)
                return def.Materials[0];
            if (_fallbackMaterial == null)
            {
                _fallbackMaterial = new Material(Shader.Find("Standard")) { name = "RoadFallback" };
                _fallbackMaterial.color = new Color(0.55f, 0.42f, 0.28f);
            }
            return _fallbackMaterial;
        }

        private Material PreviewMaterial()
        {
            if (_previewMaterial == null)
            {
                // Sprites/Default is unlit, double-sided and multiplies by vertex colour.
                var shader = Shader.Find("Sprites/Default");
                _previewMaterial = new Material(shader != null ? shader : Shader.Find("Standard")) { name = "RoadPreview" };
            }
            return _previewMaterial;
        }

        private void Discard(GameObject go, bool remove, int id)
        {
            if (remove) _segments.Remove(id);
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) Destroy(mf.sharedMesh);
            Destroy(go);
        }

        private void OnDestroy()
        {
            if (_previewMesh != null) Destroy(_previewMesh);
            if (_fallbackMaterial != null) Destroy(_fallbackMaterial);
            if (_previewMaterial != null) Destroy(_previewMaterial);
        }
    }
}
