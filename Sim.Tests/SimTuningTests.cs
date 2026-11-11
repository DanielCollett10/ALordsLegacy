using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class SimTuningTests
    {
        [Test]
        public void Defaults_MatchTuningDoc()
        {
            var t = new SimTuning();
            Assert.AreEqual(30, t.RealMinutesPerGameDayAt1x);
            CollectionAssert.AreEqual(new[] { 1, 2, 4, 8, 16, 32 }, t.Speeds);
            Assert.AreEqual(8, t.MaxTicksPerFrame);
            Assert.AreEqual(3, t.StartMonth);
            Assert.AreEqual(1, t.StartDay);
            Assert.AreEqual(6, t.StartHour);
            Assert.AreEqual(0, t.StartMinute);
            Assert.AreEqual(Weekday.Monday, t.StartWeekday);
            Assert.AreEqual(365, t.DaysPerYear);
            Assert.AreEqual(8, t.MinDaylightHours);
            Assert.AreEqual(16, t.MaxDaylightHours);
            Assert.IsFalse(t.RestDayEnabled);
            Assert.AreEqual(1.74, t.WalkSpeedMPerGameMinute, 1e-9);
            Assert.AreEqual(1.04, t.OxSpeedMPerGameMinute, 1e-9);
            Assert.AreEqual(120, t.SundayMealMinutes);
            Assert.AreEqual(6, t.MinSleepHours, 1e-9);
            Assert.AreEqual(4, t.StandardLogLengthM);
            Assert.AreEqual(12, t.TrunkLengthMinM);
            Assert.AreEqual(20, t.TrunkLengthMaxM);
            Assert.AreEqual(2, t.StartingOxen);
        }

        [Test]
        public void RoadAndAreaDefaults_MatchTuningDoc()
        {
            // tuning.md "Roads and areas".
            var t = new SimTuning();
            Assert.AreEqual(1, t.RoadSampleSpacingM, 1e-9);
            Assert.AreEqual(1, t.RoadMinPointSpacingM, 1e-9);
            Assert.AreEqual(0.5, t.RoadTreeClearanceM, 1e-9);
            Assert.AreEqual(1, t.CurveFlattenSpacingM, 1e-9);
            Assert.AreEqual(2, t.SlopeSampleGridM, 1e-9);
            Assert.AreEqual(0.1, t.OverlapToleranceM2, 1e-9);
            Assert.AreEqual(32, t.SpatialIndexCellM, 1e-9);
        }

        [Test]
        public void GameMinutesPerRealSecondAt1x_IsDerivedFromDayLength()
        {
            var t = new SimTuning();
            Assert.AreEqual(1440.0 / (t.RealMinutesPerGameDayAt1x * 60.0), t.GameMinutesPerRealSecondAt1x, 1e-9);

            t.RealMinutesPerGameDayAt1x = 15;
            Assert.AreEqual(1.6, t.GameMinutesPerRealSecondAt1x, 1e-9);
        }

        [Test]
        public void DefaultWalkAndOxSpeeds_MatchOnScreenSpeedsAt1x()
        {
            // Decision 0004: 5 km/h and 3 km/h on screen at 1x, converted at the default game minutes per real second.
            var t = new SimTuning();
            double walkMPerRealSecond = 5000.0 / 3600.0;
            double oxMPerRealSecond = 3000.0 / 3600.0;
            Assert.AreEqual(walkMPerRealSecond / t.GameMinutesPerRealSecondAt1x,
                t.WalkSpeedMPerGameMinute, 0.01);
            Assert.AreEqual(oxMPerRealSecond / t.GameMinutesPerRealSecondAt1x,
                t.OxSpeedMPerGameMinute, 0.01);
        }

        [Test]
        public void Initializer_OverridesOnlyTheGivenValues()
        {
            var t = new SimTuning { StartMonth = 9, StartDay = 15, StartWeekday = Weekday.Friday, StartingOxen = 0 };
            Assert.AreEqual(9, t.StartMonth);
            Assert.AreEqual(15, t.StartDay);
            Assert.AreEqual(Weekday.Friday, t.StartWeekday);
            Assert.AreEqual(0, t.StartingOxen);
            Assert.AreEqual(6, t.StartHour);
            Assert.AreEqual(2, new SimTuning().StartingOxen);
        }

        [Test]
        public void Instances_DoNotShareSpeedArray()
        {
            var a = new SimTuning();
            var b = new SimTuning();
            a.Speeds[0] = 99;
            Assert.AreEqual(1, b.Speeds[0]);
        }

        [Test]
        public void DefaultStartDate_BuildsTheDocumentedCalendarStart()
        {
            var t = new SimTuning();
            var cal = new GameCalendar(t.StartMonth, t.StartDay, t.StartHour, t.StartMinute,
                t.StartWeekday, t.DaysPerYear);
            var now = cal.At(0);
            Assert.AreEqual(1, now.Year);
            Assert.AreEqual(3, now.Month);
            Assert.AreEqual(1, now.DayOfMonth);
            Assert.AreEqual(6, now.Hour);
            Assert.AreEqual(Weekday.Monday, now.Weekday);
        }
    }
}
