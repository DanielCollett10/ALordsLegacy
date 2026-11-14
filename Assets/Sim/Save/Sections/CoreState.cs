using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;

namespace LordsLegacy.Sim.Save.Sections
{
    /// <summary>
    /// The world's core settings as a standalone record (decision 0034): the clock, pause flag, random state,
    /// the tuning and the type catalogs the world was created with. Needs no SimWorld.
    /// </summary>
    public sealed class CoreState
    {
        /// <summary>Game tick (game minutes since start).</summary>
        public long Tick { get; set; }
        /// <summary>True if the clock is paused.</summary>
        public bool Paused { get; set; }
        /// <summary>The <c>SimRandom.State</c> value.</summary>
        public ulong RandomState { get; set; }
        /// <summary>The tuning the world was created with. On load this wins over any tuning passed in (SV-14).</summary>
        public SimTuning Tuning { get; set; }
        /// <summary>Road types.</summary>
        public RoadTypeCatalog RoadTypes { get; set; }
        /// <summary>Area types.</summary>
        public AreaTypeCatalog AreaTypes { get; set; }
    }
}
