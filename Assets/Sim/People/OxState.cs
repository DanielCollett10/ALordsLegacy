namespace LordsLegacy.Sim.People
{
    /// <summary>The kind of <see cref="OxState"/> (WC-33).</summary>
    public enum OxStateKind
    {
        /// <summary>Not doing anything and not at a rest spot.</summary>
        Free,
        /// <summary>Held for a task that has not started yet.</summary>
        Reserved,
        /// <summary>Being led by a citizen.</summary>
        Led,
        /// <summary>Resting at a spot (a stand, beside a log storage, or the camp stand).</summary>
        Resting,
    }

    /// <summary>Immutable ox state: Free, Reserved(task), Led(by citizen) or Resting(spot).</summary>
    public sealed class OxState
    {
        /// <summary>Stand footprint id meaning "not at an oxen stand module".</summary>
        public const int NoStand = 0;

        /// <summary>The Free state.</summary>
        public static readonly OxState Free = new OxState(OxStateKind.Free, 0, 0, 0, 0, 0);

        /// <summary>The kind.</summary>
        public OxStateKind Kind { get; }
        /// <summary>Task id when Reserved; 0 otherwise.</summary>
        public int TaskId { get; }
        /// <summary>Citizen id when Led; 0 otherwise.</summary>
        public int HandlerId { get; }
        /// <summary>Rest spot x in mm when Resting.</summary>
        public long SpotXMm { get; }
        /// <summary>Rest spot z in mm when Resting.</summary>
        public long SpotZMm { get; }
        /// <summary>Footprint id of the oxen stand occupied while Resting, or <see cref="NoStand"/>.</summary>
        public int StandId { get; }

        private OxState(OxStateKind kind, int taskId, int handlerId, long x, long z, int standId)
        {
            Kind = kind; TaskId = taskId; HandlerId = handlerId; SpotXMm = x; SpotZMm = z; StandId = standId;
        }

        /// <summary>Reserved for a task.</summary>
        public static OxState Reserved(int taskId) => new OxState(OxStateKind.Reserved, taskId, 0, 0, 0, 0);

        /// <summary>Led by a citizen.</summary>
        public static OxState Led(int citizenId) => new OxState(OxStateKind.Led, 0, citizenId, 0, 0, 0);

        /// <summary>Resting at a spot; <paramref name="standId"/> is the stand footprint occupied, or 0.</summary>
        public static OxState Resting(long xMm, long zMm, int standId = NoStand) =>
            new OxState(OxStateKind.Resting, 0, 0, xMm, zMm, standId);
    }
}
