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

        /// <summary>
        /// Points every <paramref name="spacingM"/> metres of arc length from the first vertex: at 0, s, 2s, ...
        /// Nothing is returned past the end. Positions are interpolated along the segments and rounded to whole mm.
        /// Zero-length or single-point polylines (all vertices equal) give an empty list.
        /// Repeated vertices are skipped. The same input always gives the same output.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when spacingM is not greater than 0 (or is NaN).</exception>
        public static List<PointMm> PointsEvery(IReadOnlyList<PointMm> line, double spacingM)
        {
            if (!(spacingM > 0)) throw new ArgumentException("Spacing must be greater than 0.", nameof(spacingM));
            var result = new List<PointMm>();
            double total = LengthMm(line);
            if (line.Count < 2 || total <= 0) return result;

            const double eps = 1e-9; // mm; absorbs float error at the very end of the line
            double spacingMm = spacingM * PointMm.MmPerMetre;
            long k = 0;
            double target = 0, walked = 0;
            for (int i = 0; i + 1 < line.Count && target <= total + eps; i++)
            {
                PointMm a = line[i], b = line[i + 1];
                double segLen = a.DistanceTo(b);
                if (segLen <= 0) continue;
                while (target <= walked + segLen + eps && target <= total + eps)
                {
                    double t = Math.Min(1.0, Math.Max(0.0, (target - walked) / segLen));
                    result.Add(new PointMm(
                        (long)Math.Round(a.X + t * (b.X - a.X), MidpointRounding.AwayFromZero),
                        (long)Math.Round(a.Z + t * (b.Z - a.Z), MidpointRounding.AwayFromZero)));
                    k++;
                    target = k * spacingMm;
                }
                walked += segLen;
            }
            return result;
        }

        /// <summary>
        /// The subset of <see cref="PointsEvery"/> whose points lie within <paramref name="radiusM"/> metres
        /// of <paramref name="centre"/> (inclusive). Same positions and order as the full list.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when spacingM is not greater than 0, or radiusM is negative or NaN.</exception>
        public static List<PointMm> PointsEveryWithin(IReadOnlyList<PointMm> line, double spacingM, PointMm centre, double radiusM)
        {
            if (!(radiusM >= 0)) throw new ArgumentException("Radius must be 0 or greater.", nameof(radiusM));
            double radiusMm = radiusM * PointMm.MmPerMetre;
            var result = new List<PointMm>();
            foreach (PointMm p in PointsEvery(line, spacingM))
                if (p.DistanceTo(centre) <= radiusMm) result.Add(p);
            return result;
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
