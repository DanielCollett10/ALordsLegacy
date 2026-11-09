using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Areas
{
    /// <summary>
    /// AR-34: fits a building-area outline around the building areas it cuts into. Pure.
    /// </summary>
    internal static class AreaTrim
    {
        /// <summary>
        /// Subtracts every overlapping building area (overlap above the tolerance) from the outline.
        /// Returns true when the outline can be used: either nothing overlaps (<paramref name="trimmed"/> is the
        /// input, <paramref name="fittedAround"/> empty) or exactly one piece remains with every player vertex on it.
        /// Returns false when the fit would split the plot or lose a player point;
        /// <paramref name="firstNeighbour"/> is then the lowest-id neighbour that overlaps.
        /// </summary>
        public static bool TryFit(SimWorld world, AreaShape shape, PointMm[] poly, out PointMm[] trimmed,
            out List<string> fittedAround, out AreaRecord firstNeighbour)
        {
            trimmed = poly;
            fittedAround = new List<string>();
            firstNeighbour = null;
            if (poly.Length < 3) return true;

            double tol = world.Tuning.OverlapToleranceM2;
            BoundsMm b = Polygons.Bounds(poly);
            var neighbours = new List<AreaRecord>();
            foreach (AreaRecord other in world.Areas.QueryOverlapCandidates(b))
            {
                if (!other.Bounds.Intersects(b)) continue;
                if (PolygonOverlap.Overlaps(poly, other.Polygon, tol)) neighbours.Add(other);
            }
            if (neighbours.Count == 0) return true;
            neighbours.Sort((x, y) => x.Id.CompareTo(y.Id));
            firstNeighbour = neighbours[0];

            var clips = new List<IReadOnlyList<PointMm>>();
            foreach (AreaRecord n in neighbours) clips.Add(n.Polygon);
            IReadOnlyList<PointMm[]> pieces = PolygonBoolean.Difference(poly, clips, out IReadOnlyList<PointMm[]> holes);

            PointMm[] only = null;
            int kept = 0;
            foreach (PointMm[] piece in pieces)
            {
                if (System.Math.Abs(Polygons.SignedAreaM2(piece)) < tol) continue;
                only = piece; kept++;
            }
            if (kept != 1 || holes.Count > 0) return false;

            foreach (PointMm v in shape.Vertices)
                if (DistanceToRingMm(only, v) > AreaValidator.FollowsRoadToleranceMm) return false;

            trimmed = only;
            foreach (AreaRecord n in neighbours) fittedAround.Add(n.Name);
            return true;
        }

        private static double DistanceToRingMm(PointMm[] ring, PointMm p)
        {
            var closed = new PointMm[ring.Length + 1];
            for (int i = 0; i < ring.Length; i++) closed[i] = ring[i];
            closed[ring.Length] = ring[0];
            return Polylines.Nearest(closed, p).DistanceMm;
        }
    }
}
