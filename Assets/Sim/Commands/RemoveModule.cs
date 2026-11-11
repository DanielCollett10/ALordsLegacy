using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Operations;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// Removes a placed module (footprint id) from its operation and the footprint registry. Unknown id →
    /// <see cref="CommandRejectCode.NotFound"/>. Trees it cleared do not come back. Removed ids hold the module id.
    /// </summary>
    public sealed class RemoveModule : ISimCommand
    {
        /// <summary>Footprint id of the module.</summary>
        public int ModuleId { get; }

        /// <summary>Creates the command.</summary>
        public RemoveModule(int moduleId)
        {
            ModuleId = moduleId;
        }

        private Operation Owner(SimWorld world)
        {
            foreach (Operation op in world.Operations.All)
                foreach (int id in op.ModuleIds)
                    if (id == ModuleId) return op;
            return null;
        }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world) =>
            Owner(world) == null ? CommandResult.Rejected(CommandRejectCode.NotFound, "no module " + ModuleId) : CommandResult.Accepted();

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            Operation op = Owner(world);
            if (op == null) return CommandResult.Rejected(CommandRejectCode.NotFound, "no module " + ModuleId);
            world.Operations.RemoveModule(op.Id, ModuleId);
            world.Footprints.Remove(ModuleId);
            return CommandResult.Accepted(removed: new long[] { ModuleId });
        }
    }
}
