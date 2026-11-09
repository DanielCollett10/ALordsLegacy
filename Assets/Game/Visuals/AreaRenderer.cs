using System.Collections.Generic;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Terrain;
using UnityEngine;

namespace LordsLegacy.Game.Visuals
{
    /// <summary>
    /// Draws confirmed areas draped on the Sim terrain: a light fill tint plus an outline per area (one child object
    /// each). Building areas get a solid warm outline, work areas a dashed blue one. Rebuilds when
    /// <c>AreaRegistry.Version</c> changes. Also draws one draft area (<see cref="ShowPreview"/>): outline, vertex dots
    /// and edge middle handles, all red when the draft is invalid. No colliders, so areas cannot block picking.
    /// </summary>
    public sealed class AreaRenderer : MonoBehaviour
    {
        /// <summary>Height of area meshes above the terrain, in metres.</summary>
        public const float HeightOffsetM = 0.15f;
        /// <summary>Extra lift of the draft so it draws over confirmed areas.</summary>
        public const float PreviewLiftM = 0.04f;
        /// <summary>Outline ribbon width in metres.</summary>
        public const float OutlineWidthM = 0.35f;
        /// <summary>Dash and gap lengths of a work-area outline, in metres.</summary>
        public const float DashM = 2f, GapM = 1.2f;

        /// <summary>Outline colour of a building area.</summary>
        public static readonly Color BuildingOutline = new Color(0.95f, 0.8f, 0.35f, 1f);
        /// <summary>Fill tint of a building area.</summary>
        public static readonly Color BuildingFill = new Color(0.95f, 0.8f, 0.35f, 0.18f);
        /// <summary>Outline colour of a work area (dashed).</summary>
        public static readonly Color WorkOutline = new Color(0.25f, 0.55f, 1f, 1f);
        /// <summary>Fill tint of a work area.</summary>
        public static readonly Color WorkFill = new Color(0.25f, 0.55f, 1f, 0.15f);
        /// <summary>Colour of an invalid draft (outline, dots, handles).</summary>
        public static readonly Color InvalidColor = new Color(0.9f, 0.1f, 0.1f, 1f);
        /// <summary>Fill tint of an invalid draft.</summary>
        public static readonly Color InvalidFill = new Color(0.9f, 0.1f, 0.1f, 0.2f);
        /// <summary>Colour of the vertex dots of a valid draft.</summary>
        public static readonly Color DotColor = Color.white;
        /// <summary>Colour of the edge middle handles of a valid draft.</summary>
        public static readonly Color HandleColor = new Color(1f, 0.9f, 0.4f, 1f);

        [SerializeField] private SimHost host;

        private readonly Dictionary<int, GameObject> _areas = new Dictionary<int, GameObject>();
        private int _builtVersion = -1;
        private Material _material;
        private GameObject _previewObject;
        private Mesh _previewMesh;

        /// <summary>Number of confirmed-area objects currently built.</summary>
        public int AreaCount => _areas.Count;

        /// <summary>The mesh built for an area id, or null when none.</summary>
        public Mesh GetAreaMesh(int areaId) =>
            _areas.TryGetValue(areaId, out var go) ? go.GetComponent<MeshFilter>().sharedMesh : null;

        /// <summary>The draft mesh, or null while no preview is shown.</summary>
        public Mesh PreviewMesh => _previewObject != null && _previewObject.activeSelf ? _previewMesh : null;

        /// <summary>Sets the Sim host (for code that adds the component at run time).</summary>
        public void Configure(SimHost simHost)
        {
            host = simHost;
            _builtVersion = -1;
        }

        private void Update() { Sync(); }

