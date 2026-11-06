using System;

namespace LordsLegacy.Sim.Calendar
{
    /// <summary>
    /// Seasonal sunrise and sunset. Daylight length follows a smooth yearly cosine curve:
    /// <see cref="MaxDaylightHours"/> at the summer solstice (day 172), <see cref="MinDaylightHours"/>
    /// at the winter solstice (day 355), and solar noon is always 12:00.
    /// Immutable and pure: it holds only the two plain configuration values, no state.
    /// Day numbers are 1-based from 1 January, as in <see cref="GameTime.DayOfYear"/>.
    /// </summary>
    public sealed class Daylight
    {
        /// <summary>Day of year of the summer solstice (21 June in a 365-day year).</summary>
        public const int SummerSolsticeDay = 172;

        private const int DaysPerYear = 365;
        private const double SolarNoonHour = 12.0;

        /// <summary>Daylight hours at the winter solstice.</summary>
        public double MinDaylightHours { get; }

        /// <summary>Daylight hours at the summer solstice.</summary>
        public double MaxDaylightHours { get; }

        /// <param name="minDaylightHours">Shortest day, in hours. Must be greater than 0.</param>
        /// <param name="maxDaylightHours">Longest day, in hours. Must be less than 24 and at least the minimum.</param>
        public Daylight(double minDaylightHours, double maxDaylightHours)
        {
            if (!(minDaylightHours > 0 && minDaylightHours <= maxDaylightHours && maxDaylightHours < 24))
                throw new ArgumentOutOfRangeException(nameof(minDaylightHours),
                    "Need 0 < min <= max < 24 hours.");
            MinDaylightHours = minDaylightHours;
            MaxDaylightHours = maxDaylightHours;
        }

        /// <summary>Sunrise and sunset for a day of the year (1 = 1 January, 365 = 31 December).</summary>
        public SunTimes For(int dayOfYear)
        {
            if (dayOfYear < 1 || dayOfYear > DaysPerYear)
                throw new ArgumentOutOfRangeException(nameof(dayOfYear));

            double mid = (MinDaylightHours + MaxDaylightHours) / 2.0;
            double amplitude = (MaxDaylightHours - MinDaylightHours) / 2.0;
            double phase = 2.0 * Math.PI * (dayOfYear - SummerSolsticeDay) / DaysPerYear;
            double length = mid + amplitude * Math.Cos(phase);
            return new SunTimes(SolarNoonHour - length / 2.0, SolarNoonHour + length / 2.0);
        }

        /// <summary>True when the sun is up at the given game time.</summary>
        public bool IsDaylight(GameTime time)
        {
            double hour = HourOfDay(time, 0);
            SunTimes sun = For(time.DayOfYear);
            return hour >= sun.Sunrise && hour < sun.Sunset;
        }

        /// <summary>
        /// Position of the sun between sunrise and sunset: 0 at sunrise, 1 at sunset,
        /// below 0 before sunrise and above 1 after sunset (night).
        /// </summary>
        /// <param name="time">Game time to measure.</param>
        /// <param name="tickFraction">Fraction (0-1) of the current tick already elapsed, for smooth rendering.
        /// One tick is one game minute.</param>
        public double SunProgress(GameTime time, double tickFraction = 0)
        {
            double hour = HourOfDay(time, tickFraction);
            SunTimes sun = For(time.DayOfYear);
            return (hour - sun.Sunrise) / sun.DaylightHours;
        }

        private static double HourOfDay(GameTime time, double tickFraction)
        {
            return time.Hour + (time.Minute + tickFraction) / 60.0;
        }
    }
}
