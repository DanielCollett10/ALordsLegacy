using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// Assigns a family to an operation (WCO-05, POP-05, PPL-45). Never happens automatically. Refused with NotFound
    /// for an unknown operation or family, and NotAllowed when the family already has a workplace or all slots are
    /// taken. A refusal changes nothing. On success <c>Family.WorkplaceId</c> holds the operation id.
    /// </summary>
    public sealed class AssignFamily : ISimCommand
    {
        /// <summary>Operation to assign to.</summary>
        public int OperationId { get; }
        /// <summary>Family to assign.</summary>
        public int FamilyId { get; }

        /// <summary>Creates the command.</summary>
        public AssignFamily(int operationId, int familyId)
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
            if (f.WorkplaceId != null)
                return CommandResult.Rejected(CommandRejectCode.NotAllowed, "family already has a workplace");
            OperationTypeDef type = OperationTypes.Find(op.TypeId);
            int slots = type == null ? 0 : type.FamilySlots;
            if (op.FamilyIds.Count >= slots)
                return CommandResult.Rejected(CommandRejectCode.NotAllowed, "no free family slot");
            return CommandResult.Accepted();
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            CommandResult check = Validate(world);
            if (check.IsRejected) return check;
            world.Operations.AssignFamily(OperationId, FamilyId);
            world.People.FindFamily(FamilyId).WorkplaceId = OperationId;
            return CommandResult.Accepted();
        }
    }
}
