using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// Takes a family off an operation (WCO-05, POP-05). Clears <c>Family.WorkplaceId</c> and cancels the
    /// operation's unfinished tasks held by the family's members through <see cref="TaskBoard.Cancel"/>, which
    /// releases their reservations (TSK-05). NotFound for an unknown operation or family; NotAllowed when the
    /// family is not assigned to that operation. A refusal changes nothing.
    /// </summary>
    public sealed class UnassignFamily : ISimCommand
    {
        /// <summary>Operation to leave.</summary>
        public int OperationId { get; }
        /// <summary>Family to unassign.</summary>
        public int FamilyId { get; }

        /// <summary>Creates the command.</summary>
        public UnassignFamily(int operationId, int familyId)
        {
            OperationId = operationId;
            FamilyId = familyId;
        }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world)
        {
            Operation op = world.Operations.Get(OperationId);
            if (op == null) return CommandResult.Rejected(CommandRejectCode.NotFound, "no operation " + OperationId);
            Family f = world.People.FindFamily(FamilyId);
            if (f == null) return CommandResult.Rejected(CommandRejectCode.NotFound, "no family " + FamilyId);
            if (f.WorkplaceId != OperationId || !System.Linq.Enumerable.Contains(op.FamilyIds, FamilyId))
                return CommandResult.Rejected(CommandRejectCode.NotAllowed, "family is not assigned to this operation");
            return CommandResult.Accepted();
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            CommandResult check = Validate(world);
            if (check.IsRejected) return check;
            Family f = world.People.FindFamily(FamilyId);
            foreach (int memberId in f.MemberIds)
                foreach (TaskRecord t in world.Tasks.ByAssignee(memberId))
                    if (t.OwnerOperationId == OperationId) world.Tasks.Cancel(t.Id);
            world.Operations.UnassignFamily(OperationId, FamilyId);
            f.WorkplaceId = null;
            return CommandResult.Accepted();
        }
    }
}
