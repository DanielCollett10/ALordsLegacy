using System;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;

namespace LordsLegacy.Sim.Areas
{
    /// <summary>Helpers for NearRoad edges (AR-24): distance of a point from a road side line, and which way is outward.</summary>
    internal static class NearRoadGeometry
    {
        /// <summary>
        /// Distance (mm) from the side line to <paramref name="p"/>, positive when p is on the side away from the
        /// road, negative when on the road side. Within 1 mm of the line it is 0.
        /// </summary>
        public static double SignedOutwardMm(PointMm[] line, RoadSide side, PointMm p)
        {
            PolylineHit h = Polylines.Nearest(line, p);
            if (h.DistanceMm < 1.0) return 0.0;
            int i = Math.Min(h.SegmentIndex, line.Length - 2);
            (double nx, double nz) = SegmentNormal(line[i], line[i + 1], side);
            double dot = (p.X - h.X) * nx + (p.Z - h.Z) * nz;
            return dot >= 0 ? h.DistanceMm : -h.DistanceMm;
        }

        /// <summary>True if p is within <paramref name="reachM"/> of the side and not on the road side of it (5 cm slack).</summary>
        public static bool VertexOk(PointMm[] line, RoadSide side, PointMm p, double reachM)
        {
            if (line.Length < 2) return false;
            double d = SignedOutwardMm(line, side, p);
            return d >= -AreaValidator.FollowsRoadToleranceMm && Math.Abs(d) <= reachM * PointMm.MmPerMetre;
        }

        /// <summary>Unit outward normal at line vertex i (average of the adjacent segment normals).</summary>
        public static (double, double) OutwardNormal(PointMm[] line, int i, RoadSide side)
        {
            double nx = 0, nz = 0;
            if (i > 0) { (double x, double z) = SegmentNormal(line[i - 1], line[i], side); nx += x; nz += z; }
            if (i < line.Length - 1) { (double x, double z) = SegmentNormal(line[i], line[i + 1], side); nx += x; nz += z; }
            double len = Math.Sqrt(nx * nx + nz * nz);
            return len < 1e-9 ? (0.0, 0.0) : (nx / len, nz / len);
        }

        private static (double, double) SegmentNormal(PointMm a, PointMm b, RoadSide side)
        {
            double dx = b.X - a.X, dz = b.Z - a.Z;
            double len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-9) return (0.0, 0.0);
            double sign = side == RoadSide.Left ? 1 : -1;
            return (sign * -dz / len, sign * dx / len);
        }
    }
}
