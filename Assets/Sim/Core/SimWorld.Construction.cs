using LordsLegacy.Sim.Construction;

namespace LordsLegacy.Sim.Core
{
    /// <summary>The construction part of <see cref="SimWorld"/> (T-340, decision 0033).</summary>
    public sealed partial class SimWorld
    {
        /// <summary>Construction sites of unbuilt footprints. Never null.</summary>
        public ConstructionRegistry Construction { get; } = new ConstructionRegistry();

        /// <summary>True when the footprint has no construction site record (so it is Built).</summary>
        public bool IsBuilt(int footprintId) => Construction.Get(footprintId) == null;

        /// <summary>Built modules marked for deconstruction (CN-52). Never null.</summary>
        public DeconstructionRegistry Deconstruction { get; } = new DeconstructionRegistry();

        /// <summary>True when built and not marked for deconstruction: it counts for capacity, ox room and the hut.</summary>
        public bool IsWorking(int footprintId) => IsBuilt(footprintId) && Deconstruction.Get(footprintId) == null;
    }
}
