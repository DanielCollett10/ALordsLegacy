using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Operations.Workshops;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// Sets a workshop's input reserve (PL-30b): the amount of its input item that must stay in storage. Refused with
    /// NotFound for an unknown operation, NotAllowed when the operation is not a workshop or the item is not its own
    /// input, and InvalidArgument when the amount is outside 0..<see cref="MaxReserve"/>. Works while paused. A refusal
    /// changes nothing; choosing the current value is accepted and changes nothing.
    /// </summary>
    public sealed class SetWorkshopReserve : ISimCommand
    {
        /// <summary>The largest reserve the player may set.</summary>
        public const int MaxReserve = 9999;

        /// <summary>Id of the workshop operation.</summary>
        public int OperationId { get; }
        /// <summary>Item id of the workshop's input.</summary>
        public string ItemId { get; }
        /// <summary>The new reserve, 0..9999.</summary>
        public int Amount { get; }

        /// <summary>Creates the command.</summary>
        public SetWorkshopReserve(int operationId, string itemId, int amount)
        {
            OperationId = operationId; ItemId = itemId; Amount = amount;
        }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world)
        {
            Operation op = world.Operations.Get(OperationId);
            if (op == null) return CommandResult.Rejected(CommandRejectCode.NotFound, "no operation " + OperationId);
            WorkshopDef def = OperationTypes.WorkshopOf(op.TypeId, world.Tuning);
            if (def == null) return CommandResult.Rejected(CommandRejectCode.NotAllowed, "Only a workshop has an input reserve");
            bool isInput = false;
            foreach (RecipeInput input in def.Inputs) if (input.Item.Id == ItemId) isInput = true;
            if (!isInput) return CommandResult.Rejected(CommandRejectCode.NotAllowed, "Not an input of this workshop");
            if (Amount < 0 || Amount > MaxReserve)
                return CommandResult.Rejected(CommandRejectCode.InvalidArgument, "Reserve must be 0 to " + MaxReserve);
            return CommandResult.Accepted();
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            CommandResult check = Validate(world);
            if (check.IsRejected) return check;
            world.Operations.SetReserve(OperationId, ItemId, Amount, world.Tuning.WorkshopDefaultReserve);
            return CommandResult.Accepted();
        }
    }
}
