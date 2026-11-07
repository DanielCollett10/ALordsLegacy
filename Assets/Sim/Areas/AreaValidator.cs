using System;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;

namespace LordsLegacy.Sim.Areas
{
    /// <summary>
    /// Shape checks for a drawn area (AR-10..13, decision 0014). Pure: never changes the world.
    /// Placement checks (slope, overlap, roads) are added separately.
    /// </summary>
    public static class AreaValidator
    {
        /// <summary>How close (mm) a FollowsRoad edge's vertices must be to the road side line: 5 cm.</summary>
        public const double FollowsRoadToleranceMm = 50.0;

        /// <summary>
        /// Flattens the shape and reports the first failing check: point count, not on road, edges cross,
        /// outside the map, size. An unknown type id gives <see cref="AreaRefusal.UnknownType"/>.
        /// </summary>
        public static AreaPreview Validate(SimWorld world, AreaShape shape, string typeId)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (shape == null) throw new ArgumentNullException(nameof(shape));
            int count = shape.Vertices.Count;
            PointMm[] poly = count == 0
                ? new PointMm[0]
                : AreaFlattener.Flatten(shape, world.Roads, world.Tuning);
            double area = poly.Length >= 3 ? Math.Abs(Polygons.SignedAreaM2(poly)) : 0.0;

            AreaTypeDef type = AreaTypes.Find(typeId);
            if (type == null) return new AreaPreview(poly, area, count, AreaRefusal.UnknownType);

            AreaRefusal reason = FirstReason(world, shape, type, poly, area);
            return new AreaPreview(poly, area, count, reason);
        }

        private static AreaRefusal FirstReason(SimWorld world, AreaShape shape, AreaTypeDef type, PointMm[] poly, double area)
        {
            int n = shape.Vertices.Count;
            if (n < type.MinPoints) return AreaRefusal.TooFewPoints;
            if (n > type.MaxPoints) return AreaRefusal.TooManyPoints;
            if (!RoadEdgesOk(world, shape)) return AreaRefusal.NotOnRoad;
            if (HasZeroLengthEdge(shape) || !Polygons.IsSimple(poly)) return AreaRefusal.EdgesCross;
            for (int i = 0; i < poly.Length; i++)
                if (!world.Terrain.Contains((float)poly[i].XMetres, (float)poly[i].ZMetres)) return AreaRefusal.OutsideMap;
            if (area < type.MinAreaM2) return AreaRefusal.TooSmall;
            if (area > type.MaxAreaM2) return AreaRefusal.TooLarge;
            return AreaRefusal.Valid;
        }

        // The flattener drops repeated points, so zero-length edges are looked for on the shape itself.
        private static bool HasZeroLengthEdge(AreaShape shape)
        {
            int n = shape.Vertices.Count;
            for (int i = 0; i < n; i++)
                if (shape.Vertices[i] == shape.Vertices[(i + 1) % n]) return true;
            return false;
        }

        private static bool RoadEdgesOk(SimWorld world, AreaShape shape)
        {
            int n = shape.Vertices.Count;
            for (int i = 0; i < n; i++)
            {
                AreaEdge e = shape.Edges[i];
                if (e.Kind != AreaEdgeKind.FollowsRoad) continue;
                RoadSegment seg = world.Roads.GetSegment(e.SegmentId);
                if (seg == null) return false;
                PointMm[] side = seg.SideLine(e.Side);
                if (side.Length == 0) return false;
                if (Polylines.Nearest(side, shape.Vertices[i]).DistanceMm > FollowsRoadToleranceMm) return false;
                if (Polylines.Nearest(side, shape.Vertices[(i + 1) % n]).DistanceMm > FollowsRoadToleranceMm) return false;
            }
            return true;
        }
    }
}
