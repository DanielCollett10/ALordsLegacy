using LordsLegacy.Sim.Core;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// Assigns an ox from the shared pool to an operation (WC-30). Refused with NotFound for an unknown operation or ox
    /// and NotAllowed when the ox already belongs to an operation. A refusal changes nothing.
    /// </summary>
    public sealed class AssignOx : ISimCommand
    {
        /// <summary>Operation to assign to.</summary>
        public int OperationId { get; }
        /// <summary>Ox to assign.</summary>
        public int OxId { get; }

        /// <summary>Creates the command.</summary>
        public AssignOx(int operationId, int oxId)
        {
            OperationId = operationId;
            OxId = oxId;
        }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world)
        {
            if (world.Operations.Get(OperationId) == null)
                return CommandResult.Rejected(CommandRejectCode.NotFound, "no operation " + OperationId);
            if (world.People.FindOx(OxId) == null)
                return CommandResult.Rejected(CommandRejectCode.NotFound, "no ox " + OxId);
            if (world.Operations.FindOxOwner(OxId) != null)
                return CommandResult.Rejected(CommandRejectCode.NotAllowed, "ox already assigned");
            return CommandResult.Accepted();
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            CommandResult check = Validate(world);
            if (check.IsRejected) return check;
            world.Operations.AssignOx(OperationId, OxId);
            return CommandResult.Accepted();
        }
    }
}
