using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Tuning;

namespace LordsLegacy.Sim.Geometry
{
    /// <summary>One stall slot: a grid cell and the stall footprint inside it (MK-05).</summary>
    public sealed class StallSlot
    {
        /// <summary>Cell corners (4 x 5 m by default), in order around the cell.</summary>
        public PointMm[] Cell { get; }
        /// <summary>Stall footprint corners (3 x 2 m by default), in order around the stall.</summary>
        public PointMm[] Stall { get; }
        /// <summary>Centre of the stall footprint.</summary>
        public PointMm Centre { get; }

        /// <summary>Creates a slot.</summary>
        public StallSlot(PointMm[] cell, PointMm[] stall, PointMm centre)
        {
            Cell = cell; Stall = stall; Centre = centre;
        }
    }

    /// <summary>Pure subdivision of a market polygon into stall slots (MK-05). Nothing is saved: slots derive from the polygon.</summary>
    public static class StallGrid
    {
        /// <summary>Stall footprint width in mm (3 m, MK-21).</summary>
        public const double StallWidthMm = 3000;
        /// <summary>Stall footprint depth in mm (2 m, MK-21).</summary>
        public const double StallDepthMm = 2000;
        /// <summary>Clear padding kept between every cell and the market edge, in mm (2 m, PLACEHOLDER).</summary>
        public const double EdgePaddingMm = 2000;

        /// <summary>
        /// Slots aligned to the polygon's longest edge (first one on a tie), keeping only cells wholly inside.
        /// Ordered by distance from the polygon centre, nearest first; ties by grid order. Empty when nothing fits.
        /// The cell's width runs along the longest edge, its depth to the left of that edge. The stall sits at the
        /// back of the cell, centred across it, leaving the aisle in front.
        /// </summary>
        public static List<StallSlot> Slots(IReadOnlyList<PointMm> polygon, SimTuning tuning)
        {
            var result = new List<StallSlot>();
            if (polygon == null || polygon.Count < 3 || tuning == null) return result;
            double cellW = tuning.StallCellWidthM * 1000.0, cellD = tuning.StallCellDepthM * 1000.0;
            double stallW = Math.Min(StallWidthMm, cellW), stallD = Math.Min(StallDepthMm, cellD);
            if (cellW <= 0 || cellD <= 0) return result;

            int n = polygon.Count, best = -1;
            long bestLen = 0;
            for (int i = 0; i < n; i++)
            {
                PointMm d = polygon[(i + 1) % n] - polygon[i];
                long len = d.X * d.X + d.Z * d.Z;
                if (len > bestLen) { bestLen = len; best = i; }
            }
            if (best < 0) return result;

            PointMm origin = polygon[best];
            PointMm e = polygon[(best + 1) % n] - origin;
            double el = Math.Sqrt(bestLen);
            double ux = e.X / el, uz = e.Z / el, vx = -uz, vz = ux;

            double sMin = double.MaxValue, sMax = double.MinValue, tMin = double.MaxValue, tMax = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                double dx = polygon[i].X - origin.X, dz = polygon[i].Z - origin.Z;
                double s = dx * ux + dz * uz, t = dx * vx + dz * vz;
                if (s < sMin) sMin = s;
                if (s > sMax) sMax = s;
                if (t < tMin) tMin = t;
                if (t > tMax) tMax = t;
            }

            PointMm centre = Polygons.Centroid(polygon);
            var found = new List<(long dist, int j, int i, StallSlot slot)>();
            // The grid lives in the extent shrunk by the edge padding and is centred in it, so slots sit in the
            // middle of the market. A cell must also keep the padding clear of every edge (checked with the
            // cell grown by the padding less 5 mm, so mm rounding cannot reject a fit; real clearance >= 1.995 m).
            double usableS = sMax - sMin - 2 * EdgePaddingMm, usableT = tMax - tMin - 2 * EdgePaddingMm;
            if (usableS < cellW || usableT < cellD) return result;
            int cols = (int)Math.Floor(usableS / cellW), rows = (int)Math.Floor(usableT / cellD);
            double sStart = sMin + EdgePaddingMm + (usableS - cols * cellW) / 2;
            double tStart = tMin + EdgePaddingMm + (usableT - rows * cellD) / 2;
            double check = EdgePaddingMm - 5;
            for (int j = 0; j < rows; j++)
            {
                for (int i = 0; i < cols; i++)
                {
                    double s0 = sStart + i * cellW, t0 = tStart + j * cellD;
                    PointMm[] cell = Rect(origin, ux, uz, vx, vz, s0, t0, cellW, cellD);
                    if (!WhollyInside(polygon, Rect(origin, ux, uz, vx, vz, s0 - check, t0 - check, cellW + 2 * check, cellD + 2 * check))) continue;
                    PointMm[] stall = Rect(origin, ux, uz, vx, vz, s0 + (cellW - stallW) / 2, t0, stallW, stallD);
                    PointMm c = Pt(origin, ux, uz, vx, vz, s0 + cellW / 2, t0 + stallD / 2);
                    PointMm d = c - centre;
                    found.Add((d.X * d.X + d.Z * d.Z, j, i, new StallSlot(cell, stall, c)));
                }
            }
            found.Sort((a, b) =>
            {
                int c = a.dist.CompareTo(b.dist);
                if (c != 0) return c;
                c = a.j.CompareTo(b.j);
                return c != 0 ? c : a.i.CompareTo(b.i);
            });
            foreach (var f in found) result.Add(f.slot);
            return result;
        }

        private static PointMm Pt(PointMm o, double ux, double uz, double vx, double vz, double s, double t) =>
            new PointMm(o.X + (long)Math.Round(s * ux + t * vx, MidpointRounding.AwayFromZero),
                        o.Z + (long)Math.Round(s * uz + t * vz, MidpointRounding.AwayFromZero));

        private static PointMm[] Rect(PointMm o, double ux, double uz, double vx, double vz, double s, double t, double w, double d) => new[]
        {
            Pt(o, ux, uz, vx, vz, s, t), Pt(o, ux, uz, vx, vz, s + w, t),
            Pt(o, ux, uz, vx, vz, s + w, t + d), Pt(o, ux, uz, vx, vz, s, t + d),
        };

        private static bool WhollyInside(IReadOnlyList<PointMm> poly, PointMm[] cell)
        {
            foreach (var p in cell) if (!Polygons.Contains(poly, p)) return false;
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                PointMm a = poly[i], b = poly[(i + 1) % n];
                if (Polygons.Locate(cell, a) == PointLocation.Inside) return false;
                for (int k = 0; k < 4; k++)
                    if (Segments.Classify(a, b, cell[k], cell[(k + 1) % 4]) == SegmentRelation.Crossing) return false;
            }
            return true;
        }
    }
}
