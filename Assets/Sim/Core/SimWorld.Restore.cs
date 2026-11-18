using System;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Save.Sections;

namespace LordsLegacy.Sim.Core
{
    /// <summary>The restore-from-save part of <see cref="SimWorld"/> (T-360, decision 0034 "Restore path").</summary>
    public sealed partial class SimWorld
    {
        /// <summary>
        /// Builds a world for loading a save: terrain and original trees from <paramref name="map"/>, tuning and
        /// catalogs from <paramref name="core"/> (the saved tuning wins), plus the saved Tick, Paused flag and
        /// <see cref="SimRandom"/> state. No start layout is applied and no system is stepped. The caller then reads
        /// every save section in order and finishes with a full nav sync.
        /// </summary>
        internal static SimWorld ForRestore(MapData map, CoreState core)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (core == null) throw new ArgumentNullException(nameof(core));
            var world = FromMapData(map, core.Tuning, core.RoadTypes, core.AreaTypes);
            world.Tick = core.Tick;
            world.Paused = core.Paused;
            world.EntryPoint = core.EntryPoint;
            world.LastArrivalTick = core.LastArrivalTick;
            world.SettlementLog.Restore(core.SettlementEntries);
            world.CampStaysMessageSaid = core.CampStaysMessageSaid;
            world.Random.Restore(core.RandomState);
            return world;
        }
    }
}
