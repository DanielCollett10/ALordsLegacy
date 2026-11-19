using System.Linq;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Homes;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Homes
{
    /// <summary>Household level defs and Family.Level (T-588, HL-20, HL-21, HL-23, HSE-09, HSE-10).</summary>
    public class HouseholdLevelTests
    {
        private static (SimWorld w, MapData map) Make()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            return (SimWorld.FromMapData(map, new SimTuning { StartHour = 8 }), map);
        }

        [Test]
        public void Defs_LevelsOneAndTwoLoad_WithTheirNeeds()
        {
            Assert.AreEqual(2, HouseholdLevels.All.Count);
            Assert.AreEqual(2, HouseholdLevels.MaxLevel);
            Assert.AreEqual(0, HouseholdLevels.Get(1).Needs.Count);
            Assert.IsNull(HouseholdLevels.Get(3));
            var needs = HouseholdLevels.Get(2).Needs;
            Assert.AreEqual(4, needs.Count);
            Assert.AreEqual(NeedKind.DiverseFood, needs[0].Kind);
            Assert.AreEqual(0, needs[0].Amount, "the threshold is tuning, not level data");
            Assert.AreEqual(2, needs[0].Required(new LordsLegacy.Sim.Tuning.SimTuning()), "HSE-09 default");
            Assert.AreEqual(3, needs[0].Required(new LordsLegacy.Sim.Tuning.SimTuning { DiverseFoodTypes = 3 }));
            Assert.AreEqual(1, needs.Count(n => n.Kind == NeedKind.FencedPlot));
            Assert.AreEqual(2, needs.Count(n => n.Kind == NeedKind.AlwaysMet));
        }

        [Test]
        public void Defs_UpgradeCostIsEmptyInM21()
        {
            foreach (HouseholdLevelDef d in HouseholdLevels.All) Assert.IsTrue(d.UpgradeCost.IsEmpty, "level " + d.Level);
        }

        [Test]
        public void NewFamily_LevelIsOne()
        {
            var (w, _) = Make();
            Assert.AreEqual(1, w.People.AddFamily(1, "Ash", 0, 0).Level);
        }

        [Test]
        public void Level_NeverDrops()
        {
            var (w, _) = Make();
            Family f = w.People.AddFamily(1, "Ash", 0, 0);
            f.RaiseLevel(2);
            f.RaiseLevel(1);
            Assert.AreEqual(2, f.Level);
        }

        [Test]
        public void Level_KeptWhenMovingHouse()
        {
            var (w, _) = Make();
            Family f = w.People.AddFamily(1, "Ash", 0, 0);
            f.RaiseLevel(2);
            HomeRecord a = w.Homes.Add(100, 200);
            Assert.IsTrue(w.Homes.MoveIn(a, f));
            HouseRemoval.Evict(w, 100);
            Assert.IsTrue(f.IsHomeless);
            Assert.AreEqual(2, f.Level, "kept when evicted");
            HomeRecord b = w.Homes.Add(101, 201);
            Assert.IsTrue(w.Homes.MoveIn(b, f));
            Assert.AreEqual(2, f.Level, "kept in the new house");
        }

        [Test]
        public void SaveRoundTrip_KeepsLevelTwoAndLevelOneFamilies()
        {
            var (w, map) = Make();
            w.People.AddFamily(1, "Ash", 0, 0).RaiseLevel(2);
            w.People.AddFamily(2, "Oak", 0, 0);
            byte[] bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(bytes, map);
            Assert.AreEqual(2, back.People.FindFamily(1).Level);
            Assert.AreEqual(1, back.People.FindFamily(2).Level);
            Assert.AreEqual(bytes, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash));
        }

        [Test]
        public void SaveVersion_IsAtLeast40()
        {
            Assert.GreaterOrEqual(SaveWriter.CurrentVersion, 40);
        }
    }
}
