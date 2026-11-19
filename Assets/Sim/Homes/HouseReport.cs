using System;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Homes
{
    /// <summary>
    /// Everything the house panel shows for one family's house, read from the Sim in one go (HL-22, decision 0053).
    /// Immutable and read only: it reuses <see cref="HouseFence"/>, <see cref="ConstructionProgress"/> and
    /// <see cref="HouseholdNeedsReport"/> and never changes the world.
    /// </summary>
    public sealed class HouseReport
    {
        private HouseReport(int familyId, int houseId, int level, string levelName, FenceState fence, double fenceProgress,
            HouseholdNeedsReport needs)
        {
            FamilyId = familyId; HouseFootprintId = houseId; Level = level; LevelName = levelName;
            Fence = fence; FenceProgress = fenceProgress; Needs = needs;
        }

        /// <summary>The family whose house this is.</summary>
        public int FamilyId { get; }

        /// <summary>The house footprint id, or 0 when the family has no house.</summary>
        public int HouseFootprintId { get; }

        /// <summary>True when the family lives in a house.</summary>
        public bool HasHouse => HouseFootprintId != 0;

        /// <summary>The household's current level (HL-20).</summary>
        public int Level { get; }

        /// <summary>Display name of the current level, e.g. "Settled household".</summary>
        public string LevelName { get; }

        /// <summary>None, Building or Built (decision 0053).</summary>
        public FenceState Fence { get; }

        /// <summary>Fence build progress, 0..1: 0 with no fence, 1 when built, else the site's progress.</summary>
        public double FenceProgress { get; }

        /// <summary>Fence line for the panel: "no fence", "fence 45%" or "fence built".</summary>
        public string FenceText
        {
            get
            {
                switch (Fence)
                {
                    case FenceState.Built: return "fence built";
                    case FenceState.Building: return "fence " + (int)Math.Floor(FenceProgress * 100) + "%";
                    default: return "no fence";
                }
            }
        }

        /// <summary>The needs of the next level, each met or not, with the upgrade eligibility and blocker.</summary>
        public HouseholdNeedsReport Needs { get; }

        /// <summary>True when the Upgrade button is available: every need met and no blocker (HL-22).</summary>
        public bool UpgradeAvailable => Needs.Eligible;

        /// <summary>Why Upgrade is not available apart from needs, or null. The command refuses the same way.</summary>
        public string UpgradeBlocker => Needs.Blocker;

        /// <summary>Evaluates a family's house now. Returns null for an unknown family.</summary>
        public static HouseReport Of(SimWorld world, int familyId)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            Family f = world.People.FindFamily(familyId);
            if (f == null) return null;
            HouseholdNeedsReport needs = HouseholdNeedsReport.Of(world, familyId);
            HouseholdLevelDef level = HouseholdLevels.Get(f.Level);
            int houseId = f.HomeHouseId ?? 0;
            HomeRecord home = houseId == 0 ? null : world.Homes.Get(houseId);
            FenceState fence = home == null ? FenceState.None : HouseFence.StateOf(world, home);
            double progress = 0;
            if (fence == FenceState.Built) progress = 1;
            else if (fence == FenceState.Building)
            {
                ConstructionProgress p = ConstructionProgress.Of(world, home.FenceFootprintId);
                progress = p == null ? 0 : p.Fraction;
            }
            return new HouseReport(f.Id, home == null ? 0 : houseId, f.Level, level == null ? string.Empty : level.Name,
                fence, progress, needs);
        }
    }
}
