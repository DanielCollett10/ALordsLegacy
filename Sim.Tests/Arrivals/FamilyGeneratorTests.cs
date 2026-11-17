using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Arrivals;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.People;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Arrivals
{
    public class FamilyGeneratorTests
    {
        private static string Describe(FamilyPlan p)
        {
            return p.Surname + "|" + p.MakeUp + "|" + string.Join(",", p.Members.Select(m => m.Name + m.Sex + m.Age));
        }

        [Test]
        public void SameSeed_GivesSamePlans()
        {
            var cat = new ArrivalCatalog();
            var a = new SimRandom(42);
            var b = new SimRandom(42);
            for (int i = 0; i < 50; i++)
                Assert.AreEqual(Describe(FamilyGenerator.Make(cat, a, new List<string>())),
                                Describe(FamilyGenerator.Make(cat, b, new List<string>())));
        }

        [Test]
        public void UnusedSurnameChosenWhileAnyRemain()
        {
            var cat = new ArrivalCatalog();
            var rnd = new SimRandom(7);
            var used = new HashSet<string>(cat.Surnames.Take(cat.Surnames.Count - 1));
            string left = cat.Surnames.Last();
            for (int i = 0; i < 50; i++)
                Assert.AreEqual(left, FamilyGenerator.Make(cat, rnd, used).Surname);
        }

        [Test]
        public void AllSurnamesUsed_StillMakesAFamily()
        {
            var cat = new ArrivalCatalog();
            var plan = FamilyGenerator.Make(cat, new SimRandom(1), new HashSet<string>(cat.Surnames));
            Assert.Contains(plan.Surname, cat.Surnames);
        }

        [Test]
        public void MakeUpSharesWithinFivePointsOfWeights()
        {
            var cat = new ArrivalCatalog();
            var rnd = new SimRandom(2024);
            var counts = new Dictionary<FamilyMakeUp, int>();
            const int n = 1000;
            for (int i = 0; i < n; i++)
            {
                var m = FamilyGenerator.Make(cat, rnd, null).MakeUp;
                counts[m] = (counts.ContainsKey(m) ? counts[m] : 0) + 1;
            }
            double total = cat.MakeUpWeights.Values.Sum();
            foreach (var kv in cat.MakeUpWeights)
            {
                double share = (counts.ContainsKey(kv.Key) ? counts[kv.Key] : 0) / (double)n;
                Assert.AreEqual(kv.Value / total, share, 0.05, kv.Key.ToString());
            }
        }

        [Test]
        public void EveryPlanHasAnAdultAndValidShape()
        {
            var cat = new ArrivalCatalog();
            var rnd = new SimRandom(99);
            for (int i = 0; i < 1000; i++)
            {
                var p = FamilyGenerator.Make(cat, rnd, null);
                Assert.IsTrue(p.Members.Any(m => m.Age >= 14), Describe(p));
                int kids = p.Members.Count(m => m.Age < 14);
                if (p.MakeUp == FamilyMakeUp.CoupleWithChildren) Assert.That(kids, Is.InRange(2, 4));
                if (p.MakeUp == FamilyMakeUp.CoupleNoChildren) Assert.AreEqual(2, p.Members.Count);
                if (p.MakeUp == FamilyMakeUp.FamilyWithAdultChildren) Assert.AreEqual(0, kids);
            }
        }

        [Test]
        public void BirthTick_RoundTripsThroughAgeYears()
        {
            var m = new FamilyMemberPlan("A", Sex.Female, 10);
            long now = 1000000;
            var c = new Citizen(1, "A", Sex.Female, m.BirthTick(now, 2), 0, 0, 0);
            Assert.AreEqual(10.0, c.AgeYears(now, 2), 0.01);
        }
    }
}
