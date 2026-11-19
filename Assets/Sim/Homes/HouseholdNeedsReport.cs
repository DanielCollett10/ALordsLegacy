using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Homes
{
    /// <summary>One need line of a household's next level: its label, whether it is met and a reason text (HL-22).</summary>
    public sealed class NeedStatus
    {
        /// <summary>Creates a status line.</summary>
        public NeedStatus(NeedKind kind, string label, bool met, string reason)
        {
            Kind = kind; Label = label ?? string.Empty; Met = met; Reason = reason ?? string.Empty;
        }

        /// <summary>What the need checks.</summary>
        public NeedKind Kind { get; }

        /// <summary>Name shown to the player.</summary>
        public string Label { get; }

        /// <summary>True when the need is met now.</summary>
        public bool Met { get; }

        /// <summary>Why it is met or not, for the panel.</summary>
        public string Reason { get; }
    }

    /// <summary>
    /// Read-only needs check of one household for its next level (HL-21, HL-22). Everything is derived from stored inputs
    /// (last allotment food types, hunger, fence state); eligibility is never stored. Never changes the world.
    /// </summary>
    public sealed class HouseholdNeedsReport
    {
        private HouseholdNeedsReport(int familyId, int level, int nextLevel, IReadOnlyList<NeedStatus> needs, bool eligible, string blocker)
        {
            FamilyId = familyId; Level = level; NextLevel = nextLevel; Needs = needs; Eligible = eligible; Blocker = blocker;
        }

        /// <summary>The family.</summary>
        public int FamilyId { get; }

        /// <summary>Current household level.</summary>
        public int Level { get; }

        /// <summary>The level an upgrade would give, or 0 at the top level.</summary>
        public int NextLevel { get; }

        /// <summary>Each need of the next level with met or not.</summary>
        public IReadOnlyList<NeedStatus> Needs { get; }

        /// <summary>True when the family is housed, not in passage, below the top level and every need is met.</summary>
        public bool Eligible { get; }

        /// <summary>Why the household cannot upgrade apart from needs (homeless, passage, top level), or null.</summary>
        public string Blocker { get; }

        /// <summary>Evaluates a family now. Returns null for an unknown family.</summary>
        public static HouseholdNeedsReport Of(SimWorld world, int familyId)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            Family f = world.People.FindFamily(familyId);
            if (f == null) return null;
            HouseholdLevelDef next = HouseholdLevels.Get(f.Level + 1);
            var list = new List<NeedStatus>();
            if (next != null)
                foreach (NeedDef n in next.Needs) list.Add(Check(world, f, n));
            string blocker = null;
            if (next == null) blocker = "already at the highest level";
            else if (f.IsHomeless) blocker = "the family has no house";
            else if (f.IsInPassage) blocker = "the family is passing through";
            bool all = true;
            foreach (NeedStatus s in list) if (!s.Met) all = false;
            return new HouseholdNeedsReport(f.Id, f.Level, next == null ? 0 : next.Level, list, blocker == null && all, blocker);
        }

        private static NeedStatus Check(SimWorld world, Family f, NeedDef n)
        {
            switch (n.Kind)
            {
                case NeedKind.DiverseFood:
                {
                    int need = n.Required(world.Tuning);
                    bool met = f.AllotmentFoodTypes >= need && !f.IsHungry;
                    string why = f.IsHungry ? "hungry"
                        : f.AllotmentFoodTypes + " of " + need + " food types in the last allotment";
                    return new NeedStatus(n.Kind, n.Label, met, why);
                }
                case NeedKind.FencedPlot:
                {
                    HomeRecord home = f.HomeHouseId.HasValue ? world.Homes.Get(f.HomeHouseId.Value) : null;
                    FenceState s = home == null ? FenceState.None : HouseFence.StateOf(world, home);
                    return new NeedStatus(n.Kind, n.Label, s == FenceState.Built,
                        s == FenceState.Built ? "fence built" : s == FenceState.Building ? "fence still being built" : "no fence");
                }
                default:
                    return new NeedStatus(n.Kind, n.Label, true, "counted as met");
            }
        }
    }
}
