using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Operations;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// Deletes an area. Trees cleared by it do not come back (BLD-10-D). Works while paused.
    /// The removed ids hold the deleted area id.
    /// </summary>
    public sealed class DeleteArea : ISimCommand
    {
        /// <summary>Id of the area to delete.</summary>
        public int AreaId { get; }

        /// <summary>Creates the command.</summary>
        public DeleteArea(int areaId)
        {
            AreaId = areaId;
        }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world)
        {
            return world.Areas.Get(AreaId) == null
                ? CommandResult.Rejected(CommandRejectCode.NotFound, "no area " + AreaId)
                : CommandResult.Accepted();
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            if (!world.Areas.Remove(AreaId))
                return CommandResult.Rejected(CommandRejectCode.NotFound, "no area " + AreaId);
            Operation op = world.Operations.FindByArea(AreaId);
            if (op != null)
            {
                foreach (int moduleId in op.ModuleIds) world.Footprints.Remove(moduleId);
                world.Operations.Remove(op.Id);
            }
            return CommandResult.Accepted(removed: new long[] { AreaId });
        }
    }
}
