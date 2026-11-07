using System;
using System.Collections.Generic;

namespace LordsLegacy.Sim.Geometry
{
    /// <summary>Where a point is relative to a polygon.</summary>
    public enum PointLocation
    {
        /// <summary>Outside.</summary>
        Outside,
        /// <summary>On an edge or vertex.</summary>
        Boundary,
        /// <summary>Strictly inside.</summary>
        Inside
    }

    /// <summary>Helpers for polygons stored as vertex lists (no repeated closing vertex).</summary>
    public static class Polygons
    {
        private const double MmSqPerMetreSq = 1e6;

        /// <summary>Twice the signed area in mm² (exact). Positive = counter-clockwise.</summary>
        public static long SignedArea2Mm(IReadOnlyList<PointMm> poly)
        {
            long sum = 0;
            for (int i = 0, n = poly.Count; i < n; i++)
            {
                PointMm a = poly[i], b = poly[(i + 1) % n];
                sum += a.X * b.Z - b.X * a.Z;
            }
            return sum;
        }

        /// <summary>Signed area in m². Positive when counter-clockwise.</summary>
        public static double SignedAreaM2(IReadOnlyList<PointMm> poly) => SignedArea2Mm(poly) / 2.0 / MmSqPerMetreSq;

        /// <summary>True if the vertices wind counter-clockwise (positive area).</summary>
        public static bool IsCounterClockwise(IReadOnlyList<PointMm> poly) => SignedArea2Mm(poly) > 0;

        /// <summary>Returns a counter-clockwise copy (reversed if the input was clockwise).</summary>
        public static PointMm[] EnsureCounterClockwise(IReadOnlyList<PointMm> poly)
        {
            var result = new PointMm[poly.Count];
            bool reverse = SignedArea2Mm(poly) < 0;
            for (int i = 0; i < result.Length; i++) result[i] = reverse ? poly[result.Length - 1 - i] : poly[i];
            return result;
        }

        /// <summary>
        /// True if the polygon has at least 3 vertices, no zero-length edges, and no edge touches or crosses a
        /// non-adjacent edge (adjacent edges may share only their common vertex). Exact.
        /// </summary>
        public static bool IsSimple(IReadOnlyList<PointMm> poly)
        {
            int n = poly.Count;
            if (n < 3) return false;
            for (int i = 0; i < n; i++)
                if (poly[i] == poly[(i + 1) % n]) return false;

            for (int i = 0; i < n; i++)
            {
                PointMm a1 = poly[i], a2 = poly[(i + 1) % n];
                for (int j = i + 1; j < n; j++)
                {
                    PointMm b1 = poly[j], b2 = poly[(j + 1) % n];
                    SegmentRelation r = Segments.Classify(a1, a2, b1, b2);
                    bool adjacent = j == i + 1 || (i == 0 && j == n - 1);
                    if (adjacent)
                    {
                        // Only the shared vertex may be common; any overlap or extra contact is a fault.
                        if (r == SegmentRelation.Crossing || r == SegmentRelation.CollinearOverlap) return false;
                    }
                    else if (r != SegmentRelation.None) return false;
                }
            }
            return SignedArea2Mm(poly) != 0;
        }

        /// <summary>Classifies a point against the polygon. Exact for any simple polygon.</summary>
        public static PointLocation Locate(IReadOnlyList<PointMm> poly, PointMm p)
        {
            int n = poly.Count;
            bool inside = false;
            for (int i = 0; i < n; i++)
            {
                PointMm a = poly[i], b = poly[(i + 1) % n];
                if (Segments.ContainsPoint(a, b, p)) return PointLocation.Boundary;
                if ((a.Z > p.Z) != (b.Z > p.Z))
                {
                    // Does the edge cross the horizontal ray to +X? Sign of cross depends on edge direction.
                    long cross = PointMm.Cross(a, b, p);
                    if (b.Z > a.Z ? cross > 0 : cross < 0) inside = !inside;
                }
            }
            return inside ? PointLocation.Inside : PointLocation.Outside;
        }

        /// <summary>True if the point is inside or on the boundary.</summary>
        public static bool Contains(IReadOnlyList<PointMm> poly, PointMm p) => Locate(poly, p) != PointLocation.Outside;

        /// <summary>Bounding box of the vertices. Throws if empty.</summary>
        public static BoundsMm Bounds(IReadOnlyList<PointMm> pts)
        {
            if (pts.Count == 0) throw new ArgumentException("No points.", nameof(pts));
            long minX = pts[0].X, maxX = minX, minZ = pts[0].Z, maxZ = minZ;
            for (int i = 1; i < pts.Count; i++)
            {
                minX = Math.Min(minX, pts[i].X); maxX = Math.Max(maxX, pts[i].X);
                minZ = Math.Min(minZ, pts[i].Z); maxZ = Math.Max(maxZ, pts[i].Z);
            }
            return new BoundsMm(new PointMm(minX, minZ), new PointMm(maxX, maxZ));
        }
    }
}
