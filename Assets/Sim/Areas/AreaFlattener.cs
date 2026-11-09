using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;

namespace LordsLegacy.Sim.Areas
{
    /// <summary>Turns an <see cref="AreaShape"/> into one plain counter-clockwise polygon (AR-05, decision 0014).</summary>
    public static class AreaFlattener
    {
        /// <summary>Flattens with the default tuning spacing (<see cref="SimTuning.CurveFlattenSpacingM"/>).</summary>
        public static PointMm[] Flatten(AreaShape shape, RoadNetwork roads) =>
            Flatten(shape, roads, new SimTuning().CurveFlattenSpacingM);

        /// <summary>Flattens using the spacing from the given tuning.</summary>
        public static PointMm[] Flatten(AreaShape shape, RoadNetwork roads, SimTuning tuning) =>
            Flatten(shape, roads, tuning == null ? 1.0 : tuning.CurveFlattenSpacingM);

        /// <summary>
        /// Flattens a shape. Bent edges are quadratic curves through their handle, sampled about every
        /// <paramref name="spacingM"/> metres; road-following edges copy the road side line between the nearest
        /// positions of their two vertices; a missing road segment (or a null network) makes the edge Straight.
        /// The result is counter-clockwise (clockwise input is reversed) and has no consecutive duplicate points.
        /// </summary>
        public static PointMm[] Flatten(AreaShape shape, RoadNetwork roads, double spacingM) =>
            Flatten(shape, roads, null, spacingM);

        /// <summary>
        /// As above, with the area registry NearArea edges look their neighbour up in (AR-25); a missing area or a null
        /// registry makes such an edge Straight.
        /// </summary>
        public static PointMm[] Flatten(AreaShape shape, RoadNetwork roads, AreaRegistry areas, double spacingM) =>
            Flatten(shape, roads, areas, spacingM, RoadTypes.DefaultCatalog);

        /// <summary>As above, taking road widths from the given catalog (the world's, decision 0019).</summary>
        public static PointMm[] Flatten(AreaShape shape, RoadNetwork roads, AreaRegistry areas, double spacingM,
            RoadTypeCatalog roadTypes)
        {
            if (roadTypes == null) throw new ArgumentNullException(nameof(roadTypes));
            if (shape == null) throw new ArgumentNullException(nameof(shape));
            if (!(spacingM > 0)) spacingM = 1.0;
            int n = shape.Vertices.Count;
            var pts = new List<PointMm>();
            for (int i = 0; i < n; i++)
            {
                PointMm a = shape.Vertices[i], b = shape.Vertices[(i + 1) % n];
                pts.Add(a);
                AreaEdge e = shape.Edges[i];
                if (e.Kind == AreaEdgeKind.Bent) AddCurve(pts, a, b, e.Handle, spacingM);
                else if (e.Kind == AreaEdgeKind.FollowsRoad)
                {
                    RoadSegment seg = roads == null ? null : roads.GetSegment(e.SegmentId);
                    if (seg != null) AddRoadSide(pts, a, b, seg.SideLine(e.Side, roadTypes));
                }
                else if (e.Kind == AreaEdgeKind.NearRoad)
                {
                    RoadSegment seg = roads == null ? null : roads.GetSegment(e.SegmentId);
                    if (seg != null) AddNearRoad(pts, a, b, seg.SideLine(e.Side, roadTypes), e.Side);
                }
                else if (e.Kind == AreaEdgeKind.NearArea)
                {
                    AreaRecord other = areas == null ? null : areas.Get(e.AreaId);
                    if (other != null && other.Polygon.Count >= 3)
                    {
                        PointMm[] guide = NearAreaGeometry.Guide(other.Polygon, a, b, out RoadSide side);
                        if (guide.Length >= 2) AddNearGuide(pts, a, b, guide, side, 0.0, Polylines.Nearest(guide, b).ArcLengthMm);
                    }
                }
            }
            var clean = new List<PointMm>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
                if (clean.Count == 0 || clean[clean.Count - 1] != pts[i]) clean.Add(pts[i]);
            while (clean.Count > 1 && clean[0] == clean[clean.Count - 1]) clean.RemoveAt(clean.Count - 1);
            return Polygons.EnsureCounterClockwise(clean);
        }

