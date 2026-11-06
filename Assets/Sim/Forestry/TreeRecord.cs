using LordsLegacy.Sim.Tuning;

namespace LordsLegacy.Sim.Forestry
{
    /// <summary>Lifecycle state of a tree.</summary>
    public enum TreeState
    {
        /// <summary>Upright and unfelled.</summary>
        Standing = 0
    }

    /// <summary>One tree: stable id, ground position and usable trunk length.</summary>
    public sealed class TreeRecord
    {
        /// <summary>Stable id from the authoring data.</summary>
        public TreeId Id { get; }
        /// <summary>World x in metres.</summary>
        public float X { get; }
        /// <summary>World z in metres.</summary>
        public float Z { get; }
        /// <summary>Usable trunk length in metres.</summary>
        public double TrunkLengthM { get; }
        /// <summary>Current state.</summary>
        public TreeState State { get; set; } = TreeState.Standing;

        /// <summary>Creates a standing tree.</summary>
        public TreeRecord(TreeId id, float x, float z, double trunkLengthM)
        {
            Id = id; X = x; Z = z; TrunkLengthM = trunkLengthM;
        }

        /// <summary>Whole standard logs this tree yields: floor(trunk length / StandardLogLengthM).</summary>
        public int LogYield(SimTuning tuning) => TreeRegistry.LogYield(TrunkLengthM, tuning);
    }
}
