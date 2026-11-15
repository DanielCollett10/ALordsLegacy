using LordsLegacy.Sim.Homes;

namespace LordsLegacy.Sim.Core
{
    /// <summary>The homes part of <see cref="SimWorld"/> (T-405, homes.md).</summary>
    public sealed partial class SimWorld
    {
        /// <summary>Houses and the families living in them. Never null; empty at start.</summary>
        public HomeRegistry Homes { get; } = new HomeRegistry();
    }
}
