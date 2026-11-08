using System;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;

namespace LordsLegacy.Sim.Areas
{
    /// <summary>Placement checks for building areas (AR-14..16): slope, overlap with other areas, road crossing. Pure.</summary>
    internal static class AreaPlacementChecks
    {
        /// <summary>True if any grid sample inside the polygon is steeper than the limit (AR-14).</summary>
        public static bool TooSteep(SimWorld world, PointMm[] poly, double maxSlopeDeg)
        {
            if (poly.Length < 3) return false;
            double step = world.Tuning.SlopeSampleGridM;
            if (!(step > 0)) return false;
            BoundsMm b = Polygons.Bounds(poly);
            long ix0 = (long)Math.Ceiling(b.Min.XMetres / step), ix1 = (long)Math.Floor(b.Max.XMetres / step);
            long iz0 = (long)Math.Ceiling(b.Min.ZMetres / step), iz1 = (long)Math.Floor(b.Max.ZMetres / step);
            for (long iz = iz0; iz <= iz1; iz++)
                for (long ix = ix0; ix <= ix1; ix++)
                {
                    double x = ix * step, z = iz * step;
                    if (!Polygons.Contains(poly, PointMm.FromMetres(x, z))) continue;
                    if (world.Terrain.SlopeDegAt((float)x, (float)z) > maxSlopeDeg) return true;
                }
            return false;
        }

        /// <summary>The lowest-id building area overlapping the polygon by more than the tolerance, or null (AR-15).</summary>
        public static AreaRecord FindOverlappingArea(SimWorld world, PointMm[] poly)
        {
            if (poly.Length < 3) return null;
            BoundsMm b = Polygons.Bounds(poly);
            AreaRecord best = null;
            foreach (AreaRecord other in world.Areas.QueryOverlapCandidates(b))
            {
                if (best != null && other.Id >= best.Id) continue;
                if (!other.Bounds.Intersects(b)) continue;
                if (PolygonOverlap.Overlaps(poly, other.Polygon, world.Tuning.OverlapToleranceM2)) best = other;
            }
            return best;
        }

        /// <summary>True if the polygon overlaps any road corridor by more than the tolerance (AR-16).</summary>
        public static bool CrossesRoad(SimWorld world, PointMm[] poly)
        {
            if (poly.Length < 3) return false;
            BoundsMm b = Polygons.Bounds(poly);
            foreach (RoadSegment seg in world.Roads.Segments)
            {
                PointMm[] corridor = seg.CorridorPolygon(world.RoadTypes);
                if (corridor.Length < 3 || !Polygons.Bounds(corridor).Intersects(b)) continue;
                if (PolygonOverlap.Overlaps(poly, corridor, world.Tuning.OverlapToleranceM2)) return true;
            }
            return false;
        }
    }
}