        /// <summary>Brings the area meshes in line with the Sim registry if its version changed.</summary>
        public void Sync()
        {
            if (host == null || !host.IsInitialized) return;
            var areas = host.World.Areas;
            if (areas.Version == _builtVersion) return;
            _builtVersion = areas.Version;

            var live = new HashSet<int>();
            foreach (var a in areas.All) live.Add(a.Id);
            var gone = new List<int>();
            foreach (var id in _areas.Keys) if (!live.Contains(id)) gone.Add(id);
            foreach (var id in gone) { Discard(_areas[id]); _areas.Remove(id); }

            foreach (var a in areas.All)
            {
                if (_areas.ContainsKey(a.Id)) continue; // records are immutable, ids never reused
                var type = host.World.AreaTypes.Find(a.TypeId);
                bool work = type != null && type.Kind == AreaKind.Work;
                var b = new DrapeMesh();
                AddFill(b, a.Polygon, work ? WorkFill : BuildingFill, HeightOffsetM);
                AddOutline(b, a.Polygon, true, OutlineWidthM, work ? WorkOutline : BuildingOutline, HeightOffsetM + 0.01f, work);
                var go = new GameObject(a.Name);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh(a.Name);
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = GetMaterial();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                _areas[a.Id] = go;
            }
        }

