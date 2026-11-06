using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;

namespace LordsLegacy.Sim.Forestry
{
    /// <summary>All trees, keyed by explicit id, with a uniform spatial grid for area queries.</summary>
    public sealed class TreeRegistry
    {
        private readonly SortedDictionary<int, TreeRecord> _byId = new SortedDictionary<int, TreeRecord>();
        private readonly Dictionary<long, List<TreeRecord>> _cells = new Dictionary<long, List<TreeRecord>>();
        private readonly float _cellSize;

        /// <summary>Creates an empty registry. <paramref name="cellSizeM"/> is the spatial grid cell size in metres.</summary>
        public TreeRegistry(float cellSizeM = 16f)
        {
            if (!(cellSizeM > 0f)) throw new ArgumentOutOfRangeException(nameof(cellSizeM));
            _cellSize = cellSizeM;
        }

        /// <summary>Number of trees.</summary>
        public int Count => _byId.Count;

        /// <summary>All trees in ascending id order.</summary>
        public IEnumerable<TreeRecord> All => _byId.Values;

        /// <summary>Adds a standing tree. Throws <see cref="ArgumentException"/> if the id exists.</summary>
        public TreeRecord Add(TreeId id, float x, float z, double trunkLengthM)
        {
            if (_byId.ContainsKey(id.Value)) throw new ArgumentException("Duplicate tree id " + id, nameof(id));
            if (trunkLengthM < 0 || double.IsNaN(trunkLengthM)) throw new ArgumentOutOfRangeException(nameof(trunkLengthM));
            var t = new TreeRecord(id, x, z, trunkLengthM);
            _byId.Add(id.Value, t);
            long key = Key(Cell(x), Cell(z));
            if (!_cells.TryGetValue(key, out var list)) { list = new List<TreeRecord>(); _cells[key] = list; }
            list.Add(t);
            return t;
        }

        /// <summary>Returns the tree, or throws <see cref="KeyNotFoundException"/>.</summary>
        public TreeRecord Get(TreeId id)
        {
            if (!_byId.TryGetValue(id.Value, out var t)) throw new KeyNotFoundException("No tree " + id);
            return t;
        }

        /// <summary>Looks up a tree by id.</summary>
        public bool TryGet(TreeId id, out TreeRecord tree) => _byId.TryGetValue(id.Value, out tree);

        /// <summary>Whole standard logs from a trunk: floor(length / StandardLogLengthM).</summary>
        public static int LogYield(double trunkLengthM, SimTuning tuning)
        {
            if (tuning == null) throw new ArgumentNullException(nameof(tuning));
            return (int)Math.Floor(trunkLengthM / tuning.StandardLogLengthM);
        }

        /// <summary>Trees inside the rectangle (edges inclusive), in ascending id order.</summary>
        public List<TreeRecord> QueryRect(Bounds2 b)
        {
            var result = new List<TreeRecord>();
            if (b.IsEmpty) return result;
            int x0 = Cell(b.MinX), x1 = Cell(b.MaxX), z0 = Cell(b.MinZ), z1 = Cell(b.MaxZ);
            for (int cx = x0; cx <= x1; cx++)
                for (int cz = z0; cz <= z1; cz++)
                    if (_cells.TryGetValue(Key(cx, cz), out var list))
                        foreach (var t in list)
                            if (b.Contains(t.X, t.Z)) result.Add(t);
            result.Sort((a, c) => a.Id.Value.CompareTo(c.Id.Value));
            return result;
        }

        /// <summary>Trees within <paramref name="radiusM"/> of (x,z) (edge inclusive), in ascending id order.</summary>
        public List<TreeRecord> QueryRadius(float x, float z, float radiusM)
        {
            var result = new List<TreeRecord>();
            if (radiusM < 0f) return result;
            double r2 = (double)radiusM * radiusM;
            foreach (var t in QueryRect(new Bounds2(x - radiusM, z - radiusM, x + radiusM, z + radiusM)))
            {
                double dx = t.X - x, dz = t.Z - z;
                if (dx * dx + dz * dz <= r2) result.Add(t);
            }
            return result;
        }

        private int Cell(float v) => (int)Math.Floor(v / _cellSize);
        private static long Key(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;
    }
}
