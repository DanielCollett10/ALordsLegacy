using System;
using System.Collections.Generic;
using LordsLegacy.Prototypes.PerfSpike.Grid;

namespace LordsLegacy.Prototypes.PerfSpike.Nav
{
    /// <summary>
    /// THROWAWAY (T-007). A* over a <see cref="SpikeCostGrid"/> with 8 neighbours.
    /// Step cost = step length (1 or sqrt 2 cells) x cost of the cell entered. Heuristic = octile distance x
    /// grid MinCost, which never overestimates, so the first path found to the goal is optimal.
    /// Diagonal steps may not cut a blocked corner. Blocked cells are never entered.
    /// Storage is sized to the grid once and reused via generation stamps, so a search does not clear arrays.
    /// Not thread-safe; one instance per grid.
    /// </summary>
    public sealed class SpikeAStar
    {
        private const float SqrtTwo = 1.41421356f;
        private static readonly int[] Dx = { 1, -1, 0, 0, 1, -1, 1, -1 };
        private static readonly int[] Dz = { 0, 0, 1, -1, 1, 1, -1, -1 };

        private readonly SpikeCostGrid _grid;
        private readonly float[] _g;
        private readonly int[] _parent;
        private readonly int[] _seenGen;
        private readonly int[] _closedGen;
        private readonly SpikeMinHeap _open = new SpikeMinHeap(4096);
        private int _gen;

        /// <summary>Creates a searcher for <paramref name="grid"/>. Allocates one set of per-cell arrays.</summary>
        public SpikeAStar(SpikeCostGrid grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            _grid = grid;
            int n = grid.Width * grid.Height;
            _g = new float[n];
            _parent = new int[n];
            _seenGen = new int[n];
            _closedGen = new int[n];
        }

        /// <summary>
        /// Finds the cheapest path from (sx,sz) to (gx,gz). Returns false (and a null path) when the start or
        /// goal is outside or blocked, or when no route exists.
        /// </summary>
        public bool TryFindPath(int sx, int sz, int gx, int gz, out SpikePath path)
        {
            path = null;
            if (!_grid.IsPassable(sx, sz) || !_grid.IsPassable(gx, gz)) return false;

            if (sx == gx && sz == gz)
            {
                path = new SpikePath(new List<CellRef> { new CellRef(sx, sz) }, 0f, 0);
                return true;
            }

            NextGeneration();
            int width = _grid.Width;
            int start = sz * width + sx;
            int goal = gz * width + gx;

            _g[start] = 0f;
            _parent[start] = -1;
            _seenGen[start] = _gen;
            _open.Push(Heuristic(sx, sz, gx, gz), start);

            int expanded = 0;
            while (_open.Count > 0)
            {
                _open.Pop(out _, out int node);
                if (_closedGen[node] == _gen) continue;
                _closedGen[node] = _gen;
                expanded++;

                if (node == goal)
                {
                    path = BuildPath(goal, _g[goal], expanded);
                    return true;
                }

                int x = node % width;
                int z = node / width;
                for (int d = 0; d < 8; d++)
                {
                    int nx = x + Dx[d];
                    int nz = z + Dz[d];
                    if (!_grid.IsPassable(nx, nz)) continue;
                    bool diagonal = d >= 4;
                    if (diagonal && (!_grid.IsPassable(x + Dx[d], z) || !_grid.IsPassable(x, z + Dz[d]))) continue;

                    int next = nz * width + nx;
                    if (_closedGen[next] == _gen) continue;

                    float step = (diagonal ? SqrtTwo : 1f) * _grid.CostAt(nx, nz);
                    float tentative = _g[node] + step;
                    if (_seenGen[next] == _gen && tentative >= _g[next]) continue;

                    _g[next] = tentative;
                    _parent[next] = node;
                    _seenGen[next] = _gen;
                    _open.Push(tentative + Heuristic(nx, nz, gx, gz), next);
                }
            }
            return false;
        }

        private void NextGeneration()
        {
            if (_gen == int.MaxValue)
            {
                Array.Clear(_seenGen, 0, _seenGen.Length);
                Array.Clear(_closedGen, 0, _closedGen.Length);
                _gen = 0;
            }
            _gen++;
            _open.Clear();
        }

        private float Heuristic(int x, int z, int gx, int gz)
        {
            int dx = Math.Abs(gx - x);
            int dz = Math.Abs(gz - z);
            int min = Math.Min(dx, dz);
            int max = Math.Max(dx, dz);
            return ((max - min) + SqrtTwo * min) * _grid.MinCost;
        }

        private SpikePath BuildPath(int goal, float cost, int expanded)
        {
            var reversed = new List<CellRef>();
            int width = _grid.Width;
            for (int node = goal; node != -1; node = _parent[node])
            {
                reversed.Add(new CellRef(node % width, node / width));
            }
            reversed.Reverse();
            return new SpikePath(reversed, cost, expanded);
        }
    }
}
