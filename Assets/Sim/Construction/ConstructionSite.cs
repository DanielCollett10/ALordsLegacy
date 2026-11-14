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
        /// <summary>Normal or High.</summary>
        public ConstructionPriority Priority { get; set; }

        /// <summary>Creates a site in the Delivering state.</summary>
        public ConstructionSite(int footprintId, int ledgerLocationId, ConstructionPriority priority = ConstructionPriority.Normal)
        {
            FootprintId = footprintId; LedgerLocationId = ledgerLocationId; Priority = priority;
        }
    }
}
