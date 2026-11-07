using System.Collections.Generic;

namespace LordsLegacy.Sim.Geometry
{
    /// <summary>Overlap area of two simple polygons (decision 0012): triangulate, clip triangle pairs, sum.</summary>
    public static class PolygonOverlap
    {
        private const double MmSqPerM2 = 1000000.0;

        /// <summary>
        /// Area in m² shared by two simple polygons. Both are ear-clip triangulated; triangle pairs whose
        /// bounding boxes do not overlap are skipped; the rest are clipped (Sutherland–Hodgman, double) and summed.
        /// Returns 0 for null, fewer than 3 points, or zero-area polygons. Deterministic.
        /// </summary>
        public static double AreaM2(IReadOnlyList<PointMm> a, IReadOnlyList<PointMm> b)
        {
            if (a == null || b == null || a.Count < 3 || b.Count < 3) return 0.0;
            List<TriangleMm> ta = Triangulator.EarClip(a);
            List<TriangleMm> tb = Triangulator.EarClip(b);
            if (ta.Count == 0 || tb.Count == 0) return 0.0;

            var boxesB = new BoundsMm[tb.Count];
            for (int j = 0; j < tb.Count; j++) boxesB[j] = BoxOf(tb[j]);

            double sumMm2 = 0.0;
            for (int i = 0; i < ta.Count; i++)
            {
                if (ta[i].Area2Mm == 0) continue;
                BoundsMm boxA = BoxOf(ta[i]);
                for (int j = 0; j < tb.Count; j++)
                {
                    if (tb[j].Area2Mm == 0 || !boxA.Intersects(boxesB[j])) continue;
                    sumMm2 += ClipArea(ta[i], tb[j]);
                }
            }
            return sumMm2 / MmSqPerM2;
        }

        /// <summary>True only when the overlap area is strictly greater than <paramref name="toleranceM2"/>.</summary>
        public static bool Overlaps(IReadOnlyList<PointMm> a, IReadOnlyList<PointMm> b, double toleranceM2) =>
            AreaM2(a, b) > toleranceM2;

        private static BoundsMm BoxOf(TriangleMm t)
        {
            long minX = System.Math.Min(t.A.X, System.Math.Min(t.B.X, t.C.X));
            long maxX = System.Math.Max(t.A.X, System.Math.Max(t.B.X, t.C.X));
            long minZ = System.Math.Min(t.A.Z, System.Math.Min(t.B.Z, t.C.Z));
            long maxZ = System.Math.Max(t.A.Z, System.Math.Max(t.B.Z, t.C.Z));
            return new BoundsMm(new PointMm(minX, minZ), new PointMm(maxX, maxZ));
        }

        /// <summary>Area (mm²) of subject ∩ clip, both CCW triangles.</summary>
        private static double ClipArea(TriangleMm subject, TriangleMm clip)
        {
            var poly = new List<double[]>
            {
                new double[] { subject.A.X, subject.A.Z },
                new double[] { subject.B.X, subject.B.Z },
                new double[] { subject.C.X, subject.C.Z },
            };
            PointMm[] c = { clip.A, clip.B, clip.C };
            for (int e = 0; e < 3 && poly.Count > 0; e++)
            {
                PointMm p = c[e], q = c[(e + 1) % 3];
                double ex = q.X - p.X, ez = q.Z - p.Z;
                var next = new List<double[]>(poly.Count + 1);
                for (int i = 0; i < poly.Count; i++)
                {
                    double[] cur = poly[i], prev = poly[(i + poly.Count - 1) % poly.Count];
                    double dc = ex * (cur[1] - p.Z) - ez * (cur[0] - p.X);
                    double dp = ex * (prev[1] - p.Z) - ez * (prev[0] - p.X);
                    bool curIn = dc >= 0, prevIn = dp >= 0;
                    if (curIn != prevIn)
                    {
                        double t = dp / (dp - dc);
                        next.Add(new[] { prev[0] + (cur[0] - prev[0]) * t, prev[1] + (cur[1] - prev[1]) * t });
                    }
                    if (curIn) next.Add(cur);
                }
                poly = next;
            }
            if (poly.Count < 3) return 0.0;
            double s = 0.0;
            for (int i = 0; i < poly.Count; i++)
            {
                double[] u = poly[i], v = poly[(i + 1) % poly.Count];
                s += u[0] * v[1] - v[0] * u[1];
            }
            return System.Math.Abs(s) / 2.0;
        }
    }
}
