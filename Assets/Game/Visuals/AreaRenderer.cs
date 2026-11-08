using System.Collections.Generic;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Geometry;
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
        public const float HeightOffsetM = 0.08f;
        /// <summary>Extra lift of the draft so it draws over confirmed areas.</summary>
        public const float PreviewLiftM = 0.04f;
        /// <summary>Outline ribbon width in metres.</summary>
        public const float OutlineWidthM = 0.35f;
        /// <summary>Largest triangle edge of the fill before it is subdivided to follow the terrain, in metres.</summary>
        public const float FillMaxEdgeM = 4f;
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
                var type = AreaTypes.Find(a.TypeId);
                bool work = type != null && type.Kind == AreaKind.Work;
                var b = new MeshBuilder();
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

            var b = new MeshBuilder();
            if (poly.Count >= 3) AddFill(b, poly, valid ? BuildingFill : InvalidFill, lift);
            AddOutline(b, poly, poly.Count >= 3, OutlineWidthM, outline, lift + 0.01f, false);
            var verts = shape.Vertices;
            for (int i = 0; i < verts.Count; i++)
            {
                if (shape.Edges.Count > i && (verts.Count >= 3 || i < verts.Count - 1))
                    AddDiamond(b, EdgeMiddle(shape, i), 0.45f, valid ? HandleColor : InvalidColor, lift + 0.03f);
            }
            for (int i = 0; i < verts.Count; i++) AddDiamond(b, verts[i], 0.7f, tint, lift + 0.05f);

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
            _previewObject.GetComponent<MeshFilter>().sharedMesh = _previewMesh;
            _previewObject.SetActive(true);
        }

        /// <summary>Hides the draft area.</summary>
        public void HidePreview()
        {
            if (_previewObject != null) _previewObject.SetActive(false);
        }

        /// <summary>Colour of the faint ghost edges (AR-21).</summary>
        public static readonly Color GhostColor = new Color(1f, 1f, 1f, 0.35f);

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
            var b = new MeshBuilder();
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
                if (poly.Count >= 3) AddFill(b, poly, GhostFill, lift - 0.01f);
            }
            void Edge(PointMm a, PointMm c)
            {
                var p0 = V(a); var p1 = V(c);
                float len = (p1 - p0).magnitude;
                if (len < 1e-4f) return;
                AddRibbon(b, p0, p1, (p1 - p0) / len, OutlineWidthM * 0.6f, GhostColor, lift);
                edges++;
            }
            Edge(last, cursor);
            if (last != first) Edge(cursor, first);
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
        }

        /// <summary>Hides the ghost edges.</summary>
        public void HideGhost()
        {
            GhostEdgeCount = 0;
            if (_ghostObject != null) _ghostObject.SetActive(false);
        }

        private static PointMm EdgeMiddle(AreaShape shape, int i)
        {
            var a = shape.Vertices[i];
            var e = shape.Edges[i];
            if (e.Kind == AreaEdgeKind.Bent) return e.Handle;
            var c = shape.Vertices[(i + 1) % shape.Vertices.Count];
            return new PointMm((a.X + c.X) / 2, (a.Z + c.Z) / 2);
        }

        private Vector3 Drape(double x, double z, float lift) =>
            new Vector3((float)x, host.World.Terrain.HeightAt((float)x, (float)z) + lift, (float)z);

        private void AddDiamond(MeshBuilder b, PointMm p, float radius, Color c, float lift)
        {
            float x = (float)p.XMetres, z = (float)p.ZMetres;
            int i = b.Verts.Count;
            b.Add(Drape(x - radius, z, lift), c);
            b.Add(Drape(x, z + radius, lift), c);
            b.Add(Drape(x + radius, z, lift), c);
            b.Add(Drape(x, z - radius, lift), c);
            b.Quad(i, i + 1, i + 2, i + 3);
        }

        private void AddFill(MeshBuilder b, IReadOnlyList<PointMm> polygon, Color c, float lift)
        {
            foreach (var t in Triangulator.EarClip(polygon))
                Subdivide(b, V(t.A), V(t.B), V(t.C), c, lift, 0);
        }

        private static Vector2 V(PointMm p) => new Vector2((float)p.XMetres, (float)p.ZMetres);

        private void Subdivide(MeshBuilder b, Vector2 a, Vector2 bb, Vector2 c, Color col, float lift, int depth)
        {
            float longest = Mathf.Max((a - bb).magnitude, Mathf.Max((bb - c).magnitude, (c - a).magnitude));
            if (depth < 5 && longest > FillMaxEdgeM)
            {
                var ab = (a + bb) * 0.5f; var bc = (bb + c) * 0.5f; var ca = (c + a) * 0.5f;
                Subdivide(b, a, ab, ca, col, lift, depth + 1);
                Subdivide(b, ab, bb, bc, col, lift, depth + 1);
                Subdivide(b, ca, bc, c, col, lift, depth + 1);
                Subdivide(b, ab, bc, ca, col, lift, depth + 1);
                return;
            }
            int i = b.Verts.Count;
            b.Add(Drape(a.x, a.y, lift), col);
            b.Add(Drape(bb.x, bb.y, lift), col);
            b.Add(Drape(c.x, c.y, lift), col);
            b.Tri(i, i + 1, i + 2);
        }

        private void AddOutline(MeshBuilder b, IReadOnlyList<PointMm> pts, bool closed, float width, Color c, float lift, bool dashed)
        {
            int n = pts.Count;
            int segs = closed ? n : n - 1;
            float travelled = 0f;
            float period = DashM + GapM;
            for (int s = 0; s < segs; s++)
            {
                var p0 = V(pts[s]); var p1 = V(pts[(s + 1) % n]);
                float len = (p1 - p0).magnitude;
                if (len < 1e-4f) continue;
                var dir = (p1 - p0) / len;
                if (!dashed) AddRibbon(b, p0, p1, dir, width, c, lift);
                else
                {
                    float d = 0f;
                    while (d < len)
                    {
                        float phase = (travelled + d) % period;
                        if (phase < DashM)
                        {
                            float end = Mathf.Min(len, d + (DashM - phase));
                            AddRibbon(b, p0 + dir * d, p0 + dir * end, dir, width, c, lift);
                            d = Mathf.Max(end, d + 0.01f); // always advance, float rounding must not stall
                        }
                        else d += Mathf.Max(period - phase, 0.01f);
                    }
                }
                travelled += len;
            }
        }

        private void AddRibbon(MeshBuilder b, Vector2 p0, Vector2 p1, Vector2 dir, float width, Color c, float lift)
        {
            var side = new Vector2(-dir.y, dir.x) * (width * 0.5f);
            int i = b.Verts.Count;
            b.Add(Drape(p0.x - side.x, p0.y - side.y, lift), c);
            b.Add(Drape(p0.x + side.x, p0.y + side.y, lift), c);
            b.Add(Drape(p1.x + side.x, p1.y + side.y, lift), c);
            b.Add(Drape(p1.x - side.x, p1.y - side.y, lift), c);
            b.Quad(i, i + 1, i + 2, i + 3);
        }

        private Material GetMaterial()
        {
            if (_material == null)
            {
                // Sprites/Default is unlit, double-sided, alpha blended and multiplies by vertex colour.
                var shader = Shader.Find("Sprites/Default");
                _material = new Material(shader != null ? shader : Shader.Find("Standard")) { name = "AreaMaterial" };
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
            if (_material != null) Destroy(_material);
        }

        /// <summary>Collects vertices, colours and triangles; quads and triangles are emitted double-sided-safe by the material.</summary>
        private sealed class MeshBuilder
        {
            public readonly List<Vector3> Verts = new List<Vector3>();
            private readonly List<Color> _cols = new List<Color>();
            private readonly List<int> _tris = new List<int>();

            public void Add(Vector3 v, Color c) { Verts.Add(v); _cols.Add(c); }
            // Wound so the face points up (+Y) for the XZ layout used here.
            public void Tri(int a, int b, int c)
            {
                var n = Vector3.Cross(Verts[b] - Verts[a], Verts[c] - Verts[a]);
                if (n.y >= 0f) { _tris.Add(a); _tris.Add(b); _tris.Add(c); }
                else { _tris.Add(a); _tris.Add(c); _tris.Add(b); }
            }
            public void Quad(int a, int b, int c, int d) { Tri(a, b, c); Tri(a, c, d); }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                if (Verts.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(Verts);
                m.SetColors(_cols);
                m.SetTriangles(_tris, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }
    }
}
