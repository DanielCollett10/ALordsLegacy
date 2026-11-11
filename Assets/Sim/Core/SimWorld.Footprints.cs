using LordsLegacy.Sim.Footprints;

namespace LordsLegacy.Sim.Core
{
    /// <summary>The footprint part of <see cref="SimWorld"/> (T-252).</summary>
    public sealed partial class SimWorld
    {
        /// <summary>Fixed-rectangle things such as the camp. Never null; empty until placed.</summary>
        public FootprintRegistry Footprints { get; } = new FootprintRegistry();
    }
}
