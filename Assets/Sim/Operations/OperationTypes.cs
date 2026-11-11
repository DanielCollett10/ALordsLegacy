using System.Collections.Generic;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Footprints;

namespace LordsLegacy.Sim.Operations
{
    /// <summary>The operation type definitions, built in code until the definition pipeline exists (decision 0019).</summary>
    public static class OperationTypes
    {
        /// <summary>Id of the woodcutting operation.</summary>
        public const string WoodcuttingId = "operation.woodcutting";

        /// <summary>
        /// Woodcutting: woodcutting building area with a mandatory hut, at least one log storage, an optional oxen
        /// stand, 2 family slots and a 420 m default work circle (woodcutting.md WC-01..WC-11).
        /// </summary>
        public static readonly OperationTypeDef Woodcutting = new OperationTypeDef(
            WoodcuttingId,
            buildingAreaTypeId: AreaTypes.WoodcuttingId,
            workAreaTypeId: AreaTypes.WoodcuttingWorkId,
            hutTypeId: ModuleTypes.WoodcuttingHutId,
            hutCount: 1,
            logStorageTypeId: ModuleTypes.LogStorageId,
            minLogStorageCount: 1,
            oxenStandTypeId: ModuleTypes.OxenStandId,
            maxOxenStands: 1,
            familySlots: 2,
            defaultWorkRadiusM: 420);

        /// <summary>All operation types.</summary>
        public static readonly IReadOnlyList<OperationTypeDef> All = new[] { Woodcutting };

        /// <summary>Finds an operation type by id, or null if unknown.</summary>
        public static OperationTypeDef Find(string id)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Id == id) return All[i];
            return null;
        }
    }
}
