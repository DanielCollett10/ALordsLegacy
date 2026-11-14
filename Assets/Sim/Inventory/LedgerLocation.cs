using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Inventory
{
    /// <summary>Kind of place items can be held.</summary>
    public enum LocationKind
    {
        /// <summary>A pile on the ground at a point.</summary>
        GroundPile = 0,
        /// <summary>A storage module.</summary>
        Storage = 1,
        /// <summary>An ox carrying items (T-286).</summary>
        Carrier = 2,
        /// <summary>An unlimited store (INV-10).</summary>
        Stockpile = 3,
        /// <summary>Materials delivered to a construction site; owner is the footprint id (decision 0032/0033).</summary>
        Site = 4,
    }

    /// <summary>One place that holds items. Counts are owned by the <see cref="Ledger"/>.</summary>
    public sealed class LedgerLocation
    {
        internal readonly SortedDictionary<string, int> Counts = new SortedDictionary<string, int>(System.StringComparer.Ordinal);

        /// <summary>Ledger-unique id (starts at 1).</summary>
        public int Id { get; }
        /// <summary>Kind of location.</summary>
        public LocationKind Kind { get; }
        /// <summary>Ground piles: where the pile lies. Storage: default.</summary>
        public PointMm Position { get; }
        /// <summary>Ground piles: owning operation id, or null. Storage: null.</summary>
        public int? OwnerOperationId { get; }
        /// <summary>Storage: the module id it is bound to. Site: the footprint id. Ground piles: 0.</summary>
        public int ModuleId { get; }

        internal readonly SortedDictionary<string, int> ReservedCounts = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
        internal readonly SortedDictionary<string, int> IncomingCounts = new SortedDictionary<string, int>(System.StringComparer.Ordinal);

        /// <summary>Most items (all kinds together) held here; <see cref="int.MaxValue"/> means no limit.</summary>
        public int Capacity { get; internal set; }

        internal LedgerLocation(int id, LocationKind kind, PointMm position, int? owner, int moduleId, int capacity = int.MaxValue)
        {
            Id = id; Kind = kind; Position = position; OwnerOperationId = owner; ModuleId = moduleId; Capacity = capacity;
        }

        /// <summary>Items of one kind promised to leave (reserved stock).</summary>
        public int ReservedOf(string itemId) => ReservedCounts.TryGetValue(itemId, out int n) ? n : 0;

        /// <summary>Items of one kind promised to arrive (reserved capacity).</summary>
        public int IncomingOf(string itemId) => IncomingCounts.TryGetValue(itemId, out int n) ? n : 0;

        /// <summary>Available stock = total − reserved.</summary>
        public int AvailableOf(string itemId) => CountOf(itemId) - ReservedOf(itemId);

        /// <summary>Incoming items of every kind.</summary>
        public int IncomingTotal { get { int t = 0; foreach (var kv in IncomingCounts) t += kv.Value; return t; } }

        /// <summary>Room left for new items: capacity − total − incoming (never negative).</summary>
        public int FreeSpace
        {
            get
            {
                if (Capacity == int.MaxValue) return int.MaxValue;
                long f = (long)Capacity - Total - IncomingTotal;
                return f < 0 ? 0 : (int)f;
            }
        }

        /// <summary>Total number of items of any kind here.</summary>
        public int Total { get { int t = 0; foreach (var kv in Counts) t += kv.Value; return t; } }

        /// <summary>Count of one item here.</summary>
        public int CountOf(string itemId) => Counts.TryGetValue(itemId, out int n) ? n : 0;
    }
}
