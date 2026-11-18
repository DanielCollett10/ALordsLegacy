using System;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Placement
{
    /// <summary>
    /// Queries over the plot outlines of fixed structures such as the granary (placement.md PL-10, PL-11, PL-13). The
    /// outline is the footprint's building plus yard (<see cref="Footprint.PlotPolygon"/>); it exists while the footprint
    /// does, so removing the structure removes it. Pure.
    /// </summary>
    public static class FixedPlots
    {
        /// <summary>Player-facing name of a fixed plot, e.g. "Granary 4".</summary>
        public static string NameOf(Footprint f)
        {
            string kind = f.Type.Id == ModuleTypes.GranaryId ? "Granary"
                : f.Type.Id == ModuleTypes.HewingStationId ? "Hewing station"
                : f.Type.Id == ModuleTypes.SawpitId ? "Sawpit"
                : f.Type.Id == ModuleTypes.WeaverId ? "Weaver"
                : f.Type.Id == ModuleTypes.WattleYardId ? "Wattle yard" : "Structure";
            return kind + " " + f.Id;
        }

        /// <summary>The lowest-id fixed plot overlapping the polygon by more than the tolerance (touching is fine), or null.</summary>
        public static Footprint FindOverlapping(SimWorld world, PointMm[] poly)
        {
            if (poly.Length < 3) return null;
            BoundsMm b = Polygons.Bounds(poly);
            Footprint best = null;
            foreach (Footprint f in world.Footprints.All)
            {
                if (!f.Type.IsStandalone || (best != null && f.Id >= best.Id)) continue;
                if (!Polygons.Bounds(f.PlotPolygon).Intersects(b)) continue;
                if (PolygonOverlap.Overlaps(poly, f.PlotPolygon, world.Tuning.OverlapToleranceM2)) best = f;
            }
            return best;
        }

        /// <summary>The lowest-id fixed plot with the point strictly inside it (more than the boundary tolerance from its edge), or null.</summary>
        public static Footprint FindStrictlyInside(SimWorld world, PointMm p)
        {
            Footprint best = null;
            foreach (Footprint f in world.Footprints.All)
            {
                if (!f.Type.IsStandalone || (best != null && f.Id >= best.Id)) continue;
                PointMm[] plot = f.PlotPolygon;
                if (Polygons.Locate(plot, p) != PointLocation.Inside) continue;
                bool nearEdge = false;
                for (int i = 0; i < plot.Length && !nearEdge; i++)
                    nearEdge = Segments.DistanceToPoint(plot[i], plot[(i + 1) % plot.Length], p, out _) <= PlacementRules.BoundaryToleranceMm;
                if (!nearEdge) best = f;
            }
            return best;
        }

        /// <summary>
        /// Nearest corner (preferred) or edge point of any fixed plot within the distance, or null (PL-13). The hit's
        /// <see cref="AreaSnapHit.AreaId"/> is the footprint id and <see cref="AreaSnapHit.IsFixedPlot"/> is true.
        /// </summary>
        public static AreaSnapHit? FindNearestSnap(SimWorld world, PointMm point, double maxDistanceMm)
        {
            if (double.IsNaN(maxDistanceMm) || maxDistanceMm < 0) return null;
            AreaSnapHit? bestVertex = null, bestEdge = null;
            foreach (Footprint f in world.Footprints.All)
            {
                if (!f.Type.IsStandalone) continue;
                PointMm[] plot = f.PlotPolygon;
                for (int i = 0; i < plot.Length; i++)
                {
                    double d = plot[i].DistanceTo(point);
                    if (d <= maxDistanceMm && (bestVertex == null || d < bestVertex.Value.DistanceMm))
                        bestVertex = new AreaSnapHit(AreaSnapKind.Vertex, f.Id, i, plot[i], d, true);
                    PointMm a = plot[i], c = plot[(i + 1) % plot.Length];
                    double de = Segments.DistanceToPoint(a, c, point, out double t);
                    if (de > maxDistanceMm || (bestEdge != null && de >= bestEdge.Value.DistanceMm)) continue;
                    var p = new PointMm(a.X + (long)Math.Round(t * (c.X - a.X), MidpointRounding.AwayFromZero),
                                        a.Z + (long)Math.Round(t * (c.Z - a.Z), MidpointRounding.AwayFromZero));
                    bestEdge = new AreaSnapHit(AreaSnapKind.Edge, f.Id, i, p, de, true);
                }
            }
            return bestVertex ?? bestEdge;
        }

        /// <summary>
        /// The nearest snap over areas and fixed plots: a vertex beats an edge, then the closer wins, areas first on a tie.
        /// Tools call this instead of <see cref="AreaRegistry.FindNearestSnap"/>.
        /// </summary>
        public static AreaSnapHit? FindNearestSnapIncludingAreas(SimWorld world, PointMm point, double maxDistanceMm)
        {
            AreaSnapHit? a = world.Areas.FindNearestSnap(point, maxDistanceMm);
            AreaSnapHit? p = FindNearestSnap(world, point, maxDistanceMm);
            if (a == null) return p;
            if (p == null) return a;
            if (a.Value.Kind != p.Value.Kind) return a.Value.Kind == AreaSnapKind.Vertex ? a : p;
            return p.Value.DistanceMm < a.Value.DistanceMm ? p : a;
        }
    }
}
