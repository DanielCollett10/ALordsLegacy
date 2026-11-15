using System.Linq;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Food
{
    /// <summary>Daily eating at midnight and Hungry (T-416, FD-15, FD-16, FOOD-A9).</summary>
    public class EatingTests
    {
        private const long Adult = -4_000_000;
        private static PointMm P(long x, long z) => new PointMm(x, z);

        private static SimWorld Started()
        {
            var layout = new StartLayout(
                P(100000, 100000), 0, P(0, 0), P(6000, 0), P(-6000, 0), new PointMm[0],
                new[]
                {
                    new StartFamily("Ash", P(-8000, -4000), new[] { new StartCitizen("A", Sex.Male, Adult), new StartCitizen("B", Sex.Female, Adult), new StartCitizen("K", Sex.Male, 0) }),
                    new StartFamily("Birch", P(-8000, 4000), new[] { new StartCitizen("C", Sex.Female, Adult) }),
                }, 2);
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());
            StartLayoutApplier.Apply(w, layout);
            return w;
        }

        private static bool IsMidnight(SimWorld w) => w.Now.Hour == 0 && w.Now.Minute == 0;

        /// <summary>Steps until the tick just processed is a midnight (always moves at least one tick).</summary>
        private static void StepToMidnight(SimWorld w)
        {
            do w.Step(); while (!IsMidnight(w));
        }

        private static double LarderRations(SimWorld w, Family f) =>
            Larders.ForFamily(w, f).CountOf(Items.Meat.Id) * 3.0 + f.CarriedRations;

        [Test]
        public void Midnight_StockFallsByAdultsTimesOnePlusChildrenTimesHalf_PerDay()
        {
            SimWorld w = Started();
            Family ash = w.People.Families.First(), birch = w.People.Families.Last();
            w.Inventory.Produce(Larders.ForFamily(w, ash), Items.Meat, 10);
            w.Inventory.Produce(Larders.ForFamily(w, birch), Items.Meat, 10);
            for (int day = 1; day <= 3; day++)
            {
                StepToMidnight(w);
                Assert.AreEqual(30 - 2.5 * day, LarderRations(w, ash), 1e-9, "Ash day " + day);
                Assert.AreEqual(30 - 1.0 * day, LarderRations(w, birch), 1e-9, "Birch day " + day);
                Assert.IsFalse(ash.IsHungry);
                Assert.IsFalse(birch.IsHungry);
            }
        }

        [Test]
        public void Midnight_WholeItemsFirst_FractionIsCarried()
        {
            SimWorld w = Started();
            Family birch = w.People.Families.Last();
            w.Inventory.Produce(Larders.ForFamily(w, birch), Items.Meat, 2); // 6 rations, need 1/day
            StepToMidnight(w);
            Assert.AreEqual(1, Larders.ForFamily(w, birch).CountOf(Items.Meat.Id));
            Assert.AreEqual(2.0, birch.CarriedRations, 1e-9);
            StepToMidnight(w);
            Assert.AreEqual(1, Larders.ForFamily(w, birch).CountOf(Items.Meat.Id), "carried fraction is eaten before a new item");
            Assert.AreEqual(1.0, birch.CarriedRations, 1e-9);
        }

        [Test]
        public void Midnight_EmptyLarder_IsHungry_AndFedLarderIsNot()
        {
            SimWorld w = Started();
            Family ash = w.People.Families.First(), birch = w.People.Families.Last();
            w.Inventory.Produce(Larders.ForFamily(w, birch), Items.Meat, 1);
            StepToMidnight(w);
            Assert.IsTrue(ash.IsHungry);
            Assert.IsFalse(birch.IsHungry);
        }

        [Test]
        public void Midnight_ShortLarder_EatsWhatThereIsAndIsHungry_ThenRecoversWhenFed()
        {
            SimWorld w = Started();
            Family ash = w.People.Families.First();
            w.Inventory.Produce(Larders.ForFamily(w, ash), Items.Berries, 1); // 1 ration (if berries are 1) < 2.5
            StepToMidnight(w);
            Assert.IsTrue(ash.IsHungry);
            Assert.AreEqual(0, Larders.ForFamily(w, ash).Total);
            Assert.AreEqual(0, ash.CarriedRations, 1e-9);
            w.Inventory.Produce(Larders.ForFamily(w, ash), Items.Meat, 1);
            StepToMidnight(w);
            Assert.IsFalse(ash.IsHungry, "3 rations cover 2.5");
            Assert.AreEqual(0.5, ash.CarriedRations, 1e-9);
        }

        [Test]
        public void Hungry_DoesNotChangeWorkerTimingsOrPositions()
        {
            SimWorld fed = Started(), hungry = Started();
            foreach (Family f in fed.People.Families) fed.Inventory.Produce(Larders.ForFamily(fed, f), Items.Meat, 40);
            int ticks = 60 * 24 * 3;
            fed.Step(ticks); hungry.Step(ticks);
            Assert.IsTrue(hungry.People.Families.All(f => f.IsHungry));
            Assert.IsFalse(fed.People.Families.Any(f => f.IsHungry));
            var a = fed.People.Citizens.ToList(); var b = hungry.People.Citizens.ToList();
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].X, b[i].X, 0, "x " + a[i].Name);
                Assert.AreEqual(a[i].Z, b[i].Z, 0, "z " + a[i].Name);
                Assert.AreEqual(a[i].Activity, b[i].Activity, a[i].Name);
            }
            Assert.AreEqual(fed.WorkDay.WindowOf(fed, WorkDay.DayStart(fed)).Sunrise, hungry.WorkDay.WindowOf(hungry, WorkDay.DayStart(hungry)).Sunrise);
        }

        [Test]
        public void SaveRoundTrip_CarriedFractionAndHungryFlag_AreIdentical()
        {
            MapData map = SyntheticMap.CreateRamp();
            SimWorld w = Started();
            Family ash = w.People.Families.First(), birch = w.People.Families.Last();
            w.Inventory.Produce(Larders.ForFamily(w, birch), Items.Meat, 2);
            StepToMidnight(w);
            Assert.IsTrue(ash.IsHungry);
            Assert.AreEqual(2.0, birch.CarriedRations, 1e-9);
            byte[] first = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(first, map);
            Assert.IsTrue(back.People.FindFamily(ash.Id).IsHungry);
            Assert.IsFalse(back.People.FindFamily(birch.Id).IsHungry);
            Assert.AreEqual(2.0, back.People.FindFamily(birch.Id).CarriedRations, 1e-9);
            Assert.IsNull(SaveDiff.FirstDifference(first, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash)));
        }
    }
}
