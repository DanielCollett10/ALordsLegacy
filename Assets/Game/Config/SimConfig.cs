using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;
using UnityEngine;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Every Sim tunable on one asset (decision 0019, tuning.md "Inspector config"). <see cref="SimHost"/> reads it
    /// once at start; edits take effect the next time Play starts. Defaults equal the code defaults.
    /// Presentation values stay on ViewSettings.
    /// </summary>
    [CreateAssetMenu(fileName = "SimConfig", menuName = "Lords Legacy/Sim Config")]
    public sealed class SimConfig : ScriptableObject
    {
        /// <summary>One road type's values (RD-04).</summary>
        [Serializable]
        public sealed class RoadTypeConfig
        {
            /// <summary>Stable id.</summary>
            public string id;
            [Tooltip("RD-04 full road width, metres")] [Range(0.5f, 12f)] public float widthM = 2.5f;
            [Tooltip("RD-05 max grade %, rise over run per 1 m step")] [Range(1f, 100f)] public float maxGradePercent = 15f;
            [Tooltip("ROAD-10-A walking speed multiplier on this road (decision 0023)")] [Range(0.5f, 5f)] public float speedMultiplier = 1.25f;
            [Tooltip("Id the view uses to pick the look of this road")] public string visualId;

            /// <summary>Builds the Sim definition.</summary>
            public RoadTypeDef ToDef() => new RoadTypeDef(id, D(widthM), D(maxGradePercent) / 100.0, D(speedMultiplier), visualId);
        }

        /// <summary>One area type's values (AR-02, AR-13, AR-14, AR-16, AR-24).</summary>
        [Serializable]
        public sealed class AreaTypeConfig
        {
            /// <summary>Stable id.</summary>
            public string id;
            [Tooltip("Prefix for generated names")] public string namePrefix;
            [Tooltip("Building areas block each other; work areas may overlap")] public AreaKind kind;
            [Tooltip("AR-02 fewest vertices")] [Range(3, 64)] public int minPoints = 4;
            [Tooltip("AR-02 most vertices")] [Range(3, 64)] public int maxPoints = 8;
            [Tooltip("AR-13 smallest size, m2")] [Range(1f, 10000f)] public float minAreaM2 = 100f;
            [Tooltip("AR-13 largest size, m2")] [Range(1f, 200000f)] public float maxAreaM2 = 5000f;
            [Tooltip("AR-14 tick to apply a slope limit")] public bool limitSlope = true;
            [Tooltip("AR-14 steepest allowed slope, degrees")] [Range(0f, 90f)] public float maxSlopeDegrees = 10f;
            [Tooltip("Confirming removes trees inside")] public bool clearsTrees = true;
            [Tooltip("AR-16 roads may not cross the area")] public bool blocksRoads = true;
            [Tooltip("AR-24 NearRoad reach, metres")] [Range(0f, 100f)] public float nearRoadReachM = 10f;

            /// <summary>Builds the Sim definition.</summary>
            public AreaTypeDef ToDef() => new AreaTypeDef(id, namePrefix, kind, minPoints, maxPoints, D(minAreaM2), D(maxAreaM2),
                limitSlope ? D(maxSlopeDegrees) : (double?)null, clearsTrees, blocksRoads, D(nearRoadReachM));
        }

        [Header("Time")]
        [Tooltip("TIME-A2 real minutes per game day at 1x (30 = the 30-minute day, decision 0023)")] [Range(1f, 120f)] public float realMinutesPerGameDayAt1x = 30f;
        [Tooltip("TIME-A3 selectable speed multipliers")] public int[] speeds = { 1, 2, 4, 8, 16, 32 };
        [Tooltip("CLK-05 most ticks per frame")] [Range(1, 64)] public int maxTicksPerFrame = 8;
        [Tooltip("Start month (no rule ID)")] [Range(1, 12)] public int startMonth = 3;
        [Tooltip("Start day of month (no rule ID)")] [Range(1, 31)] public int startDay = 1;
        [Tooltip("Start hour (no rule ID)")] [Range(0, 23)] public int startHour = 6;
        [Tooltip("Start minute (no rule ID)")] [Range(0, 59)] public int startMinute = 0;
        [Tooltip("Weekday of the start date (no rule ID)")] public Weekday startWeekday = Weekday.Monday;
        [Tooltip("TIME-01 days per game year; must match the calendar (365)")] [Range(365, 365)] public int daysPerYear = 365;
        [Tooltip("TIME-A5 shortest daylight, hours")] [Range(0f, 24f)] public float minDaylightHours = 8f;
        [Tooltip("TIME-A5 longest daylight, hours")] [Range(0f, 24f)] public float maxDaylightHours = 16f;
        [Tooltip("FOOD-A4 Sunday is a rest day (off until a church exists)")] public bool restDayEnabled = false;
        [Tooltip("PPL-21 minutes a Sunday meal takes off the end of Sunday's working day")] [Range(0, 720)] public int sundayMealMinutes = 120;

        [Header("Movement")]
        [Tooltip("MOV-20 walking speed, metres per game minute (1.74 = 5 km/h on screen at 1x)")] [Range(0.1f, 10f)] public float walkSpeedMPerGameMinute = 1.74f;
        [Tooltip("MOV-21 ox speed, metres per game minute (1.04 = 3 km/h on screen at 1x)")] [Range(0.1f, 10f)] public float oxSpeedMPerGameMinute = 1.04f;
        [Tooltip("PPL-26 shortest sleep a citizen takes each night, game hours")] [Range(0f, 24f)] public float minSleepHours = 6f;

        [Header("Construction")]
        [Tooltip("CN-23 most odd-jobbers building one site at once (PLACEHOLDER rule value)")] [Range(1, 16)] public int maxBuildersPerSite = 4;
        [Tooltip("CN-53 deconstruction takes this share of the recipe's build minutes (PLACEHOLDER)")] [Range(0f, 1f)] public float deconstructTimeShare = 0.5f;
        [Tooltip("CN-54 share of the recipe's logs returned as a ground pile, rounded down")] [Range(0f, 1f)] public float deconstructRefundShare = 0.5f;

        [Header("Timber and oxen")]
        [Tooltip("TMB-04 standard log length, metres")] [Range(1f, 10f)] public float standardLogLengthM = 4f;
        [Tooltip("TMB-06 shortest usable trunk, metres")] [Range(1f, 40f)] public float trunkLengthMinM = 12f;
        [Tooltip("TMB-06 longest usable trunk, metres")] [Range(1f, 60f)] public float trunkLengthMaxM = 20f;
        [Tooltip("Oxen owned at start (tuning.md, no rule ID)")] [Range(0, 50)] public int startingOxen = 2;

        [Header("Roads and areas")]
        [Tooltip("RD-03 sample spacing along a road, metres")] [Range(0.1f, 10f)] public float roadSampleSpacingM = 1f;
        [Tooltip("RD-02 shortest distance between road points, metres")] [Range(0.1f, 10f)] public float roadMinPointSpacingM = 1f;
        [Tooltip("RD-11 tree clearance beyond each road edge, metres")] [Range(0f, 10f)] public float roadTreeClearanceM = 0.5f;
        [Tooltip("AR-05 curve flatten spacing, metres")] [Range(0.1f, 10f)] public float curveFlattenSpacingM = 1f;
        [Tooltip("AR-14 slope sample grid, metres")] [Range(0.25f, 20f)] public float slopeSampleGridM = 2f;
        [Tooltip("AR-15 overlap tolerance, m2")] [Range(0f, 10f)] public float overlapToleranceM2 = 0.1f;
        [Tooltip("Decision 0012 spatial index cell size, metres")] [Range(4f, 256f)] public float spatialIndexCellM = 32f;

        [Header("Food and foraging (M14, tuning.md \"Food\", \"Markets\", \"Foraging\")")]
        [Tooltip("FD-03 meat in the camp food store at the start, items")] [Range(0, 10000)] public int startingMeat = 400;
        [Tooltip("FD-04 rations an adult (14+) eats per day")] [Range(0f, 5f)] public float adultRations = 1f;
        [Tooltip("FD-04 rations a child eats per day")] [Range(0f, 5f)] public float childRations = 0.5f;
        [Tooltip("FD-11, MKT-06 camp market walking range, metres")] [Range(0f, 5000f)] public float marketRangeM = 630f;
        [Tooltip("FD-21 baskets a forage bush holds when full")] [Range(0, 20)] public int bushBaskets = 3;
        [Tooltip("FD-21 days an emptied bush takes to regrow to full")] [Range(1, 365)] public int bushRegrowDays = 14;
        [Tooltip("FD-23 youngest age that takes forage tasks, years")] [Range(0, 120)] public int foragerMinAge = 8;
        [Tooltip("FD-23b food items the forager hut store holds")] [Range(0, 10000)] public int foragerHutCapacity = 30;
        [Tooltip("FD-24 game minutes to pick one basket")] [Range(1, 600)] public int gatherMinutesPerBasket = 30;
        [Tooltip("FD-24, MKT-03e most items carried by hand on one trip")] [Range(1, 100)] public int handCarryItems = 10;

        [Header("Growth (GR-03, GR-21, WCO-07; PLACEHOLDER values)")]
        [Tooltip("GR-03 consecutive Hungry days after which a household leaves")] [Range(1, 3650)] public int hungryDaysToLeave = 30;
        [Tooltip("GR-21 days of food per household a village must hold for a family to arrive")] [Range(0, 3650)] public int arrivalFoodDays = 30;
        [Tooltip("GR-21 days with no Hungry household before a family may arrive")] [Range(0, 3650)] public int arrivalNoHungerDays = 7;
        [Tooltip("GR-21 minimum days between two arrivals")] [Range(0, 3650)] public int arrivalGapDays = 7;
        [Tooltip("GR-21 tolerance for deciding a point is on the map edge, metres")] [Range(0f, 50f)] public float mapEdgeToleranceM = 2f;
        [Tooltip("WCO-07 most logs an ox carries per trip")] [Range(1, 100)] public int oxLoadLogs = 1;

        [Header("Timber chain (HW-05, PL-14, PL-16, PL-21; decisions 0046, 0047)")]
        [Tooltip("PL-14 planks in the camp General goods store at the start")] [Range(0, 10000)] public int startingPlanks = 10;
        [Tooltip("PL-16 planks an odd-jobber carries to a site per trip")] [Range(1, 100)] public int handCarryPlanks = 2;
        [Tooltip("PL-21 most beams an ox carries per trip")] [Range(1, 100)] public int oxLoadBeams = 1;
        [Tooltip("HW-05 beams a hewing station makes from one log")] [Range(1, 100)] public int hewingBeamsPerLog = 1;
        [Tooltip("HW-05 logs one family hews per day")] [Range(1, 100)] public int hewingLogsPerDay = 3;
        [Tooltip("HW-05 most logs waiting in a hewing station's input stock")] [Range(1, 1000)] public int hewingStockCapacity = 10;
        [Tooltip("HW-05 most beams a hewing station's output store holds")] [Range(1, 1000)] public int hewingStoreCapacity = 10;
        [Tooltip("HW-05 planks a sawpit saws from one beam")] [Range(1, 100)] public int sawingPlanksPerBeam = 3;
        [Tooltip("HW-05 beams one family saws per day")] [Range(1, 100)] public int sawingBeamsPerDay = 3;
        [Tooltip("HW-05 most beams waiting in a sawpit's input stock")] [Range(1, 1000)] public int sawingStockCapacity = 10;
        [Tooltip("HW-05 most planks a sawpit's output store holds")] [Range(1, 1000)] public int sawingStoreCapacity = 20;
        [Tooltip("HW-05 workshop shift length, game minutes; 0 = the daylight work window")] [Range(0, 1440)] public int workshopShiftMinutes = 0;
        [Tooltip("PL-30b input a workshop leaves in storage (0 = none)")] [Range(0, 1000)] public int workshopDefaultReserve = 5;

        [Header("Storage yard (ST-03, ST-42, ST-43; M18)")]
        [Tooltip("ST-03 yard footprint width, metres")] [Range(1f, 100f)] public float storageYardWidthM = 8f;
        [Tooltip("ST-03 yard footprint depth, metres")] [Range(1f, 100f)] public float storageYardDepthM = 6f;
        [Tooltip("ST-03 logs a yard takes to build")] [Range(0, 100)] public int storageYardLogs = 8;
        [Tooltip("ST-03 game minutes a yard takes to build")] [Range(1, 100000)] public int storageYardMinutes = 480;
        [Tooltip("ST-03 most General goods a yard holds")] [Range(1, 100000)] public int storageYardCapacity = 300;
        [Tooltip("ST-43 families that move goods for one yard")] [Range(0, 20)] public int storageYardFamilySlots = 2;
        [Tooltip("ST-42 share of a workshop store that must be full before the yard collects (0 = any free output)")] [Range(0f, 1f)] public float yardCollectThreshold = 0f;

        [Header("Coppice, weaver and wattle (M19, cw-1; MAT-14..24, MAT-53; PROPOSED tuning)")]
        [Tooltip("MAT-15 hazel stools scattered at new game")] [Range(0, 1000)] public int hazelStoolCount = 40;
        [Tooltip("MAT-15 willow stools scattered at new game")] [Range(0, 1000)] public int willowStoolCount = 20;
        [Tooltip("MAT-16 rods one cut takes from a stool")] [Range(1, 100)] public int coppiceRodsPerCut = 10;
        [Tooltip("MAT-16 days an emptied stool takes to regrow")] [Range(0, 3650)] public int coppiceRegrowDays = 30;
        [Tooltip("MAT-17 game minutes to cut one rod")] [Range(1, 600)] public int coppiceMinutesPerRod = 6;
        [Tooltip("MAT-14 most rods of each kind a coppice hut stores")] [Range(1, 10000)] public int coppiceHutStorePerKind = 40;
        [Tooltip("MAT-23 most rods a person carries by hand on one trip")] [Range(1, 100)] public int handCarryRods = 10;
        [Tooltip("MAT-23 most baskets a person carries by hand on one trip")] [Range(1, 100)] public int handCarryBaskets = 5;
        [Tooltip("MAT-23 most wattle panels a person carries by hand on one trip")] [Range(1, 100)] public int handCarryPanels = 2;
        [Tooltip("MAT-22 willow rods one basket takes")] [Range(1, 100)] public int weaverWillowPerBasket = 6;
        [Tooltip("MAT-22 baskets one family weaves per day")] [Range(1, 100)] public int weaverBasketsPerDay = 4;
        [Tooltip("MAT-24 most willow rods waiting in the weaver's input stock")] [Range(1, 1000)] public int weaverWillowStock = 30;
        [Tooltip("MAT-24 most baskets the weaver's output store holds")] [Range(1, 1000)] public int weaverBasketStore = 10;
        [Tooltip("MAT-22 hazel rods one wattle panel takes")] [Range(1, 100)] public int wattleHazelPerPanel = 6;
        [Tooltip("MAT-22 willow rods one wattle panel takes")] [Range(1, 100)] public int wattleWillowPerPanel = 2;
        [Tooltip("MAT-22 wattle panels one family makes per day")] [Range(1, 100)] public int wattlePanelsPerDay = 5;
        [Tooltip("MAT-24 most hazel rods waiting in the wattle yard's input stock")] [Range(1, 1000)] public int wattleHazelStock = 30;
        [Tooltip("MAT-24 most willow rods waiting in the wattle yard's input stock")] [Range(1, 1000)] public int wattleWillowStock = 10;
        [Tooltip("MAT-24 most wattle panels the wattle yard's output store holds")] [Range(1, 1000)] public int wattlePanelStore = 20;
        [Tooltip("MAT-53 baskets the forager hut keeps as kit, one per slot")] [Range(0, 100)] public int foragerHutBasketSlots = 5;
        [Tooltip("MAT-53 baskets the granary keeps as kit, one per family slot")] [Range(0, 100)] public int granaryBasketSlots = 2;

        [Header("Thatch and thatcher's hut (M20, th-1; MAT-30..39a, MAT-02; PROPOSED tuning)")]
        [Tooltip("MAT-39a reed beds scattered on low ground at new game")] [Range(0, 1000)] public int reedBedCount = 20;
        [Tooltip("MAT-34 sheaves one cut takes from a reed bed")] [Range(1, 100)] public int reedSheavesPerCut = 8;
        [Tooltip("MAT-34 days an emptied reed bed takes to regrow")] [Range(0, 3650)] public int reedRegrowDays = 90;
        [Tooltip("MAT-35 game minutes to cut one bundle of reed in winter")] [Range(1, 600)] public int thatchCutMinutesPerBundle = 30;
        [Tooltip("MAT-35 how many times slower cutting is out of season")] [Range(1, 20)] public int thatchOffSeasonFactor = 4;
        [Tooltip("MAT-38 days a stook cures before the sheaves are dry")] [Range(0, 365)] public int thatchCureDays = 7;
        [Tooltip("MAT-30 most thatch sheaves a thatcher hut stores")] [Range(1, 1000)] public int thatcherHutStore = 40;
        [Tooltip("MAT-30 thatcher hut footprint width, metres")] [Range(1f, 100f)] public float thatcherHutWidthM = 6f;
        [Tooltip("MAT-30 thatcher hut footprint depth, metres")] [Range(1f, 100f)] public float thatcherHutDepthM = 4f;
        [Tooltip("MAT-30 logs a thatcher hut takes to build")] [Range(0, 100)] public int thatcherHutLogs = 8;
        [Tooltip("MAT-30 game minutes a thatcher hut takes to build")] [Range(1, 100000)] public int thatcherHutMinutes = 480;
        [Tooltip("MAT-02 most thatch sheaves a person carries by hand on one trip")] [Range(1, 100)] public int handCarryThatch = 1;

        /// <summary>One module type's construction recipe (CN-02).</summary>
        [Serializable]
        public sealed class ModuleRecipeConfig
        {
            /// <summary>Module type id, e.g. module.woodcutting_hut.</summary>
            public string moduleId;
            [Tooltip("Logs taken from the stockpile to build")] [Range(0, 100)] public int logs;
            [Tooltip("Build work, worker-minutes")] [Range(0, 100000)] public int buildWorkerMinutes;
        }

        [Header("Road types (RD-04, RD-05)")]
        public List<RoadTypeConfig> roadTypes = DefaultRoadTypes();

        [Header("Area types (AR-02, AR-13, AR-14, AR-16, AR-24)")]
        public List<AreaTypeConfig> areaTypes = DefaultAreaTypes();

        [Header("Module recipes (CN-02)")]
        [Tooltip("Module construction recipes. NOT YET APPLIED: the Sim still reads ModuleTypes (code) until a module type catalog is wired.")]
        public List<ModuleRecipeConfig> moduleRecipes = DefaultModuleRecipes();

        /// <summary>Builds a SimTuning from these values.</summary>
        public SimTuning BuildTuning() => new SimTuning
        {
            RealMinutesPerGameDayAt1x = D(realMinutesPerGameDayAt1x),
            Speeds = speeds != null && speeds.Length > 0 ? (int[])speeds.Clone() : new[] { 1 },
            MaxTicksPerFrame = maxTicksPerFrame,
            StartMonth = startMonth, StartDay = startDay, StartHour = startHour, StartMinute = startMinute,
            StartWeekday = startWeekday, DaysPerYear = daysPerYear,
            MinDaylightHours = D(minDaylightHours), MaxDaylightHours = D(maxDaylightHours),
            RestDayEnabled = restDayEnabled, SundayMealMinutes = sundayMealMinutes, MinSleepHours = D(minSleepHours),
            MaxBuildersPerSite = maxBuildersPerSite,
            DeconstructTimeShare = D(deconstructTimeShare), DeconstructRefundShare = D(deconstructRefundShare),
            WalkSpeedMPerGameMinute = D(walkSpeedMPerGameMinute), OxSpeedMPerGameMinute = D(oxSpeedMPerGameMinute),
            StandardLogLengthM = D(standardLogLengthM), TrunkLengthMinM = D(trunkLengthMinM),
            TrunkLengthMaxM = D(trunkLengthMaxM), StartingOxen = startingOxen,
            RoadSampleSpacingM = D(roadSampleSpacingM), RoadMinPointSpacingM = D(roadMinPointSpacingM),
            RoadTreeClearanceM = D(roadTreeClearanceM), CurveFlattenSpacingM = D(curveFlattenSpacingM),
            SlopeSampleGridM = D(slopeSampleGridM), OverlapToleranceM2 = D(overlapToleranceM2),
            SpatialIndexCellM = D(spatialIndexCellM),
            StartingMeat = startingMeat, AdultRations = D(adultRations), ChildRations = D(childRations),
            MarketRangeM = D(marketRangeM), BushBaskets = bushBaskets, BushRegrowDays = bushRegrowDays,
            ForagerMinAge = foragerMinAge, ForagerHutCapacity = foragerHutCapacity,
            GatherMinutesPerBasket = gatherMinutesPerBasket, HandCarryItems = handCarryItems,
            HungryDaysToLeave = hungryDaysToLeave, ArrivalFoodDays = arrivalFoodDays,
            ArrivalNoHungerDays = arrivalNoHungerDays, ArrivalGapDays = arrivalGapDays,
            MapEdgeToleranceM = D(mapEdgeToleranceM), OxLoadLogs = oxLoadLogs,
            StartingPlanks = startingPlanks, HandCarryPlanks = handCarryPlanks, OxLoadBeams = oxLoadBeams,
            HewingBeamsPerLog = hewingBeamsPerLog, HewingLogsPerDay = hewingLogsPerDay,
            HewingStockCapacity = hewingStockCapacity, HewingStoreCapacity = hewingStoreCapacity,
            SawingPlanksPerBeam = sawingPlanksPerBeam, SawingBeamsPerDay = sawingBeamsPerDay,
            SawingStockCapacity = sawingStockCapacity, SawingStoreCapacity = sawingStoreCapacity,
            WorkshopShiftMinutes = workshopShiftMinutes, WorkshopDefaultReserve = workshopDefaultReserve,
            StorageYardWidthM = D(storageYardWidthM), StorageYardDepthM = D(storageYardDepthM),
            StorageYardLogs = storageYardLogs, StorageYardMinutes = storageYardMinutes,
            StorageYardCapacity = storageYardCapacity, StorageYardFamilySlots = storageYardFamilySlots,
            YardCollectThreshold = D(yardCollectThreshold),
            HazelStoolCount = hazelStoolCount, WillowStoolCount = willowStoolCount,
            CoppiceRodsPerCut = coppiceRodsPerCut, CoppiceRegrowDays = coppiceRegrowDays,
            CoppiceMinutesPerRod = coppiceMinutesPerRod, CoppiceHutStorePerKind = coppiceHutStorePerKind,
            HandCarryRods = handCarryRods, HandCarryBaskets = handCarryBaskets, HandCarryPanels = handCarryPanels,
            WeaverWillowPerBasket = weaverWillowPerBasket, WeaverBasketsPerDay = weaverBasketsPerDay,
            WeaverWillowStock = weaverWillowStock, WeaverBasketStore = weaverBasketStore,
            WattleHazelPerPanel = wattleHazelPerPanel, WattleWillowPerPanel = wattleWillowPerPanel,
            WattlePanelsPerDay = wattlePanelsPerDay, WattleHazelStock = wattleHazelStock,
            WattleWillowStock = wattleWillowStock, WattlePanelStore = wattlePanelStore,
            ForagerHutBasketSlots = foragerHutBasketSlots, GranaryBasketSlots = granaryBasketSlots,
            ReedBedCount = reedBedCount, ReedSheavesPerCut = reedSheavesPerCut, ReedRegrowDays = reedRegrowDays,
            ThatchCutMinutesPerBundle = thatchCutMinutesPerBundle, ThatchOffSeasonFactor = thatchOffSeasonFactor,
            ThatchCureDays = thatchCureDays, ThatcherHutStore = thatcherHutStore,
            ThatcherHutWidthM = D(thatcherHutWidthM), ThatcherHutDepthM = D(thatcherHutDepthM),
            ThatcherHutLogs = thatcherHutLogs, ThatcherHutMinutes = thatcherHutMinutes, HandCarryThatch = handCarryThatch,
        };

        /// <summary>Builds the road catalog from these values.</summary>
        public RoadTypeCatalog BuildRoadCatalog()
        {
            var defs = new List<RoadTypeDef>();
            if (roadTypes != null) foreach (var t in roadTypes) if (t != null && !string.IsNullOrEmpty(t.id)) defs.Add(t.ToDef());
            return new RoadTypeCatalog(defs);
        }

        /// <summary>Builds the area catalog from these values.</summary>
        public AreaTypeCatalog BuildAreaCatalog()
        {
            var defs = new List<AreaTypeDef>();
            if (areaTypes != null) foreach (var t in areaTypes) if (t != null && !string.IsNullOrEmpty(t.id)) defs.Add(t.ToDef());
            return new AreaTypeCatalog(defs);
        }

        /// <summary>Builds the module recipes from these values, keyed by module id (unused by the Sim until wired).</summary>
        public Dictionary<string, ConstructionRecipe> BuildModuleRecipes()
        {
            var map = new Dictionary<string, ConstructionRecipe>();
            if (moduleRecipes != null)
                foreach (var r in moduleRecipes)
                    if (r != null && !string.IsNullOrEmpty(r.moduleId)) map[r.moduleId] = new ConstructionRecipe(r.logs, r.buildWorkerMinutes);
            return map;
        }

        /// <summary>Code-default module recipes as editable config.</summary>
        public static List<ModuleRecipeConfig> DefaultModuleRecipes()
        {
            var list = new List<ModuleRecipeConfig>();
            foreach (var d in ModuleTypes.All)
                list.Add(new ModuleRecipeConfig
                {
                    moduleId = d.Id, logs = d.ConstructionRecipe.Logs, buildWorkerMinutes = d.ConstructionRecipe.BuildWorkerMinutes
                });
            return list;
        }

        /// <summary>
        /// Resets every value to the code defaults. Not named Reset(): Unity calls Reset() on every new
        /// ScriptableObject, and this method creates one, which would recurse forever. Field initialisers
        /// already give the defaults on creation, so no Reset() hook is needed.
        /// </summary>
        public void ResetToDefaults()
        {
            var fresh = CreateInstance<SimConfig>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fresh), this);
            DestroyImmediate(fresh);
        }

        /// <summary>Code-default road types as editable config.</summary>
        public static List<RoadTypeConfig> DefaultRoadTypes()
        {
            var list = new List<RoadTypeConfig>();
            foreach (var d in RoadTypes.All)
                list.Add(new RoadTypeConfig
                {
                    id = d.Id, widthM = (float)d.WidthM, maxGradePercent = (float)(d.MaxGrade * 100.0),
                    speedMultiplier = (float)d.SpeedMultiplier, visualId = d.VisualId
                });
            return list;
        }

        /// <summary>Code-default area types as editable config.</summary>
        public static List<AreaTypeConfig> DefaultAreaTypes()
        {
            var list = new List<AreaTypeConfig>();
            foreach (var d in AreaTypes.All)
                list.Add(new AreaTypeConfig
                {
                    id = d.Id, namePrefix = d.NamePrefix, kind = d.Kind, minPoints = d.MinPoints, maxPoints = d.MaxPoints,
                    minAreaM2 = (float)d.MinAreaM2, maxAreaM2 = (float)d.MaxAreaM2,
                    limitSlope = d.MaxSlopeDegrees.HasValue, maxSlopeDegrees = (float)(d.MaxSlopeDegrees ?? 10.0),
                    clearsTrees = d.ClearsTrees, blocksRoads = d.BlocksRoads, nearRoadReachM = (float)d.NearRoadReachM
                });
            return list;
        }

        // float -> double without float noise (0.87f becomes 0.87, not 0.8700000047...).
        private static double D(float f) => (double)(decimal)f;
    }
}
