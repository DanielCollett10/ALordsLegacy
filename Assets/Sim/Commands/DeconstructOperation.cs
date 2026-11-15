using System.Collections.Generic;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Operations;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// Deconstruct a whole operation (CN-58): every module is marked as by <see cref="MarkForDeconstruction"/>, and the
    /// operation with its area goes when the last module is gone. All or nothing: if any module would be refused
    /// (e.g. reserved logs, CN-34) the whole command is refused. Modules already being deconstructed are left as they are.
    /// </summary>
    public sealed class DeconstructOperation : ISimCommand
    {
        /// <summary>Id of the operation.</summary>
        public int OperationId { get; }

        /// <summary>Creates the command.</summary>
        public DeconstructOperation(int operationId) { OperationId = operationId; }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world)
        {
            Operation op = world.Operations.Get(OperationId);
            if (op == null) return CommandResult.Rejected(CommandRejectCode.NotFound, "no operation " + OperationId);
            foreach (int id in op.ModuleIds)
            {
                if (world.Deconstruction.Get(id) != null) continue;
                CommandResult r = new MarkForDeconstruction(id).Validate(world);
                if (r.IsRejected) return r;
            }
            return CommandResult.Accepted();
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            CommandResult check = Validate(world);
            if (check.IsRejected) return check;
            Operation op = world.Operations.Get(OperationId);
            world.Deconstruction.AddPendingOperation(OperationId);
            var removed = new List<long>();
            foreach (int id in new List<int>(op.ModuleIds))
            {
                if (world.Deconstruction.Get(id) != null) continue;
                new MarkForDeconstruction(id).Apply(world);
            }
            if (world.Operations.Get(OperationId) == null || DeconstructionWork.CompletePendingOperation(world, OperationId))
                removed.Add(op.AreaId);
            return CommandResult.Accepted(removed: removed.ToArray());
        }
    }
}
