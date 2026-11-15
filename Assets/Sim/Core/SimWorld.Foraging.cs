using LordsLegacy.Sim.Foraging;

namespace LordsLegacy.Sim.Core
{
    /// <summary>The forage bush part of <see cref="SimWorld"/> (T-421, FD-20..21).</summary>
    public sealed partial class SimWorld
    {
        /// <summary>All forage bushes. Never null; empty unless built from a map (scattered at new game) or loaded.</summary>
        public BushRegistry Bushes { get; } = new BushRegistry();
    }
}
