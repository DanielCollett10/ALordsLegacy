using System;
using System.Collections.Generic;

namespace LordsLegacy.Sim.Geometry
{
    /// <summary>Result of a nearest-point query on a polyline.</summary>
    public readonly struct PolylineHit
    {
        /// <summary>Nearest point X in mm (not rounded).</summary>
        public readonly double X;
        /// <summary>Nearest point Z in mm (not rounded).</summary>
        public readonly double Z;
        /// <summary>Distance from the query point in mm.</summary>
        public readonly double DistanceMm;
        /// <summary>Arc length from the polyline start to the nearest point, in mm.</summary>
        public readonly double ArcLengthMm;
        /// <summary>Index of the segment holding the nearest point.</summary>
        public readonly int SegmentIndex;

        /// <summary>Creates a hit.</summary>
        public PolylineHit(double x, double z, double distanceMm, double arcLengthMm, int segmentIndex)
        { X = x; Z = z; DistanceMm = distanceMm; ArcLengthMm = arcLengthMm; SegmentIndex = segmentIndex; }
    }

    /// <summary>Helpers for open polylines (lists of points).</summary>
    public static class Polylines
    {
        /// <summary>Total length in mm.</summary>
        public static double LengthMm(IReadOnlyList<PointMm> line)
        {
            double len = 0;
            for (int i = 0; i + 1 < line.Count; i++) len += line[i].DistanceTo(line[i + 1]);
            return len;
        }

        /// <summary>Total length in metres.</summary>
        public static double LengthMetres(IReadOnlyList<PointMm> line) => LengthMm(line) / PointMm.MmPerMetre;

        /// <summary>Nearest point on the polyline to p. Ties go to the earliest segment. Needs at least 1 point.</summary>
        public static PolylineHit Nearest(IReadOnlyList<PointMm> line, PointMm p)
        {
            if (line.Count == 0) throw new ArgumentException("Empty polyline.", nameof(line));
            if (line.Count == 1) return new PolylineHit(line[0].X, line[0].Z, p.DistanceTo(line[0]), 0, 0);

            double best = double.MaxValue, bestArc = 0, bx = 0, bz = 0, walked = 0;
            int bestSeg = 0;
            for (int i = 0; i + 1 < line.Count; i++)
            {
                PointMm a = line[i], b = line[i + 1];
                double segLen = a.DistanceTo(b);
                double d = Segments.DistanceToPoint(a, b, p, out double t);
                if (d < best)
                {
                    best = d; bestSeg = i; bestArc = walked + t * segLen;
                    bx = a.X + t * (b.X - a.X); bz = a.Z + t * (b.Z - a.Z);
                }
                walked += segLen;
            }
            return new PolylineHit(bx, bz, best, bestArc, bestSeg);
        }

        /// <summary>Minimum distance in mm between two polylines (0 if they touch or cross).</summary>
        public static double MinDistanceMm(IReadOnlyList<PointMm> a, IReadOnlyList<PointMm> b)
        {
            if (a.Count == 0 || b.Count == 0) throw new ArgumentException("Empty polyline.");
            if (a.Count == 1) return Nearest(b, a[0]).DistanceMm;
            if (b.Count == 1) return Nearest(a, b[0]).DistanceMm;
            double best = double.MaxValue;
            for (int i = 0; i + 1 < a.Count; i++)
                for (int j = 0; j + 1 < b.Count; j++)
                {
                    best = Math.Min(best, Segments.Distance(a[i], a[i + 1], b[j], b[j + 1]));
                    if (best == 0) return 0;
                }
            return best;
        }
    }
}
