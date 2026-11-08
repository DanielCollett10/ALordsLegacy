using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Terrain;
using UnityEngine;

namespace LordsLegacy.Game.Visuals
{
    /// <summary>Plain mesh data produced by <see cref="TerrainDrape"/> (vertices, colours, up-facing triangles).</summary>
    public sealed class DrapeMesh
    {
        /// <summary>Vertex positions (Sim x/z = Unity x/z, y = height).</summary>
        public readonly List<Vector3> Vertices = new List<Vector3>();
        /// <summary>One colour per vertex.</summary>
        public readonly List<Color> Colors = new List<Color>();
        /// <summary>Triangle indices, three per triangle, wound so the face points up.</summary>
        public readonly List<int> Triangles = new List<int>();

        /// <summary>Adds a vertex and returns its index.</summary>
        public int Add(Vector3 v, Color c) { Vertices.Add(v); Colors.Add(c); return Vertices.Count - 1; }

        /// <summary>Adds a triangle, flipping the winding if needed so it faces +Y.</summary>
        public void Tri(int a, int b, int c)
        {
            var n = Vector3.Cross(Vertices[b] - Vertices[a], Vertices[c] - Vertices[a]);
            if (n.y >= 0f) { Triangles.Add(a); Triangles.Add(b); Triangles.Add(c); }
            else { Triangles.Add(a); Triangles.Add(c); Triangles.Add(b); }
        }

        /// <summary>Copies another mesh's data into this one.</summary>
        public void Append(DrapeMesh other)
        {
            int o = Vertices.Count;
            Vertices.AddRange(other.Vertices);
            Colors.AddRange(other.Colors);
            foreach (int i in other.Triangles) Triangles.Add(i + o);
        }

