using System;
using System.Collections.Generic;

namespace LordsLegacy.Sim.Navigation
{
    /// <summary>
    /// A* over the <see cref="NavGrid"/> (MOV-10..13). 8 neighbours, no corner cutting past blocked cells. A step costs
    /// its length divided by the mean of the two cells' speed multipliers (minutes at 1 m/min); the heuristic is
    /// straight distance divided by the grid's top multiplier. Ties break on f, then h, then cell index, so the same
    /// query always gives the same path. The result is smoothed by dropping corners where the straight line crosses
    /// only walkable cells of one multiplier.
    /// </summary>
    public sealed class GridPathfinder : IPathfinder
    {
        /// <summary>How far a blocked start or goal may snap to the nearest walkable cell centre, in metres.</summary>
        public const double SnapRadiusM = 20.0;

        private const double Sqrt2 = 1.4142135623730951;

        /// <inheritdoc />
        PathResult IPathfinder.Find(NavGrid nav, PathPoint from, PathPoint to) => Find(nav, from, to);

        /// <summary>
        /// Fastest route between two points (metres). A blocked start or goal snaps to the nearest walkable cell
        /// centre within <see cref="SnapRadiusM"/>; if none, or no route exists, the result is NoRoute.
        /// </summary>
        public static PathResult Find(NavGrid nav, PathPoint from, PathPoint to)
        {
            if (nav == null) throw new ArgumentNullException(nameof(nav));
            if (nav.Width == 0 || nav.Depth == 0) return PathResult.NoRoute();

            bool startExact = Snap(nav, from, out int sx, out int sz);
            if (sx < 0) return PathResult.NoRoute();
            bool goalExact = Snap(nav, to, out int gx, out int gz);
            if (gx < 0) return PathResult.NoRoute();

            // Endpoints: the exact input, unless it was snapped to a cell centre.
            PathPoint start = startExact ? from : new PathPoint(nav.CentreX(sx), nav.CentreZ(sz));
            PathPoint goal = goalExact ? to : new PathPoint(nav.CentreX(gx), nav.CentreZ(gz));

            if (sx == gx && sz == gz)
            {
                double len = Dist(start, goal);
                PathPoint[] pts = len == 0 ? new[] { start } : new[] { start, goal };
                return PathResult.Found(pts, len / nav.SpeedMultiplier(sx, sz));
            }

            List<int> cells = AStar(nav, sx, sz, gx, gz);
            return cells == null ? PathResult.NoRoute() : Build(nav, cells, start, goal);
        }

        // Finds the cell for a point. Returns true if the point's own cell is walkable; otherwise snaps to the
        // nearest walkable centre in range (cx = -1 when none).
        private static bool Snap(NavGrid nav, PathPoint p, out int cx, out int cz)
        {
            int ix = nav.CellX(p.X), iz = nav.CellZ(p.Z);
            if (!nav.IsBlocked(ix, iz)) { cx = ix; cz = iz; return true; }

            int r = (int)Math.Ceiling(SnapRadiusM / nav.CellSizeM) + 1;
            double best = double.MaxValue;
            cx = -1; cz = -1;
            for (int z = iz - r; z <= iz + r; z++)
                for (int x = ix - r; x <= ix + r; x++)
                {
                    if (nav.IsBlocked(x, z)) continue;
                    double d = Dist(p, new PathPoint(nav.CentreX(x), nav.CentreZ(z)));
                    if (d > SnapRadiusM || d >= best) continue; // scan order (z, then x) breaks ties
                    best = d; cx = x; cz = z;
                }
            return false;
        }

        private static double Dist(PathPoint a, PathPoint b)
        {
            double dx = a.X - b.X, dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        private static double StepCost(NavGrid nav, int ax, int az, int bx, int bz)
        {
            double len = (ax != bx && az != bz ? Sqrt2 : 1.0) * nav.CellSizeM;
            return len * 0.5 * (1.0 / nav.SpeedMultiplier(ax, az) + 1.0 / nav.SpeedMultiplier(bx, bz));
        }

        private static List<int> AStar(NavGrid nav, int sx, int sz, int gx, int gz)
        {
            int w = nav.Width, n = w * nav.Depth;
            double maxMult = 1.0;
            for (int z = 0; z < nav.Depth; z++)
                for (int x = 0; x < w; x++)
                    if (!nav.IsBlocked(x, z)) maxMult = Math.Max(maxMult, nav.SpeedMultiplier(x, z));

            var g = new double[n];
            var parent = new int[n];
            var closed = new bool[n];
            for (int i = 0; i < n; i++) { g[i] = double.PositiveInfinity; parent[i] = -1; }

            double H(int x, int z)
            {
                double dx = (x - gx) * nav.CellSizeM, dz = (z - gz) * nav.CellSizeM;
                return Math.Sqrt(dx * dx + dz * dz) / maxMult;
            }

            var heap = new Heap();
            int s = sz * w + sx, goal = gz * w + gx;
            g[s] = 0;
            heap.Push(H(sx, sz), H(sx, sz), s);
            while (heap.Count > 0)
            {
                int cur = heap.Pop();
                if (closed[cur]) continue;
                if (cur == goal) break;
                closed[cur] = true;
                int cx = cur % w, cz = cur / w;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int nx = cx + dx, nz = cz + dz;
                        if (nav.IsBlocked(nx, nz)) continue;
                        if (dx != 0 && dz != 0 && (nav.IsBlocked(cx + dx, cz) || nav.IsBlocked(cx, cz + dz))) continue;
                        int ni = nz * w + nx;
                        if (closed[ni]) continue;
                        double ng = g[cur] + StepCost(nav, cx, cz, nx, nz);
                        if (ng >= g[ni]) continue;
                        g[ni] = ng; parent[ni] = cur;
                        double h = H(nx, nz);
                        heap.Push(ng + h, h, ni);
                    }
            }
            if (double.IsPositiveInfinity(g[goal])) return null;

            var path = new List<int>();
            for (int c = goal; c >= 0; c = parent[c]) path.Add(c);
            path.Reverse();
            return path;
        }

