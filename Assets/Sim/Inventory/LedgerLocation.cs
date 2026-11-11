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
        /// <summary>Storage: the module id it is bound to. Ground piles: 0.</summary>
        public int ModuleId { get; }

        internal LedgerLocation(int id, LocationKind kind, PointMm position, int? owner, int moduleId)
        {
            Id = id; Kind = kind; Position = position; OwnerOperationId = owner; ModuleId = moduleId;
        }

        /// <summary>Total number of items of any kind here.</summary>
        public int Total { get { int t = 0; foreach (var kv in Counts) t += kv.Value; return t; } }

        /// <summary>Count of one item here.</summary>
        public int CountOf(string itemId) => Counts.TryGetValue(itemId, out int n) ? n : 0;
    }
}
