namespace LordsLegacy.Sim.Operations
{
    /// <summary>
    /// Definition of one kind of production operation (woodcutting.md, decision 0019). Immutable.
    /// It names the area types and module types the operation uses; it holds no rules of its own.
    /// </summary>
    public sealed class OperationTypeDef
    {
        /// <summary>Stable id, e.g. <c>operation.woodcutting</c>.</summary>
        public string Id { get; }
        /// <summary>Id of the building area type the operation is drawn in (WC-01).</summary>
        public string BuildingAreaTypeId { get; }
        /// <summary>Id of the work area type it harvests in (WC-10..12).</summary>
        public string WorkAreaTypeId { get; }
        /// <summary>Id of the mandatory hut module type (WC-05).</summary>
        public string HutTypeId { get; }
        /// <summary>Number of huts the operation needs: always 1 (WC-05).</summary>
        public int HutCount { get; }
        /// <summary>Id of the log storage module type (WC-05).</summary>
        public string LogStorageTypeId { get; }
        /// <summary>Fewest log storage modules the operation needs: at least 1 (WC-05).</summary>
        public int MinLogStorageCount { get; }
        /// <summary>Id of the optional oxen stand module type, or null if the operation has none (WC-05).</summary>
        public string OxenStandTypeId { get; }
        /// <summary>Most oxen stands the operation may hold: 1 (WC-05, PROPOSED).</summary>
        public int MaxOxenStands { get; }
        /// <summary>Number of family slots (WCO, tuning.md: 2 per hut).</summary>
        public int FamilySlots { get; }
        /// <summary>Radius in metres of the default work circle around the hut (WC-11).</summary>
        public double DefaultWorkRadiusM { get; }

        /// <summary>Creates an operation type definition.</summary>
        public OperationTypeDef(string id, string buildingAreaTypeId, string workAreaTypeId, string hutTypeId,
            int hutCount, string logStorageTypeId, int minLogStorageCount, string oxenStandTypeId, int maxOxenStands,
            int familySlots, double defaultWorkRadiusM)
        {
            Id = id;
            BuildingAreaTypeId = buildingAreaTypeId;
            WorkAreaTypeId = workAreaTypeId;
            HutTypeId = hutTypeId;
            HutCount = hutCount;
            LogStorageTypeId = logStorageTypeId;
            MinLogStorageCount = minLogStorageCount;
            OxenStandTypeId = oxenStandTypeId;
            MaxOxenStands = maxOxenStands;
            FamilySlots = familySlots;
            DefaultWorkRadiusM = defaultWorkRadiusM;
        }
    }
}
