using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;

namespace LordsLegacy.Sim.Areas
{
    /// <summary>
    /// Helpers for NearArea edges (AR-25): another area's flattened outline is used like a road side line. The outline is
    /// counter-clockwise, so "away from the area" is the right-hand side of its direction of travel.
    /// </summary>
    internal static class NearAreaGeometry
    {
        /// <summary>The outline as a closed line (first point repeated at the end).</summary>
        public static PointMm[] ClosedRing(IReadOnlyList<PointMm> poly)
        {
            var line = new PointMm[poly.Count + 1];
            for (int i = 0; i < poly.Count; i++) line[i] = poly[i];
            line[poly.Count] = poly[0];
            return line;
        }

        /// <summary>True if p is within reach of the outline and not inside the area (5 cm slack).</summary>
        public static bool VertexOk(IReadOnlyList<PointMm> poly, PointMm p, double reachM)
        {
            if (poly == null || poly.Count < 3) return false;
            return NearRoadGeometry.VertexOk(ClosedRing(poly), RoadSide.Right, p, reachM);
        }

        /// <summary>
        /// The outline opened at the point nearest <paramref name="a"/> and running the shorter way round towards
        /// <paramref name="b"/>. <paramref name="side"/> is the outward side of the returned line's direction.
        /// </summary>
        public static PointMm[] Guide(IReadOnlyList<PointMm> poly, PointMm a, PointMm b, out RoadSide side)
        {
            PointMm[] closed = ClosedRing(poly);
            PolylineHit h = Polylines.Nearest(closed, a);
            int k = Math.Min(h.SegmentIndex, closed.Length - 2);
            var p = new PointMm((long)Math.Round(h.X, MidpointRounding.AwayFromZero), (long)Math.Round(h.Z, MidpointRounding.AwayFromZero));
            var line = new List<PointMm>(closed.Length + 2) { p };
            for (int i = k + 1; i < closed.Length; i++) Add(line, closed[i]);
            for (int i = 1; i <= k; i++) Add(line, closed[i]);
            Add(line, p);
            PointMm[] result = line.ToArray();
            side = RoadSide.Right;
            double total = Polylines.LengthMm(result);
            if (Polylines.Nearest(result, b).ArcLengthMm > total / 2.0)
            {
                Array.Reverse(result);
                side = RoadSide.Left;
            }
            return result;
        }

        private static void Add(List<PointMm> line, PointMm q)
        {
            if (line[line.Count - 1] != q) line.Add(q);
        }
    }
}
