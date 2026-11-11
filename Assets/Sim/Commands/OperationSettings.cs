using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Operations;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>Shared checks for the operation setting commands.</summary>
    internal static class OperationCommandChecks
    {
        internal static CommandResult NoOperation(SimWorld world, int operationId) =>
            world.Operations.Get(operationId) == null
                ? CommandResult.Rejected(CommandRejectCode.NotFound, "no operation " + operationId)
                : null;
    }

    /// <summary>Pauses an operation (WCO-10). NotFound if unknown; NotAllowed if already paused.</summary>
    public sealed class PauseOperation : ISimCommand
    {
        /// <summary>Operation to pause.</summary>
        public int OperationId { get; }
        /// <summary>Creates the command.</summary>
        public PauseOperation(int operationId) { OperationId = operationId; }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world)
        {
            CommandResult r = OperationCommandChecks.NoOperation(world, OperationId);
            if (r != null) return r;
            return world.Operations.Get(OperationId).Paused
                ? CommandResult.Rejected(CommandRejectCode.NotAllowed, "operation is already paused")
                : CommandResult.Accepted();
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            CommandResult c = Validate(world);
            if (c.IsRejected) return c;
            world.Operations.SetPaused(OperationId, true);
            return CommandResult.Accepted();
        }
    }

    /// <summary>Resumes a paused operation (WCO-10). NotFound if unknown; NotAllowed if not paused.</summary>
    public sealed class ResumeOperation : ISimCommand
    {
        /// <summary>Operation to resume.</summary>
        public int OperationId { get; }
        /// <summary>Creates the command.</summary>
        public ResumeOperation(int operationId) { OperationId = operationId; }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world)
        {
            CommandResult r = OperationCommandChecks.NoOperation(world, OperationId);
            if (r != null) return r;
            return !world.Operations.Get(OperationId).Paused
                ? CommandResult.Rejected(CommandRejectCode.NotAllowed, "operation is not paused")
                : CommandResult.Accepted();
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            CommandResult c = Validate(world);
            if (c.IsRejected) return c;
            world.Operations.SetPaused(OperationId, false);
            return CommandResult.Accepted();
        }
    }

    /// <summary>
    /// Links an operation to a work area (WC-12). The area must exist and be of the operation type's work area type
    /// (<c>area.work_woodcutting</c>). NotFound for an unknown operation or area; NotAllowed for the wrong type.
    /// Choosing the area already linked is accepted and changes nothing.
    /// </summary>
    public sealed class SetWorkArea : ISimCommand
    {
        /// <summary>Operation to change.</summary>
        public int OperationId { get; }
        /// <summary>Work area to link.</summary>
        public int WorkAreaId { get; }
        /// <summary>Creates the command.</summary>
        public SetWorkArea(int operationId, int workAreaId) { OperationId = operationId; WorkAreaId = workAreaId; }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world)
        {
            CommandResult r = OperationCommandChecks.NoOperation(world, OperationId);
            if (r != null) return r;
            AreaRecord area = world.Areas.Get(WorkAreaId);
            if (area == null) return CommandResult.Rejected(CommandRejectCode.NotFound, "no area " + WorkAreaId);
            OperationTypeDef type = OperationTypes.Find(world.Operations.Get(OperationId).TypeId);
            if (type == null || area.TypeId != type.WorkAreaTypeId)
                return CommandResult.Rejected(CommandRejectCode.NotAllowed, "area is not a work area for this operation");
            return CommandResult.Accepted();
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            CommandResult c = Validate(world);
            if (c.IsRejected) return c;
            world.Operations.SetWorkArea(OperationId, WorkAreaId);
            return CommandResult.Accepted();
        }
    }

    /// <summary>Goes back to the default circle round the hut (WC-11). NotFound if unknown; accepted if already default.</summary>
    public sealed class UseDefaultWorkArea : ISimCommand
    {
        /// <summary>Operation to change.</summary>
        public int OperationId { get; }
        /// <summary>Creates the command.</summary>
        public UseDefaultWorkArea(int operationId) { OperationId = operationId; }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world) =>
            OperationCommandChecks.NoOperation(world, OperationId) ?? CommandResult.Accepted();

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            CommandResult c = Validate(world);
            if (c.IsRejected) return c;
            world.Operations.SetWorkArea(OperationId, null);
            return CommandResult.Accepted();
        }
    }
}
