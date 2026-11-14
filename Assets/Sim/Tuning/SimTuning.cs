using LordsLegacy.Sim.Calendar;

namespace LordsLegacy.Sim.Tuning
{
    /// <summary>
    /// Every PLACEHOLDER balancing number the Sim needs, in one place. Defaults and units come from
    /// docs/design/tuning.md (the human owns that file). Nothing else in the Sim may hard-code these.
    /// Override values with an object initializer, e.g. <c>new SimTuning { StartMonth = 9 }</c>.
    /// Instances hold no static state; each one has its own arrays.
    /// </summary>
    public sealed class SimTuning
    {
        /// <summary>
        /// Real minutes taken by one game day at 1× speed. Unit: real minutes. Rule: TIME-A2.
        /// Only the Unity tick pacer reads this directly; the Sim uses
        /// <see cref="GameMinutesPerRealSecondAt1x"/>.
        /// </summary>
        public double RealMinutesPerGameDayAt1x { get; set; } = 30;

        /// <summary>
        /// Selectable game speed multipliers, in order. Unit: × (multiplier of 1×). Rule: TIME-A3.
        /// </summary>
        public int[] Speeds { get; set; } = new[] { 1, 2, 4, 8, 16, 32 };

        /// <summary>
        /// Most Sim ticks run in one frame; above this the game slows down instead of skipping.
        /// Unit: ticks (game minutes). Rule: CLK-05.
        /// </summary>
        public int MaxTicksPerFrame { get; set; } = 8;

        /// <summary>
        /// Month of the start date, 1-12. Unit: calendar month. Rule: gdd-amendments Time (no rule ID).
        /// Default 3 (March) with <see cref="StartDay"/> 1 gives 1 Mar, Year 1.
        /// </summary>
        public int StartMonth { get; set; } = 3;

        /// <summary>
        /// Day of the start date, from 1. Unit: day of month. Rule: gdd-amendments Time (no rule ID).
        /// </summary>
        public int StartDay { get; set; } = 1;

        /// <summary>
        /// Hour of the start time, 0-23. Unit: game hours. Rule: gdd-amendments Time (no rule ID).
        /// </summary>
        public int StartHour { get; set; } = 6;

        /// <summary>
        /// Minute of the start time, 0-59. Unit: game minutes. Rule: gdd-amendments Time (no rule ID).
        /// </summary>
        public int StartMinute { get; set; } = 0;

        /// <summary>
        /// Weekday of the start date. Unit: weekday. Rule: gdd-amendments Time (no rule ID).
        /// Default Monday, so 1 Mar Year 1 is a Monday.
        /// </summary>
        public Weekday StartWeekday { get; set; } = Weekday.Monday;

        /// <summary>
        /// How fast citizens age compared with the calendar. Unit: × calendar rate. Rule: POP-27 (PLACEHOLDER).
        /// </summary>
        public double AgeingRate { get; set; } = 2;

        /// <summary>
        /// Days in one game year. Unit: days. Rule: TIME-01. Must match the calendar's month table (365).
        /// </summary>
        public int DaysPerYear { get; set; } = 365;

        /// <summary>
        /// Shortest daylight in a day (winter). Unit: game hours. Rule: TIME-A5.
        /// </summary>
        public double MinDaylightHours { get; set; } = 8;

        /// <summary>
        /// Longest daylight in a day (summer). Unit: game hours. Rule: TIME-A5.
        /// </summary>
        public double MaxDaylightHours { get; set; } = 16;

        /// <summary>
        /// Whether Sunday is a rest day. Unit: switch (true = on). Rule: FOOD-A4.
        /// </summary>
        public bool RestDayEnabled { get; set; } = false;

        /// <summary>
        /// Minutes a Sunday meal takes on a rest day. Unit: game minutes. Rule: PPL-21 (decision 0023).
        /// Not read by the Sim yet; defined here so the value lives in one place.
        /// </summary>
        public int SundayMealMinutes { get; set; } = 120;

        /// <summary>
        /// Shortest sleep a citizen takes each night. Unit: game hours. Rule: PPL-26 (decision 0023).
        /// Not read by the Sim yet; defined here so the value lives in one place.
        /// </summary>
        public double MinSleepHours { get; set; } = 6;

        /// <summary>
        /// Walking speed off-road. Unit: metres per game minute (5 km/h on screen at 1× with the 30-minute
        /// day). Rule: MOV-20 (decision 0023).
        /// </summary>
        public double WalkSpeedMPerGameMinute { get; set; } = 1.74;

        /// <summary>
        /// Ox speed. Unit: metres per game minute (3 km/h on screen at 1× with the 30-minute day).
        /// Rule: MOV-21 (decision 0023).
        /// </summary>
        public double OxSpeedMPerGameMinute { get; set; } = 1.04;

        /// <summary>Most logs an ox carries per trip. Unit: logs. Rule: WCO-07.</summary>
        public int OxLoadLogs { get; set; } = 4;

        /// <summary>Most odd-jobbers building one construction site at once. Unit: people. Rule: CN-23 (PLACEHOLDER).</summary>
        public int MaxBuildersPerSite { get; set; } = 4;

        /// <summary>Time to load an ox. Unit: game minutes. Rule: woodcutting.md (tuning.md).</summary>
        public int OxLoadMinutes { get; set; } = 10;

        /// <summary>Time to unload an ox. Unit: game minutes. Rule: woodcutting.md (tuning.md).</summary>
        public int OxUnloadMinutes { get; set; } = 10;

