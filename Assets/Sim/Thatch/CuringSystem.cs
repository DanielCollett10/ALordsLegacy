using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;

namespace LordsLegacy.Sim.Thatch
{
    /// <summary>Dries curing stooks at their ready tick (MAT-32).</summary>
    public sealed class CuringSystem : ISimSystem
    {
        /// <inheritdoc />
        public void Step(SimWorld world) { world.Curing.Step(world.Tick, world.Inventory); }

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result) { }
    }
}
