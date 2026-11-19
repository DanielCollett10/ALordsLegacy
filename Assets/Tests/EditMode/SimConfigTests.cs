using LordsLegacy.EditorTools.Scenes;
using LordsLegacy.Game;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LordsLegacy.Tests.EditMode
{
    /// <summary>SimConfig defaults equal the code defaults (T-169, decision 0019).</summary>
    public class SimConfigTests
    {
        private const double Eps = 1e-9;

        [Test]
        public void AssetExistsAndIsIdempotent()
        {
            var a = SimConfigGenerator.EnsureAsset();
            var b = SimConfigGenerator.EnsureAsset();
            Assert.IsNotNull(a);
            Assert.AreSame(a, b);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SimConfig>(SimConfigGenerator.AssetPath));
        }

        [Test]
        public void AssetTuningEqualsCodeDefaults()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<SimConfig>(SimConfigGenerator.AssetPath);
            Assert.IsNotNull(cfg, "Run LordsLegacy/Config/Create default SimConfig");
            AssertTuningEquals(cfg.BuildTuning(), new SimTuning());
        }

        [Test]
        public void DefaultSimConfigBuildsCodeDefaultTuning()
        {
            var cfg = ScriptableObject.CreateInstance<SimConfig>();
            try
            {
                AssertTuningEquals(cfg.BuildTuning(), new SimTuning());
                Assert.AreEqual(30, cfg.BuildTuning().RealMinutesPerGameDayAt1x, Eps);
                Assert.AreEqual(1.74, cfg.BuildTuning().WalkSpeedMPerGameMinute, Eps);
                Assert.AreEqual(1.04, cfg.BuildTuning().OxSpeedMPerGameMinute, Eps);
                Assert.IsFalse(cfg.BuildTuning().RestDayEnabled);
                Assert.AreEqual(120, cfg.BuildTuning().SundayMealMinutes);
                Assert.AreEqual(6, cfg.BuildTuning().MinSleepHours, Eps);
                Assert.AreEqual(1.25, cfg.BuildRoadCatalog().All[0].SpeedMultiplier, Eps);
            }
            finally { Object.DestroyImmediate(cfg); }
        }

        private static void AssertTuningEquals(SimTuning t, SimTuning d)
        {
            foreach (var p in typeof(SimTuning).GetProperties())
            {
                if (!p.CanWrite) continue;
                object x = p.GetValue(t), y = p.GetValue(d);
                if (x is int[] xa) CollectionAssert.AreEqual((int[])y, xa, p.Name);
                else if (x is double xd) Assert.AreEqual((double)y, xd, Eps, p.Name);
                else Assert.AreEqual(y, x, p.Name);
            }
        }

        [Test]
        public void MaxBuildersPerSiteComesFromConfig()
        {
            var cfg = ScriptableObject.CreateInstance<SimConfig>();
            try
            {
                Assert.AreEqual(new SimTuning().MaxBuildersPerSite, cfg.BuildTuning().MaxBuildersPerSite);
                cfg.maxBuildersPerSite = 2;
                Assert.AreEqual(2, cfg.BuildTuning().MaxBuildersPerSite);
            }
            finally { Object.DestroyImmediate(cfg); }
        }

        [Test]
        public void CoppiceWeaverWattleFieldsReachTuning()
        {
            var cfg = ScriptableObject.CreateInstance<SimConfig>();
            try
            {
                var d = new SimTuning();
                var t0 = cfg.BuildTuning();
                Assert.AreEqual(d.CoppiceRodsPerCut, t0.CoppiceRodsPerCut, "cw-1 default: rods per cut");
                Assert.AreEqual(d.WattlePanelStore, t0.WattlePanelStore, "cw-1 default: panel store");
                Assert.AreEqual(d.ForagerHutBasketSlots, t0.ForagerHutBasketSlots, "cw-1 default: forager basket slots");
                Assert.AreEqual(d.HandCarryDaub, t0.HandCarryDaub, "default: daub hand load");
                Assert.AreEqual(d.DaubPerDay, t0.DaubPerDay, "default: daub per day");
                Assert.AreEqual(d.DaubPitStore, t0.DaubPitStore, "default: daub pit store");
                cfg.hazelStoolCount = 11; cfg.willowStoolCount = 12; cfg.coppiceRodsPerCut = 13; cfg.coppiceRegrowDays = 14;
                cfg.coppiceMinutesPerRod = 15; cfg.coppiceHutStorePerKind = 16; cfg.handCarryRods = 17;
                cfg.handCarryBaskets = 18; cfg.handCarryPanels = 19; cfg.weaverWillowPerBasket = 20;
                cfg.weaverBasketsPerDay = 21; cfg.weaverWillowStock = 22; cfg.weaverBasketStore = 23;
                cfg.wattleHazelPerPanel = 24; cfg.wattleWillowPerPanel = 25; cfg.wattlePanelsPerDay = 26;
                cfg.wattleHazelStock = 27; cfg.wattleWillowStock = 28; cfg.wattlePanelStore = 29;
                cfg.foragerHutBasketSlots = 30; cfg.granaryBasketSlots = 31;
                cfg.handCarryDaub = 32; cfg.daubPerDay = 33; cfg.daubPitStore = 34;
                var t = cfg.BuildTuning();
                Assert.AreEqual(11, t.HazelStoolCount); Assert.AreEqual(12, t.WillowStoolCount);
                Assert.AreEqual(13, t.CoppiceRodsPerCut); Assert.AreEqual(14, t.CoppiceRegrowDays);
                Assert.AreEqual(15, t.CoppiceMinutesPerRod); Assert.AreEqual(16, t.CoppiceHutStorePerKind);
                Assert.AreEqual(17, t.HandCarryRods); Assert.AreEqual(18, t.HandCarryBaskets); Assert.AreEqual(19, t.HandCarryPanels);
                Assert.AreEqual(20, t.WeaverWillowPerBasket); Assert.AreEqual(21, t.WeaverBasketsPerDay);
                Assert.AreEqual(22, t.WeaverWillowStock); Assert.AreEqual(23, t.WeaverBasketStore);
                Assert.AreEqual(24, t.WattleHazelPerPanel); Assert.AreEqual(25, t.WattleWillowPerPanel);
                Assert.AreEqual(26, t.WattlePanelsPerDay); Assert.AreEqual(27, t.WattleHazelStock);
                Assert.AreEqual(28, t.WattleWillowStock); Assert.AreEqual(29, t.WattlePanelStore);
                Assert.AreEqual(30, t.ForagerHutBasketSlots); Assert.AreEqual(31, t.GranaryBasketSlots);
                Assert.AreEqual(32, t.HandCarryDaub); Assert.AreEqual(33, t.DaubPerDay); Assert.AreEqual(34, t.DaubPitStore);
            }
            finally { Object.DestroyImmediate(cfg); }
        }

        [Test]
        public void DefaultSimConfigBuildsCodeAreaCatalog()
        {
            var cfg = ScriptableObject.CreateInstance<SimConfig>();
            try
            {
                var cat = cfg.BuildAreaCatalog();
                Assert.AreEqual(AreaTypes.All.Count, cat.All.Count);
                for (int i = 0; i < cat.All.Count; i++)
                {
                    var a = cat.All[i]; var d = AreaTypes.All[i];
                    Assert.AreEqual(d.Id, a.Id);
                    Assert.AreEqual(d.MinAreaM2, a.MinAreaM2, Eps);
                    Assert.AreEqual(d.MaxAreaM2, a.MaxAreaM2, Eps);
                    Assert.AreEqual(d.MaxSlopeDegrees.HasValue, a.MaxSlopeDegrees.HasValue);
                    Assert.AreEqual(d.NearRoadReachM, a.NearRoadReachM, Eps);
                }
            }
            finally { Object.DestroyImmediate(cfg); }
        }

        [Test]
        public void DeconstructSharesComeFromConfig()
        {
            var cfg = ScriptableObject.CreateInstance<SimConfig>();
            try
            {
                Assert.AreEqual(new SimTuning().DeconstructTimeShare, cfg.BuildTuning().DeconstructTimeShare, Eps);
                Assert.AreEqual(new SimTuning().DeconstructRefundShare, cfg.BuildTuning().DeconstructRefundShare, Eps);
                cfg.deconstructTimeShare = 0.25f;
                cfg.deconstructRefundShare = 0.75f;
                Assert.AreEqual(0.25, cfg.BuildTuning().DeconstructTimeShare, Eps);
                Assert.AreEqual(0.75, cfg.BuildTuning().DeconstructRefundShare, Eps);
            }
            finally { Object.DestroyImmediate(cfg); }
        }

        [Test]
        public void FoodAndForagingFieldsReachTuning()
        {
            var cfg = ScriptableObject.CreateInstance<SimConfig>();
            try
            {
                cfg.startingMeat = 123; cfg.adultRations = 1.5f; cfg.childRations = 0.25f; cfg.marketRangeM = 700f;
                cfg.bushBaskets = 4; cfg.bushRegrowDays = 20; cfg.foragerMinAge = 10; cfg.foragerHutCapacity = 40;
                cfg.gatherMinutesPerBasket = 45; cfg.handCarryItems = 12;
                var t = cfg.BuildTuning();
                Assert.AreEqual(123, t.StartingMeat);
                Assert.AreEqual(1.5, t.AdultRations, Eps);
                Assert.AreEqual(0.25, t.ChildRations, Eps);
                Assert.AreEqual(700, t.MarketRangeM, Eps);
                Assert.AreEqual(4, t.BushBaskets);
                Assert.AreEqual(20, t.BushRegrowDays);
                Assert.AreEqual(10, t.ForagerMinAge);
                Assert.AreEqual(40, t.ForagerHutCapacity);
                Assert.AreEqual(45, t.GatherMinutesPerBasket);
                Assert.AreEqual(12, t.HandCarryItems);
            }
            finally { Object.DestroyImmediate(cfg); }
        }

        [Test]
        public void GrowthDefaultsEqualSimTuning()
        {
            var cfg = ScriptableObject.CreateInstance<SimConfig>();
            try
            {
                var t = cfg.BuildTuning();
                var d = new SimTuning();
                Assert.AreEqual(d.HungryDaysToLeave, t.HungryDaysToLeave);
                Assert.AreEqual(d.ArrivalFoodDays, t.ArrivalFoodDays);
                Assert.AreEqual(d.ArrivalNoHungerDays, t.ArrivalNoHungerDays);
                Assert.AreEqual(d.ArrivalGapDays, t.ArrivalGapDays);
                Assert.AreEqual(d.MapEdgeToleranceM, t.MapEdgeToleranceM, Eps);
                Assert.AreEqual(d.OxLoadLogs, t.OxLoadLogs);
                Assert.AreEqual(1, t.OxLoadLogs);
            }
            finally { Object.DestroyImmediate(cfg); }
        }

        private static void AssertTimberEquals(SimTuning t, SimTuning d)
        {
            Assert.AreEqual(d.StartingPlanks, t.StartingPlanks);
            Assert.AreEqual(d.HandCarryPlanks, t.HandCarryPlanks);
            Assert.AreEqual(d.OxLoadBeams, t.OxLoadBeams);
            Assert.AreEqual(d.HewingBeamsPerLog, t.HewingBeamsPerLog);
            Assert.AreEqual(d.HewingLogsPerDay, t.HewingLogsPerDay);
            Assert.AreEqual(d.HewingStockCapacity, t.HewingStockCapacity);
            Assert.AreEqual(d.HewingStoreCapacity, t.HewingStoreCapacity);
            Assert.AreEqual(d.SawingPlanksPerBeam, t.SawingPlanksPerBeam);
            Assert.AreEqual(d.SawingBeamsPerDay, t.SawingBeamsPerDay);
            Assert.AreEqual(d.SawingStockCapacity, t.SawingStockCapacity);
            Assert.AreEqual(d.SawingStoreCapacity, t.SawingStoreCapacity);
            Assert.AreEqual(d.WorkshopShiftMinutes, t.WorkshopShiftMinutes);
            Assert.AreEqual(d.WorkshopDefaultReserve, t.WorkshopDefaultReserve);
            Assert.AreEqual(10, t.HewingStoreCapacity);
            Assert.AreEqual(10, t.HewingStockCapacity);
            Assert.AreEqual(10, t.SawingStockCapacity);
            Assert.AreEqual(d.StorageYardWidthM, t.StorageYardWidthM, 1e-6);
            Assert.AreEqual(d.StorageYardDepthM, t.StorageYardDepthM, 1e-6);
            Assert.AreEqual(d.StorageYardLogs, t.StorageYardLogs);
            Assert.AreEqual(d.StorageYardMinutes, t.StorageYardMinutes);
            Assert.AreEqual(d.StorageYardCapacity, t.StorageYardCapacity);
            Assert.AreEqual(d.StorageYardFamilySlots, t.StorageYardFamilySlots);
            Assert.AreEqual(d.YardCollectThreshold, t.YardCollectThreshold, 1e-6);
        }

        [Test]
        public void TimberChainDefaultsEqualSimTuning()
        {
            var cfg = ScriptableObject.CreateInstance<SimConfig>();
            try { AssertTimberEquals(cfg.BuildTuning(), new SimTuning()); }
            finally { Object.DestroyImmediate(cfg); }
        }

        [Test]
        public void CommittedAssetHoldsTimberChainValues()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<SimConfig>(SimConfigGenerator.AssetPath);
            Assert.IsNotNull(cfg, "Run LordsLegacy/Config/Create default SimConfig");
            AssertTimberEquals(cfg.BuildTuning(), new SimTuning());
        }

        [Test]
        public void TimberChainValuesFlowIntoTuning()
        {
            var cfg = ScriptableObject.CreateInstance<SimConfig>();
            try
            {
                cfg.startingPlanks = 33; cfg.handCarryPlanks = 4; cfg.oxLoadBeams = 2; cfg.hewingLogsPerDay = 5;
                cfg.sawingStoreCapacity = 77; cfg.workshopShiftMinutes = 480; cfg.workshopDefaultReserve = 9;
                cfg.storageYardCapacity = 123; cfg.storageYardFamilySlots = 3; cfg.yardCollectThreshold = 0.75f;
                var t = cfg.BuildTuning();
                Assert.AreEqual(33, t.StartingPlanks);
                Assert.AreEqual(4, t.HandCarryPlanks);
                Assert.AreEqual(2, t.OxLoadBeams);
                Assert.AreEqual(5, t.HewingLogsPerDay);
                Assert.AreEqual(77, t.SawingStoreCapacity);
                Assert.AreEqual(480, t.WorkshopShiftMinutes);
                Assert.AreEqual(9, t.WorkshopDefaultReserve);
                Assert.AreEqual(123, t.StorageYardCapacity);
                Assert.AreEqual(3, t.StorageYardFamilySlots);
                Assert.AreEqual(0.75, t.YardCollectThreshold, 1e-6);
            }
            finally { Object.DestroyImmediate(cfg); }
        }

        [Test]
        public void CommittedAssetHoldsGrowthValues()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<SimConfig>(SimConfigGenerator.AssetPath);
            Assert.IsNotNull(cfg, "Run LordsLegacy/Config/Create default SimConfig");
            var t = cfg.BuildTuning();
            Assert.AreEqual(1, t.OxLoadLogs, "SimConfig.asset must hold OxLoadLogs 1");
            Assert.AreEqual(new SimTuning().HungryDaysToLeave, t.HungryDaysToLeave);
            Assert.AreEqual(new SimTuning().MapEdgeToleranceM, t.MapEdgeToleranceM, Eps);
        }

        [Test]
        public void AssetModuleRecipesEqualCodeDefaults()
        {
            var recipes = AssetDatabase.LoadAssetAtPath<SimConfig>(SimConfigGenerator.AssetPath).BuildModuleRecipes();
            Assert.AreEqual(ModuleTypes.All.Count, recipes.Count);
            foreach (var d in ModuleTypes.All)
            {
                Assert.IsTrue(recipes.ContainsKey(d.Id), d.Id);
                Assert.AreEqual(d.ConstructionRecipe.Logs, recipes[d.Id].Logs, d.Id);
                Assert.AreEqual(d.ConstructionRecipe.BuildWorkerMinutes, recipes[d.Id].BuildWorkerMinutes, d.Id);
            }
        }

        [Test]
        public void AssetRoadTypesEqualCodeDefaults()
        {
            var cat = AssetDatabase.LoadAssetAtPath<SimConfig>(SimConfigGenerator.AssetPath).BuildRoadCatalog();
            Assert.AreEqual(RoadTypes.All.Count, cat.All.Count);
            for (int i = 0; i < cat.All.Count; i++)
            {
                var a = cat.All[i]; var d = RoadTypes.All[i];
                Assert.AreEqual(d.Id, a.Id);
                Assert.AreEqual(d.WidthM, a.WidthM, Eps);
                Assert.AreEqual(d.MaxGrade, a.MaxGrade, Eps);
                Assert.AreEqual(d.SpeedMultiplier, a.SpeedMultiplier, Eps);
                Assert.AreEqual(d.VisualId, a.VisualId);
            }
        }

        [Test]
        public void AssetAreaTypesEqualCodeDefaults()
        {
            var cat = AssetDatabase.LoadAssetAtPath<SimConfig>(SimConfigGenerator.AssetPath).BuildAreaCatalog();
            Assert.AreEqual(AreaTypes.All.Count, cat.All.Count);
            for (int i = 0; i < cat.All.Count; i++)
            {
                var a = cat.All[i]; var d = AreaTypes.All[i];
                Assert.AreEqual(d.Id, a.Id);
                Assert.AreEqual(d.NamePrefix, a.NamePrefix);
                Assert.AreEqual(d.Kind, a.Kind);
                Assert.AreEqual(d.MinPoints, a.MinPoints);
                Assert.AreEqual(d.MaxPoints, a.MaxPoints);
                Assert.AreEqual(d.MinAreaM2, a.MinAreaM2, Eps);
                Assert.AreEqual(d.MaxAreaM2, a.MaxAreaM2, Eps);
                Assert.AreEqual(d.MaxSlopeDegrees.HasValue, a.MaxSlopeDegrees.HasValue);
                if (d.MaxSlopeDegrees.HasValue) Assert.AreEqual(d.MaxSlopeDegrees.Value, a.MaxSlopeDegrees.Value, Eps);
                Assert.AreEqual(d.ClearsTrees, a.ClearsTrees);
                Assert.AreEqual(d.BlocksRoads, a.BlocksRoads);
                Assert.AreEqual(d.NearRoadReachM, a.NearRoadReachM, Eps);
            }
        }
    }
}
