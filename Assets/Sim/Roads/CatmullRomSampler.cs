using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Tuning;

namespace LordsLegacy.Sim.Roads
{
    /// <summary>Centripetal (alpha 0.5) Catmull-Rom sampler, RD-01 to RD-03. Deterministic and stateless.</summary>
    public sealed class CatmullRomSampler : ICurveSampler
    {
        private readonly double _spacingM;
        private readonly double _minPointSpacingM;

        /// <summary>Creates a sampler reading <c>RoadSampleSpacingM</c> and <c>RoadMinPointSpacingM</c> from tuning.</summary>
        public CatmullRomSampler(SimTuning tuning)
            : this(tuning.RoadSampleSpacingM, tuning.RoadMinPointSpacingM) { }

        /// <summary>Creates a sampler with explicit values in metres.</summary>
        public CatmullRomSampler(double sampleSpacingM, double minPointSpacingM)
        {
            if (!(sampleSpacingM > 0)) throw new ArgumentOutOfRangeException(nameof(sampleSpacingM));
            _spacingM = sampleSpacingM;
            _minPointSpacingM = minPointSpacingM;
        }

        /// <inheritdoc />
        public CurveKind Kind => CurveKind.CatmullRomCentripetal;

        /// <inheritdoc />
        public IReadOnlyList<PointMm> Sample(IReadOnlyList<PointMm> controlPoints)
        {
            List<PointMm> pts = ControlPointFilter.Clean(controlPoints, _minPointSpacingM);
            int n = pts.Count;
            if (n < 2) return pts;

            var result = new List<PointMm> { pts[0] };
            double spacingMm = _spacingM * PointMm.MmPerMetre;
            for (int i = 0; i < n - 1; i++)
            {
                PointMm p1 = pts[i], p2 = pts[i + 1];
                // Phantom end points reflect the neighbour, so the end spans leave straight-ish.
                PointMm p0 = i > 0 ? pts[i - 1] : Reflect(p2, p1);
                PointMm p3 = i + 2 < n ? pts[i + 2] : Reflect(p1, p2);

                double chord = p1.DistanceTo(p2);
                int fine = (int)Math.Min(4000, Math.Max(16, Math.Ceiling(chord / 50.0)));
                var fx = new double[fine + 1];
                var fz = new double[fine + 1];
                var cum = new double[fine + 1];
                for (int k = 0; k <= fine; k++)
                {
                    Evaluate(p0, p1, p2, p3, (double)k / fine, out fx[k], out fz[k]);
                    if (k > 0)
                    {
                        double dx = fx[k] - fx[k - 1], dz = fz[k] - fz[k - 1];
                        cum[k] = cum[k - 1] + Math.Sqrt(dx * dx + dz * dz);
                    }
                }
                double length = cum[fine];
                int steps = Math.Max(1, (int)Math.Round(length / spacingMm));
                int seg = 0;
                for (int s = 1; s < steps; s++)
                {
                    double target = length * s / steps;
                    while (seg < fine - 1 && cum[seg + 1] < target) seg++;
                    double span = cum[seg + 1] - cum[seg];
                    double f = span > 0 ? (target - cum[seg]) / span : 0;
                    double x = fx[seg] + (fx[seg + 1] - fx[seg]) * f;
                    double z = fz[seg] + (fz[seg + 1] - fz[seg]) * f;
                    result.Add(new PointMm(
                        (long)Math.Round(x, MidpointRounding.AwayFromZero),
                        (long)Math.Round(z, MidpointRounding.AwayFromZero)));
                }
                result.Add(p2);
            }
            return result;
        }

        private static PointMm Reflect(PointMm through, PointMm origin) =>
            new PointMm(2 * origin.X - through.X, 2 * origin.Z - through.Z);

        // Barry-Goldman pyramid with centripetal knots; u in [0,1] maps to [t1,t2].
        private static void Evaluate(PointMm a, PointMm b, PointMm c, PointMm d, double u, out double x, out double z)
        {
            double t0 = 0;
            double t1 = t0 + Knot(a, b);
            double t2 = t1 + Knot(b, c);
            double t3 = t2 + Knot(c, d);
            double t = t1 + (t2 - t1) * u;

            Lerp(a.X, a.Z, b.X, b.Z, t0, t1, t, out double a1x, out double a1z);
            Lerp(b.X, b.Z, c.X, c.Z, t1, t2, t, out double a2x, out double a2z);
            Lerp(c.X, c.Z, d.X, d.Z, t2, t3, t, out double a3x, out double a3z);
            Lerp(a1x, a1z, a2x, a2z, t0, t2, t, out double b1x, out double b1z);
            Lerp(a2x, a2z, a3x, a3z, t1, t3, t, out double b2x, out double b2z);
            Lerp(b1x, b1z, b2x, b2z, t1, t2, t, out x, out z);
        }

        private static double Knot(PointMm p, PointMm q)
        {
            double d = p.DistanceTo(q);
            return d > 0 ? Math.Sqrt(d) : 1.0;
        }

        private static void Lerp(double px, double pz, double qx, double qz, double ta, double tb, double t,
            out double x, out double z)
        {
            double w = (t - ta) / (tb - ta);
            x = px + (qx - px) * w;
            z = pz + (qz - pz) * w;
        }
    }
}
