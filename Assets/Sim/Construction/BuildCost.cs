using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;

namespace LordsLegacy.Sim.Construction
{
    /// <summary>
    /// Pure cost queries for the building tool (building-tool.md BT-01..03, decision 0037). They read module recipes
    /// and <see cref="ConstructionSources"/> and change nothing. Costs are in logs.
    /// </summary>
    public static class BuildCost
    {
        /// <summary>
        /// Minimum logs to start an operation: the recipes of its mandatory modules at their minimum count
        /// (hut × HutCount, log storage × MinLogStorageCount). The oxen stand is optional and not counted.
        /// Woodcutting: hut 8 + one storage 0 = 8 (BT-01).
        /// </summary>
        /// <exception cref="ArgumentNullException">The operation type is null.</exception>
        /// <exception cref="ArgumentException">A mandatory module type id is unknown.</exception>
        public static int MinimumFor(OperationTypeDef type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            int total = type.HutCount * LogsOf(type.HutTypeId);
            if (type.LogStorageTypeId != null) total += type.MinLogStorageCount * LogsOf(type.LogStorageTypeId);
            return total;
        }

        /// <summary>
        /// Total logs in the recipes of the given module type ids, counted once per entry. An empty list costs 0.
        /// </summary>
        /// <exception cref="ArgumentNullException">The list is null, or holds a null id.</exception>
        /// <exception cref="ArgumentException">An id is not a known module type.</exception>
        public static int Of(IEnumerable<string> moduleTypeIds)
        {
            if (moduleTypeIds == null) throw new ArgumentNullException(nameof(moduleTypeIds));
            int total = 0;
            foreach (string id in moduleTypeIds)
            {
                if (id == null) throw new ArgumentNullException(nameof(moduleTypeIds), "Null module type id");
                total += LogsOf(id);
            }
            return total;
        }

        /// <summary>
        /// True when available logs (CN-11: total − reserved over the construction sources, site-owned piles
        /// excluded) cover <paramref name="logs"/>. Zero or less is always affordable.
        /// </summary>
        /// <exception cref="ArgumentNullException">The world is null.</exception>
        public static bool CanAfford(SimWorld world, int logs)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (logs <= 0) return true;
            return ConstructionSources.Available(world, Items.Log) >= logs;
        }

        private static int LogsOf(string moduleTypeId)
        {
            FootprintTypeDef type = ModuleTypes.Find(moduleTypeId);
            if (type == null) throw new ArgumentException("Unknown module type " + moduleTypeId, nameof(moduleTypeId));
            return type.ConstructionRecipe.Logs;
        }
    }
}
