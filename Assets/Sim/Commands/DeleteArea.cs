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
            if (world.Areas.Get(AreaId) == null)
                return CommandResult.Rejected(CommandRejectCode.NotFound, "no area " + AreaId);
            if (world.Homes.FindByPlot(AreaId) != null)
                return CommandResult.Rejected(CommandRejectCode.NotAllowed, "Deconstruct the house first"); // HM-14b
            Operation op = world.Operations.FindByArea(AreaId);
            if (op != null && op.ModuleIds.Count > 0)
                return CommandResult.Rejected(CommandRejectCode.NotAllowed, "Deconstruct its buildings first");
            return CommandResult.Accepted();
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            CommandResult check = Validate(world);
            if (check.IsRejected) return check;
            if (!world.Areas.Remove(AreaId))
                return CommandResult.Rejected(CommandRejectCode.NotFound, "no area " + AreaId);
            foreach (Operation linked in new System.Collections.Generic.List<Operation>(world.Operations.All))
                if (linked.WorkAreaId == AreaId) world.Operations.SetWorkArea(linked.Id, null);
            LordsLegacy.Sim.Markets.Market market = world.Markets.FindByArea(AreaId);
            if (market != null) LordsLegacy.Sim.Markets.StallSystem.RemoveStalls(world, market); // MK-04: food drops as ground piles
            if (world.Markets.RemoveByArea(AreaId))
                foreach (Operation granary in new System.Collections.Generic.List<Operation>(world.Operations.All))
                    if (granary.SuppliesMarket) world.Operations.SetSuppliesMarket(granary.Id, false); // no market, no supply
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
