using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;

namespace LordsLegacy.Sim.Start
{
    /// <summary>The placed camp (CMP-03): its footprint id, the fixed points in world coordinates and the stockpile.</summary>
    public sealed class CampInfo
    {
        /// <summary>Creates the info.</summary>
        public CampInfo(int footprintId, PointMm fire, PointMm stockpile, PointMm oxStand, LedgerLocation stockpileLocation = null)
        {
            FootprintId = footprintId; Fire = fire; Stockpile = stockpile; OxStand = oxStand;
            StockpileLocation = stockpileLocation;
        }

        /// <summary>
        /// The camp stockpile in the ledger (CON-08, INV-10): a <see cref="LocationKind.Stockpile"/> holding the
        /// starting stock. Not a storage module, so it is never part of an operation's pool (WC-37).
        /// </summary>
        public LedgerLocation StockpileLocation { get; }

        /// <summary>Id of the camp footprint in <c>SimWorld.Footprints</c>.</summary>
        public int FootprintId { get; }
        /// <summary>Camp fire, world mm.</summary>
        public PointMm Fire { get; }
        /// <summary>Stockpile spot, world mm.</summary>
        public PointMm Stockpile { get; }
        /// <summary>Ox stand point, world mm.</summary>
        public PointMm OxStand { get; }
    }
}
