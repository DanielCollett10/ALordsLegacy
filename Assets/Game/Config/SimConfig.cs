using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Calendar;
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

        [Header("Road types (RD-04, RD-05)")]
        public List<RoadTypeConfig> roadTypes = DefaultRoadTypes();

        [Header("Area types (AR-02, AR-13, AR-14, AR-16, AR-24)")]
        public List<AreaTypeConfig> areaTypes = DefaultAreaTypes();

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
            WalkSpeedMPerGameMinute = D(walkSpeedMPerGameMinute), OxSpeedMPerGameMinute = D(oxSpeedMPerGameMinute),
            StandardLogLengthM = D(standardLogLengthM), TrunkLengthMinM = D(trunkLengthMinM),
            TrunkLengthMaxM = D(trunkLengthMaxM), StartingOxen = startingOxen,
            RoadSampleSpacingM = D(roadSampleSpacingM), RoadMinPointSpacingM = D(roadMinPointSpacingM),
            RoadTreeClearanceM = D(roadTreeClearanceM), CurveFlattenSpacingM = D(curveFlattenSpacingM),
            SlopeSampleGridM = D(slopeSampleGridM), OverlapToleranceM2 = D(overlapToleranceM2),
            SpatialIndexCellM = D(spatialIndexCellM),
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
