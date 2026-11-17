using System.Collections.Generic;

namespace LordsLegacy.Sim.Construction
{
    /// <summary>Stage of an unbuilt footprint (CN-03). Built is "no record".</summary>
    public enum ConstructionState { Delivering = 0, Building = 1 }

    /// <summary>Build priority of a site.</summary>
    public enum ConstructionPriority { Normal = 0, High = 1 }

    /// <summary>One unbuilt footprint (decision 0033).</summary>
    public sealed class ConstructionSite
    {
        /// <summary>The footprint being built.</summary>
        public int FootprintId { get; }
        /// <summary>Delivering or Building.</summary>
        public ConstructionState State { get; set; }
        /// <summary>Ledger reservation ids held for this site's materials.</summary>
        public List<int> ReservationIds { get; } = new List<int>();
        /// <summary>Id of the ledger location of kind Site holding the delivered materials.</summary>
        public int LedgerLocationId { get; set; }
        /// <summary>Build progress, in worker-minutes.</summary>
        public int ProgressMinutes { get; set; }
        /// <summary>Units of each item (id to count) already used up by the build work (CN-23b); delivered, but no longer on site.</summary>
        public SortedDictionary<string, int> Consumed { get; } = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
        /// <summary>Logs already used up by the build work; a view of <see cref="Consumed"/> for the log item.</summary>
        public int LogsConsumed { get => ConsumedOf(Inventory.Items.Log.Id); set => SetConsumed(Inventory.Items.Log.Id, value); }
        /// <summary>Units of the item already used up by the build work.</summary>
        public int ConsumedOf(string itemId) => Consumed.TryGetValue(itemId, out int n) ? n : 0;
        /// <summary>Sets the consumed count of an item; zero removes the entry.</summary>
        public void SetConsumed(string itemId, int count) { if (count > 0) Consumed[itemId] = count; else Consumed.Remove(itemId); }
        /// <summary>Normal or High.</summary>
        public ConstructionPriority Priority { get; set; }

        /// <summary>Creates a site in the Delivering state.</summary>
        public ConstructionSite(int footprintId, int ledgerLocationId, ConstructionPriority priority = ConstructionPriority.Normal)
        {
            FootprintId = footprintId; LedgerLocationId = ledgerLocationId; Priority = priority;
        }
    }
}
