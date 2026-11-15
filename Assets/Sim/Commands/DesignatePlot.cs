using System;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Inventory;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// Confirms a house plot (homes.md HM-05, decision 0037 pattern): the plot area and its house construction site in
    /// one all-or-nothing Apply. Validate refuses an invalid plot (<see cref="AreaValidator"/>), a house that does not
    /// fit (<see cref="HousePlacement"/>) and not enough logs (CN-12), and changes nothing. Apply adds the plot (its
    /// trees are cleared), the house footprint, a Delivering site with the logs reserved, and an Empty home that
    /// <see cref="Homes.MoveInSystem"/> fills once the house is Built. Created ids: plot area id, then house footprint id.
    /// Works while paused. Saved by the Homes section.
    /// </summary>
    public sealed class DesignatePlot : ISimCommand
    {
        /// <summary>The player's plot shape.</summary>
        public AreaShape Shape { get; }

        /// <summary>Creates the command.</summary>
        public DesignatePlot(AreaShape shape)
        {
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
        }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world)
        {
            AreaPreview p = AreaValidator.Validate(world, Shape, AreaTypes.PlotId);
            if (!p.IsValid) return CommandResult.Rejected(CodeFor(p.Reason), p.ReasonText);
            HousePlacementResult h = HousePlacement.Place(Shape, p.Polygon, world.Footprints.All);
            if (!h.Ok) return CommandResult.Rejected(CommandRejectCode.NotAllowed, h.Text);
            int logs = ModuleTypes.House.ConstructionRecipe.Logs;
            if (!BuildCost.CanAfford(world, logs))
                return CommandResult.Rejected(CommandRejectCode.NotAllowed, "Not enough logs");
            return CommandResult.Accepted();
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            CommandResult check = Validate(world);
            if (check.IsRejected) return check;

            AreaPreview p = AreaValidator.Validate(world, Shape, AreaTypes.PlotId);
            HousePlacementResult h = HousePlacement.Place(Shape, p.Polygon, world.Footprints.All);
            FootprintTypeDef type = ModuleTypes.House;
            int fpId = world.Footprints.NextId();
            var site = new ConstructionSite(fpId, world.Inventory.GetOrAddSite(fpId).Id);
            var ids = ConstructionSources.TryReserve(world, Items.Log, type.ConstructionRecipe.Logs, h.Centre, fpId);
            if (ids == null) return CommandResult.Rejected(CommandRejectCode.NotAllowed, "Not enough logs");
            site.ReservationIds.AddRange(ids);

            CommandResult area = new DesignateArea(Shape, AreaTypes.PlotId).Apply(world);
            if (area.IsRejected) throw new InvalidOperationException("DesignatePlot: plot refused after validation: " + area.Text);
            world.Construction.Add(site);
            Footprint fp = world.Footprints.Add(fpId, type, h.Centre, h.RotationDegrees);
            int plotId = (int)area.Created[0];
            Food.Larders.ForHome(world, world.Homes.Add(fp.Id, plotId));
            return CommandResult.Accepted(created: new long[] { plotId, fp.Id }, removedTreeIds: area.RemovedTreeIds);
        }

        private static CommandRejectCode CodeFor(AreaRefusal reason)
        {
            switch (reason)
            {
                case AreaRefusal.UnknownType:
                case AreaRefusal.TooFewPoints:
                case AreaRefusal.TooManyPoints: return CommandRejectCode.InvalidArgument;
                case AreaRefusal.OverlapsArea:
                case AreaRefusal.CrossesRoad: return CommandRejectCode.Conflict;
                default: return CommandRejectCode.NotAllowed;
            }
        }
    }
}
