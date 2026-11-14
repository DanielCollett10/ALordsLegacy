using System;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// Sets a construction site's priority to Normal or High (CN-30). The site and its open and assigned construction
    /// tasks (deliver and build, matched by <see cref="TaskRecord.SiteFootprintId"/>) take the new priority, so the
    /// dispatcher hands out High work first (CN-31). Unknown footprint → <see cref="CommandRejectCode.NotFound"/>;
    /// a footprint with no site (already built) → <see cref="CommandRejectCode.NotAllowed"/>; a value that is not
    /// Normal or High → <see cref="CommandRejectCode.InvalidArgument"/>. Setting the current priority is accepted and
    /// changes nothing. Works while paused.
    /// </summary>
    public sealed class SetSitePriority : ISimCommand
    {
        /// <summary>Footprint id of the site.</summary>
        public int FootprintId { get; }

        /// <summary>The new priority.</summary>
        public ConstructionPriority Priority { get; }

        /// <summary>Creates the command.</summary>
        public SetSitePriority(int footprintId, ConstructionPriority priority)
        {
            FootprintId = footprintId;
            Priority = priority;
        }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (Priority != ConstructionPriority.Normal && Priority != ConstructionPriority.High)
                return CommandResult.Rejected(CommandRejectCode.InvalidArgument, "unknown priority " + (int)Priority);
            if (world.Footprints.Get(FootprintId) == null)
                return CommandResult.Rejected(CommandRejectCode.NotFound, "no footprint " + FootprintId);
            if (world.Construction.Get(FootprintId) == null)
                return CommandResult.Rejected(CommandRejectCode.NotAllowed, "footprint " + FootprintId + " is already built");
            return CommandResult.Accepted();
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            CommandResult check = Validate(world);
            if (check.IsRejected) return check;

            world.Construction.Get(FootprintId).Priority = Priority;
            TaskPriority taskPriority = Priority == ConstructionPriority.High ? TaskPriority.High : TaskPriority.Normal;
            foreach (TaskRecord t in world.Tasks.All)
                if (!t.IsFinished && t.SiteFootprintId == FootprintId) world.Tasks.SetPriority(t.Id, taskPriority);
            return CommandResult.Accepted();
        }
    }
}