        /// <summary>
        /// Milling spots are picked within this distance of the camp fire. Unit: metres. Rule: PPL-31.
        /// </summary>
        public double MillRadiusM { get; set; } = 25;

        /// <summary>Shortest stay at a milling spot. Unit: game minutes. Rule: PPL-31.</summary>
        public int MillStayMinMinutes { get; set; } = 20;

        /// <summary>Longest stay at a milling spot. Unit: game minutes. Rule: PPL-31.</summary>
        public int MillStayMaxMinutes { get; set; } = 90;

        /// <summary>
        /// Longest random delay after sunrise before a sleeper gets up, so people do not move in step.
        /// Unit: game minutes. Rule: PPL-31 ("start times are spread").
        /// </summary>
        public int WakeStaggerMaxMinutes { get; set; } = 30;

        /// <summary>
        /// Game minutes a citizen stays where a debug send-to left them, before the routine resumes.
        /// Unit: game minutes. Rule: PPL-33 (tuning.md "Debug send-to linger").
        /// </summary>
        public int DebugLingerMinutes { get; set; } = 30;

        /// <summary>
        /// The dispatcher runs on ticks that are a multiple of this. Unit: game minutes. Rule: tech spec section 6.
        /// </summary>
        public int DispatchIntervalMinutes { get; set; } = 10;

        /// <summary>
        /// Length of one standard log. Unit: metres. Rule: TMB-04.
        /// </summary>
        public double StandardLogLengthM { get; set; } = 4;

        /// <summary>
        /// Shortest usable trunk. Unit: metres. Rule: TMB-06 (12-20 m, i.e. 3-5 logs).
        /// </summary>
        public double TrunkLengthMinM { get; set; } = 12;

        /// <summary>
        /// Longest usable trunk. Unit: metres. Rule: TMB-06 (12-20 m, i.e. 3-5 logs).
        /// </summary>
        public double TrunkLengthMaxM { get; set; } = 20;

        /// <summary>
        /// Oxen owned at the start of a game. Unit: oxen. Rule: tuning.md "Starting oxen" (no rule ID).
        /// </summary>
        public int StartingOxen { get; set; } = 2;

        /// <summary>
        /// Seed of the world's random source (<c>SimWorld.Random</c>). Same seed, same sequence of routine choices.
        /// Unit: integer seed (no unit). Rule: CLK-07 (decision 0022). Read by the world at construction.
        /// </summary>
        public int WorldSeed { get; set; } = 12345;

        /// <summary>
        /// Distance between sampled points along a road centre line. Unit: metres. Rule: RD-03
        /// (tuning.md "Roads and areas"). Read by roads.
        /// </summary>
        public double RoadSampleSpacingM { get; set; } = 1;

        /// <summary>
        /// Shortest allowed distance between two consecutive road points. Unit: metres. Rule: RD-02
        /// (tuning.md "Roads and areas"). Read by roads.
        /// </summary>
        public double RoadMinPointSpacingM { get; set; } = 1;

        /// <summary>
        /// Clear ground kept beyond each road edge (half width plus this) free of trees. Unit: metres.
        /// Rule: RD-11 (tuning.md "Roads and areas"). Read by roads.
        /// </summary>
        public double RoadTreeClearanceM { get; set; } = 0.5;

        /// <summary>
        /// Spacing used to flatten curved area edges into straight segments. Unit: metres. Rule: AR-05
        /// (tuning.md "Roads and areas"). Read by areas.
        /// </summary>
        public double CurveFlattenSpacingM { get; set; } = 1;

        /// <summary>
        /// Grid spacing at which an area's footprint is sampled for slope checks. Unit: metres. Rule: AR-14
        /// (tuning.md "Roads and areas"). Read by areas.
        /// </summary>
        public double SlopeSampleGridM { get; set; } = 2;

        /// <summary>
        /// Side of one walk-grid cell. Unit: metres. Rule: decision 0022 (NavGrid). Read by navigation.
        /// </summary>
        public double NavCellSizeM { get; set; } = 2;

        /// <summary>
        /// Ground steeper than this at a cell centre cannot be walked. Unit: degrees. Rule: MOV-02, decision 0022.
        /// Read by navigation.
        /// </summary>
        public double MaxWalkSlopeDeg { get; set; } = 30;

        /// <summary>
        /// Overlap area allowed between two areas before they count as overlapping. Unit: square metres.
        /// Rule: AR-15 (tuning.md "Roads and areas"). Read by areas.
        /// </summary>
        public double OverlapToleranceM2 { get; set; } = 0.1;

        /// <summary>
        /// Cell size of the spatial index used for roads and areas. Unit: metres. Rule: decision 0012
        /// (tuning.md "Roads and areas"). Read by roads/areas.
        /// </summary>
        public double SpatialIndexCellM { get; set; } = 32;

        /// <summary>
        /// Game minutes that pass per real second at 1× speed. Unit: game minutes per real second.
        /// Derived: 1440 ÷ (<see cref="RealMinutesPerGameDayAt1x"/> × 60); 0.8 at the default of 30.
        /// Rule: decision 0004.
        /// </summary>
        public double GameMinutesPerRealSecondAt1x =>
            MinutesPerGameDay / (RealMinutesPerGameDayAt1x * 60.0);

        /// <summary>Game minutes in one game day (24 × 60). Unit: game minutes. Rule: CLK-01.</summary>
        private const int MinutesPerGameDay = 1440;
    }
}
