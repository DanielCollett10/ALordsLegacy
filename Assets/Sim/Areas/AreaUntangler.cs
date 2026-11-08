using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Areas
{
    /// <summary>
    /// Fixes a draft whose straight edges cross (an hourglass, AR-11) by reordering its points by angle around their
    /// centroid. The result is only offered when it is a simple polygon; the first point stays first.
    /// </summary>
    public static class AreaUntangler
    {
        /// <summary>
        /// Finds a point order that gives a simple polygon. Returns false (and an identity order) when the points are
        /// already simple, fewer than 3, or no angular order is simple. <paramref name="order"/>[k] is the index in
        /// <paramref name="points"/> of the point that comes k-th in the fixed polygon.
        /// </summary>
        public static bool TryUntangle(IReadOnlyList<PointMm> points, out int[] order)
        {
            int n = points.Count;
            order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            if (n < 3 || Polygons.IsSimple(points)) return false;

            double cx = 0, cz = 0;
            for (int i = 0; i < n; i++) { cx += points[i].X; cz += points[i].Z; }
            cx /= n; cz /= n;

            var idx = new int[n];
            var ang = new double[n];
            var dist = new double[n];
            for (int i = 0; i < n; i++)
            {
                idx[i] = i;
                double dx = points[i].X - cx, dz = points[i].Z - cz;
                ang[i] = Math.Atan2(dz, dx);
                dist[i] = dx * dx + dz * dz;
            }
            Array.Sort(idx, (a, b) =>
            {
                int c = ang[a].CompareTo(ang[b]);
                if (c != 0) return c;
                c = dist[a].CompareTo(dist[b]);
                return c != 0 ? c : a.CompareTo(b);
            });

            int start = Array.IndexOf(idx, 0);
            var sorted = new int[n];
            var poly = new PointMm[n];
            for (int k = 0; k < n; k++)
            {
                sorted[k] = idx[(start + k) % n];
                poly[k] = points[sorted[k]];
            }
            if (!Polygons.IsSimple(poly)) return false;
            order = sorted;
            return true;
        }
    }
}
