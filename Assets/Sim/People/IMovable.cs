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
    }
}
