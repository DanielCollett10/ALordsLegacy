using System;
using System.Collections.Generic;

namespace LordsLegacy.Sim.Geometry
{
    /// <summary>A counter-clockwise (or zero-area) triangle in integer millimetres.</summary>
    public readonly struct TriangleMm
    {
        /// <summary>First corner.</summary>
        public readonly PointMm A;
        /// <summary>Second corner.</summary>
        public readonly PointMm B;
        /// <summary>Third corner.</summary>
        public readonly PointMm C;

        /// <summary>Creates a triangle from its corners.</summary>
        public TriangleMm(PointMm a, PointMm b, PointMm c) { A = a; B = b; C = c; }

        /// <summary>Twice the area in mm² (exact, non-negative for triangles from the triangulator).</summary>
        public long Area2Mm => PointMm.Cross(A, B, C);
    }

    /// <summary>Splits simple polygons into triangles using integer tests only (decision 0012).</summary>
    public static class Triangulator
    {
        /// <summary>
        /// Ear-clip triangulation of a simple polygon (convex or concave, either winding; clockwise input is
        /// normalised first). Returns exactly n−2 triangles. Collinear vertices are removed as zero-area triangles,
        /// so the total area equals the polygon area exactly. Returns an empty list if the polygon has zero area.
        /// </summary>
        /// <exception cref="ArgumentException">Fewer than 3 points.</exception>
        public static List<TriangleMm> EarClip(IReadOnlyList<PointMm> polygon)
        {
            if (polygon == null || polygon.Count < 3)
                throw new ArgumentException("A polygon needs at least 3 points.", nameof(polygon));
            var result = new List<TriangleMm>();
            if (Polygons.SignedArea2Mm(polygon) == 0) return result;

            PointMm[] pts = Polygons.EnsureCounterClockwise(polygon);
            var idx = new List<int>(pts.Length);
            for (int i = 0; i < pts.Length; i++) idx.Add(i);

            while (idx.Count > 3)
            {
                int pick = FindEar(pts, idx);
                int m = idx.Count;
                result.Add(new TriangleMm(pts[idx[(pick + m - 1) % m]], pts[idx[pick]], pts[idx[(pick + 1) % m]]));
                idx.RemoveAt(pick);
            }
            result.Add(new TriangleMm(pts[idx[0]], pts[idx[1]], pts[idx[2]]));
            return result;
        }

        /// <summary>Picks the position in <paramref name="idx"/> to clip next.</summary>
        private static int FindEar(PointMm[] pts, List<int> idx)
        {
            int m = idx.Count;
            int firstConvex = -1;
            for (int k = 0; k < m; k++)
            {
                PointMm prev = pts[idx[(k + m - 1) % m]], cur = pts[idx[k]], next = pts[idx[(k + 1) % m]];
                long cross = PointMm.Cross(prev, cur, next);
                if (cross == 0) return k;            // collinear: clip as a zero-area triangle
                if (cross < 0) continue;             // reflex
                if (firstConvex < 0) firstConvex = k;
                bool blocked = false;
                for (int j = 0; j < m && !blocked; j++)
                {
                    if (j == k || j == (k + m - 1) % m || j == (k + 1) % m) continue;
                    blocked = InsideOrOn(prev, cur, next, pts[idx[j]]);
                }
                if (!blocked) return k;
            }
            // Only reachable for non-simple input: clip something so we still terminate.
            return firstConvex >= 0 ? firstConvex : 0;
        }

        /// <summary>True if p is inside or on the boundary of the CCW triangle abc (integer only).</summary>
        private static bool InsideOrOn(PointMm a, PointMm b, PointMm c, PointMm p) =>
            PointMm.Cross(a, b, p) >= 0 && PointMm.Cross(b, c, p) >= 0 && PointMm.Cross(c, a, p) >= 0;
    }
}
