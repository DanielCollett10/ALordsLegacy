using LordsLegacy.Sim.Tuning;

namespace LordsLegacy.Sim.Foraging
{
    /// <summary>What a bush gives: forage (FD-21) or coppice rods (decision 0050, MAT-13/15/16).</summary>
    public enum BushKind
    {
        /// <summary>Berries.</summary>
        Berries = 0,
        /// <summary>Mushrooms.</summary>
        Mushrooms = 1,
        /// <summary>Hazel stool, cut for rods (MAT-13).</summary>
        HazelStool = 2,
        /// <summary>Willow stool, cut for rods (MAT-15).</summary>
        WillowStool = 3,
        /// <summary>Reed bed on low flat ground, cut for thatch sheaves (MAT-34, MAT-39a).</summary>
        ReedBed = 4
    }

    /// <summary>Per-kind tuning lookups for bushes.</summary>
    public static class BushKinds
    {
        /// <summary>True for the coppice stool kinds.</summary>
        public static bool IsStool(BushKind k) => k == BushKind.HazelStool || k == BushKind.WillowStool;

        /// <summary>Units a full bush holds (baskets for forage, rods per cut for stools).</summary>
        public static int FullUnits(BushKind k, SimTuning t)
        {
            switch (k)
            {
                case BushKind.HazelStool:
                case BushKind.WillowStool: return t.CoppiceRodsPerCut;
                case BushKind.ReedBed: return t.ReedSheavesPerCut;
                default: return t.BushBaskets;
            }
        }

        /// <summary>Days an emptied bush takes to regrow.</summary>
        public static int RegrowDays(BushKind k, SimTuning t)
        {
            switch (k)
            {
                case BushKind.HazelStool:
                case BushKind.WillowStool: return t.CoppiceRegrowDays;
                case BushKind.ReedBed: return t.ReedRegrowDays;
                default: return t.BushRegrowDays;
            }
        }
    }
}
