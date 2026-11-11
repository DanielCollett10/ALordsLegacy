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
        private readonly HashSet<int> _removedIds = new HashSet<int>();
        private readonly Dictionary<long, List<TreeRecord>> _cells = new Dictionary<long, List<TreeRecord>>();
        private readonly float _cellSize;
        private int _version;
        private int _standing;

        /// <summary>Creates an empty registry. <paramref name="cellSizeM"/> is the spatial grid cell size in metres.</summary>
        public TreeRegistry(float cellSizeM = 16f)
        {
            if (!(cellSizeM > 0f)) throw new ArgumentOutOfRangeException(nameof(cellSizeM));
            _cellSize = cellSizeM;
        }

        /// <summary>Number of standing trees (removed trees are not counted).</summary>
        public int Count => _standing;

        /// <summary>
        /// Increases by one on every change (each <see cref="Add"/>, each successful <see cref="Remove"/> and each
        /// <see cref="SetState"/> that changes a state). Reservations do not change it.
        /// Views compare it with the last value they drew to know when to rebuild.
        /// </summary>
        public int Version => _version;

        /// <summary>Standing trees in ascending id order. Do not change trees while enumerating this.</summary>
        public IEnumerable<TreeRecord> All => InState(TreeState.Standing);

        /// <summary>Stumps in ascending id order (for the renderer).</summary>
        public IEnumerable<TreeRecord> Stumps => InState(TreeState.Stump);

        /// <summary>Felled trunks in ascending id order (for the renderer).</summary>
        public IEnumerable<TreeRecord> Felled => InState(TreeState.Felled);

        private IEnumerable<TreeRecord> InState(TreeState s)
        {
            foreach (var t in _byId.Values)
                if (t.State == s) yield return t;
        }

        /// <summary>
        /// Moves a tree to a new state and bumps <see cref="Version"/>. Only forward steps are allowed
        /// (Standing → Felled → Stump, or Standing → Stump). Leaving Standing cancels any reservation.
        /// Returns false (no change) if the id is unknown or the state is already <paramref name="state"/>;
        /// throws <see cref="InvalidOperationException"/> for a backward step.
        /// </summary>
        public bool SetState(TreeId id, TreeState state)
        {
            if (!_byId.TryGetValue(id.Value, out var t)) return false;
            if (t.State == state) return false;
            if (state < t.State) throw new InvalidOperationException("Tree " + id + " cannot go from " + t.State + " to " + state);
            if (t.State == TreeState.Standing) _standing--;
            t.State = state;
            t.ReservedBy = null;
            _version++;
            return true;
        }

        /// <summary>
        /// Reserves a standing tree for <paramref name="ownerId"/>. Returns false if the tree is unknown, not
        /// standing, or reserved by someone else. Reserving again as the same owner returns true.
        /// </summary>
        public bool Reserve(TreeId id, int ownerId)
        {
            if (!_byId.TryGetValue(id.Value, out var t) || t.State != TreeState.Standing) return false;
            if (t.ReservedBy.HasValue) return t.ReservedBy.Value == ownerId;
            t.ReservedBy = ownerId;
            return true;
        }

        /// <summary>Clears a reservation. Returns false if the tree is unknown or was not reserved.</summary>
        public bool Release(TreeId id)
        {
            if (!_byId.TryGetValue(id.Value, out var t) || !t.ReservedBy.HasValue) return false;
            t.ReservedBy = null;
            return true;
        }

        /// <summary>True when any standing tree has trunk inside the region.</summary>
        public bool AnyStandingIn(TreeRegion region) => CountStandingIn(region, true) > 0;

        /// <summary>Number of standing trees with trunk inside the region.</summary>
        public int CountStandingIn(TreeRegion region) => CountStandingIn(region, false);

        private int CountStandingIn(TreeRegion region, bool stopAtOne)
        {
            if (region == null) throw new ArgumentNullException(nameof(region));
            int n = 0;
            foreach (var t in QueryRect(region.Bounds()))
            {
                if (!region.Contains(t.X, t.Z)) continue;
                n++;
                if (stopAtOne) break;
            }
            return n;
        }

        /// <summary>
        /// The standing, unreserved tree closest to (x,z) that lies inside <paramref name="region"/> (null = anywhere),
        /// or null if none. Ties go to the lowest id.
        /// </summary>
        public TreeRecord NearestUnreservedStanding(float x, float z, TreeRegion region = null)
        {
            TreeRecord best = null;
            double bestD = double.MaxValue;
            foreach (var t in region == null ? All : QueryRect(region.Bounds()))
            {
                if (t.State != TreeState.Standing || t.ReservedBy.HasValue) continue;
                if (region != null && !region.Contains(t.X, t.Z)) continue;
                double dx = t.X - x, dz = t.Z - z, d = dx * dx + dz * dz;
                if (d < bestD || (d == bestD && t.Id.Value < best.Id.Value)) { best = t; bestD = d; }
            }
            return best;
        }

        /// <summary>
        /// Adds a standing tree. Throws <see cref="ArgumentException"/> if the id exists or was removed before
        /// (removed trees never come back, BLD-10-D).
        /// </summary>
        public TreeRecord Add(TreeId id, float x, float z, double trunkLengthM)
        {
            if (_byId.ContainsKey(id.Value)) throw new ArgumentException("Duplicate tree id " + id, nameof(id));
            if (_removedIds.Contains(id.Value)) throw new ArgumentException("Tree " + id + " was removed and cannot come back", nameof(id));
            if (trunkLengthM < 0 || double.IsNaN(trunkLengthM)) throw new ArgumentOutOfRangeException(nameof(trunkLengthM));
            var t = new TreeRecord(id, x, z, trunkLengthM);
            _byId.Add(id.Value, t);
            long key = Key(Cell(x), Cell(z));
            if (!_cells.TryGetValue(key, out var list)) { list = new List<TreeRecord>(); _cells[key] = list; }
            list.Add(t);
            _standing++;
            _version++;
            return t;
        }

        /// <summary>
        /// Removes a standing tree from the records and the spatial grid. Returns false (and changes nothing)
        /// if the id is unknown or already removed. A removed id can never be added again.
        /// </summary>
        public bool Remove(TreeId id)
        {
            if (!_byId.TryGetValue(id.Value, out var t)) return false;
            _byId.Remove(id.Value);
            _removedIds.Add(id.Value);
            if (t.State == TreeState.Standing) _standing--;
            t.ReservedBy = null; // removing a tree cancels its reservation
            long key = Key(Cell(t.X), Cell(t.Z));
            var list = _cells[key];
            list.Remove(t);
            if (list.Count == 0) _cells.Remove(key);
            _version++;
            return true;
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
                            if (t.State == TreeState.Standing && b.Contains(t.X, t.Z)) result.Add(t);
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