        private static PathResult Build(NavGrid nav, List<int> cells, PathPoint start, PathPoint goal)
        {
            int w = nav.Width;
            int last = cells.Count - 1;
            // Smooth: from each anchor, reach as far along the path as a uniform-multiplier straight line allows.
            var keep = new List<int> { 0 };
            double minutes = 0;
            int a = 0;
            while (a < last)
            {
                int ax = cells[a] % w, az = cells[a] / w;
                int b = a + 1;
                while (b + 1 <= last && LineUniform(nav, ax, az, cells[b + 1] % w, cells[b + 1] / w, out _)) b++;
                int bx = cells[b] % w, bz = cells[b] / w;
                if (b == a + 1)
                {
                    minutes += StepCost(nav, ax, az, bx, bz);
                }
                else
                {
                    LineUniform(nav, ax, az, bx, bz, out double mult);
                    double dx = (bx - ax) * nav.CellSizeM, dz = (bz - az) * nav.CellSizeM;
                    minutes += Math.Sqrt(dx * dx + dz * dz) / mult;
                }
                keep.Add(b);
                a = b;
            }

            var pts = new List<PathPoint>();
            var firstC = new PathPoint(nav.CentreX(cells[0] % w), nav.CentreZ(cells[0] / w));
            var lastC = new PathPoint(nav.CentreX(cells[last] % w), nav.CentreZ(cells[last] / w));
            AddDistinct(pts, start);
            foreach (int k in keep) AddDistinct(pts, new PathPoint(nav.CentreX(cells[k] % w), nav.CentreZ(cells[k] / w)));
            AddDistinct(pts, goal);
            minutes += Dist(start, firstC) / nav.SpeedMultiplier(cells[0] % w, cells[0] / w);
            minutes += Dist(lastC, goal) / nav.SpeedMultiplier(cells[last] % w, cells[last] / w);
            return PathResult.Found(pts, minutes);
        }

        private static void AddDistinct(List<PathPoint> pts, PathPoint p)
        {
            if (pts.Count == 0 || !pts[pts.Count - 1].Equals(p)) pts.Add(p);
        }

        // True if every cell the straight line between two cell centres touches is walkable with one shared
        // multiplier (a line through a shared corner touches both side cells too).
        private static bool LineUniform(NavGrid nav, int ax, int az, int bx, int bz, out double mult)
        {
            mult = nav.SpeedMultiplier(ax, az);
            if (nav.IsBlocked(ax, az)) return false;
            int nx = Math.Abs(bx - ax), nz = Math.Abs(bz - az);
            int sx = bx > ax ? 1 : -1, sz = bz > az ? 1 : -1;
            int x = ax, z = az, ix = 0, iz = 0;
            while (ix < nx || iz < nz)
            {
                long decision = (1L + 2 * ix) * nz - (1L + 2 * iz) * nx;
                if (decision == 0)
                {
                    if (!Same(nav, x + sx, z, mult) || !Same(nav, x, z + sz, mult)) return false;
                    x += sx; z += sz; ix++; iz++;
                }
                else if (decision < 0) { x += sx; ix++; }
                else { z += sz; iz++; }
                if (!Same(nav, x, z, mult)) return false;
            }
            return true;
        }

        private static bool Same(NavGrid nav, int x, int z, double mult) =>
            !nav.IsBlocked(x, z) && nav.SpeedMultiplier(x, z) == mult;

        // Binary min-heap ordered by (f, h, cell index) so pops are fully deterministic.
        private sealed class Heap
        {
            private readonly List<double> _f = new List<double>();
            private readonly List<double> _h = new List<double>();
            private readonly List<int> _id = new List<int>();
            public int Count => _id.Count;

            private bool Less(int i, int j) =>
                _f[i] != _f[j] ? _f[i] < _f[j] : _h[i] != _h[j] ? _h[i] < _h[j] : _id[i] < _id[j];

            private void Swap(int i, int j)
            {
                double f = _f[i]; _f[i] = _f[j]; _f[j] = f;
                double h = _h[i]; _h[i] = _h[j]; _h[j] = h;
                int d = _id[i]; _id[i] = _id[j]; _id[j] = d;
            }

            public void Push(double f, double h, int id)
            {
                _f.Add(f); _h.Add(h); _id.Add(id);
                int i = _id.Count - 1;
                while (i > 0 && Less(i, (i - 1) / 2)) { Swap(i, (i - 1) / 2); i = (i - 1) / 2; }
            }

            public int Pop()
            {
                int top = _id[0], last = _id.Count - 1;
                Swap(0, last);
                _f.RemoveAt(last); _h.RemoveAt(last); _id.RemoveAt(last);
                int i = 0, n = _id.Count;
                while (true)
                {
                    int l = 2 * i + 1, r = l + 1, m = i;
                    if (l < n && Less(l, m)) m = l;
                    if (r < n && Less(r, m)) m = r;
                    if (m == i) break;
                    Swap(i, m); i = m;
                }
                return top;
            }
        }
    }
}
