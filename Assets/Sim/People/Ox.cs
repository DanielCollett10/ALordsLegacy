namespace LordsLegacy.Sim.People
{
    /// <summary>One ox (PPL-13): a record with a home stand point, position, facing and activity.</summary>
    public sealed class Ox : IMovable
    {
        /// <summary>Creates an ox record. Use <see cref="Population.AddOx"/>.</summary>
        public Ox(int id, string name, double homeX, double homeZ, double facingRad = 0,
            Activity activity = Activity.Waiting)
        {
            Id = id;
            Name = name ?? string.Empty;
            HomeX = homeX;
            HomeZ = homeZ;
            X = homeX;
            Z = homeZ;
            PreviousX = homeX;
            PreviousZ = homeZ;
            FacingRad = facingRad;
            Activity = activity;
        }

        /// <summary>Unique id; never reused.</summary>
        public int Id { get; }

        /// <summary>Name.</summary>
        public string Name { get; }

        /// <summary>Home stand point x in metres.</summary>
        public double HomeX { get; }

        /// <summary>Home stand point z in metres.</summary>
        public double HomeZ { get; }

        /// <summary>Position x in metres.</summary>
        public double X { get; set; }

        /// <summary>Position z in metres.</summary>
        public double Z { get; set; }

        /// <summary>Facing in radians.</summary>
        public double FacingRad { get; set; }

        /// <summary>Current activity.</summary>
        public Activity Activity { get; set; }

        /// <summary>Pool state (Free, Reserved, Led, Resting). Changed through <c>OxPool</c>.</summary>
        public OxState State { get; set; } = OxState.Free;

        /// <inheritdoc />
        public double PreviousX { get; set; }

        /// <inheritdoc />
        public double PreviousZ { get; set; }

        /// <inheritdoc />
        public double CurrentMultiplier { get; set; } = 1.0;

        /// <inheritdoc />
        public MoveOrder Move { get; set; }

        /// <inheritdoc />
        public FollowLink Follow { get; set; }

        /// <summary>Ledger id of this ox's carrier location (T-289); 0 until first needed.</summary>
        public int CarrierId { get; set; }
    }
}
