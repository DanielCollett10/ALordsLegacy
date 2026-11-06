using System;
using LordsLegacy.Sim.Calendar;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class DaylightTests
    {
        private static Daylight Default() => new Daylight(8, 16);

        private static GameTime At(int dayOfYear, int hour, int minute)
        {
            return new GameTime(1, 1, 1, dayOfYear, Weekday.Monday, hour, minute, Season.Winter);
        }

        [Test]
        public void SummerSolstice_HitsMaxWithin5Minutes()
        {
            var sun = Default().For(172);
            Assert.That(Math.Abs(sun.DaylightHours - 16.0) * 60.0, Is.LessThan(5.0));
        }

        [Test]
        public void WinterSolstice_HitsMinWithin5Minutes()
        {
            var sun = Default().For(355);
            Assert.That(Math.Abs(sun.DaylightHours - 8.0) * 60.0, Is.LessThan(5.0));
        }

        [TestCase(80)]
        [TestCase(264)]
        public void Equinoxes_AreAboutTwelveHours(int day)
        {
            Assert.That(Default().For(day).DaylightHours, Is.EqualTo(12.0).Within(0.1));
        }

        [Test]
        public void SolarNoon_IsCentredOn1200()
        {
            var sun = Default().For(100);
            Assert.That((sun.Sunrise + sun.Sunset) / 2.0, Is.EqualTo(12.0).Within(1e-9));
        }

        [Test]
        public void SunriseIsBeforeSunset_EveryDayOfTheYear()
        {
            var d = Default();
            for (int day = 1; day <= 365; day++)
            {
                var sun = d.For(day);
                Assert.That(sun.Sunrise, Is.LessThan(sun.Sunset), "day " + day);
                Assert.That(sun.Sunrise, Is.GreaterThanOrEqualTo(0), "day " + day);
                Assert.That(sun.Sunset, Is.LessThanOrEqualTo(24), "day " + day);
            }
        }

        [Test]
        public void DaylightIsWithinMinAndMax_EveryDay()
        {
            var d = Default();
            for (int day = 1; day <= 365; day++)
            {
                double h = d.For(day).DaylightHours;
                Assert.That(h, Is.InRange(8.0 - 1e-9, 16.0 + 1e-9), "day " + day);
            }
        }

        [Test]
        public void SixAmOn21Dec_IsNotDaylight()
        {
            // 21 Dec is day 355 in a 365-day year.
            Assert.IsFalse(Default().IsDaylight(At(355, 6, 0)));
        }

        [Test]
        public void SixAmOn21Jun_IsDaylight()
        {
            // 21 Jun is day 172.
            Assert.IsTrue(Default().IsDaylight(At(172, 6, 0)));
        }

        [Test]
        public void SunProgress_IsZeroAtSunriseAndOneAtSunset()
        {
            var d = Default();
            var sun = d.For(172);
            int riseMinutes = (int)Math.Round(sun.Sunrise * 60);
            int setMinutes = (int)Math.Round(sun.Sunset * 60);
            Assert.That(d.SunProgress(At(172, riseMinutes / 60, riseMinutes % 60)), Is.EqualTo(0).Within(0.01));
            Assert.That(d.SunProgress(At(172, setMinutes / 60, setMinutes % 60)), Is.EqualTo(1).Within(0.01));
        }

        [Test]
        public void SunProgress_IsNegativeBeforeSunrise_AndAboveOneAfterSunset()
        {
            var d = Default();
            Assert.That(d.SunProgress(At(355, 3, 0)), Is.LessThan(0));
            Assert.That(d.SunProgress(At(355, 22, 0)), Is.GreaterThan(1));
        }

        [Test]
        public void SunProgress_TickFractionAddsPartOfAMinute()
        {
            var d = Default();
            var t = At(172, 12, 0);
            double a = d.SunProgress(t, 0);
            double b = d.SunProgress(t, 0.5);
            Assert.That(b - a, Is.EqualTo(0.5 / 60.0 / d.For(172).DaylightHours).Within(1e-12));
        }

        [Test]
        public void For_RejectsOutOfRangeDay()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Default().For(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Default().For(366));
        }

        [Test]
        public void Constructor_RejectsBadRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Daylight(0, 16));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Daylight(16, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Daylight(8, 24));
        }
    }
}
