using LordsLegacy.Sim.Kit;

namespace LordsLegacy.Sim.Core
{
    /// <summary>The kit part of <see cref="SimWorld"/> (decision 0052).</summary>
    public sealed partial class SimWorld
    {
        /// <summary>Buildings claiming kit, oldest first (ST-24). Never null.</summary>
        public KitClaims KitClaims { get; } = new KitClaims();
    }
}
