using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;

namespace LordsLegacy.Sim.Placement
{
    /// <summary>
    /// Where a single point may be placed (docs/design/placement.md PL-01..05, PL-07). Pure queries: nothing in the
    /// world changes. The caller passes the point after snapping (PL-05). The shape checks (AR-15, AR-16, RD-13)
    /// still run on the finished shape.
    /// </summary>
    public static class PlacementRules
    {
        /// <summary>A point this close (mm) to an area edge or road side line counts as on the boundary: allowed.</summary>
        public const double BoundaryToleranceMm = 50;

        /// <summary>
        /// Whether a vertex of an area of the given type may go at <paramref name="point"/>. Work areas: anywhere on the
        /// map. Building areas: not inside another building area or a road corridor (boundary allowed).
        /// </summary>
        /// <exception cref="ArgumentException">Unknown area type.</exception>
        public static PlacementResult CanPlaceAreaVertex(SimWorld world, string areaTypeId, PointMm point)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            AreaTypeDef type = world.AreaTypes.Find(areaTypeId);
            if (type == null) throw new ArgumentException("Unknown area type " + areaTypeId, nameof(areaTypeId));
            if (!OnMap(world, point)) return PlacementResult.Refused(PlacementRefusal.OutsideMap);
            if (type.Kind == AreaKind.Work) return PlacementResult.Allowed;
            AreaRecord area = StrictlyInsideBuildingArea(world, point);
            if (area != null) return PlacementResult.InsideArea(area.Name);
            if (StrictlyInsideRoad(world, point)) return PlacementResult.Refused(PlacementRefusal.OnRoad);
            return PlacementResult.Allowed;
        }

        /// <summary>
        /// Whether a road point (new point or dragged node) may go at <paramref name="point"/>: not outside the map and
        /// not inside a building area (boundary allowed). Roads and work areas do not block.
        /// </summary>
        public static PlacementResult CanPlaceRoadPoint(SimWorld world, PointMm point)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (!OnMap(world, point)) return PlacementResult.Refused(PlacementRefusal.OutsideMap);
            AreaRecord area = StrictlyInsideBuildingArea(world, point);
            return area != null ? PlacementResult.InsideArea(area.Name) : PlacementResult.Allowed;
        }

        private static bool OnMap(SimWorld world, PointMm p) =>
            world.Terrain.Contains((float)p.XMetres, (float)p.ZMetres);

        /// <summary>Lowest-id building area with the point inside and more than the tolerance from its edge.</summary>
        private static AreaRecord StrictlyInsideBuildingArea(SimWorld world, PointMm p)
        {
            long t = (long)Math.Ceiling(BoundaryToleranceMm);
            var box = new BoundsMm(new PointMm(p.X - t, p.Z - t), new PointMm(p.X + t, p.Z + t));
            AreaRecord best = null;
            foreach (AreaRecord r in world.Areas.QueryOverlapCandidates(box))
            {
                if (best != null && r.Id >= best.Id) continue;
                if (StrictlyInside(r.Polygon, p)) best = r;
            }
            return best;
        }

        private static bool StrictlyInsideRoad(SimWorld world, PointMm p)
        {
            foreach (RoadSegment seg in world.Roads.Segments)
            {
                PointMm[] corridor = seg.CorridorPolygon(world.RoadTypes);
                if (corridor.Length >= 3 && StrictlyInside(corridor, p)) return true;
            }
            return false;
        }

        private static bool StrictlyInside(IReadOnlyList<PointMm> poly, PointMm p)
        {
            if (poly.Count < 3) return false;
            if (Polygons.Locate(poly, p) != PointLocation.Inside) return false;
            for (int i = 0; i < poly.Count; i++)
                if (Segments.DistanceToPoint(poly[i], poly[(i + 1) % poly.Count], p, out _) <= BoundaryToleranceMm)
                    return false;
            return true;
        }
    }
}
