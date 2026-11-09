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

        /// <summary>
        /// One representative point where two segments meet. Throws <see cref="ArgumentException"/> when
        /// <see cref="Classify"/> reports <see cref="SegmentRelation.None"/>.
        /// Crossing: the intersection rounded to the nearest mm (away from zero on ties), so it may be up to
        /// 0.5 mm off the exact point. Touching: the shared point (an endpoint). Collinear overlap: the start of
        /// the overlap, the lower end along the dominant axis (X when the segments span more in X, else Z).
        /// Zero-length segments are handled: a point segment's own point is the meeting point.
        /// </summary>
        public static PointMm MeetingPoint(PointMm a1, PointMm a2, PointMm b1, PointMm b2)
        {
            SegmentRelation rel = Classify(a1, a2, b1, b2);
            if (rel == SegmentRelation.None)
                throw new ArgumentException("Segments do not meet.");

            if (rel == SegmentRelation.Crossing)
            {
                // a1 + t*r = b1 + u*s  =>  t = cross(b1 - a1, s) / cross(r, s); denominator is non-zero for a crossing.
                long rx = a2.X - a1.X, rz = a2.Z - a1.Z, sx = b2.X - b1.X, sz = b2.Z - b1.Z;
                long qx = b1.X - a1.X, qz = b1.Z - a1.Z;
                double t = (double)(qx * sz - qz * sx) / (rx * sz - rz * sx);
                return new PointMm(RoundMm(a1.X + t * rx), RoundMm(a1.Z + t * rz));
            }

            // Touching or collinear: every shared point is one of the four endpoints, so take the lowest of those
            // that lie on both segments. For a single touch that is the only candidate.
            bool useX = SpanX(a1, a2) + SpanX(b1, b2) >= SpanZ(a1, a2) + SpanZ(b1, b2);
            PointMm best = default;
            bool found = false;
            foreach (PointMm p in new[] { a1, a2, b1, b2 })
            {
                if (!ContainsPoint(a1, a2, p) || !ContainsPoint(b1, b2, p)) continue;
                if (!found || Less(p, best, useX)) { best = p; found = true; }
            }
            return best;
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
        private static bool Less(PointMm p, PointMm q, bool useX) =>
            useX ? (p.X < q.X || (p.X == q.X && p.Z < q.Z)) : (p.Z < q.Z || (p.Z == q.Z && p.X < q.X));
        private static long RoundMm(double v) => (long)Math.Round(v, MidpointRounding.AwayFromZero);
        private static long SpanX(PointMm a, PointMm b) => Math.Abs(a.X - b.X);
        private static long SpanZ(PointMm a, PointMm b) => Math.Abs(a.Z - b.Z);

        private static bool OnSegment(PointMm a, PointMm b, PointMm p) =>
            p.X >= Math.Min(a.X, b.X) && p.X <= Math.Max(a.X, b.X)
            && p.Z >= Math.Min(a.Z, b.Z) && p.Z <= Math.Max(a.Z, b.Z);
    }
}
