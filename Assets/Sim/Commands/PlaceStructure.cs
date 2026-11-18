using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Placement;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// Places a standalone structure such as the granary as one piece (FD-30, FD-30b). Validate runs
    /// <see cref="StructurePlacement.Preview"/>. Apply, in one step, reserves the recipe's logs, creates the
    /// construction site and the footprint, clears trees under the whole plot (building and yard), and gives a food
    /// type its food-only store. Created ids hold the footprint id. Works while paused.
    /// </summary>
    public sealed class PlaceStructure : ISimCommand
    {
        /// <summary>Structure type id, e.g. <see cref="ModuleTypes.GranaryId"/>.</summary>
        public string TypeId { get; }
        /// <summary>Centre of the building.</summary>
        public PointMm Centre { get; }
        /// <summary>Rotation in degrees, counter-clockwise.</summary>
        public double RotationDegrees { get; }

        /// <summary>Creates the command.</summary>
        public PlaceStructure(string typeId, PointMm centre, double rotationDegrees)
        {
            TypeId = typeId; Centre = centre; RotationDegrees = rotationDegrees;
        }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world)
        {
            StructurePlacementResult p = StructurePlacement.Preview(world, TypeId, Centre, RotationDegrees);
            return p.Ok ? CommandResult.Accepted() : CommandResult.Rejected(CodeFor(p.Refusal), p.Text);
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            CommandResult check = Validate(world);
            if (check.IsRejected) return check;

            FootprintTypeDef type = ModuleTypes.Find(TypeId);
            int fpId = world.Footprints.NextId();
            if (!type.ConstructionRecipe.IsEmpty)
            {
                var site = new ConstructionSite(fpId, world.Inventory.GetOrAddSite(fpId).Id);
                var ids = SiteMaterials.TryReserveAll(world, type.ConstructionRecipe, Centre, fpId);
                if (ids == null)
                    return CommandResult.Rejected(CommandRejectCode.NotAllowed, SiteMaterials.Shortfall(world, type.ConstructionRecipe) ?? "Not enough materials");
                site.ReservationIds.AddRange(ids);
                world.Construction.Add(site);
            }
            Footprint fp = world.Footprints.Add(fpId, type, Centre, RotationDegrees);
            if (type.FoodCapacity > 0) world.Inventory.AddFoodStore(type.FoodCapacity, fp.Id);

            if (type.Id == ModuleTypes.GranaryId)
            {
                Operation op = world.Operations.Create(OperationTypes.GranaryId, 0);
                world.Operations.AddModule(op.Id, fp.Id);
            }

            if (type.Id == ModuleTypes.StorageYardId)
            {
                Operation op = world.Operations.Create(OperationTypes.StorageYardOpId, 0);
                world.Operations.AddModule(op.Id, fp.Id);
            }

            string workshopOp = OperationTypes.WorkshopOperationFor(type.Id);
            if (workshopOp != null)
            {
                Operation op = world.Operations.Create(workshopOp, 0);
                world.Operations.AddModule(op.Id, fp.Id);
                if (type.ConstructionRecipe.IsEmpty) Operations.Workshops.WorkshopBuildings.OnBuilt(world, fp);
            }

            var removed = TreeClearing.RemoveInPolygon(world.Trees, fp.PlotPolygon);
            var trees = new long[removed.Count];
            for (int i = 0; i < trees.Length; i++) trees[i] = removed[i].Value;
            return CommandResult.Accepted(created: new long[] { fp.Id }, removedTreeIds: trees);
        }

        private static CommandRejectCode CodeFor(StructureRefusal r)
        {
            switch (r)
            {
                case StructureRefusal.UnknownType:
                case StructureRefusal.BadRotation: return CommandRejectCode.InvalidArgument;
                case StructureRefusal.NotEnoughMaterials: return CommandRejectCode.NotAllowed;
                default: return CommandRejectCode.Conflict;
            }
        }
    }
}
