using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Roads
{
    /// <summary>Control point clean-up (RD-02).</summary>
    public static class ControlPointFilter
    {
        /// <summary>
        /// Drops each point closer than <paramref name="minSpacingM"/> to the previous kept point. The last point is never
        /// dropped: if it is that close it replaces the previous point (and any earlier kept points it is also too close to,
        /// but never the first point). If only the first point remains, the last is appended unless it equals the first.
        /// </summary>
        public static List<PointMm> Clean(IReadOnlyList<PointMm> points, double minSpacingM)
        {
            var kept = new List<PointMm>();
            if (points == null || points.Count == 0) return kept;
            double minMm = minSpacingM * PointMm.MmPerMetre;
            kept.Add(points[0]);
            int n = points.Count;
            for (int i = 1; i < n - 1; i++)
                if (points[i].DistanceTo(kept[kept.Count - 1]) >= minMm) kept.Add(points[i]);
            if (n == 1) return kept;
            PointMm last = points[n - 1];
            while (kept.Count > 1 && last.DistanceTo(kept[kept.Count - 1]) < minMm) kept.RemoveAt(kept.Count - 1);
            if (last != kept[kept.Count - 1]) kept.Add(last);
            return kept;
        }
    }
}
