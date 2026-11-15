using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;

namespace LordsLegacy.Sim.Foraging
{
    /// <summary>Operations phase: refills emptied bushes after <c>BushRegrowDays</c> (FD-21).</summary>
    public sealed class BushRegrowSystem : ISimSystem
    {
        /// <inheritdoc />
        public void Step(SimWorld world) { world.Bushes.Regrow(world.Tick, world.Tuning); }

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result) { }
    }
}
