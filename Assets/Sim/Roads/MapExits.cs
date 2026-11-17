using System;
using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Navigation;

namespace LordsLegacy.Sim.Roads
{
    /// <summary>
    /// Pure queries for where the map can be left (GR-11, GR-24, GR-24b, decision 0045). The edge is the terrain bounds;
    /// a point is on it within <c>MapEdgeToleranceM</c>. Exits are derived from the road graph and never saved; only the
    /// entry point computed once at a new game is saved (<see cref="SimWorld.EntryPoint"/>).
    /// </summary>
    public static class MapExits
    {
        /// <summary>True if <paramref name="p"/> lies on the map and within the edge tolerance of its bounds.</summary>
        public static bool IsOnEdge(SimWorld world, PointMm p)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            return IsOnEdge(world, p, ToleranceMm(world));
        }

        /// <summary>Every road node (segment end or junction) on the edge, in node id order. Derived, never saved.</summary>
        public static IReadOnlyList<RoadNode> All(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            long tol = ToleranceMm(world);
            return world.Roads.Nodes.Where(n => IsOnEdge(world, n.Position, tol)).OrderBy(n => n.Id).ToList();
        }

        /// <summary>
        /// The exit with the shortest walking cost from <paramref name="from"/>. With no reachable road exit, the nearest
        /// walkable edge point; null only when no edge point is walkable.
        /// </summary>
        public static PointMm? Nearest(SimWorld world, PointMm from)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            PointMm? best = null;
            double bestCost = double.MaxValue;
            foreach (RoadNode n in All(world))
            {
                PathResult r = GridPathfinder.Find(world.Nav, new PathPoint(from.XMetres, from.ZMetres),
                    new PathPoint(n.Position.XMetres, n.Position.ZMetres));
                if (!r.Ok || r.TravelMinutes >= bestCost) continue;
                bestCost = r.TravelMinutes;
                best = n.Position;
            }
            return best ?? NearestWalkableEdgePoint(world, from);
        }

        /// <summary>
        /// The entry point for a new game: the trunk polyline's end on the edge if it has one, else the walkable edge
        /// point nearest its end that is closer to the edge. Null for an empty trunk or no walkable edge.
        /// </summary>
        public static PointMm? ComputeEntry(SimWorld world, IReadOnlyList<PointMm> trunk)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (trunk == null || trunk.Count == 0) return null;
            long tol = ToleranceMm(world);
            PointMm first = trunk[0], last = trunk[trunk.Count - 1];
            if (IsOnEdge(world, first, tol)) return first;
            if (IsOnEdge(world, last, tol)) return last;
            return NearestWalkableEdgePoint(world, EdgeDistanceMm(world, first) <= EdgeDistanceMm(world, last) ? first : last);
        }

        private static PointMm? NearestWalkableEdgePoint(SimWorld world, PointMm from)
        {
            NavGrid nav = world.Nav;
            if (nav.Width == 0 || nav.Depth == 0) return null;
            PointMm? best = null;
            double bestD = double.MaxValue;
            for (int iz = 0; iz < nav.Depth; iz++)
                for (int ix = 0; ix < nav.Width; ix++)
                {
                    bool ring = ix == 0 || iz == 0 || ix == nav.Width - 1 || iz == nav.Depth - 1;
                    if (!ring || nav.IsBlocked(ix, iz)) continue;
                    PointMm p = SnapToEdge(world, nav.CentreX(ix), nav.CentreZ(iz), ix == 0, ix == nav.Width - 1, iz == 0, iz == nav.Depth - 1);
                    double d = p.DistanceTo(from);
                    if (d < bestD) { bestD = d; best = p; }
                }
            return best;
        }

        private static PointMm SnapToEdge(SimWorld world, double x, double z, bool minX, bool maxX, bool minZ, bool maxZ)
        {
            var b = world.Terrain.Bounds;
            if (minX) x = b.MinX; else if (maxX) x = b.MaxX;
            if (minZ) z = b.MinZ; else if (maxZ) z = b.MaxZ;
            return PointMm.FromMetres(x, z);
        }

        private static long ToleranceMm(SimWorld world) => (long)Math.Round(world.Tuning.MapEdgeToleranceM * 1000.0);

        private static long Mm(float metres) => (long)Math.Round(metres * 1000.0);

        private static bool IsOnEdge(SimWorld world, PointMm p, long tolMm)
        {
            var b = world.Terrain.Bounds;
            long minX = Mm(b.MinX), maxX = Mm(b.MaxX), minZ = Mm(b.MinZ), maxZ = Mm(b.MaxZ);
            if (p.X < minX || p.X > maxX || p.Z < minZ || p.Z > maxZ) return false;
            return p.X - minX <= tolMm || maxX - p.X <= tolMm || p.Z - minZ <= tolMm || maxZ - p.Z <= tolMm;
        }

        private static long EdgeDistanceMm(SimWorld world, PointMm p)
        {
            var b = world.Terrain.Bounds;
            return Math.Min(Math.Min(Math.Abs(p.X - Mm(b.MinX)), Math.Abs(Mm(b.MaxX) - p.X)),
                Math.Min(Math.Abs(p.Z - Mm(b.MinZ)), Math.Abs(Mm(b.MaxZ) - p.Z)));
        }
    }
}
