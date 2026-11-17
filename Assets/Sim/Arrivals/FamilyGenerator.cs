using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Arrivals
{
    /// <summary>One person in a <see cref="FamilyPlan"/>.</summary>
    public sealed class FamilyMemberPlan
    {
        /// <summary>First name.</summary>
        public string Name { get; }
        /// <summary>Sex.</summary>
        public Sex Sex { get; }
        /// <summary>Age in years.</summary>
        public int Age { get; }

        /// <summary>Creates a member plan.</summary>
        public FamilyMemberPlan(string name, Sex sex, int age) { Name = name; Sex = sex; Age = age; }

        /// <summary>
        /// Birth tick at <paramref name="nowTick"/>: now minus age in ticks, given <c>SimTuning.AgeingRate</c>
        /// (see <see cref="Citizen.AgeYears"/>). Can be negative.
        /// </summary>
        public long BirthTick(long nowTick, double ageingRate)
        {
            if (double.IsNaN(ageingRate) || ageingRate <= 0) throw new ArgumentOutOfRangeException(nameof(ageingRate));
            return nowTick - (long)Math.Round(Age * Citizen.TicksPerYear / ageingRate);
        }
    }

    /// <summary>A generated family: surname, make-up and members. Not yet in the world.</summary>
    public sealed class FamilyPlan
    {
        /// <summary>Family surname.</summary>
        public string Surname { get; }
        /// <summary>The shape that was drawn.</summary>
        public FamilyMakeUp MakeUp { get; }
        /// <summary>Members, adults first.</summary>
        public IReadOnlyList<FamilyMemberPlan> Members { get; }

        /// <summary>Creates a plan.</summary>
        public FamilyPlan(string surname, FamilyMakeUp makeUp, List<FamilyMemberPlan> members)
        {
            Surname = surname; MakeUp = makeUp; Members = members;
        }
    }

    /// <summary>Makes believable new families from seeded random and catalog data (GR-23). Pure: never touches the world.</summary>
    public static class FamilyGenerator
    {
        /// <summary>
        /// Draws one family. A surname not in <paramref name="usedSurnames"/> (may be null) is chosen while any
        /// remain; once all are used, any surname may repeat. Same catalog and random state give the same plan.
        /// </summary>
        public static FamilyPlan Make(ArrivalCatalog catalog, SimRandom random, ICollection<string> usedSurnames)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (random == null) throw new ArgumentNullException(nameof(random));
            catalog.Validate();

            string surname = PickSurname(catalog, random, usedSurnames);
            FamilyMakeUp makeUp = PickMakeUp(catalog, random);
            var members = new List<FamilyMemberPlan>();

            switch (makeUp)
            {
                case FamilyMakeUp.CoupleWithChildren:
                    AddCouple(catalog, random, members, catalog.AdultAge);
                    AddChildren(catalog, random, members, catalog.CoupleChildCount, catalog.ChildAge);
                    break;
                case FamilyMakeUp.SingleParentWithChildren:
                    members.Add(Person(catalog, random, RandomSex(random), catalog.AdultAge));
                    AddChildren(catalog, random, members, catalog.SingleParentChildCount, catalog.ChildAge);
                    break;
                case FamilyMakeUp.CoupleNoChildren:
                    AddCouple(catalog, random, members, catalog.AdultAge);
                    break;
                default:
                    AddCouple(catalog, random, members, catalog.ElderParentAge);
                    AddChildren(catalog, random, members, catalog.AdultChildCount, catalog.AdultChildAge);
                    break;
            }
            return new FamilyPlan(surname, makeUp, members);
        }

        private static Sex RandomSex(SimRandom r)
        {
            return r.NextInt(0, 2) == 0 ? Sex.Female : Sex.Male;
        }

        private static string PickSurname(ArrivalCatalog catalog, SimRandom random, ICollection<string> used)
        {
            var free = new List<string>();
            foreach (string s in catalog.Surnames)
                if (used == null || !used.Contains(s)) free.Add(s);
            List<string> pool = free.Count > 0 ? free : catalog.Surnames;
            return pool[random.NextInt(0, pool.Count)];
        }

        private static FamilyMakeUp PickMakeUp(ArrivalCatalog catalog, SimRandom random)
        {
            var order = (FamilyMakeUp[])Enum.GetValues(typeof(FamilyMakeUp));
            double total = 0;
            foreach (var m in order) total += Weight(catalog, m);
            double roll = random.NextDouble() * total;
            FamilyMakeUp last = order[0];
            foreach (var m in order)
            {
                double w = Weight(catalog, m);
                if (w <= 0) continue;
                last = m;
                if (roll < w) return m;
                roll -= w;
            }
            return last;
        }

        private static double Weight(ArrivalCatalog c, FamilyMakeUp m)
        {
            double w;
            return c.MakeUpWeights.TryGetValue(m, out w) ? w : 0;
        }

        private static void AddCouple(ArrivalCatalog c, SimRandom r, List<FamilyMemberPlan> list, AgeRange age)
        {
            list.Add(Person(c, r, Sex.Female, age));
            list.Add(Person(c, r, Sex.Male, age));
        }

        private static void AddChildren(ArrivalCatalog c, SimRandom r, List<FamilyMemberPlan> list, AgeRange count, AgeRange age)
        {
            int n = r.NextInt(count.Min, count.Max + 1);
            for (int i = 0; i < n; i++) list.Add(Person(c, r, RandomSex(r), age));
        }

        private static FamilyMemberPlan Person(ArrivalCatalog c, SimRandom r, Sex sex, AgeRange age)
        {
            List<string> names = sex == Sex.Female ? c.FemaleNames : c.MaleNames;
            return new FamilyMemberPlan(names[r.NextInt(0, names.Count)], sex, r.NextInt(age.Min, age.Max + 1));
        }
    }
}