        /// <summary>Builds a Unity mesh from the data.</summary>
        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (Vertices.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(Vertices);
            m.SetColors(Colors);
            m.SetTriangles(Triangles, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }

    /// <summary>
    /// Shared helper that turns 2D lines and polygons (x, z in metres) into ground-hugging mesh data (decision 0020).
    /// Heights come from the Sim <see cref="IHeightField"/>, never from a collider. Lines are densified so no step
    /// exceeds a maximum; fills are subdivided until every triangle edge is short. Plan-view shape never changes.
    /// </summary>
    public static class TerrainDrape
    {
        /// <summary>Default largest plan-view step of a draped line, in metres.</summary>
        public const float DefaultMaxStepM = 1f;
        /// <summary>Largest triangle edge of a draped fill, in metres.</summary>
        public const float FillMaxEdgeM = 2f;
        /// <summary>Default dash length of a dashed ribbon, in metres.</summary>
        public const float DashM = 2f;
        /// <summary>Default gap length of a dashed ribbon, in metres.</summary>
        public const float GapM = 1.2f;

        /// <summary>A point on the ground at (x, z) raised by <paramref name="lift"/>.</summary>
        public static Vector3 Point(IHeightField terrain, float x, float z, float lift) =>
            new Vector3(x, terrain.HeightAt(x, z) + lift, z);

        /// <summary>
        /// Inserts points on each segment so no step exceeds <paramref name="maxStepM"/>. Every input point is kept
        /// exactly and in order; inserted points lie on the segments. When <paramref name="closed"/> the wrap segment
        /// last-to-first is densified too (the first point is not repeated).
        /// </summary>
        public static List<Vector2> Densify(IReadOnlyList<Vector2> polyline, float maxStepM = DefaultMaxStepM, bool closed = false)
        {
            var result = new List<Vector2>();
            if (polyline == null || polyline.Count == 0) return result;
            if (maxStepM < 0.01f) maxStepM = 0.01f;
            int n = polyline.Count;
            int segs = closed ? n : n - 1;
            for (int s = 0; s < segs; s++)
            {
                var a = polyline[s]; var b = polyline[(s + 1) % n];
                result.Add(a);
                int pieces = Mathf.CeilToInt((b - a).magnitude / maxStepM - 1e-4f);
                for (int k = 1; k < pieces; k++) result.Add(Vector2.Lerp(a, b, k / (float)pieces));
            }
            if (!closed) result.Add(polyline[n - 1]);
            return result;
        }

        /// <summary>
        /// Builds a strip along the polyline. Centre points are densified and draped (height + lift); the strip is
        /// <paramref name="width"/> wide in plan view. When <paramref name="dashed"/> only dash pieces are drawn, each
        /// densified too.
        /// </summary>
        public static DrapeMesh Ribbon(IHeightField terrain, IReadOnlyList<Vector2> polyline, float width, float lift,
            Color colour, bool closed, bool dashed, float dashM = DashM, float gapM = GapM)
        {
            var mesh = new DrapeMesh();
            if (polyline == null || polyline.Count < 2) return mesh;
            if (!dashed)
            {
                AddStrip(mesh, terrain, Densify(polyline, DefaultMaxStepM, closed), closed, width, lift, colour);
                return mesh;
            }
            foreach (var dash in SplitDashes(polyline, closed, dashM, gapM))
                AddStrip(mesh, terrain, Densify(dash), false, width, lift, colour);
            return mesh;
        }

        /// <summary>
        /// Triangulates the polygon with <see cref="Triangulator.EarClip"/> and subdivides every triangle until all
        /// its edges are at most <see cref="FillMaxEdgeM"/>, then drapes the vertices at height + lift.
        /// </summary>
        public static DrapeMesh Fill(IHeightField terrain, IReadOnlyList<PointMm> polygon, float lift, Color colour)
        {
            var mesh = new DrapeMesh();
            if (polygon == null || polygon.Count < 3) return mesh;
            foreach (var t in Triangulator.EarClip(polygon))
            {
                var a = V(t.A); var b = V(t.B); var c = V(t.C);
                float longest = Mathf.Max((a - b).magnitude, Mathf.Max((b - c).magnitude, (c - a).magnitude));
                // Halving the longest edge `depth` times gets it under the limit: the depth follows the triangle's size.
                int depth = longest <= FillMaxEdgeM ? 0 : Mathf.CeilToInt(Mathf.Log(longest / FillMaxEdgeM, 2f) - 1e-4f);
                Subdivide(mesh, terrain, a, b, c, lift, colour, depth);
            }
            return mesh;
        }

        private static void Subdivide(DrapeMesh m, IHeightField t, Vector2 a, Vector2 b, Vector2 c, float lift, Color col, int depth)
        {
            if (depth > 0)
            {
                var ab = (a + b) * 0.5f; var bc = (b + c) * 0.5f; var ca = (c + a) * 0.5f;
                Subdivide(m, t, a, ab, ca, lift, col, depth - 1);
                Subdivide(m, t, ab, b, bc, lift, col, depth - 1);
                Subdivide(m, t, ca, bc, c, lift, col, depth - 1);
                Subdivide(m, t, ab, bc, ca, lift, col, depth - 1);
                return;
            }
            int i = m.Add(Point(t, a.x, a.y, lift), col);
            m.Add(Point(t, b.x, b.y, lift), col);
            m.Add(Point(t, c.x, c.y, lift), col);
            m.Tri(i, i + 1, i + 2);
        }

        private static Vector2 V(PointMm p) => new Vector2((float)p.XMetres, (float)p.ZMetres);

        private static void AddStrip(DrapeMesh m, IHeightField terrain, List<Vector2> pts, bool closed, float width, float lift, Color col)
        {
            // Drop repeated points so directions are defined.
            var p = new List<Vector2>(pts.Count);
            foreach (var q in pts) if (p.Count == 0 || (q - p[p.Count - 1]).sqrMagnitude > 1e-8f) p.Add(q);
            if (closed && p.Count > 1 && (p[0] - p[p.Count - 1]).sqrMagnitude <= 1e-8f) p.RemoveAt(p.Count - 1);
            int n = p.Count;
            if (n < 2) return;
            float half = width * 0.5f;
            int first = m.Vertices.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 dirIn = i > 0 ? (p[i] - p[i - 1]).normalized : (closed ? (p[0] - p[n - 1]).normalized : Vector2.zero);
                Vector2 dirOut = i < n - 1 ? (p[i + 1] - p[i]).normalized : (closed ? (p[0] - p[n - 1]).normalized : Vector2.zero);
                if (dirIn == Vector2.zero) dirIn = dirOut;
                if (dirOut == Vector2.zero) dirOut = dirIn;
                var dir = dirIn + dirOut;
                dir = dir.sqrMagnitude < 1e-6f ? dirOut : dir.normalized;
                var side = new Vector2(-dir.y, dir.x);
                float miter = Mathf.Min(1f / Mathf.Max(Vector2.Dot(dir, dirOut), 0.5f), 2f); // keep sharp corners bounded
                var off = side * (half * miter);
                m.Add(Point(terrain, p[i].x - off.x, p[i].y - off.y, lift), col);
                m.Add(Point(terrain, p[i].x + off.x, p[i].y + off.y, lift), col);
            }
            int segs = closed ? n : n - 1;
            for (int s = 0; s < segs; s++)
            {
                int a = first + s * 2, b = first + ((s + 1) % n) * 2;
                m.Tri(a, a + 1, b + 1);
                m.Tri(a, b + 1, b);
            }
        }

        private static List<List<Vector2>> SplitDashes(IReadOnlyList<Vector2> pts, bool closed, float dashM, float gapM)
        {
            var result = new List<List<Vector2>>();
            float period = dashM + gapM;
            if (period < 0.05f || dashM < 0.01f) return result;
            int n = pts.Count;
            int segs = closed ? n : n - 1;
            float travelled = 0f;
            List<Vector2> cur = null;
            for (int s = 0; s < segs; s++)
            {
                var p0 = pts[s]; var p1 = pts[(s + 1) % n];
                float len = (p1 - p0).magnitude;
                if (len < 1e-4f) continue;
                var dir = (p1 - p0) / len;
                float d = 0f;
                while (d < len)
                {
                    float phase = (travelled + d) % period;
                    if (phase < dashM)
                    {
                        float end = Mathf.Min(len, d + (dashM - phase));
                        if (cur == null) { cur = new List<Vector2> { p0 + dir * d }; result.Add(cur); }
                        cur.Add(p0 + dir * end); // a dash running across a corner keeps its list; the corner is already in it
                        if (phase + (end - d) >= dashM - 1e-4f) cur = null; // dash complete
                        d = Mathf.Max(end, d + 0.01f);
                    }
                    else { cur = null; d += Mathf.Max(period - phase, 0.01f); }
                }
                travelled += len;
            }
            return result;
        }
    }
}