        /// <summary>
        /// Draws a draft area: fill, outline (closed when the polygon has 3+ points), a dot per shape vertex and a
        /// handle at the middle of each edge. Everything turns red when the preview is invalid.
        /// </summary>
        public void ShowPreview(AreaPreview preview, AreaShape shape)
        {
            if (preview == null || shape == null || host == null || !host.IsInitialized || shape.Vertices.Count == 0)
            { HidePreview(); return; }
            bool valid = preview.IsValid;
            var tint = valid ? DotColor : InvalidColor;
            var outline = valid ? BuildingOutline : InvalidColor;
            IReadOnlyList<PointMm> poly = preview.Polygon.Count >= 2 ? preview.Polygon : shape.Vertices;
            float lift = HeightOffsetM + PreviewLiftM;

            var b = new DrapeMesh();
            // AR-32: fill only when the flattened outline is simple; otherwise no fill and a red cross per crossing.
            PreviewCrossCount = 0;
            PreviewFillAreaM2 = 0.0;
            PreviewFillTriangleCount = 0;
            bool simple = poly.Count < 3 || Polygons.IsSimple(poly);
            if (poly.Count >= 3 && simple)
            {
                var fill = TerrainDrape.Fill(Ground, poly, lift, valid ? BuildingFill : InvalidFill);
                PreviewFillTriangleCount = fill.Triangles.Count / 3;
                PreviewFillAreaM2 = PlanArea(fill);
                b.Append(fill);
            }
            AddOutline(b, poly, poly.Count >= 3, OutlineWidthM, simple ? outline : InvalidColor, lift + 0.01f, false);
            var crosses = new DrapeMesh();
            if (!simple)
            {
                foreach (var c in Polygons.Crossings(poly)) { AddCross(crosses, c, lift + 0.06f); PreviewCrossCount++; }
                b.Append(crosses);
            }
            var m = new DrapeMesh(); // dots and handles only: shown instead of b while the ghost draws the whole shape
            var verts = shape.Vertices;
            for (int i = 0; i < verts.Count; i++)
            {
                if (shape.Edges.Count > i && (verts.Count >= 3 || i < verts.Count - 1))
                {
                    AddDiamond(b, EdgeMiddle(shape, i), 0.45f, valid ? HandleColor : InvalidColor, lift + 0.03f);
                    AddDiamond(m, EdgeMiddle(shape, i), 0.45f, valid ? HandleColor : InvalidColor, lift + 0.03f);
                }
            }
            for (int i = 0; i < verts.Count; i++)
            {
                AddDiamond(b, verts[i], 0.7f, tint, lift + 0.05f);
                AddDiamond(m, verts[i], 0.7f, tint, lift + 0.05f);
            }

            m.Append(crosses);
            if (_previewObject == null)
            {
                _previewObject = new GameObject("Area preview");
                _previewObject.transform.SetParent(transform, false);
                _previewObject.AddComponent<MeshFilter>();
                var mr = _previewObject.AddComponent<MeshRenderer>();
                mr.sharedMaterial = GetMaterial();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            if (_previewMesh != null) Destroy(_previewMesh);
            _previewMesh = b.ToMesh("Area preview");
            if (_previewMarksMesh != null) Destroy(_previewMarksMesh);
            _previewMarksMesh = m.ToMesh("Area preview marks");
            _previewObject.SetActive(true);
            ApplyPreviewMesh();
        }

        private Mesh _previewMarksMesh;

        /// <summary>Red crosses drawn on the draft (one per self-crossing point, AR-32).</summary>
        public int PreviewCrossCount { get; private set; }
        /// <summary>Triangles in the draft's fill (0 when the outline is not simple).</summary>
        public int PreviewFillTriangleCount { get; private set; }
        /// <summary>Plan-view area of the draft's fill in m².</summary>
        public double PreviewFillAreaM2 { get; private set; }

        private static double PlanArea(DrapeMesh mesh)
        {
            double sum = 0;
            var t = mesh.Triangles; var v = mesh.Vertices;
            for (int i = 0; i + 2 < t.Count; i += 3)
            {
                var a = v[t[i]]; var c = v[t[i + 1]]; var d = v[t[i + 2]];
                sum += System.Math.Abs(((double)c.x - a.x) * ((double)d.z - a.z) - ((double)d.x - a.x) * ((double)c.z - a.z)) * 0.5;
            }
            return sum;
        }

        private void AddCross(DrapeMesh b, PointMm p, float lift)
        {
            float x = (float)p.XMetres, z = (float)p.ZMetres, r = 0.9f;
            b.Append(TerrainDrape.Ribbon(Ground, new List<Vector2> { new Vector2(x - r, z - r), new Vector2(x + r, z + r) }, 0.3f, lift, InvalidColor, false, false));
            b.Append(TerrainDrape.Ribbon(Ground, new List<Vector2> { new Vector2(x - r, z + r), new Vector2(x + r, z - r) }, 0.3f, lift, InvalidColor, false, false));
        }

        // While the ghost is up it draws the one total shape, so the placed shape's own fill/outline is left out.
        private void ApplyPreviewMesh()
        {
            if (_previewObject == null) return;
            _previewObject.GetComponent<MeshFilter>().sharedMesh = GhostEdgeCount > 0 ? _previewMarksMesh : _previewMesh;
        }

        /// <summary>Hides the draft area.</summary>
        public void HidePreview()
        {
            PreviewCrossCount = 0; PreviewFillTriangleCount = 0; PreviewFillAreaM2 = 0.0;
            if (_previewObject != null) _previewObject.SetActive(false);
        }

        /// <summary>Colour of the faint ghost edges (AR-21).</summary>
        public static readonly Color GhostColor = new Color(0.95f, 0.8f, 0.35f, 0.6f);

        /// <summary>Colour of the faint ghost fill (AR-21).</summary>
        public static readonly Color GhostFill = new Color(1f, 1f, 1f, 0.12f);

        private GameObject _ghostObject;
        private Mesh _ghostMesh;

        /// <summary>Number of ghost edges currently drawn (0 when hidden).</summary>
        public int GhostEdgeCount { get; private set; }

        /// <summary>
        /// Draws the faint ghost edges last point -> cursor -> first point (AR-21). With a single placed point
        /// (<paramref name="last"/> equals <paramref name="first"/>) only one edge is drawn. Not validated.
        /// </summary>
        public void ShowGhost(IReadOnlyList<PointMm> points, PointMm cursor)
        {
            if (host == null || !host.IsInitialized || points == null || points.Count == 0) { HideGhost(); return; }
            var last = points[points.Count - 1];
            var first = points[0];
            var b = new DrapeMesh();
            float lift = HeightOffsetM + PreviewLiftM + 0.02f;
            int edges = 0;
            if (points.Count >= 2)
            {
                // Fill the whole shape the click would make (as placed, after the hourglass auto-fix), not just the new edges.
                var poly = new List<PointMm>(points) { cursor };
                if (poly.Count >= 3 && AreaUntangler.TryUntangle(poly, out int[] order))
                {
                    var re = new List<PointMm>(poly.Count);
                    foreach (int k in order) re.Add(poly[k]);
                    poly = re;
                }
                // One total shape: fill and closed outline of the whole polygon, drawn instead of the placed shape's own.
                bool simple = Polygons.IsSimple(poly);
                if (simple) AddFill(b, poly, BuildingFill, lift - 0.01f); // AR-32: no misleading fill when crossing
                AddOutline(b, poly, true, OutlineWidthM, simple ? GhostColor : InvalidColor, lift, false);
                if ((V(last) - V(cursor)).magnitude >= 1e-4f) edges++;
                if (last != first && (V(cursor) - V(first)).magnitude >= 1e-4f) edges++;
            }
            else if ((V(last) - V(cursor)).magnitude >= 1e-4f)
            {
                var p0 = V(last); var p1 = V(cursor);
                b.Append(TerrainDrape.Ribbon(Ground, new List<Vector2> { p0, p1 }, OutlineWidthM * 0.6f, lift, GhostColor, false, false));
                edges++;
            }
            if (edges == 0) { HideGhost(); return; }
            if (_ghostObject == null)
            {
                _ghostObject = new GameObject("Area ghost");
                _ghostObject.transform.SetParent(transform, false);
                _ghostObject.AddComponent<MeshFilter>();
                var mr = _ghostObject.AddComponent<MeshRenderer>();
                mr.sharedMaterial = GetMaterial();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            if (_ghostMesh != null) Destroy(_ghostMesh);
            _ghostMesh = b.ToMesh("Area ghost");
            _ghostObject.GetComponent<MeshFilter>().sharedMesh = _ghostMesh;
            _ghostObject.SetActive(true);
            GhostEdgeCount = edges;
            ApplyPreviewMesh();
        }

        /// <summary>Hides the ghost edges.</summary>
        public void HideGhost()
        {
            GhostEdgeCount = 0;
            if (_ghostObject != null) _ghostObject.SetActive(false);
            ApplyPreviewMesh();
        }

        private static PointMm EdgeMiddle(AreaShape shape, int i)
        {
            var a = shape.Vertices[i];
            var e = shape.Edges[i];
            if (e.Kind == AreaEdgeKind.Bent) return e.Handle;
            var c = shape.Vertices[(i + 1) % shape.Vertices.Count];
            return new PointMm((a.X + c.X) / 2, (a.Z + c.Z) / 2);
        }

        private IHeightField Ground => host.World.Terrain;

        private static Vector2 V(PointMm p) => new Vector2((float)p.XMetres, (float)p.ZMetres);

        private static List<Vector2> Plan(IReadOnlyList<PointMm> pts)
        {
            var l = new List<Vector2>(pts.Count);
            foreach (var p in pts) l.Add(V(p));
            return l;
        }

        private void AddDiamond(DrapeMesh b, PointMm p, float radius, Color c, float lift)
        {
            float x = (float)p.XMetres, z = (float)p.ZMetres;
            int i = b.Add(TerrainDrape.Point(Ground, x - radius, z, lift), c);
            b.Add(TerrainDrape.Point(Ground, x, z + radius, lift), c);
            b.Add(TerrainDrape.Point(Ground, x + radius, z, lift), c);
            b.Add(TerrainDrape.Point(Ground, x, z - radius, lift), c);
            b.Tri(i, i + 1, i + 2);
            b.Tri(i, i + 2, i + 3);
        }

        private void AddFill(DrapeMesh b, IReadOnlyList<PointMm> polygon, Color c, float lift) =>
            b.Append(TerrainDrape.Fill(Ground, polygon, lift, c));

        private void AddOutline(DrapeMesh b, IReadOnlyList<PointMm> pts, bool closed, float width, Color c, float lift, bool dashed) =>
            b.Append(TerrainDrape.Ribbon(Ground, Plan(pts), width, lift, c, closed, dashed, DashM, GapM));

        private Material GetMaterial()
        {
            if (_material == null)
            {
                var shared = GroundOverlay.Material();
                _material = shared != null
                    ? new Material(shared) { name = "AreaMaterial" }
                    : new Material(Shader.Find("Sprites/Default")) { name = "AreaMaterial" };
            }
            return _material;
        }

        private static void Discard(GameObject go)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) Destroy(mf.sharedMesh);
            Destroy(go);
        }

        private void OnDestroy()
        {
            if (_previewMesh != null) Destroy(_previewMesh);
            if (_ghostMesh != null) Destroy(_ghostMesh);
            if (_previewMarksMesh != null) Destroy(_previewMarksMesh);
            if (_material != null) Destroy(_material);
        }
    }
}
