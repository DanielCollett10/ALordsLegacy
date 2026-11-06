using System;

namespace LordsLegacy.Sim.Calendar
{
    /// <summary>
    /// Turns a tick count (1 tick = 1 game minute) into a <see cref="GameTime"/>.
    /// Pure: built from plain values, no tuning dependency. 365-day year, no leap years.
    /// </summary>
    public sealed class GameCalendar
    {
        private const int MinutesPerDay = 1440;
        private static readonly int[] MonthLengths = new[] { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

        private readonly int _startDayIndex;      // 0-based day of year of the start
        private readonly int _startMinuteOfDay;
        private readonly Weekday _startWeekday;

        /// <summary>Days in a year; must equal the sum of the month table (365).</summary>
        public int DaysPerYear { get; }

        /// <param name="startMonth">1-12.</param>
        /// <param name="startDay">Day of month.</param>
        /// <param name="startHour">0-23.</param>
        /// <param name="startMinute">0-59.</param>
        /// <param name="startWeekday">Weekday of the start date.</param>
        /// <param name="daysPerYear">Must be 365; scaled calendars are unsupported.</param>
        public GameCalendar(int startMonth = 3, int startDay = 1, int startHour = 6, int startMinute = 0,
            Weekday startWeekday = Weekday.Monday, int daysPerYear = 365)
        {
            int total = 0;
            foreach (int l in MonthLengths) total += l;
            if (daysPerYear != total)
                throw new ArgumentException("DaysPerYear must be " + total + " to match the month table.", nameof(daysPerYear));
            if (startMonth < 1 || startMonth > 12) throw new ArgumentOutOfRangeException(nameof(startMonth));
            if (startDay < 1 || startDay > MonthLengths[startMonth - 1]) throw new ArgumentOutOfRangeException(nameof(startDay));
            if (startHour < 0 || startHour > 23) throw new ArgumentOutOfRangeException(nameof(startHour));
            if (startMinute < 0 || startMinute > 59) throw new ArgumentOutOfRangeException(nameof(startMinute));
            if (!Enum.IsDefined(typeof(Weekday), startWeekday)) throw new ArgumentOutOfRangeException(nameof(startWeekday));

            DaysPerYear = daysPerYear;
            _startWeekday = startWeekday;
            _startMinuteOfDay = startHour * 60 + startMinute;
            int idx = startDay - 1;
            for (int m = 0; m < startMonth - 1; m++) idx += MonthLengths[m];
            _startDayIndex = idx;
        }

        /// <summary>Returns the calendar date and time at the given tick (0 = start).</summary>
        public GameTime At(long tick)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
            long minutes = _startMinuteOfDay + tick;
            long daysSinceStart = minutes / MinutesPerDay;
            int minuteOfDay = (int)(minutes % MinutesPerDay);

            long absDay = _startDayIndex + daysSinceStart;
            int year = (int)(absDay / DaysPerYear) + 1;
            int dayIndex = (int)(absDay % DaysPerYear);

            int month = 0, rem = dayIndex;
            while (rem >= MonthLengths[month]) { rem -= MonthLengths[month]; month++; }

            var weekday = (Weekday)(int)(((int)_startWeekday + daysSinceStart) % 7);
            return new GameTime(year, month + 1, rem + 1, dayIndex + 1, weekday,
                minuteOfDay / 60, minuteOfDay % 60, SeasonOf(month + 1));
        }

        /// <summary>Season for a month 1-12.</summary>
        public static Season SeasonOf(int month)
        {
            if (month >= 3 && month <= 5) return Season.Spring;
            if (month >= 6 && month <= 8) return Season.Summer;
            if (month >= 9 && month <= 11) return Season.Autumn;
            return Season.Winter;
        }

        /// <summary>Formats the time at a tick, e.g. "Mon 1 Mar · Spring · Year 1 · 06:00".</summary>
        public string Format(long tick) => At(tick).Format();
    }
}
