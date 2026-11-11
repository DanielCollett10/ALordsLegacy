using System.Collections.Generic;

namespace LordsLegacy.Sim.Operations
{
    /// <summary>
    /// One production operation, e.g. a woodcutter (woodcutting.md). Holds only ids and flags; its status is computed
    /// by <see cref="OperationStatus"/>, never stored. Changes go through <see cref="OperationRegistry"/>.
    /// </summary>
    public sealed class Operation
    {
        internal readonly List<int> ModuleIdList = new List<int>();
        internal readonly List<int> FamilyIdList = new List<int>();
        internal readonly List<int> OxIdList = new List<int>();

        /// <summary>Unique id within the registry.</summary>
        public int Id { get; }
        /// <summary>Operation type id, e.g. <see cref="OperationTypes.WoodcuttingId"/>.</summary>
        public string TypeId { get; }
        /// <summary>Id of the building area the operation lives in.</summary>
        public int AreaId { get; }
        /// <summary>Footprint ids of the modules placed inside the area, in the order added.</summary>
        public IReadOnlyList<int> ModuleIds => ModuleIdList;
        /// <summary>Ids of the assigned families, in the order assigned.</summary>
        public IReadOnlyList<int> FamilyIds => FamilyIdList;
        /// <summary>Ids of the assigned oxen (always empty in M8).</summary>
        public IReadOnlyList<int> OxIds => OxIdList;
        /// <summary>Id of the work area, or null for the default circle around the hut (WC-11).</summary>
        public int? WorkAreaId { get; internal set; }
        /// <summary>True when the player paused the operation.</summary>
        public bool Paused { get; internal set; }

        internal Operation(int id, string typeId, int areaId)
        {
            Id = id; TypeId = typeId; AreaId = areaId;
        }
    }
}
