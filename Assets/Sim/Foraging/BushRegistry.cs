using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Tuning;

namespace LordsLegacy.Sim.Foraging
{
    /// <summary>All forage bushes by id (FD-20, FD-21). Reservable per forager like trees (TMB-08).</summary>
    public sealed class BushRegistry
    {
        private const int TicksPerDay = 1440;
        private readonly SortedDictionary<int, BushRecord> _byId = new SortedDictionary<int, BushRecord>();
        private int _nextId = 1;

        /// <summary>Number of bushes.</summary>
        public int Count => _byId.Count;

        /// <summary>Next id to hand out (for the save).</summary>
        public int NextIdValue => _nextId;

        /// <summary>Increases on every change except reservations. Views rebuild when it moves.</summary>
        public int Version { get; private set; }

        /// <summary>All bushes in ascending id order.</summary>
        public IEnumerable<BushRecord> All => _byId.Values;

        /// <summary>Adds a bush with a new id and the given baskets.</summary>
        public BushRecord Add(float x, float z, BushKind kind, int baskets)
        {
            if (baskets < 0) throw new ArgumentOutOfRangeException(nameof(baskets));
            var b = new BushRecord(_nextId++, x, z, kind, baskets);
            _byId.Add(b.Id, b);
            Version++;
            return b;
        }

        /// <summary>Looks up a bush by id.</summary>
        public bool TryGet(int id, out BushRecord bush) => _byId.TryGetValue(id, out bush);

        /// <summary>Reserves a bush with baskets for <paramref name="ownerId"/>. False if unknown, empty or reserved by another.</summary>
        public bool Reserve(int id, int ownerId)
        {
            if (!_byId.TryGetValue(id, out var b) || b.Baskets <= 0) return false;
            if (b.ReservedBy.HasValue) return b.ReservedBy.Value == ownerId;
            b.ReservedBy = ownerId;
            return true;
        }

        /// <summary>Clears a reservation. False if the bush is unknown or not reserved.</summary>
        public bool Release(int id)
        {
            if (!_byId.TryGetValue(id, out var b) || !b.ReservedBy.HasValue) return false;
            b.ReservedBy = null;
            return true;
        }

        /// <summary>
        /// Takes one basket. Only the reserving owner may pick. Taking the last basket starts the regrow clock at
        /// <paramref name="tick"/> and clears the reservation. Returns false (no change) if not allowed.
        /// </summary>
        public bool TakeBasket(int id, int ownerId, long tick)
        {
            if (!_byId.TryGetValue(id, out var b) || b.Baskets <= 0 || b.ReservedBy != ownerId) return false;
            b.Baskets--;
            if (b.Baskets == 0) { b.EmptiedAtTick = tick; b.ReservedBy = null; }
            Version++;
            return true;
        }

        /// <summary>The nearest bush with baskets that nobody has reserved, or null. Ties go to the lowest id.</summary>
        public BushRecord NearestAvailable(float x, float z, params BushKind[] kinds)
        {
            BushRecord best = null;
            double bestD = double.MaxValue;
            foreach (var b in _byId.Values)
            {
                if (b.Baskets <= 0 || b.ReservedBy.HasValue || Array.IndexOf(kinds, b.Kind) < 0) continue;
                double dx = b.X - x, dz = b.Z - z, d = dx * dx + dz * dz;
                if (d < bestD) { best = b; bestD = d; }
            }
            return best;
        }

        /// <summary>True when any bush with baskets left (reserved or not) lies inside the region.</summary>
        public bool AnyWithBasketsIn(Forestry.TreeRegion region, params BushKind[] kinds)
        {
            foreach (var b in _byId.Values)
                if (b.Baskets > 0 && Array.IndexOf(kinds, b.Kind) >= 0 && region.Contains(b.X, b.Z)) return true;
            return false;
        }

        /// <summary>The nearest unreserved bush with baskets inside the region, or null. Ties go to the lowest id.</summary>
        public BushRecord NearestAvailable(float x, float z, Forestry.TreeRegion region, params BushKind[] kinds)
        {
            BushRecord best = null;
            double bestD = double.MaxValue;
            foreach (var b in _byId.Values)
            {
                if (b.Baskets <= 0 || b.ReservedBy.HasValue || Array.IndexOf(kinds, b.Kind) < 0 || !region.Contains(b.X, b.Z)) continue;
                double dx = b.X - x, dz = b.Z - z, d = dx * dx + dz * dz;
                if (d < bestD) { best = b; bestD = d; }
            }
            return best;
        }

        /// <summary>Refills every emptied bush whose regrow time has passed (FD-21). Returns how many regrew.</summary>
        public int Regrow(long tick, SimTuning tuning)
        {
            int n = 0;
            foreach (var b in _byId.Values)
            {
                long wait = (long)BushKinds.RegrowDays(b.Kind, tuning) * TicksPerDay;
                if (b.Baskets > 0 || tick - b.EmptiedAtTick < wait) continue;
                b.Baskets = BushKinds.FullUnits(b.Kind, tuning);
                n++;
            }
            if (n > 0) Version++;
            return n;
        }

        /// <summary>Restore: removes everything (a fresh scatter is replaced by the saved bushes).</summary>
        internal void RestoreClear() { _byId.Clear(); _nextId = 1; Version++; }

        /// <summary>Restore: adds a bush with its saved id and state.</summary>
        internal void RestoreBush(int id, float x, float z, BushKind kind, int baskets, long emptiedAt, int? reservedBy)
        {
            if (_byId.ContainsKey(id)) throw new InvalidOperationException("Duplicate bush id " + id);
            _byId.Add(id, new BushRecord(id, x, z, kind, baskets) { EmptiedAtTick = emptiedAt, ReservedBy = reservedBy });
            Version++;
        }

        /// <summary>Restore: sets the id counter.</summary>
        internal void RestoreNextId(int next) { _nextId = next; }
    }
}
