using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Operations;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>One module ghost in a <see cref="DesignateOperation"/>: type, centre and rotation. Immutable.</summary>
    public sealed class ModuleSpec
    {
        /// <summary>Module type id, e.g. <see cref="ModuleTypes.WoodcuttingHutId"/>.</summary>
        public string TypeId { get; }
        /// <summary>Centre of the module.</summary>
        public PointMm Centre { get; }
        /// <summary>Rotation in degrees, counter-clockwise.</summary>
        public double RotationDegrees { get; }

        /// <summary>Creates a module spec.</summary>
        public ModuleSpec(string typeId, PointMm centre, double rotationDegrees)
        {
            TypeId = typeId; Centre = centre; RotationDegrees = rotationDegrees;
        }
    }

    /// <summary>
    /// Confirms a drafted operation in one go (building-tool.md BT-15, BT-16; decision 0037): the building area, its
    /// operation and every module in list order, all or nothing. Validate refuses an invalid area, a missing mandatory
    /// module ("Place a Hut"), any module that fails <see cref="DraftModulePlacement"/>, or an unaffordable total, and
    /// changes nothing. Apply reuses <see cref="DesignateArea"/> and <see cref="PlaceModule"/>, so footprints, sites and
    /// reservations (nearest source first, in list order) and tree clearing (WC-08) match placing them one by one.
    /// Created ids: the area id first, then one footprint id per module in list order. Works while paused. Adds no
    /// saved state.
    /// </summary>
    public sealed class DesignateOperation : ISimCommand
    {
        /// <summary>The player's area shape.</summary>
        public AreaShape Shape { get; }
        /// <summary>Operation type id, e.g. <see cref="OperationTypes.WoodcuttingId"/>.</summary>
        public string OperationTypeId { get; }
        /// <summary>The module ghosts in placement order.</summary>
        public IReadOnlyList<ModuleSpec> Modules { get; }

        /// <summary>Creates the command. The module list is copied.</summary>
        public DesignateOperation(AreaShape shape, string operationTypeId, IReadOnlyList<ModuleSpec> modules)
        {
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            if (modules == null) throw new ArgumentNullException(nameof(modules));
            OperationTypeId = operationTypeId;
            Modules = new List<ModuleSpec>(modules);
        }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world)
        {
            OperationTypeDef opType = OperationTypes.Find(OperationTypeId);
            if (opType == null)
                return CommandResult.Rejected(CommandRejectCode.InvalidArgument, "Unknown operation type");

            AreaPreview p = AreaValidator.Validate(world, Shape, opType.BuildingAreaTypeId);
            if (!p.IsValid) return CommandResult.Rejected(AreaCode(p.Reason), p.ReasonText);

            var ghosts = new List<Footprint>();
            int cost = 0;
            int huts = 0, storages = 0;
            foreach (ModuleSpec m in Modules)
            {
                if (m == null) return CommandResult.Rejected(CommandRejectCode.InvalidArgument, "Missing module");
                if (double.IsNaN(m.RotationDegrees) || double.IsInfinity(m.RotationDegrees))
                    return CommandResult.Rejected(CommandRejectCode.InvalidArgument, "Rotation is not a number");
                ModulePlacementResult r = DraftModulePlacement.Preview(world, opType, p.Polygon, ghosts, m.TypeId,
                    m.Centre, m.RotationDegrees, cost);
                if (!r.Ok) return CommandResult.Rejected(ModuleCode(r.Refusal), r.Text);
                FootprintTypeDef type = ModuleTypes.Find(m.TypeId);
                ghosts.Add(new Footprint(0, type, m.Centre, m.RotationDegrees));
                cost += type.ConstructionRecipe.Logs;
                if (type.Id == opType.HutTypeId) huts++;
                if (type.Id == opType.LogStorageTypeId) storages++;
            }
            if (huts < opType.HutCount)
                return CommandResult.Rejected(CommandRejectCode.NotAllowed, "Place a Hut");
            if (storages < opType.MinLogStorageCount)
                return CommandResult.Rejected(CommandRejectCode.NotAllowed, "Place a Log storage");
            return CommandResult.Accepted();
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            CommandResult check = Validate(world);
            if (check.IsRejected) return check;

            OperationTypeDef opType = OperationTypes.Find(OperationTypeId);
            CommandResult area = new DesignateArea(Shape, opType.BuildingAreaTypeId).Apply(world);
            if (area.IsRejected) throw new InvalidOperationException("DesignateOperation: area refused after validation: " + area.Text);
            int areaId = (int)area.Created[0];
            Operation op = world.Operations.FindByArea(areaId);

            var created = new List<long> { areaId };
            var trees = new List<long>(area.RemovedTreeIds);
            foreach (ModuleSpec m in Modules)
            {
                CommandResult r = new PlaceModule(op.Id, m.TypeId, m.Centre, m.RotationDegrees).Apply(world);
                if (r.IsRejected) throw new InvalidOperationException("DesignateOperation: module refused after validation: " + r.Text);
                created.Add(r.Created[0]);
                trees.AddRange(r.RemovedTreeIds);
            }
            return CommandResult.Accepted(created: created.ToArray(), removedTreeIds: trees.ToArray());
        }

        private static CommandRejectCode AreaCode(AreaRefusal reason)
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

        private static CommandRejectCode ModuleCode(ModuleRefusal r) =>
            r == ModuleRefusal.OverlapsModule ? CommandRejectCode.Conflict : CommandRejectCode.NotAllowed;
    }
}
