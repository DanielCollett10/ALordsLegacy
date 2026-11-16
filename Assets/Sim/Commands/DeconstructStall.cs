using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Markets;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// Deconstruct an unstaffed market stall (MK-23b, T-460). Refused for a staffed stall ("Staffed by X: unassign them
    /// first"). Otherwise the stall gets a deconstruction entry like any module (CN-50..52): odd-jobbers take it down
    /// (decision 0039), <see cref="CancelDeconstruction"/> undoes the mark (CN-57), and on completion
    /// <see cref="DeconstructionWork.Finish"/> drops its food as an unowned ground pile (CN-55); the refund is 0 logs.
    /// </summary>
    public sealed class DeconstructStall : ISimCommand
    {
        /// <summary>Footprint id of the stall.</summary>
        public int StallId { get; }

        /// <summary>Creates the command.</summary>
        public DeconstructStall(int stallId) { StallId = stallId; }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world)
        {
            Market market = world.Markets.Current;
            Footprint fp = world.Footprints.Get(StallId);
            if (market == null || fp == null || !market.StallFootprintIds.Contains(StallId))
                return CommandResult.Rejected(CommandRejectCode.NotFound, "no stall " + StallId);
            int family = market.FamilyOf(StallId);
            if (family != 0)
            {
                Family f = world.People.FindFamily(family);
                string name = f != null ? f.Surname : "family " + family;
                return CommandResult.Rejected(CommandRejectCode.NotAllowed, "Staffed by " + name + ": unassign them first");
            }
            if (world.Deconstruction.Get(StallId) != null)
                return CommandResult.Rejected(CommandRejectCode.NotAllowed, "already being deconstructed");
            if (!world.IsBuilt(StallId))
                return CommandResult.Rejected(CommandRejectCode.NotAllowed, "stall is not built yet");
            return CommandResult.Accepted();
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            CommandResult check = Validate(world);
            if (check.IsRejected) return check;
            world.Deconstruction.Add(new DeconstructionEntry(StallId));
            return CommandResult.Accepted();
        }
    }
}
