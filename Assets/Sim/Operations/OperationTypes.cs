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

        /// <summary>Id of the foraging operation.</summary>
        public const string ForagingId = "operation.foraging";

        /// <summary>
        /// Foraging camp: foraging building area with a mandatory forager hut (which is also its food store) and nothing
        /// else, 2 family slots and a 300 m default work circle (FD-22, FD-23b).
        /// </summary>
        public static readonly OperationTypeDef Foraging = new OperationTypeDef(
            ForagingId,
            buildingAreaTypeId: AreaTypes.ForagingId,
            workAreaTypeId: AreaTypes.ForagingWorkId,
            hutTypeId: ModuleTypes.ForagerHutId,
            hutCount: 1,
            logStorageTypeId: null,
            minLogStorageCount: 0,
            oxenStandTypeId: null,
            maxOxenStands: 0,
            familySlots: 2,
            defaultWorkRadiusM: 300);

        /// <summary>Id of the granary operation.</summary>
        public const string GranaryId = "operation.granary";

        /// <summary>
        /// Granary: an operation without an area (AreaId 0) that owns the granary footprint as its hut. 2 family slots
        /// (PLACEHOLDER, FD-32), no storage, oxen or work area.
        /// </summary>
        public static readonly OperationTypeDef Granary = new OperationTypeDef(
            GranaryId,
            buildingAreaTypeId: null,
            workAreaTypeId: null,
            hutTypeId: ModuleTypes.GranaryId,
            hutCount: 1,
            logStorageTypeId: null,
            minLogStorageCount: 0,
            oxenStandTypeId: null,
            maxOxenStands: 0,
            familySlots: 2,
            defaultWorkRadiusM: 0);

        /// <summary>All operation types.</summary>
        public static readonly IReadOnlyList<OperationTypeDef> All = new[] { Woodcutting, Foraging, Granary };

        /// <summary>Finds the operation type drawn in this building area type, or null if none.</summary>
        public static OperationTypeDef FindByBuildingArea(string areaTypeId)
        {
            if (areaTypeId == null) return null;
            for (int i = 0; i < All.Count; i++) if (All[i].BuildingAreaTypeId == areaTypeId) return All[i];
            return null;
        }

        /// <summary>Finds an operation type by id, or null if unknown.</summary>
        public static OperationTypeDef Find(string id)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Id == id) return All[i];
            return null;
        }
    }
}
