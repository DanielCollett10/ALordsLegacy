using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;

namespace LordsLegacy.Sim.Areas
{
    /// <summary>
    /// AR-34: fits a building-area outline around the building areas and road corridors it cuts into. Pure.
    /// </summary>
    internal static class AreaTrim
    {
        /// <summary>Outcome of <see cref="Fit"/>.</summary>
        public enum FitResult { Ok, OverlapsArea, CrossesRoad }

        /// <summary>
        /// Subtracts every overlapping building area and road corridor (overlap above the tolerance) from the outline.
        /// Returns <see cref="FitResult.Ok"/> when the outline can be used: either nothing overlaps
        /// (<paramref name="trimmed"/> is the input, <paramref name="fittedAround"/> empty) or exactly one piece remains
        /// with every player vertex on it. "road" is added once to <paramref name="fittedAround"/> (after the area names)
        /// when a corridor trimmed the outline. A failed fit is <see cref="FitResult.OverlapsArea"/> when areas alone
        /// split the plot or lose a point (the area refusal wins), otherwise <see cref="FitResult.CrossesRoad"/>.
        /// <paramref name="firstNeighbour"/> is the lowest-id overlapping area, or null.
        /// </summary>
        public static FitResult Fit(SimWorld world, AreaShape shape, PointMm[] poly, out PointMm[] trimmed,
            out List<string> fittedAround, out AreaRecord firstNeighbour)
        {
            trimmed = poly;
            fittedAround = new List<string>();
            firstNeighbour = null;
            if (poly.Length < 3) return FitResult.Ok;

            double tol = world.Tuning.OverlapToleranceM2;
            BoundsMm b = Polygons.Bounds(poly);
            var neighbours = new List<AreaRecord>();
            foreach (AreaRecord other in world.Areas.QueryOverlapCandidates(b))
            {
                if (!other.Bounds.Intersects(b)) continue;
                if (PolygonOverlap.Overlaps(poly, other.Polygon, tol)) neighbours.Add(other);
            }
            neighbours.Sort((x, y) => x.Id.CompareTo(y.Id));
            if (neighbours.Count > 0) firstNeighbour = neighbours[0];

            var corridors = new List<PointMm[]>();
            foreach (RoadSegment seg in world.Roads.Segments)
            {
                PointMm[] corridor = seg.CorridorPolygon(world.RoadTypes);
                if (corridor.Length < 3 || !Polygons.Bounds(corridor).Intersects(b)) continue;
                if (PolygonOverlap.Overlaps(poly, corridor, tol)) corridors.Add(corridor);
            }
            if (neighbours.Count == 0 && corridors.Count == 0) return FitResult.Ok;

            var clips = new List<IReadOnlyList<PointMm>>();
            foreach (AreaRecord n in neighbours) clips.Add(n.Polygon);
            if (neighbours.Count > 0 && corridors.Count > 0 && !SinglePiece(shape, poly, clips, tol, out _))
                return FitResult.OverlapsArea;
            foreach (PointMm[] c in corridors) clips.Add(c);
            if (!SinglePiece(shape, poly, clips, tol, out PointMm[] only))
                return corridors.Count == 0 ? FitResult.OverlapsArea : FitResult.CrossesRoad;

            trimmed = only;
            foreach (AreaRecord n in neighbours) fittedAround.Add(n.Name);
            if (corridors.Count > 0) fittedAround.Add("road");
            return FitResult.Ok;
        }

        private static bool SinglePiece(AreaShape shape, PointMm[] poly, List<IReadOnlyList<PointMm>> clips, double tol,
            out PointMm[] only)
        {
            only = null;
            IReadOnlyList<PointMm[]> pieces = PolygonBoolean.Difference(poly, clips, out IReadOnlyList<PointMm[]> holes);
            int kept = 0;
            foreach (PointMm[] piece in pieces)
            {
                if (System.Math.Abs(Polygons.SignedAreaM2(piece)) < tol) continue;
                only = piece; kept++;
            }
            if (kept != 1 || holes.Count > 0) return false;
            foreach (PointMm v in shape.Vertices)
                if (DistanceToRingMm(only, v) > AreaValidator.FollowsRoadToleranceMm) return false;
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
