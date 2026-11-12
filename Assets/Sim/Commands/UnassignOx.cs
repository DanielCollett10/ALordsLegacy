using LordsLegacy.Sim.Core;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// Returns an assigned ox to the shared pool (WC-30). Refused with NotFound for an unknown operation and NotAllowed
    /// when the ox is not assigned to that operation. A task using the ox is cancelled by the step runner, which drops
    /// the carried logs (T-289).
    /// </summary>
    public sealed class UnassignOx : ISimCommand
    {
        /// <summary>Operation to remove the ox from.</summary>
        public int OperationId { get; }
        /// <summary>Ox to remove.</summary>
        public int OxId { get; }

        /// <summary>Creates the command.</summary>
        public UnassignOx(int operationId, int oxId)
        {
            OperationId = operationId;
            OxId = oxId;
        }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world)
        {
            var op = world.Operations.Get(OperationId);
            if (op == null) return CommandResult.Rejected(CommandRejectCode.NotFound, "no operation " + OperationId);
            if (!op.OxIdList.Contains(OxId))
                return CommandResult.Rejected(CommandRejectCode.NotAllowed, "ox is not assigned to this operation");
            return CommandResult.Accepted();
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            CommandResult check = Validate(world);
            if (check.IsRejected) return check;
            world.Operations.UnassignOx(OperationId, OxId);
            return CommandResult.Accepted();
        }
    }
}
