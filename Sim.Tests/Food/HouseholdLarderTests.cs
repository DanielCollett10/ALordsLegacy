using System.Linq;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Homes;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Food
{
    /// <summary>Read-only larder numbers for the house panel (T-420, FD-10, FD-16).</summary>
    public class HouseholdLarderTests
    {
        private const long Adult = -4_000_000;

        private static SimWorld World()
        {
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());
            w.People.AddFamily(1, "Archer", 10, 10);
            w.People.AddCitizen(1, "Alda", Sex.Female, Adult, 1, 10, 10);
            w.People.AddCitizen(2, "Bran", Sex.Male, Adult, 1, 10, 10);
            return w;
        }

        [Test]
        public void CountsOf_ListsEveryFoodTypeInCatalogOrder_WithZeros_AndCreatesNoLarder()
        {
            SimWorld w = World();
            Family f = w.People.Families.First();
            var counts = HouseholdLarder.CountsOf(w, f);
            CollectionAssert.AreEqual(new[] { Items.Meat.Id, Items.Berries.Id, Items.Mushrooms.Id },
                counts.Select(c => c.Item.Id).ToArray());
            Assert.IsTrue(counts.All(c => c.Count == 0));
            Assert.AreEqual(0, f.LarderLocationId, "A query must not create a larder.");
        }

        [Test]
        public void DaysLeft_IsRationsHeld_OverDailyNeed()
        {
            SimWorld w = World();
            Family f = w.People.Families.First();
            w.Inventory.Produce(Larders.ForFamily(w, f), Items.Meat, 10);
            var counts = HouseholdLarder.CountsOf(w, f);
            Assert.AreEqual(10, counts.Single(c => c.Item == Items.Meat).Count);
            double need = 2 * w.Tuning.AdultRations;
            Assert.AreEqual(10 * Items.Meat.RationsPerItem / need, HouseholdLarder.DaysLeft(w, f), 1e-9);
        }

        [Test]
        public void Rations_IncludesThePartOfAnOpenedItem()
        {
            SimWorld w = World();
            Family f = w.People.Families.First();
            f.CarriedRations = 0.5;
            Assert.AreEqual(0.5, HouseholdLarder.Rations(w, f), 1e-9);
        }

        [Test]
        public void HousedFamily_ReadsTheHouseLarderNotTheCampLarder()
        {
            SimWorld w = World();
            Family f = w.People.Families.First();
            w.Inventory.Produce(Larders.ForFamily(w, f), Items.Meat, 7);
            HomeRecord home = w.Homes.Add(500, 501);
            Assert.IsTrue(w.Homes.MoveIn(home, f));
            w.Inventory.Produce(Larders.ForHome(w, home), Items.Berries, 3);

            var counts = HouseholdLarder.CountsOf(w, f);
            Assert.AreEqual(0, counts.Single(c => c.Item == Items.Meat).Count, "The camp meat stays in the camp larder; only the house larder is read.");
            Assert.AreEqual(3, counts.Single(c => c.Item == Items.Berries).Count);
        }

        [Test]
        public void DaysLeft_IsInfinityWhenNobodyEats()
        {
            SimWorld w = World();
            Family empty = w.People.AddFamily(2, "Empty", 10, 10);
            Assert.IsTrue(double.IsPositiveInfinity(HouseholdLarder.DaysLeft(w, empty)));
        }
    }
}
