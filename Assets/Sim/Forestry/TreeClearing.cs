using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Terrain;

namespace LordsLegacy.Sim.Forestry
{
    /// <summary>
    /// Removes the trees a road or building area clears (BLD-10-A/B, RD-11). Trees are tested by their trunk
    /// position, rounded to the nearest millimetre, with the exact geo-1 predicates. Removed trees yield nothing.
    /// </summary>
    public static class TreeClearing
    {
        /// <summary>Extra search margin, in metres, so float-to-mm rounding never drops a candidate at the edge.</summary>
        private const double QueryMarginM = 0.01;

        /// <summary>
        /// Removes every standing tree whose trunk lies inside or on the boundary of <paramref name="polygon"/>.
        /// The polygon is a vertex list without a repeated closing vertex; it should be simple (see
        /// <see cref="Polygons.IsSimple"/>). Repeated vertices are allowed. Throws <see cref="ArgumentException"/>
        /// for fewer than 3 vertices. Returns the removed ids in ascending order (empty if none).
        /// </summary>
        public static List<TreeId> RemoveInPolygon(TreeRegistry trees, IReadOnlyList<PointMm> polygon)
        {
            if (trees == null) throw new ArgumentNullException(nameof(trees));
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));
            if (polygon.Count < 3) throw new ArgumentException("A polygon needs at least 3 vertices.", nameof(polygon));

            var candidates = trees.QueryRect(ToRect(Polygons.Bounds(polygon), 0));
            var doomed = new List<TreeId>();
            foreach (var t in candidates)
                if (Polygons.Contains(polygon, PointMm.FromMetres(t.X, t.Z))) doomed.Add(t.Id);
            return RemoveAll(trees, doomed);
        }

        /// <summary>
        /// Removes every standing tree whose trunk is within <paramref name="distanceM"/> (inclusive) of the
        /// polyline. A single-point polyline is a disc around that point; repeated points are allowed.
        /// Throws <see cref="ArgumentException"/> for an empty polyline and <see cref="ArgumentOutOfRangeException"/>
        /// for a negative, NaN or infinite distance. Returns the removed ids in ascending order (empty if none).
        /// </summary>
        public static List<TreeId> RemoveNearPolyline(TreeRegistry trees, IReadOnlyList<PointMm> line, double distanceM)
        {
            if (trees == null) throw new ArgumentNullException(nameof(trees));
            if (line == null) throw new ArgumentNullException(nameof(line));
            if (line.Count == 0) throw new ArgumentException("Empty polyline.", nameof(line));
            if (!(distanceM >= 0) || double.IsInfinity(distanceM)) throw new ArgumentOutOfRangeException(nameof(distanceM));

            double maxMm = distanceM * PointMm.MmPerMetre;
            var candidates = trees.QueryRect(ToRect(Polygons.Bounds(line), distanceM));
            var doomed = new List<TreeId>();
            foreach (var t in candidates)
                if (Polylines.Nearest(line, PointMm.FromMetres(t.X, t.Z)).DistanceMm <= maxMm) doomed.Add(t.Id);
            return RemoveAll(trees, doomed);
        }

        private static List<TreeId> RemoveAll(TreeRegistry trees, List<TreeId> ids)
        {
            // Collected first, then removed: removing while enumerating the registry is not allowed.
            foreach (var id in ids) trees.Remove(id);
            return ids;
        }

        /// <summary>Metre rectangle around a millimetre box, widened by <paramref name="marginM"/> plus the search margin.</summary>
        private static Bounds2 ToRect(BoundsMm b, double marginM)
        {
            double m = marginM + QueryMarginM;
            return new Bounds2(
                (float)(b.Min.XMetres - m), (float)(b.Min.ZMetres - m),
                (float)(b.Max.XMetres + m), (float)(b.Max.ZMetres + m));
        }
    }
}
