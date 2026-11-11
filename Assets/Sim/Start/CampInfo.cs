using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Start
{
    /// <summary>The placed camp (CMP-03): its footprint id and the fixed points in world coordinates.</summary>
    public sealed class CampInfo
    {
        /// <summary>Creates the info.</summary>
        public CampInfo(int footprintId, PointMm fire, PointMm stockpile, PointMm oxStand)
        {
            FootprintId = footprintId; Fire = fire; Stockpile = stockpile; OxStand = oxStand;
        }

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
