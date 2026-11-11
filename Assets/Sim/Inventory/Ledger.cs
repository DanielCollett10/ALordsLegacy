using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Inventory
{
    /// <summary>
    /// The inventory ledger (spec section 7, part 1): exact, located records of items. Items enter only through
    /// <see cref="Produce"/>; <see cref="Audit"/> proves nothing changed any other way (LED-09).
    /// </summary>
    public sealed class Ledger
    {
        /// <summary>Ground piles with the same owner within this distance (mm) merge (1 m).</summary>
        public const long PileMergeRadiusMm = 1000;

        private readonly SortedDictionary<int, LedgerLocation> _locations = new SortedDictionary<int, LedgerLocation>();
        private readonly SortedDictionary<string, int> _produced = new SortedDictionary<string, int>(StringComparer.Ordinal);
        private int _nextId = 1;

        /// <summary>Bumps on every change to the ledger.</summary>
        public int Version { get; private set; }

        /// <summary>All locations in id order.</summary>
        public IEnumerable<LedgerLocation> Locations => _locations.Values;

        /// <summary>Finds a location by id, or null.</summary>
        public LedgerLocation Get(int id) => _locations.TryGetValue(id, out var l) ? l : null;

        /// <summary>Returns the storage location bound to a module, creating it if needed.</summary>
        public LedgerLocation GetOrAddStorage(int moduleId)
        {
            foreach (var l in _locations.Values)
                if (l.Kind == LocationKind.Storage && l.ModuleId == moduleId) return l;
            return Add(new LedgerLocation(_nextId++, LocationKind.Storage, default, null, moduleId));
        }

        /// <summary>
        /// Returns the ground pile for a point: the nearest existing pile with the same owner within 1 m
        /// (ties to the lowest id), otherwise a new one.
        /// </summary>
        public LedgerLocation GetOrAddGroundPile(PointMm position, int? ownerOperationId)
        {
            LedgerLocation best = null;
            double bestD = double.MaxValue;
            foreach (var l in _locations.Values)
            {
                if (l.Kind != LocationKind.GroundPile || l.OwnerOperationId != ownerOperationId) continue;
                double d = l.Position.DistanceTo(position);
                if (d <= PileMergeRadiusMm && d < bestD) { best = l; bestD = d; }
            }
            return best ?? Add(new LedgerLocation(_nextId++, LocationKind.GroundPile, position, ownerOperationId, 0));
        }

        /// <summary>Creates <paramref name="n"/> items of <paramref name="item"/> at a location.</summary>
        public void Produce(LedgerLocation location, ItemDef item, int n)
        {
            if (location == null) throw new ArgumentNullException(nameof(location));
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n), "Must be positive.");
            if (Get(location.Id) != location) throw new ArgumentException("Location is not in this ledger.", nameof(location));
            location.Counts.TryGetValue(item.Id, out int have);
            location.Counts[item.Id] = checked(have + n);
            _produced.TryGetValue(item.Id, out int made);
            _produced[item.Id] = checked(made + n);
            Version++;
        }

        /// <summary>Produces items on the ground, merging into a nearby same-owner pile. Returns the pile.</summary>
        public LedgerLocation ProduceOnGround(PointMm position, int? ownerOperationId, ItemDef item, int n)
        {
            var pile = GetOrAddGroundPile(position, ownerOperationId);
            Produce(pile, item, n);
            return pile;
        }

        /// <summary>Total of one item across every location.</summary>
        public int TotalOf(ItemDef item) => TotalOf(item.Id);

        /// <summary>Total of one item id across every location.</summary>
        public int TotalOf(string itemId)
        {
            int t = 0;
            foreach (var l in _locations.Values) t += l.CountOf(itemId);
            return t;
        }

        /// <summary>
        /// Debug check (LED-09): for every item, the sum over locations equals what operations produced, and no
        /// location holds a non-positive count. Returns null when consistent, else a description of the first fault.
        /// </summary>
        public string Audit()
        {
            var seen = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var l in _locations.Values)
                foreach (var kv in l.Counts)
                {
                    if (kv.Value <= 0) return "Location " + l.Id + " holds " + kv.Value + " of " + kv.Key;
                    seen.TryGetValue(kv.Key, out int s);
                    seen[kv.Key] = s + kv.Value;
                }
            foreach (var kv in _produced)
            {
                seen.TryGetValue(kv.Key, out int s);
                if (s != kv.Value) return kv.Key + ": locations hold " + s + " but operations produced " + kv.Value;
            }
            foreach (var kv in seen)
                if (!_produced.ContainsKey(kv.Key)) return kv.Key + ": locations hold " + kv.Value + " but none produced";
            return null;
        }

        private LedgerLocation Add(LedgerLocation l)
        {
            _locations.Add(l.Id, l);
            Version++;
            return l;
        }
    }
}
