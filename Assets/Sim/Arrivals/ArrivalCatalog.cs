using System;
using System.Collections.Generic;

namespace LordsLegacy.Sim.Arrivals
{
    /// <summary>The shape of a newly arriving family (GR-23).</summary>
    public enum FamilyMakeUp
    {
        /// <summary>Two parents with 2 to 4 children.</summary>
        CoupleWithChildren,
        /// <summary>One parent with children.</summary>
        SingleParentWithChildren,
        /// <summary>Two adults, no children.</summary>
        CoupleNoChildren,
        /// <summary>Older couple with grown-up children.</summary>
        FamilyWithAdultChildren
    }

    /// <summary>An inclusive integer range (ages in years, or counts).</summary>
    public struct AgeRange
    {
        /// <summary>Lowest value, inclusive.</summary>
        public int Min;
        /// <summary>Highest value, inclusive.</summary>
        public int Max;

        /// <summary>Creates a range.</summary>
        public AgeRange(int min, int max) { Min = min; Max = max; }
    }

    /// <summary>
    /// Data for the family generator: name lists, make-up weights, child counts and age ranges. Defaults are in
    /// code. PLACEHOLDER: every list and number here is a stand-in until the design pass.
    /// </summary>
    public sealed class ArrivalCatalog
    {
        /// <summary>Surnames to pick from.</summary>
        public List<string> Surnames { get; } = new List<string>
        {
            "Ashby", "Barlow", "Carter", "Dunn", "Eddowes", "Fletcher", "Garth", "Hobbs", "Ingram", "Jessop",
            "Kemp", "Lovell", "Marsh", "Nash", "Oakes", "Palmer", "Quincey", "Rudd", "Sayer", "Thorne",
            "Underhill", "Vickers", "Wainwright", "Yates"
        };

        /// <summary>Female first names.</summary>
        public List<string> FemaleNames { get; } = new List<string>
        {
            "Agnes", "Alice", "Beatrice", "Cecily", "Edith", "Eleanor", "Emma", "Joan", "Margery", "Maud",
            "Rose", "Isabel"
        };

        /// <summary>Male first names.</summary>
        public List<string> MaleNames { get; } = new List<string>
        {
            "Alan", "Edmund", "Geoffrey", "Henry", "Hugh", "John", "Nicholas", "Osbert", "Ralph", "Roger",
            "Thomas", "Walter"
        };

        /// <summary>Relative weight of each make-up (need not sum to 1).</summary>
        public Dictionary<FamilyMakeUp, double> MakeUpWeights { get; } = new Dictionary<FamilyMakeUp, double>
        {
            { FamilyMakeUp.CoupleWithChildren, 0.40 },
            { FamilyMakeUp.SingleParentWithChildren, 0.20 },
            { FamilyMakeUp.CoupleNoChildren, 0.20 },
            { FamilyMakeUp.FamilyWithAdultChildren, 0.20 }
        };

        /// <summary>Age of a parent or adult in a young family.</summary>
        public AgeRange AdultAge { get; set; } = new AgeRange(20, 45);
        /// <summary>Age of a child (all under 14).</summary>
        public AgeRange ChildAge { get; set; } = new AgeRange(0, 13);
        /// <summary>Age of a parent whose children are grown up.</summary>
        public AgeRange ElderParentAge { get; set; } = new AgeRange(46, 60);
        /// <summary>Age of a grown-up child.</summary>
        public AgeRange AdultChildAge { get; set; } = new AgeRange(18, 28);
        /// <summary>Children in a couple's family, inclusive.</summary>
        public AgeRange CoupleChildCount { get; set; } = new AgeRange(2, 4);
        /// <summary>Children of a single parent, inclusive.</summary>
        public AgeRange SingleParentChildCount { get; set; } = new AgeRange(1, 3);
        /// <summary>Grown-up children in an older family, inclusive.</summary>
        public AgeRange AdultChildCount { get; set; } = new AgeRange(1, 3);

        /// <summary>Checks the lists and weights are usable.</summary>
        public void Validate()
        {
            if (Surnames.Count == 0 || FemaleNames.Count == 0 || MaleNames.Count == 0)
                throw new InvalidOperationException("Name lists must not be empty.");
            double total = 0;
            foreach (var kv in MakeUpWeights)
            {
                if (kv.Value < 0) throw new InvalidOperationException("Negative weight.");
                total += kv.Value;
            }
            if (total <= 0) throw new InvalidOperationException("Make-up weights must have a positive total.");
        }
    }
}
