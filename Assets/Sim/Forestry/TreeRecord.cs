using LordsLegacy.Sim.Tuning;

namespace LordsLegacy.Sim.Forestry
{
    /// <summary>Lifecycle state of a tree.</summary>
    public enum TreeState
    {
        /// <summary>Upright and unfelled.</summary>
        Standing = 0,
        /// <summary>Cut down; the trunk lies on the ground waiting to be cut into logs.</summary>
        Felled = 1,
        /// <summary>Only the stump is left.</summary>
        Stump = 2
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
        public TreeState State { get; internal set; } = TreeState.Standing;
        /// <summary>Owner (e.g. worker id) that reserved this tree, or null when unreserved.</summary>
        public int? ReservedBy { get; internal set; }

        /// <summary>Creates a standing tree.</summary>
        public TreeRecord(TreeId id, float x, float z, double trunkLengthM)
        {
            Id = id; X = x; Z = z; TrunkLengthM = trunkLengthM;
        }

        /// <summary>Whole standard logs this tree yields: floor(trunk length / StandardLogLengthM).</summary>
        public int LogYield(SimTuning tuning) => TreeRegistry.LogYield(TrunkLengthM, tuning);
    }
}
