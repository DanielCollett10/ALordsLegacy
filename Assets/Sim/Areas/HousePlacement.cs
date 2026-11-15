using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Areas
{
    /// <summary>Why a house could not be placed on a plot.</summary>
    public enum HousePlacementRefusal
    {
        /// <summary>Placed.</summary>
        None = 0,
        /// <summary>No edge of the plot follows a road, so there is no frontage.</summary>
        NoFrontage = 1,
        /// <summary>The house does not fit inside the plot, or it would overlap a footprint.</summary>
        NoRoom = 2
    }

    /// <summary>Answer of <see cref="HousePlacement.Place(AreaShape, IReadOnlyList{PointMm}, IEnumerable{Footprint})"/>. Immutable.</summary>
    public sealed class HousePlacementResult
    {
        /// <summary>True if the house fits.</summary>
        public bool Ok => Refusal == HousePlacementRefusal.None;
        /// <summary>Why not, or None.</summary>
        public HousePlacementRefusal Refusal { get; }
        /// <summary>Centre of the house rectangle (valid when Ok).</summary>
        public PointMm Centre { get; }
        /// <summary>Rotation in degrees, counter-clockwise, as <see cref="Footprint"/> takes it (valid when Ok).</summary>
        public double RotationDegrees { get; }
        /// <summary>The four corners as a <see cref="Footprint"/> would compute them (empty when refused).</summary>
        public IReadOnlyList<PointMm> Polygon { get; }
        /// <summary>Index of the plot edge the house sits on, or -1.</summary>
        public int FrontageEdge { get; }

        /// <summary>Player-facing text: "No room for the house" for a refusal, empty when Ok.</summary>
        public string Text => Refusal == HousePlacementRefusal.None ? "" : "No room for the house";

        internal HousePlacementResult(HousePlacementRefusal refusal, PointMm centre, double rotation,
            IReadOnlyList<PointMm> polygon, int edge)
        {
            Refusal = refusal; Centre = centre; RotationDegrees = rotation; Polygon = polygon; FrontageEdge = edge;
        }
    }

    /// <summary>
    /// Puts the house on a plot (HM-04): centred on the longest road-frontage edge, long side along it, 1 m back.
    /// Pure and deterministic. The footprint's local X points at the road, so its 8 m depth runs along the frontage.
    /// For a curved frontage the house sits tangent to the curve at the middle of the edge (by length).
    /// </summary>
    public static class HousePlacement
    {
        /// <summary>Gap in mm between the frontage edge and the house (HM-04).</summary>
        public const long BackMm = 1000;

        private const double OverlapToleranceM2 = 0.0001;

        /// <summary>
        /// Finds the house for a plot. <paramref name="shape"/> gives the edge kinds (FollowsRoad = frontage);
        /// <paramref name="polygon"/> is its flattened outline. Ties between equally long frontage edges go to the
        /// lowest edge index. Refuses when there is no frontage, or the rectangle leaves the plot, or it overlaps
        /// any of <paramref name="footprints"/> (may be null).
        /// </summary>
        public static HousePlacementResult Place(AreaShape shape, IReadOnlyList<PointMm> polygon, IEnumerable<Footprint> footprints)
        {
            if (shape == null) throw new ArgumentNullException(nameof(shape));
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));
            if (polygon.Count < 3 || shape.Vertices.Count < 3) return Refuse(HousePlacementRefusal.NoRoom, -1);

            var record = new AreaRecord(0, AreaTypes.PlotId, "plot", shape, polygon);
            PointMm[] outline = record.ShapeOrderOutline(out int[] starts);
            int best = -1;
            double bestLen = -1;
            for (int i = 0; i < shape.Edges.Count; i++)
            {
                if (shape.Edges[i].Kind != AreaEdgeKind.FollowsRoad) continue;
                double len = RunLength(outline, starts, i);
                if (len > bestLen) { bestLen = len; best = i; }
            }
            if (best < 0) return Refuse(HousePlacementRefusal.NoFrontage, -1);
            if (bestLen <= 0) return Refuse(HousePlacementRefusal.NoRoom, best);

            if (!MidpointAndDirection(outline, starts, best, bestLen, out double mx, out double mz, out double dx, out double dz))
                return Refuse(HousePlacementRefusal.NoRoom, best);

            // Inward normal: left of travel for a counter-clockwise outline, right for clockwise.
            bool ccw = Polygons.SignedArea2Mm(outline) > 0;
            double nx = ccw ? -dz : dz, nz = ccw ? dx : -dx;
            FootprintTypeDef type = ModuleTypes.House;
            double reach = BackMm + type.WidthMm / 2.0;
            var centre = new PointMm((long)Math.Round(mx + nx * reach, MidpointRounding.AwayFromZero),
                                     (long)Math.Round(mz + nz * reach, MidpointRounding.AwayFromZero));
            // Local X (width, 6 m) points at the road = -inward normal; local Z is X turned 90° counter-clockwise.
            double rot = Math.Atan2(-nz, -nx) * 180.0 / Math.PI;
            var house = new Footprint(0, type, centre, rot);

            if (!FitsInside(outline, house.Polygon)) return Refuse(HousePlacementRefusal.NoRoom, best);
            if (footprints != null)
                foreach (Footprint f in footprints)
                {
                    if (f == null || !house.Bounds.Intersects(f.Bounds)) continue;
                    if (PolygonOverlap.Overlaps(house.Polygon, f.Polygon, OverlapToleranceM2))
                        return Refuse(HousePlacementRefusal.NoRoom, best);
                }
            return new HousePlacementResult(HousePlacementRefusal.None, centre, rot, house.Polygon, best);
        }

        private static HousePlacementResult Refuse(HousePlacementRefusal r, int edge) =>
            new HousePlacementResult(r, default, 0, new PointMm[0], edge);

        // Outline points of edge i, inclusive of both ends (the last edge wraps to point 0).
        private static int RunEnd(PointMm[] outline, int[] starts, int i) =>
            i + 1 < starts.Length ? starts[i + 1] : outline.Length;

        private static double RunLength(PointMm[] outline, int[] starts, int i)
        {
            double sum = 0;
            for (int k = starts[i]; k < RunEnd(outline, starts, i); k++)
                sum += outline[k].DistanceTo(outline[(k + 1) % outline.Length]);
            return sum;
        }

        private static bool MidpointAndDirection(PointMm[] outline, int[] starts, int i, double total,
            out double mx, out double mz, out double dx, out double dz)
        {
            double half = total / 2.0, walked = 0;
            mx = mz = dx = dz = 0;
            for (int k = starts[i]; k < RunEnd(outline, starts, i); k++)
            {
                PointMm a = outline[k], b = outline[(k + 1) % outline.Length];
                double len = a.DistanceTo(b);
                if (len <= 0) continue;
                if (walked + len >= half || k == RunEnd(outline, starts, i) - 1)
                {
                    double t = Math.Min(1.0, Math.Max(0.0, (half - walked) / len));
                    mx = a.X + (b.X - a.X) * t; mz = a.Z + (b.Z - a.Z) * t;
                    dx = (b.X - a.X) / len; dz = (b.Z - a.Z) / len;
                    return true;
                }
                walked += len;
            }
            return false;
        }

        // Every corner inside or on the plot, no plot edge crossing the rectangle, no plot vertex strictly inside it.
        private static bool FitsInside(PointMm[] plot, PointMm[] rect)
        {
            for (int i = 0; i < rect.Length; i++)
                if (!Polygons.Contains(plot, rect[i])) return false;
            for (int i = 0; i < plot.Length; i++)
            {
                PointMm a = plot[i], b = plot[(i + 1) % plot.Length];
                for (int j = 0; j < rect.Length; j++)
                    if (Segments.Classify(a, b, rect[j], rect[(j + 1) % rect.Length]) == SegmentRelation.Crossing) return false;
                if (Polygons.Locate(rect, a) == PointLocation.Inside) return false;
            }
            return true;
        }
    }
}
