using System.Collections.Generic;

namespace LordsLegacy.Sim.Footprints
{
    /// <summary>
    /// The module footprint types of the woodcutting operation (woodcutting.md WC-02, WC-05, WC-06), built in code
    /// like <see cref="FootprintTypeDef.Camp"/> until the definition pipeline exists (decision 0019).
    /// </summary>
    public static class ModuleTypes
    {
        /// <summary>Id of the woodcutting hut module.</summary>
        public const string WoodcuttingHutId = "module.woodcutting_hut";
        /// <summary>Id of the log storage module.</summary>
        public const string LogStorageId = "module.log_storage";
        /// <summary>Id of the oxen stand module.</summary>
        public const string OxenStandId = "module.oxen_stand";

        /// <summary>Woodcutting hut: 6 × 4 m, blocks movement, clears trees (WC-02, WC-06, WC-08).</summary>
        public static readonly FootprintTypeDef WoodcuttingHut =
            new FootprintTypeDef(WoodcuttingHutId, 6000, 4000, true, true);

        /// <summary>Log storage: 8 × 6 m, walkable, clears trees, holds 20 logs (WC-02, WC-06, INV-A3).</summary>
        public static readonly FootprintTypeDef LogStorage =
            new FootprintTypeDef(LogStorageId, 8000, 6000, false, true, logCapacity: 20);

        /// <summary>Oxen stand: 3 × 3 m, walkable, clears trees, holds 1 ox (WC-02, WC-05, WC-06).</summary>
        public static readonly FootprintTypeDef OxenStand =
            new FootprintTypeDef(OxenStandId, 3000, 3000, false, true, oxCapacity: 1);

        /// <summary>All woodcutting module types.</summary>
        public static readonly IReadOnlyList<FootprintTypeDef> All = new[] { WoodcuttingHut, LogStorage, OxenStand };

        /// <summary>Finds a module type by id, or null if unknown.</summary>
        public static FootprintTypeDef Find(string id)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Id == id) return All[i];
            return null;
        }
    }
}
