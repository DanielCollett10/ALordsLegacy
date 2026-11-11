using LordsLegacy.Sim.Navigation;

namespace LordsLegacy.Sim.Core
{
    /// <summary>The walk grid part of <see cref="SimWorld"/> (T-256, decision 0022).</summary>
    public sealed partial class SimWorld
    {
        /// <summary>
        /// The walk grid over the terrain bounds. Never null. Kept current by <see cref="NavSyncSystem"/>; a world
        /// built with a custom system list that omits it must call <see cref="NavGrid.Sync"/> itself.
        /// </summary>
        public NavGrid Nav { get; private set; }

        private void InitNav()
        {
            Nav = new NavGrid(Terrain.Bounds, Tuning.NavCellSizeM, Tuning.MaxWalkSlopeDeg);
        }
    }
}
