using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Operations;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// Places a module in an operation's area (WC-03, WC-07, WC-08). Re-runs <see cref="ModulePlacement.Preview"/>,
    /// adds the footprint, lists it on the operation and removes the trees under it (ids in
    /// <see cref="CommandResult.RemovedTreeIds"/>). Built at once (M8 placeholder). Created ids hold the footprint id.
    /// Agents under a blocking module are moved aside by the nav sync (MOV-12). Works while paused.
    /// </summary>
    public sealed class PlaceModule : ISimCommand
    {
        /// <summary>Operation to place into.</summary>
        public int OperationId { get; }
        /// <summary>Module type id, e.g. <see cref="ModuleTypes.WoodcuttingHutId"/>.</summary>
        public string ModuleTypeId { get; }
        /// <summary>Centre of the module.</summary>
        public PointMm Centre { get; }
        /// <summary>Rotation in degrees, counter-clockwise.</summary>
        public double RotationDegrees { get; }

        /// <summary>Creates the command.</summary>
        public PlaceModule(int operationId, string moduleTypeId, PointMm centre, double rotationDegrees)
        {
            OperationId = operationId; ModuleTypeId = moduleTypeId; Centre = centre; RotationDegrees = rotationDegrees;
        }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world)
        {
            if (double.IsNaN(RotationDegrees) || double.IsInfinity(RotationDegrees))
                return CommandResult.Rejected(CommandRejectCode.InvalidArgument, "Rotation is not a number");
            ModulePlacementResult p = ModulePlacement.Preview(world, OperationId, ModuleTypeId, Centre, RotationDegrees);
            return p.Ok ? CommandResult.Accepted() : CommandResult.Rejected(CodeFor(p.Refusal), p.Text);
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            CommandResult check = Validate(world);
            if (check.IsRejected) return check;

            FootprintTypeDef type = ModuleTypes.Find(ModuleTypeId);
            Footprint fp = world.Footprints.Add(world.Footprints.NextId(), type, Centre, RotationDegrees);
            world.Operations.AddModule(OperationId, fp.Id);

            long[] trees = null;
            if (type.ClearsTrees)
            {
                var removed = TreeClearing.RemoveInPolygon(world.Trees, fp.Polygon);
                trees = new long[removed.Count];
                for (int i = 0; i < trees.Length; i++) trees[i] = removed[i].Value;
            }
            return CommandResult.Accepted(created: new long[] { fp.Id }, removedTreeIds: trees);
        }

        private static CommandRejectCode CodeFor(ModuleRefusal r)
        {
            switch (r)
            {
                case ModuleRefusal.UnknownOperation: return CommandRejectCode.NotFound;
                case ModuleRefusal.OverlapsModule: return CommandRejectCode.Conflict;
                default: return CommandRejectCode.NotAllowed;
            }
        }
    }
}
