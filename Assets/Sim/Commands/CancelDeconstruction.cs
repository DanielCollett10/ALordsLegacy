using LordsLegacy.Sim.Core;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>Cancels a deconstruction under way (CN-57): the module works again at once, progress is dropped.</summary>
    public sealed class CancelDeconstruction : ISimCommand
    {
        /// <summary>Footprint id of the module.</summary>
        public int ModuleId { get; }

        /// <summary>Creates the command.</summary>
        public CancelDeconstruction(int moduleId) { ModuleId = moduleId; }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world) =>
            world.Deconstruction.Get(ModuleId) == null
                ? CommandResult.Rejected(CommandRejectCode.NotFound, "module " + ModuleId + " is not being deconstructed")
                : CommandResult.Accepted();

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            CommandResult check = Validate(world);
            if (check.IsRejected) return check;
            world.Deconstruction.Remove(ModuleId);
            foreach (LordsLegacy.Sim.Operations.Operation op in world.Operations.All)
                foreach (int id in op.ModuleIds)
                    if (id == ModuleId) world.Deconstruction.RemovePendingOperation(op.Id);
            return CommandResult.Accepted();
        }
    }
}
