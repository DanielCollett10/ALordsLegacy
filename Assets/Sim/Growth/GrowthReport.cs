using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Arrivals;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Homes;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Growth
{
    /// <summary>Read-only growth numbers for panels (T-506). Never changes the world or draws from its random.</summary>
    public sealed class GrowthReport
    {
        private static readonly FamilyPlan Probe = new FamilyPlan("Probe", FamilyMakeUp.CoupleWithChildren,
            new List<FamilyMemberPlan> { new FamilyMemberPlan("A", Sex.Male, 30), new FamilyMemberPlan("B", Sex.Female, 30) });

        /// <summary>Families in the village.</summary>
        public int Families { get; private set; }
        /// <summary>Citizens in the village.</summary>
        public int Citizens { get; private set; }
        /// <summary>Families walking in.</summary>
        public int Arriving { get; private set; }
        /// <summary>Families walking out.</summary>
        public int Leaving { get; private set; }
        /// <summary>
        /// Why the next family would not arrive right now (an <see cref="ArrivalRules"/> reason, judged for a typical
        /// couple), or null when one would be let in.
        /// </summary>
        public string ArrivalReason { get; private set; }
        /// <summary>Households by level for the top bar tooltip (HL-26).</summary>
        public HouseholdLevelCounts Levels { get; private set; }

        private GrowthReport() { }

        /// <summary>Builds the village report.</summary>
        public static GrowthReport Of(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var r = new GrowthReport { Citizens = world.People.CitizenCount };
            foreach (Family f in world.People.Families)
            {
                r.Families++;
                if (f.Passage == FamilyPassage.Arriving) r.Arriving++;
                else if (f.Passage == FamilyPassage.Leaving) r.Leaving++;
            }
            r.ArrivalReason = ArrivalRules.Check(world, Probe).Reason;
            r.Levels = HouseholdLevelCounts.Of(world);
            return r;
        }

        /// <summary>Status text for one family: "Leaving (hungry)", "Arriving", "Hungry (n / 30 days)" or null.</summary>
        public static string StatusOf(SimWorld world, int familyId)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            Family f = world.People.FindFamily(familyId);
            if (f == null) return null;
            if (f.Passage == FamilyPassage.Leaving) return "Leaving (hungry)";
            if (f.Passage == FamilyPassage.Arriving) return "Arriving";
            if (f.HungryDays >= 1) return "Hungry (" + f.HungryDays + " / " + world.Tuning.HungryDaysToLeave + " days)";
            return null;
        }
    }
}
