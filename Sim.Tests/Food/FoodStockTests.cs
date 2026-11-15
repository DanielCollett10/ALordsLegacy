using System.Linq;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Food
{
    /// <summary>Raw food counts by type for the top bar tooltip (T-419, FD-04).</summary>
    public class FoodStockTests
    {
        private const long Adult = -4_000_000;
        private static PointMm P(long x, long z) => new PointMm(x, z);

        private static SimWorld Started()
        {
            var layout = new StartLayout(
                P(100000, 100000), 0, P(0, 0), P(6000, 0), P(-6000, 0), new PointMm[0],
                new[]
                {
                    new StartFamily("Ash", P(-8000, -4000), new[] { new StartCitizen("A", Sex.Male, Adult) }),
                }, 2);
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning { StartingMeat = 0 });
            StartLayoutApplier.Apply(w, layout);
            return w;
        }

        [Test]
        public void CountsByType_ListsEveryFoodTypeIncludingZeros_WithRawCountsAndRations()
        {
            SimWorld w = Started();
            w.Inventory.Produce(w.Camp.FoodStoreLocation, Items.Meat, 4);
            w.Inventory.Produce(w.Camp.FoodStoreLocation, Items.Berries, 2);

            var counts = FoodStats.CountsByType(w);

            CollectionAssert.AreEqual(new[] { Items.Meat.Id, Items.Berries.Id, Items.Mushrooms.Id },
                counts.Select(c => c.Item.Id).ToArray());
            Assert.AreEqual(4, counts[0].Count);
            Assert.AreEqual(12, counts[0].Rations, "meat is 3 rations each");
            Assert.AreEqual(2, counts[1].Count);
            Assert.AreEqual(2, counts[1].Rations);
            Assert.AreEqual(0, counts[2].Count, "mushrooms listed even at zero");
        }

        [Test]
        public void CountsByType_SumsEveryLocation_LikeTotalRations()
        {
            SimWorld w = Started();
            w.Inventory.Produce(w.Camp.FoodStoreLocation, Items.Meat, 1);
            Family ash = w.People.Families.First();
            w.Inventory.Produce(Larders.ForFamily(w, ash), Items.Mushrooms, 5);

            Assert.AreEqual(FoodStats.TotalRations(w), FoodStats.CountsByType(w).Sum(c => c.Rations));
        }
    }
}
