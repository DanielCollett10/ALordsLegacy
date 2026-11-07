using System;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Forestry;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// Designates an area (decision 0014). Re-runs <see cref="AreaValidator"/>; if valid, adds the area to the
    /// <see cref="AreaRegistry"/> and, for types that clear trees (building kinds), removes the trees inside the
    /// flattened outline. Created ids hold the new area id; removed tree ids are in
    /// <see cref="CommandResult.RemovedTreeIds"/>. Works while paused.
    /// </summary>
    public sealed class DesignateArea : ISimCommand
    {
        /// <summary>The player's shape.</summary>
        public AreaShape Shape { get; }

        /// <summary>Area type id.</summary>
        public string TypeId { get; }

        /// <summary>Creates the command.</summary>
        public DesignateArea(AreaShape shape, string typeId)
        {
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            TypeId = typeId;
        }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world)
        {
            AreaPreview p = AreaValidator.Validate(world, Shape, TypeId);
            return p.IsValid ? CommandResult.Accepted() : CommandResult.Rejected(CodeFor(p.Reason), p.ReasonText);
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            AreaPreview p = AreaValidator.Validate(world, Shape, TypeId);
            if (!p.IsValid) return CommandResult.Rejected(CodeFor(p.Reason), p.ReasonText);

            AreaTypeDef type = AreaTypes.Find(TypeId);
            var record = new AreaRecord(world.Areas.NextId(), type.Id, world.Areas.NextName(type), Shape, p.Polygon);
            world.Areas.Add(record);

            long[] trees = null;
            if (type.ClearsTrees)
            {
                var removed = TreeClearing.RemoveInPolygon(world.Trees, record.Polygon);
                trees = new long[removed.Count];
                for (int i = 0; i < trees.Length; i++) trees[i] = removed[i].Value;
            }
            return CommandResult.Accepted(created: new long[] { record.Id }, removedTreeIds: trees);
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
