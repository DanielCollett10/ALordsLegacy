using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Operations;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// Adds several modules to an existing operation in one all-or-nothing command (building-tool.md BT-20..BT-22,
    /// decision 0037: Expand). Validate refuses an empty list, a missing operation, any ghost that fails
    /// <see cref="DraftModulePlacement"/> against the operation's area, its existing modules and the other ghosts
    /// (so a second hut or an overlap is refused), or an unaffordable total, and changes nothing. Apply reuses
    /// <see cref="PlaceModule"/> in list order, so sites, reservations and tree clearing match one-by-one placing.
    /// Existing built modules keep working while the new ones are built (BT-22, CN-04). Created ids: one footprint id
    /// per module in list order. Works while paused. Adds no saved state.
    /// </summary>
    public sealed class PlaceModules : ISimCommand
    {
        /// <summary>Operation to expand.</summary>
        public int OperationId { get; }
        /// <summary>The module ghosts in placement order.</summary>
        public IReadOnlyList<ModuleSpec> Modules { get; }

        /// <summary>Creates the command. The module list is copied.</summary>
        public PlaceModules(int operationId, IReadOnlyList<ModuleSpec> modules)
        {
            if (modules == null) throw new ArgumentNullException(nameof(modules));
            OperationId = operationId;
            Modules = new List<ModuleSpec>(modules);
        }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world)
        {
            Operation op = world.Operations.Get(OperationId);
            if (op == null) return CommandResult.Rejected(CommandRejectCode.NotFound, "No operation " + OperationId);
            OperationTypeDef opType = OperationTypes.Find(op.TypeId);
            var area = world.Areas.Get(op.AreaId);
            if (opType == null || area == null)
                return CommandResult.Rejected(CommandRejectCode.NotAllowed, "The operation's area is gone");
            if (Modules.Count == 0)
                return CommandResult.Rejected(CommandRejectCode.InvalidArgument, "Place at least one module");

            // Existing modules count towards per-type limits; they also overlap-check as world footprints.
            var ghosts = new List<Footprint>();
            foreach (int id in op.ModuleIds)
            {
                Footprint f = world.Footprints.Get(id);
                if (f != null) ghosts.Add(f);
            }
            int cost = 0;
            foreach (ModuleSpec m in Modules)
            {
                if (m == null) return CommandResult.Rejected(CommandRejectCode.InvalidArgument, "Missing module");
                if (double.IsNaN(m.RotationDegrees) || double.IsInfinity(m.RotationDegrees))
                    return CommandResult.Rejected(CommandRejectCode.InvalidArgument, "Rotation is not a number");
                ModulePlacementResult r = DraftModulePlacement.Preview(world, opType, area.Polygon, ghosts, m.TypeId,
                    m.Centre, m.RotationDegrees, cost);
                if (!r.Ok)
                    return CommandResult.Rejected(r.Refusal == ModuleRefusal.OverlapsModule ? CommandRejectCode.Conflict
                        : CommandRejectCode.NotAllowed, r.Text);
                FootprintTypeDef type = ModuleTypes.Find(m.TypeId);
                ghosts.Add(new Footprint(0, type, m.Centre, m.RotationDegrees));
                cost += type.ConstructionRecipe.Logs;
            }
            return CommandResult.Accepted();
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            CommandResult check = Validate(world);
            if (check.IsRejected) return check;

            var created = new List<long>();
            var trees = new List<long>();
            foreach (ModuleSpec m in Modules)
            {
                CommandResult r = new PlaceModule(OperationId, m.TypeId, m.Centre, m.RotationDegrees).Apply(world);
                if (r.IsRejected) throw new InvalidOperationException("PlaceModules: module refused after validation: " + r.Text);
                created.Add(r.Created[0]);
                if (r.RemovedTreeIds != null) trees.AddRange(r.RemovedTreeIds);
            }
            return CommandResult.Accepted(created: created.ToArray(), removedTreeIds: trees.ToArray());
        }
    }
}
