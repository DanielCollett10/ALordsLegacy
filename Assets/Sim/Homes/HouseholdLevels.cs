using System.Collections.Generic;
using LordsLegacy.Sim.Footprints;

namespace LordsLegacy.Sim.Homes
{
    /// <summary>The kinds of need a household level can ask for (HL-21).</summary>
    public enum NeedKind
    {
        /// <summary>The last Sunday allotment held 2 or more food types and the family is not Hungry.</summary>
        DiverseFood = 0,

        /// <summary>The house has a built fence round its plot (HL-12).</summary>
        FencedPlot = 1,

        /// <summary>Counted as met for now (water, church: HSE-19).</summary>
        AlwaysMet = 2,
    }

    /// <summary>One need of a household level: a kind, a label for the panel and a count (food types for DiverseFood).</summary>
    public sealed class NeedDef
    {
        /// <summary>Creates a need.</summary>
        public NeedDef(NeedKind kind, string label, int amount = 0)
        {
            Kind = kind;
            Label = label ?? string.Empty;
            Amount = amount;
        }

        /// <summary>What the need checks.</summary>
        public NeedKind Kind { get; }

        /// <summary>Short name shown to the player.</summary>
        public string Label { get; }

        /// <summary>Threshold for the kind (the food types needed for DiverseFood; 0 otherwise).</summary>
        public int Amount { get; }
    }

    /// <summary>A household level as data (HSE-09, HL-21): its number, its needs and the cost of upgrading to it.</summary>
    public sealed class HouseholdLevelDef
    {
        /// <summary>Creates a level definition.</summary>
        public HouseholdLevelDef(int level, string name, IReadOnlyList<NeedDef> needs, ConstructionRecipe upgradeCost)
        {
            Level = level;
            Name = name ?? string.Empty;
            Needs = needs ?? new NeedDef[0];
            UpgradeCost = upgradeCost ?? ConstructionRecipe.Empty;
        }

        /// <summary>Level number, from 1.</summary>
        public int Level { get; }

        /// <summary>Display name.</summary>
        public string Name { get; }

        /// <summary>The needs a household must meet to be eligible for this level.</summary>
        public IReadOnlyList<NeedDef> Needs { get; }

        /// <summary>Materials and labour to upgrade to this level. Empty in M21 (HL-23): later cost goes here.</summary>
        public ConstructionRecipe UpgradeCost { get; }
    }

    /// <summary>The household level table (HSE-09..12, HL-21, HL-23). Level 1 has no needs; level 2 has four.</summary>
    public static class HouseholdLevels
    {
        /// <summary>Level every family starts at (HL-20).</summary>
        public const int StartLevel = 1;

        private static readonly HouseholdLevelDef[] _all =
        {
            new HouseholdLevelDef(1, "Household", new NeedDef[0], ConstructionRecipe.Empty),
            new HouseholdLevelDef(2, "Settled household", new[]
            {
                new NeedDef(NeedKind.DiverseFood, "Diverse food", 2),
                new NeedDef(NeedKind.FencedPlot, "Fenced plot"),
                new NeedDef(NeedKind.AlwaysMet, "Water"),
                new NeedDef(NeedKind.AlwaysMet, "Church"),
            }, ConstructionRecipe.Empty),
        };

        /// <summary>Every level, ascending.</summary>
        public static IReadOnlyList<HouseholdLevelDef> All => _all;

        /// <summary>The highest level defined.</summary>
        public static int MaxLevel => _all[_all.Length - 1].Level;

        /// <summary>The definition of a level, or null if undefined.</summary>
        public static HouseholdLevelDef Get(int level)
        {
            foreach (HouseholdLevelDef d in _all) if (d.Level == level) return d;
            return null;
        }
    }
}
