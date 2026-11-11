using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;

namespace LordsLegacy.Sim.Navigation
{
    /// <summary>
    /// Sync-phase system that keeps <see cref="SimWorld.Nav"/> current with the roads and footprints: every tick,
    /// and straight after each accepted command.
    /// </summary>
    public sealed class NavSyncSystem : ISimSystem
    {
        /// <inheritdoc />
        public void Step(SimWorld world) => world.Nav.Sync(world);

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result) => world.Nav.Sync(world);
    }
}
