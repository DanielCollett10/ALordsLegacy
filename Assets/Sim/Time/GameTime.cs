using System.Globalization;

namespace LordsLegacy.Sim.Calendar
{
    /// <summary>A calendar date and time of day produced by <see cref="GameCalendar.At"/>.</summary>
    public readonly struct GameTime
    {
        private static readonly string[] MonthNames =
            new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

        private static readonly string[] WeekdayNames =
            new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        /// <summary>Year number, starting at 1. Increments on 1 January.</summary>
        public int Year { get; }
        /// <summary>Month 1-12.</summary>
        public int Month { get; }
        /// <summary>Day of month, from 1.</summary>
        public int DayOfMonth { get; }
        /// <summary>Day of year counted from 1 Jan = 1.</summary>
        public int DayOfYear { get; }
        /// <summary>Day of the week.</summary>
        public Weekday Weekday { get; }
        /// <summary>Hour 0-23.</summary>
        public int Hour { get; }
        /// <summary>Minute 0-59.</summary>
        public int Minute { get; }
        /// <summary>Season of the year.</summary>
        public Season Season { get; }

        /// <summary>True on Sundays.</summary>
        public bool IsSunday => Weekday == Weekday.Sunday;

        public GameTime(int year, int month, int dayOfMonth, int dayOfYear, Weekday weekday,
            int hour, int minute, Season season)
        {
            Year = year;
            Month = month;
            DayOfMonth = dayOfMonth;
            DayOfYear = dayOfYear;
            Weekday = weekday;
            Hour = hour;
            Minute = minute;
            Season = season;
        }

        /// <summary>Formats as e.g. "Mon 1 Mar · Spring · Year 1 · 06:00".</summary>
        public string Format()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0} {1} {2} · {3} · Year {4} · {5:00}:{6:00}",
                WeekdayNames[(int)Weekday], DayOfMonth, MonthNames[Month - 1], Season, Year, Hour, Minute);
        }

        public override string ToString() => Format();
    }
}
