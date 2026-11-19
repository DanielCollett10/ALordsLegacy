using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Homes;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// Fences a built house (HL-10, decision 0053). Adds a <see cref="ModuleTypes.FenceId"/> footprint at the plot's frontage
    /// access point with a construction site (wattle panels reserved through <see cref="SiteMaterials"/>), and links it in
    /// <see cref="HomeRecord.FenceFootprintId"/>. Refused for an unknown or unbuilt house, one being deconstructed, one that
    /// already has a fence, a plot with no fence line, or when the panels are short. Created ids hold the fence footprint id.
    /// </summary>
    public sealed class BuildFence : ISimCommand
    {
        /// <summary>Footprint id of the house.</summary>
        public int HomeId { get; }

        /// <summary>Creates the command.</summary>
        public BuildFence(int homeId) { HomeId = homeId; }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world)
        {
            HomeRecord home = world.Homes.Get(HomeId);
            if (home == null) return CommandResult.Rejected(CommandRejectCode.NotFound, "no house " + HomeId);
            if (!world.IsBuilt(HomeId)) return CommandResult.Rejected(CommandRejectCode.NotAllowed, "the house is not built yet");
            if (world.Deconstruction.Get(HomeId) != null) return CommandResult.Rejected(CommandRejectCode.NotAllowed, "the house is being taken down");
            if (HouseFence.StateOf(world, home) != FenceState.None) return CommandResult.Rejected(CommandRejectCode.Conflict, "the house already has a fence");
            AreaRecord plot = world.Areas.Get(home.PlotAreaId);
            if (plot == null) return CommandResult.Rejected(CommandRejectCode.NotFound, "no plot for house " + HomeId);
            ConstructionRecipe recipe = FenceLine.Of(plot).Recipe(world.Tuning);
            if (recipe.IsEmpty) return CommandResult.Rejected(CommandRejectCode.NotAllowed, "the plot has no fence line");
            string shortfall = SiteMaterials.Shortfall(world, recipe);
            return shortfall != null ? CommandResult.Rejected(CommandRejectCode.NotAllowed, shortfall) : CommandResult.Accepted();
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            CommandResult check = Validate(world);
            if (check.IsRejected) return check;
            HomeRecord home = world.Homes.Get(HomeId);
            FenceLine line = FenceLine.Of(world.Areas.Get(home.PlotAreaId));
            ConstructionRecipe recipe = line.Recipe(world.Tuning);
            int fpId = world.Footprints.NextId();
            var ids = SiteMaterials.TryReserveAll(world, recipe, line.AccessPoint, fpId);
            if (ids == null)
                return CommandResult.Rejected(CommandRejectCode.NotAllowed, SiteMaterials.Shortfall(world, recipe) ?? "Not enough materials");
            var site = new ConstructionSite(fpId, world.Inventory.GetOrAddSite(fpId).Id);
            site.ReservationIds.AddRange(ids);
            world.Construction.Add(site);
            world.Footprints.Add(fpId, ModuleTypes.Fence, line.AccessPoint, 0);
            world.Homes.SetFence(home, fpId);
            return CommandResult.Accepted(created: new long[] { fpId });
        }
    }
}
