using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Inventory
{
    /// <summary>
    /// The inventory ledger (spec section 7, part 1): exact, located records of items. Items enter only through
    /// <see cref="Produce"/>; <see cref="Audit"/> proves nothing changed any other way (LED-09).
    /// </summary>
    public sealed partial class Ledger
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

        /// <summary>Finds the storage location bound to a module, or null (never creates).</summary>
        public LedgerLocation FindStorage(int moduleId)
        {
            foreach (var l in _locations.Values)
                if (l.Kind == LocationKind.Storage && l.ModuleId == moduleId) return l;
            return null;
        }

        /// <summary>Returns the storage location bound to a module, creating it if needed.</summary>
        public LedgerLocation GetOrAddStorage(int moduleId) => GetOrAddStorage(moduleId, int.MaxValue);

        /// <summary>
        /// Returns the storage location bound to a module, creating it if needed. Capacity comes from the module
        /// type (<see cref="Footprints.FootprintTypeDef.LogCapacity"/>) and is refreshed on every call.
        /// </summary>
        public LedgerLocation GetOrAddStorage(int moduleId, Footprints.FootprintTypeDef type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return GetOrAddStorage(moduleId, type.LogCapacity);
        }

        /// <summary>Returns the storage location bound to a module with an explicit capacity.</summary>
        public LedgerLocation GetOrAddStorage(int moduleId, int capacity)
        {
            foreach (var l in _locations.Values)
                if (l.Kind == LocationKind.Storage && l.ModuleId == moduleId)
                {
                    if (l.Capacity != capacity && capacity != int.MaxValue) l.Capacity = capacity;
                    return l;
                }
            return Add(new LedgerLocation(_nextId++, LocationKind.Storage, default, null, moduleId, capacity));
        }

        /// <summary>Adds a carrier (an ox) that can hold up to <paramref name="capacity"/> items.</summary>
        public LedgerLocation AddCarrier(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Must be positive.");
            return Add(new LedgerLocation(_nextId++, LocationKind.Carrier, default, null, 0, capacity));
        }

        /// <summary>Finds the Site location of a footprint, or null (never creates).</summary>
        public LedgerLocation FindSite(int footprintId)
        {
            foreach (var l in _locations.Values)
                if (l.Kind == LocationKind.Site && l.ModuleId == footprintId) return l;
            return null;
        }

        /// <summary>Returns the Site location of a footprint (unlimited capacity), creating it if needed.</summary>
        public LedgerLocation GetOrAddSite(int footprintId) =>
            FindSite(footprintId) ?? Add(new LedgerLocation(_nextId++, LocationKind.Site, default, null, footprintId));

        /// <summary>Adds a stockpile with no position and no capacity limit (INV-10).</summary>
        public LedgerLocation AddStockpile() => AddStockpile(default);

        /// <summary>Adds a stockpile at a point (the camp stockpile spot, CON-08), with no capacity limit (INV-10).</summary>
        public LedgerLocation AddStockpile(PointMm position) =>
            Add(new LedgerLocation(_nextId++, LocationKind.Stockpile, position, null, 0));

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
            if (location.Capacity != int.MaxValue && (long)location.Total + n > location.Capacity)
                throw new InvalidOperationException("Location " + location.Id + " would exceed its capacity of " + location.Capacity + ".");
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
            string fault = AuditReservations();
            if (fault != null) return fault;
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
