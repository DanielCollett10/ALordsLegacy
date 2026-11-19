using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Growth;
using LordsLegacy.Sim.Homes;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Homes
{
    /// <summary>T-591: village counts of households by level for the top bar tooltip (HL-26), read through GrowthReport.</summary>
    public class HouseholdLevelCountsTests
    {
        private static SimWorld Flat()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            return SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
        }

        [Test]
        public void Of_NoFamilies_ZeroAtEveryDefinedLevel()
        {
            HouseholdLevelCounts c = HouseholdLevelCounts.Of(Flat());
            Assert.AreEqual(HouseholdLevels.All.Count, c.Lines.Count, "one line per defined level");
            Assert.AreEqual(0, c.Total);
            Assert.AreEqual(0, c.CountAt(1));
            Assert.AreEqual(0, c.CountAt(2));
        }

        [Test]
        public void Of_ThreeFamiliesOneRaised_CountsEachLevelAndTooltipText()
        {
            // HL-26 acceptance: two level 1 households and one level 2 household.
            SimWorld w = Flat();
            w.People.AddFamily(1, "Ash", 0, 0);
            w.People.AddFamily(2, "Birch", 0, 0);
            w.People.AddFamily(3, "Cedar", 0, 0).RaiseLevel(2);
            HouseholdLevelCounts c = HouseholdLevelCounts.Of(w);
            Assert.AreEqual(2, c.CountAt(1));
            Assert.AreEqual(1, c.CountAt(2));
            Assert.AreEqual(3, c.Total);
            Assert.AreEqual("Level 1 Household: 2\nLevel 2 Settled household: 1", c.TooltipText);
        }

        [Test]
        public void Of_UndefinedLevel_CountIsZero()
        {
            Assert.AreEqual(0, HouseholdLevelCounts.Of(Flat()).CountAt(9));
        }

        [Test]
        public void Of_FamilyInPassage_IsStillCounted()
        {
            SimWorld w = Flat();
            w.People.AddFamily(1, "Ash", 0, 0).Passage = FamilyPassage.Leaving;
            Assert.AreEqual(1, HouseholdLevelCounts.Of(w).CountAt(1));
        }

        [Test]
        public void GrowthReport_Levels_MatchesCounts()
        {
            SimWorld w = Flat();
            w.People.AddFamily(1, "Ash", 0, 0);
            w.People.AddFamily(2, "Birch", 0, 0).RaiseLevel(2);
            HouseholdLevelCounts c = GrowthReport.Of(w).Levels;
            Assert.AreEqual(1, c.CountAt(1));
            Assert.AreEqual(1, c.CountAt(2));
        }

        // Real command path: a level 2 household after UpgradeHousehold (its needs met by fixture food and fence).
        [Test]
        public void AfterUpgradeHousehold_CountMovesFromLevelOneToLevelTwo()
        {
            SimWorld w = Flat();
            Family f = w.People.AddFamily(1, "Ash", 0, 0);
            HomeRecord home = w.Homes.Add(100, 0);
            Assert.IsTrue(w.Homes.MoveIn(home, f));
            f.AllotmentFoodTypes = 2;
            w.Footprints.Add(101, ModuleTypes.Fence, PointMm.FromMetres(5, 5), 0);
            w.Homes.SetFence(home, 101);
            Assert.AreEqual(1, HouseholdLevelCounts.Of(w).CountAt(1));
            Assert.IsTrue(w.Execute(new UpgradeHousehold(1)).IsAccepted);
            HouseholdLevelCounts c = HouseholdLevelCounts.Of(w);
            Assert.AreEqual(0, c.CountAt(1));
            Assert.AreEqual(1, c.CountAt(2));
        }
    }
}