        // Interior samples only (the end vertices are added by the caller). The control point of the quadratic
        // is 2*handle - (a+b)/2, so the curve passes through the handle at t = 0.5.
        private static void AddCurve(List<PointMm> pts, PointMm a, PointMm b, PointMm handle, double spacingM)
        {
            double cx = 2.0 * handle.X - (a.X + b.X) / 2.0, cz = 2.0 * handle.Z - (a.Z + b.Z) / 2.0;
            double chord = a.DistanceTo(b);
            if (Math.Abs(2 * handle.X - (a.X + b.X)) < 1e-9 && Math.Abs(2 * handle.Z - (a.Z + b.Z)) < 1e-9) return; // handle at midpoint: straight
            double ac = Math.Sqrt((cx - a.X) * (cx - a.X) + (cz - a.Z) * (cz - a.Z));
            double cb = Math.Sqrt((b.X - cx) * (b.X - cx) + (b.Z - cz) * (b.Z - cz));
            double lengthMm = (2 * chord + 2 * (ac + cb)) / 4.0; // standard quadratic arc-length estimate
            int steps = Math.Max(2, (int)Math.Ceiling(lengthMm / (spacingM * PointMm.MmPerMetre)));
            for (int k = 1; k < steps; k++)
            {
                double t = k / (double)steps, u = 1 - t;
                pts.Add(new PointMm(
                    (long)Math.Round(u * u * a.X + 2 * u * t * cx + t * t * b.X, MidpointRounding.AwayFromZero),
                    (long)Math.Round(u * u * a.Z + 2 * u * t * cz + t * t * b.Z, MidpointRounding.AwayFromZero)));
            }
        }

        // NearRoad (AR-24): side-line samples between the vertices' nearest positions, each pushed outward by a
        // distance interpolated by arc length from vertex a's distance to vertex b's.
        private static void AddNearRoad(List<PointMm> pts, PointMm a, PointMm b, PointMm[] line, RoadSide side)
        {
            if (line.Length < 2) return;
            AddNearGuide(pts, a, b, line, side, Polylines.Nearest(line, a).ArcLengthMm, Polylines.Nearest(line, b).ArcLengthMm);
        }

        // AR-33: from the snapped (nearer) vertex A to the far vertex B, the edge doubles back when
        // dot(P - A, B - A) < 0, where P is B's nearest point on the line.
        private static bool DoublesBack(PointMm a, PointMm b, PointMm[] line)
        {
            PolylineHit ha = Polylines.Nearest(line, a), hb = Polylines.Nearest(line, b);
            bool aSnapped = ha.DistanceMm <= hb.DistanceMm;
            PointMm snapped = aSnapped ? a : b, far = aSnapped ? b : a;
            PolylineHit p = aSnapped ? hb : ha;
            double dot = (p.X - snapped.X) * (far.X - snapped.X) + (p.Z - snapped.Z) * (far.Z - snapped.Z);
            return dot < 0;
        }

        private static void AddNearGuide(List<PointMm> pts, PointMm a, PointMm b, PointMm[] line, RoadSide side, double ta, double tb)
        {
            if (!(Math.Abs(ta - tb) > 0)) return;
            if (DoublesBack(a, b, line)) return; // AR-33: the edge would run back along the line, so it stays straight
            double da = Math.Max(0.0, NearRoadGeometry.SignedOutwardMm(line, side, a));
            double db = Math.Max(0.0, NearRoadGeometry.SignedOutwardMm(line, side, b));
            var cum = new double[line.Length];
            for (int i = 1; i < line.Length; i++) cum[i] = cum[i - 1] + line[i - 1].DistanceTo(line[i]);
            double lo = Math.Min(ta, tb), hi = Math.Max(ta, tb);
            var inner = new List<PointMm>();
            for (int i = 0; i < line.Length; i++)
            {
                if (!(cum[i] > lo && cum[i] < hi)) continue;
                double f = Math.Abs(cum[i] - ta) / (hi - lo);
                double d = da + (db - da) * f;
                (double nx, double nz) = NearRoadGeometry.OutwardNormal(line, i, side);
                inner.Add(d <= 0 ? line[i] : new PointMm(
                    line[i].X + (long)Math.Round(nx * d, MidpointRounding.AwayFromZero),
                    line[i].Z + (long)Math.Round(nz * d, MidpointRounding.AwayFromZero)));
            }
            if (ta > tb) inner.Reverse();
            pts.AddRange(inner);
        }

        private static void AddRoadSide(List<PointMm> pts, PointMm a, PointMm b, PointMm[] line)
        {
            if (line.Length < 2) return;
            double ta = Polylines.Nearest(line, a).ArcLengthMm, tb = Polylines.Nearest(line, b).ArcLengthMm;
            var cum = new double[line.Length];
            for (int i = 1; i < line.Length; i++) cum[i] = cum[i - 1] + line[i - 1].DistanceTo(line[i]);
            double lo = Math.Min(ta, tb), hi = Math.Max(ta, tb);
            var inner = new List<PointMm>();
            for (int i = 0; i < line.Length; i++)
                if (cum[i] > lo && cum[i] < hi) inner.Add(line[i]);
            if (ta > tb) inner.Reverse();
            pts.AddRange(inner);
        }
    }
}
