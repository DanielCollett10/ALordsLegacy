using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;

namespace LordsLegacy.Sim.Areas
{
    /// <summary>
    /// Shape checks for a drawn area (AR-10..13, decision 0014). Pure: never changes the world.
    /// Building types then get placement checks 6-8 (slope, overlap, roads): <see cref="AreaPlacementChecks"/>.
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
                : AreaFlattener.Flatten(shape, world.Roads, world.Areas, world.Tuning.CurveFlattenSpacingM, world.RoadTypes);
            double area = poly.Length >= 3 ? Math.Abs(Polygons.SignedAreaM2(poly)) : 0.0;

            AreaTypeDef type = world.AreaTypes.Find(typeId);
            if (type == null) return new AreaPreview(poly, area, count, AreaRefusal.UnknownType);

            AreaRefusal reason = FirstReason(world, shape, type, ref poly, ref area, out string overlapName,
                out List<string> fitted);
            return new AreaPreview(poly, area, count, reason, overlapName, fitted);
        }

        private static AreaRefusal FirstReason(SimWorld world, AreaShape shape, AreaTypeDef type, ref PointMm[] poly,
            ref double area, out string overlapName, out List<string> fitted)
        {
            overlapName = string.Empty;
            fitted = null;
            int n = shape.Vertices.Count;
            if (n < type.MinPoints) return AreaRefusal.TooFewPoints;
            if (n > type.MaxPoints) return AreaRefusal.TooManyPoints;
            bool isMarket = type.Id == AreaTypes.MarketId;
            if (isMarket && world.Markets.Count > 0) return AreaRefusal.OnlyOneMarket;
            if (!RoadEdgesOk(world, shape, type)) return AreaRefusal.NotOnRoad;
            if (type.RequiresRoadFrontage && !HasFrontage(shape)) return AreaRefusal.NoFrontage;
            if (HasZeroLengthEdge(shape) || !Polygons.IsSimple(poly)) return AreaRefusal.EdgesCross;
            for (int i = 0; i < poly.Length; i++)
                if (!world.Terrain.Contains((float)poly[i].XMetres, (float)poly[i].ZMetres)) return AreaRefusal.OutsideMap;
            if (type.Kind == AreaKind.Building)
            {
                // AR-34 / PL-11: fit around neighbouring building areas and fixed plots (granary yard), before
                // size/slope/overlap/road checks. A fit that cannot keep every player point is refused.
                AreaTrim.FitResult fit = AreaTrim.Fit(world, shape, poly, out PointMm[] trimmed, out fitted,
                    out string first);
                if (fit == AreaTrim.FitResult.OverlapsArea)
                {
                    overlapName = first;
                    return AreaRefusal.OverlapsArea;
                }
                if (fit == AreaTrim.FitResult.CrossesRoad) return AreaRefusal.CrossesRoad;
                if (fitted.Count > 0) { poly = trimmed; area = Math.Abs(Polygons.SignedAreaM2(poly)); }
            }
            double minM2 = isMarket ? world.Tuning.MarketMinAreaM2 : type.MinAreaM2;
            double maxM2 = isMarket ? world.Tuning.MarketMaxAreaM2 : type.MaxAreaM2;
            if (area < minM2) return isMarket ? AreaRefusal.MarketTooSmall : type.RequiresRoadFrontage ? AreaRefusal.PlotTooSmall : AreaRefusal.TooSmall;
            if (area > maxM2) return AreaRefusal.TooLarge;
            if (type.Kind != AreaKind.Building) return AreaRefusal.Valid; // work areas skip checks 6-8
            if (type.MaxSlopeDegrees.HasValue && AreaPlacementChecks.TooSteep(world, poly, type.MaxSlopeDegrees.Value))
                return AreaRefusal.TooSteep;
            AreaRecord other = AreaPlacementChecks.FindOverlappingArea(world, poly);
            if (other != null) { overlapName = other.Name; return AreaRefusal.OverlapsArea; }
            if (AreaPlacementChecks.CrossesRoad(world, poly)) return AreaRefusal.CrossesRoad;
            // MK-05: a market must fit at least one stall slot, however big its area is (a thin strip has none).
            if (isMarket && StallGrid.Slots(poly, world.Tuning).Count == 0) return AreaRefusal.MarketTooSmall;
            return AreaRefusal.Valid;
        }

        // HM-02: frontage means at least one edge flush to a road side (FollowsRoad; RoadEdgesOk already checked it is on that road).
        private static bool HasFrontage(AreaShape shape)
        {
            for (int i = 0; i < shape.Edges.Count; i++)
                if (shape.Edges[i].Kind == AreaEdgeKind.FollowsRoad) return true;
            return false;
        }

        // The flattener drops repeated points, so zero-length edges are looked for on the shape itself.
        private static bool HasZeroLengthEdge(AreaShape shape)
        {
            int n = shape.Vertices.Count;
            for (int i = 0; i < n; i++)
                if (shape.Vertices[i] == shape.Vertices[(i + 1) % n]) return true;
            return false;
        }

        private static bool RoadEdgesOk(SimWorld world, AreaShape shape, AreaTypeDef type)
        {
            int n = shape.Vertices.Count;
            for (int i = 0; i < n; i++)
            {
                AreaEdge e = shape.Edges[i];
                if (e.Kind == AreaEdgeKind.NearArea)
                {
                    AreaRecord other = world.Areas.Get(e.AreaId);
                    if (other == null) return false;
                    if (!NearAreaGeometry.VertexOk(other.Polygon, shape.Vertices[i], type.NearRoadReachM)) return false;
                    if (!NearAreaGeometry.VertexOk(other.Polygon, shape.Vertices[(i + 1) % n], type.NearRoadReachM)) return false;
                    continue;
                }
                if (e.Kind != AreaEdgeKind.FollowsRoad && e.Kind != AreaEdgeKind.NearRoad) continue;
                RoadSegment seg = world.Roads.GetSegment(e.SegmentId);
                if (seg == null) return false;
                PointMm[] side = seg.SideLine(e.Side, world.RoadTypes);
                if (side.Length == 0) return false;
                if (e.Kind == AreaEdgeKind.NearRoad)
                {
                    if (!NearRoadGeometry.VertexOk(side, e.Side, shape.Vertices[i], type.NearRoadReachM)) return false;
                    if (!NearRoadGeometry.VertexOk(side, e.Side, shape.Vertices[(i + 1) % n], type.NearRoadReachM)) return false;
                    continue;
                }
                if (Polylines.Nearest(side, shape.Vertices[i]).DistanceMm > FollowsRoadToleranceMm) return false;
                if (Polylines.Nearest(side, shape.Vertices[(i + 1) % n]).DistanceMm > FollowsRoadToleranceMm) return false;
            }
            return true;
        }
    }
}
