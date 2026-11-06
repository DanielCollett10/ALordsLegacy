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
        public double RealMinutesPerGameDayAt1x { get; set; } = 15;

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
        public bool RestDayEnabled { get; set; } = true;

        /// <summary>
        /// Walking speed off-road. Unit: metres per game minute (5 km/h on screen at 1×). Rule: TIME-04,
        /// decision 0004.
        /// </summary>
        public double WalkSpeedMPerGameMinute { get; set; } = 0.87;

        /// <summary>
        /// Ox speed. Unit: metres per game minute (3 km/h on screen at 1×). Rule: TIME-04, decision 0004.
        /// </summary>
        public double OxSpeedMPerGameMinute { get; set; } = 0.52;

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
        /// Game minutes that pass per real second at 1× speed. Unit: game minutes per real second.
        /// Derived: 1440 ÷ (<see cref="RealMinutesPerGameDayAt1x"/> × 60); 1.6 at the default of 15.
        /// Rule: decision 0004.
        /// </summary>
        public double GameMinutesPerRealSecondAt1x =>
            MinutesPerGameDay / (RealMinutesPerGameDayAt1x * 60.0);

        /// <summary>Game minutes in one game day (24 × 60). Unit: game minutes. Rule: CLK-01.</summary>
        private const int MinutesPerGameDay = 1440;
    }
}
