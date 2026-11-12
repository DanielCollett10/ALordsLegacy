namespace LordsLegacy.Sim.People
{
    /// <summary>What <c>MovementSystem</c> needs from anything that walks: a citizen or an ox.</summary>
    public interface IMovable
    {
        /// <summary>Position x in metres.</summary>
        double X { get; set; }

        /// <summary>Position z in metres.</summary>
        double Z { get; set; }

        /// <summary>Facing in radians (atan2(dx, dz) of the last step).</summary>
        double FacingRad { get; set; }

        /// <summary>X at the start of the latest tick, for Unity interpolation (CLK-02).</summary>
        double PreviousX { get; set; }

        /// <summary>Z at the start of the latest tick, for Unity interpolation (CLK-02).</summary>
        double PreviousZ { get; set; }

        /// <summary>Speed multiplier of the ground under the agent in the latest tick; drives stride speed (MOV-09).</summary>
        double CurrentMultiplier { get; set; }

        /// <summary>The walk order, or null.</summary>
        MoveOrder Move { get; set; }

        /// <summary>Set while this agent follows a leader (MOV-16); null otherwise. Use <c>MovementSystem.Attach</c>.</summary>
        FollowLink Follow { get; set; }
    }

    /// <summary>A follower's tie to its leader: placed <see cref="OffsetM"/> behind the leader's facing (MOV-16).</summary>
    public sealed class FollowLink
    {
        /// <summary>Creates a link.</summary>
        public FollowLink(IMovable leader, double offsetM) { Leader = leader; OffsetM = offsetM; }

        /// <summary>The agent being followed.</summary>
        public IMovable Leader { get; }

        /// <summary>Distance behind the leader in metres.</summary>
        public double OffsetM { get; }
    }
}
