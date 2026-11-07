using System;

namespace LordsLegacy.Sim.Geometry
{
    /// <summary>How two segments meet.</summary>
    public enum SegmentRelation
    {
        /// <summary>No common point.</summary>
        None,
        /// <summary>Interiors cross at a single point.</summary>
        Crossing,
        /// <summary>They share exactly one point that is an endpoint of at least one (T-join, shared vertex).</summary>
        Touching,
        /// <summary>Collinear and sharing more than one point.</summary>
        CollinearOverlap
    }

    /// <summary>Exact integer segment predicates and distances.</summary>
    public static class Segments
    {
        /// <summary>Classifies how segment a1-a2 meets b1-b2. Exact.</summary>
        public static SegmentRelation Classify(PointMm a1, PointMm a2, PointMm b1, PointMm b2)
        {
            // Zero-length segments are points: handle before the orientation tests.
            if (a1 == a2 && b1 == b2) return a1 == b1 ? SegmentRelation.Touching : SegmentRelation.None;
            if (a1 == a2) return ContainsPoint(b1, b2, a1) ? SegmentRelation.Touching : SegmentRelation.None;
            if (b1 == b2) return ContainsPoint(a1, a2, b1) ? SegmentRelation.Touching : SegmentRelation.None;

            int d1 = Math.Sign(PointMm.Cross(a1, a2, b1));
            int d2 = Math.Sign(PointMm.Cross(a1, a2, b2));
            int d3 = Math.Sign(PointMm.Cross(b1, b2, a1));
            int d4 = Math.Sign(PointMm.Cross(b1, b2, a2));

            if (d1 * d2 < 0 && d3 * d4 < 0) return SegmentRelation.Crossing;

            if (d1 == 0 && d2 == 0)
            {
                // Collinear (or degenerate): compare projections on the dominant axis.
                bool useX = SpanX(a1, a2) + SpanX(b1, b2) >= SpanZ(a1, a2) + SpanZ(b1, b2);
                long aLo = Math.Min(Axis(a1, useX), Axis(a2, useX)), aHi = Math.Max(Axis(a1, useX), Axis(a2, useX));
                long bLo = Math.Min(Axis(b1, useX), Axis(b2, useX)), bHi = Math.Max(Axis(b1, useX), Axis(b2, useX));
                long lo = Math.Max(aLo, bLo), hi = Math.Min(aHi, bHi);
                if (lo > hi) return SegmentRelation.None;
                return lo == hi ? SegmentRelation.Touching : SegmentRelation.CollinearOverlap;
            }

            if ((d1 == 0 && OnSegment(a1, a2, b1)) || (d2 == 0 && OnSegment(a1, a2, b2))
                || (d3 == 0 && OnSegment(b1, b2, a1)) || (d4 == 0 && OnSegment(b1, b2, a2)))
                return SegmentRelation.Touching;
            return SegmentRelation.None;
        }

        /// <summary>True if the segments share any point (crossing, touching or overlapping).</summary>
        public static bool Intersects(PointMm a1, PointMm a2, PointMm b1, PointMm b2) =>
            Classify(a1, a2, b1, b2) != SegmentRelation.None;

        /// <summary>True if p lies on segment a-b (endpoints included). Exact.</summary>
        public static bool ContainsPoint(PointMm a, PointMm b, PointMm p) =>
            PointMm.Cross(a, b, p) == 0 && OnSegment(a, b, p);

        /// <summary>Distance from p to segment a-b in mm, plus the parameter t in [0,1] of the nearest point.</summary>
        public static double DistanceToPoint(PointMm a, PointMm b, PointMm p, out double t)
        {
            double dx = b.X - a.X, dz = b.Z - a.Z;
            double len2 = dx * dx + dz * dz;
            t = len2 == 0 ? 0 : ((p.X - a.X) * dx + (p.Z - a.Z) * dz) / len2;
            if (t < 0) t = 0; else if (t > 1) t = 1;
            double nx = a.X + t * dx - p.X, nz = a.Z + t * dz - p.Z;
            return Math.Sqrt(nx * nx + nz * nz);
        }

        /// <summary>Minimum distance between two segments in mm (0 if they intersect).</summary>
        public static double Distance(PointMm a1, PointMm a2, PointMm b1, PointMm b2)
        {
            if (Intersects(a1, a2, b1, b2)) return 0;
            double d = DistanceToPoint(a1, a2, b1, out _);
            d = Math.Min(d, DistanceToPoint(a1, a2, b2, out _));
            d = Math.Min(d, DistanceToPoint(b1, b2, a1, out _));
            return Math.Min(d, DistanceToPoint(b1, b2, a2, out _));
        }

        private static long Axis(PointMm p, bool useX) => useX ? p.X : p.Z;
        private static long SpanX(PointMm a, PointMm b) => Math.Abs(a.X - b.X);
        private static long SpanZ(PointMm a, PointMm b) => Math.Abs(a.Z - b.Z);

        private static bool OnSegment(PointMm a, PointMm b, PointMm p) =>
            p.X >= Math.Min(a.X, b.X) && p.X <= Math.Max(a.X, b.X)
            && p.Z >= Math.Min(a.Z, b.Z) && p.Z <= Math.Max(a.Z, b.Z);
    }
}
