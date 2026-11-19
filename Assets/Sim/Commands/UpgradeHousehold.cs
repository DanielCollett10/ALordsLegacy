using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Homes;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// Raises an eligible household to its next level (HL-22, HL-23). Refused for an unknown, homeless or passing family
    /// or one whose needs are not all met. Costs nothing in M21 (the level's UpgradeCost is empty). Logs the new level.
    /// Nothing else raises a level.
    /// </summary>
    public sealed class UpgradeHousehold : ISimCommand
    {
        /// <summary>The family to upgrade.</summary>
        public int FamilyId { get; }

        /// <summary>Creates the command.</summary>
        public UpgradeHousehold(int familyId) { FamilyId = familyId; }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world)
        {
            Family f = world.People.FindFamily(FamilyId);
            if (f == null) return CommandResult.Rejected(CommandRejectCode.NotFound, "no family " + FamilyId);
            HouseholdNeedsReport r = HouseholdNeedsReport.Of(world, FamilyId);
            if (r.Blocker != null) return CommandResult.Rejected(CommandRejectCode.NotAllowed, r.Blocker);
            foreach (NeedStatus s in r.Needs)
                if (!s.Met) return CommandResult.Rejected(CommandRejectCode.NotAllowed, s.Label + ": " + s.Reason);
            return CommandResult.Accepted();
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            CommandResult check = Validate(world);
            if (check.IsRejected) return check;
            Family f = world.People.FindFamily(FamilyId);
            f.RaiseLevel(f.Level + 1);
            world.SettlementLog.Add(world.Tick, "The " + f.Surname + " household reached level " + f.Level);
            return CommandResult.Accepted();
        }
    }
}
