using System;
using LordsLegacy.Sim.Calendar;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class GameCalendarTests
    {
        private const long Day = 1440;
        private static GameCalendar Default() => new GameCalendar();

        // Ticks from the 1 Mar 06:00 start to 1 Jan 00:00 of year 2.
        private static long TicksToNewYear() => (306 * Day) - 360;

        [Test]
        public void Tick0_IsDay60Year1WithStartWeekday()
        {
            var t = Default().At(0);
            Assert.AreEqual(60, t.DayOfYear);
            Assert.AreEqual(1, t.Year);
            Assert.AreEqual(3, t.Month);
            Assert.AreEqual(1, t.DayOfMonth);
            Assert.AreEqual(Weekday.Monday, t.Weekday);
            Assert.AreEqual(6, t.Hour);
            Assert.AreEqual(0, t.Minute);
            Assert.AreEqual(Season.Spring, t.Season);
        }

        [Test]
        public void Sundays_FallOn7_14_21_28Mar_AndMondayOn29()
        {
            var c = Default();
            foreach (int d in new[] { 7, 14, 21, 28 })
            {
                var t = c.At((d - 1) * Day);
                Assert.AreEqual(d, t.DayOfMonth);
                Assert.IsTrue(t.IsSunday, "Mar " + d);
            }
            var m = c.At(28 * Day);
            Assert.AreEqual(29, m.DayOfMonth);
            Assert.AreEqual(Weekday.Monday, m.Weekday);
            Assert.IsFalse(m.IsSunday);
        }

        [Test]
        public void NewYear_IsYear2Day1()
        {
            var c = Default();
            var t = c.At(TicksToNewYear());
            Assert.AreEqual(2, t.Year);
            Assert.AreEqual(1, t.DayOfYear);
            Assert.AreEqual(1, t.Month);
            Assert.AreEqual(1, t.DayOfMonth);
            Assert.AreEqual(0, t.Hour);
            var before = c.At(TicksToNewYear() - 1);
            Assert.AreEqual(1, before.Year);
            Assert.AreEqual(365, before.DayOfYear);
            Assert.AreEqual(31, before.DayOfMonth);
            Assert.AreEqual(12, before.Month);
            Assert.AreEqual(23, before.Hour);
            Assert.AreEqual(59, before.Minute);
        }

        [Test]
        public void Weekdays_ContinueAcrossYearBoundary()
        {
            var c = Default();
            var before = c.At(TicksToNewYear() - 1);
            var after = c.At(TicksToNewYear());
            Assert.AreEqual(((int)before.Weekday + 1) % 7, (int)after.Weekday);
            // 306 days after a Monday: 306 % 7 = 5 -> Saturday
            Assert.AreEqual(Weekday.Saturday, after.Weekday);
        }

        [Test]
        public void MonthBoundaries()
        {
            var c = Default();
            Assert.AreEqual(31, c.At(30 * Day).DayOfMonth);
            Assert.AreEqual(3, c.At(30 * Day).Month);
            var apr = c.At(31 * Day);
            Assert.AreEqual(4, apr.Month);
            Assert.AreEqual(1, apr.DayOfMonth);
            // Feb has 28 days: 28 Feb then 1 Mar of year 2.
            long feb28 = TicksToNewYear() + (31 + 27) * Day;
            Assert.AreEqual(28, c.At(feb28).DayOfMonth);
            Assert.AreEqual(2, c.At(feb28).Month);
            var mar = c.At(feb28 + Day);
            Assert.AreEqual(3, mar.Month);
            Assert.AreEqual(1, mar.DayOfMonth);
            Assert.AreEqual(60, mar.DayOfYear);
            Assert.AreEqual(2, mar.Year);
        }

        [Test]
        public void TimeOfDayRollsOver()
        {
            var t = Default().At(18 * 60);
            Assert.AreEqual(2, t.DayOfMonth);
            Assert.AreEqual(0, t.Hour);
            Assert.AreEqual(5, Default().At(5).Minute);
        }

        [TestCase(3, Season.Spring)]
        [TestCase(5, Season.Spring)]
        [TestCase(6, Season.Summer)]
        [TestCase(8, Season.Summer)]
        [TestCase(9, Season.Autumn)]
        [TestCase(11, Season.Autumn)]
        [TestCase(12, Season.Winter)]
        [TestCase(1, Season.Winter)]
        [TestCase(2, Season.Winter)]
        public void Seasons(int month, Season expected)
        {
            Assert.AreEqual(expected, GameCalendar.SeasonOf(month));
        }

        [Test]
        public void SeasonChangesAtMonthBoundaries()
        {
            var c = Default();
            Assert.AreEqual(Season.Spring, c.At(30 * Day).Season);                    // 31 Mar
            Assert.AreEqual(Season.Spring, c.At((31 + 30 + 31 - 1) * Day).Season);   // 31 May
            Assert.AreEqual(Season.Summer, c.At((31 + 30 + 31) * Day).Season);        // 1 Jun
            Assert.AreEqual(Season.Winter, c.At(TicksToNewYear() - 1).Season);
            Assert.AreEqual(Season.Winter, c.At(TicksToNewYear() + 58 * Day).Season); // 28 Feb
            Assert.AreEqual(Season.Spring, c.At(TicksToNewYear() + 59 * Day).Season); // 1 Mar
        }

        [Test]
        public void DifferentStartWeekday_ShiftsAllWeekdays()
        {
            var c = new GameCalendar(startWeekday: Weekday.Wednesday);
            Assert.AreEqual(Weekday.Wednesday, c.At(0).Weekday);
            Assert.AreEqual(Weekday.Sunday, c.At(4 * Day).Weekday);
            Assert.AreEqual(Weekday.Monday, c.At(5 * Day).Weekday);
            Assert.AreEqual(Weekday.Wednesday, c.At(7 * Day).Weekday);
        }

        [Test]
        public void Format_Tick0()
        {
            Assert.AreEqual("Mon 1 Mar · Spring · Year 1 · 06:00", Default().Format(0));
            Assert.AreEqual("Mon 1 Mar · Spring · Year 1 · 06:00", Default().At(0).ToString());
        }

        [Test]
        public void CustomStartTime()
        {
            var c = new GameCalendar(6, 15, 13, 45, Weekday.Friday);
            var t = c.At(0);
            Assert.AreEqual(Season.Summer, t.Season);
            Assert.AreEqual(13, t.Hour);
            Assert.AreEqual(45, t.Minute);
            Assert.AreEqual(31 + 28 + 31 + 30 + 31 + 15, t.DayOfYear);
        }

        [Test]
        public void MismatchedDaysPerYear_Throws()
        {
            Assert.Throws<ArgumentException>(() => new GameCalendar(daysPerYear: 360));
            Assert.DoesNotThrow(() => new GameCalendar(daysPerYear: 365));
        }
    }
}
