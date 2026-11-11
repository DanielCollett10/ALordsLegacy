using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Game.Tools.Shared
{
    /// <summary>
    /// Presentation-only edge snap for the module ghost (decision 0017, woodcutting.md WC-04). Given the operation's
    /// area outline (counter-clockwise), a module type, a cursor centre and a rotation, finds the nearest outline edge
    /// within the snap distance and returns the centre that puts the module flush against it, turned so a side runs along
    /// that edge. The Sim does not know about snapping; it only judges the result in <c>ModulePlacement.Preview</c>.
    /// </summary>
    public static class ModuleSnap
    {
        /// <summary>The module is nudged this far (m) inside the edge so integer rounding of its corners never pokes out.</summary>
        public const double InsetM = 0.003;

        /// <summary>
        /// Tries to snap. True with <paramref name="centre"/> and <paramref name="rotationDeg"/> set when an edge is within
        /// <paramref name="snapDistanceM"/> (gap between the module's side and the edge, measured along the edge normal).
        /// The side chosen is the one whose rotation is closest to <paramref name="rotationDeg"/>, so Q/E pick the side.
        /// </summary>
        public static bool TrySnap(IReadOnlyList<PointMm> outline, FootprintTypeDef type, PointMm cursor, double rotationDeg,
            double snapDistanceM, out PointMm centre, out double snappedRotationDeg)
        {
            centre = cursor; snappedRotationDeg = rotationDeg;
            if (outline == null || outline.Count < 3 || type == null || snapDistanceM <= 0) return false;
            double hw = type.WidthMm / 2000.0, hd = type.DepthMm / 2000.0;
            double bestGap = double.MaxValue;
            for (int i = 0; i < outline.Count; i++)
            {
                PointMm a = outline[i], b = outline[(i + 1) % outline.Count];
                double ex = (b.X - a.X) / 1000.0, ez = (b.Z - a.Z) / 1000.0;
                double len = Math.Sqrt(ex * ex + ez * ez);
                if (len < 1e-6) continue;
                double dx = ex / len, dz = ez / len;
                double nx = -dz, nz = dx; // inward for a counter-clockwise outline
                double px = (cursor.X - a.X) / 1000.0, pz = (cursor.Z - a.Z) / 1000.0;
                double t = px * dx + pz * dz;
                if (t < 0 || t > len) continue;
                double d = px * nx + pz * nz;

                double theta = Math.Atan2(dz, dx) * 180.0 / Math.PI;
                int k = ClosestQuarter(theta, rotationDeg);
                double extent = (k % 2 == 0) ? hd : hw;
                double gap = Math.Abs(d - extent);
                if (gap > snapDistanceM || gap >= bestGap) continue;
                bestGap = gap;
                double shift = extent + InsetM - d;
                centre = new PointMm(cursor.X + (long)Math.Round(nx * shift * 1000.0), cursor.Z + (long)Math.Round(nz * shift * 1000.0));
                snappedRotationDeg = Normalize(theta + 90.0 * k);
            }
            return bestGap != double.MaxValue;
        }

        /// <summary>Wraps degrees into [0, 360).</summary>
        public static double Normalize(double deg)
        {
            double r = deg % 360.0;
            return r < 0 ? r + 360.0 : r;
        }

        private static int ClosestQuarter(double theta, double rotationDeg)
        {
            int best = 0; double bestDiff = double.MaxValue;
            for (int k = 0; k < 4; k++)
            {
                double diff = Math.Abs(Normalize(theta + 90.0 * k - rotationDeg + 180.0) - 180.0);
                if (diff < bestDiff) { bestDiff = diff; best = k; }
            }
            return best;
        }
    }
}
