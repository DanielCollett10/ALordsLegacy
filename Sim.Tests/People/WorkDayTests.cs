using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    /// <summary>Working day for assigned workers (T-277, PPL-21, PPL-24..26, PPL-45).</summary>
    public class WorkDayTests
    {
        private const long AdultBirth = -4_000_000;

        // 200 m flat map with one tree at (tx, tz); date and hour as given. Dec 21 = day 355 (sunrise 08:00, sunset 16:00),
        // Jun 21 = day 172 (sunrise 04:00, sunset 20:00) with the default 8/16 hour daylight.
        private static SimWorld NewWorld(int month, int day, int hour, Weekday wd, float tx, float tz)
        {
            var trees = new[] { new MapTree(new TreeId(1), tx, tz, 16) };
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), trees);
            return SimWorld.FromMapData(map, new SimTuning
            {
                StartMonth = month, StartDay = day, StartHour = hour, StartMinute = 0, StartWeekday = wd,
            });
        }

        private static Citizen AddSleeper(SimWorld w, double x, double z, int? workplace)
        {
            Family f = w.People.AddFamily(1, "F", x, z);
            f.WorkplaceId = workplace;
            return w.People.AddCitizen(1, "W", Sex.Male, AdultBirth, 1, x, z, 0, Activity.Sleeping);
        }

        private static TaskDef Fell(int workTicks) =>
            new TaskDef("task.fake", new[] { TaskStep.MoveTo(), TaskStep.Work(workTicks) }, EligibilityPolicy.AssignedFamilyOnly);

        private static int MinuteOfDay(SimWorld w) => w.Now.Hour * 60 + w.Now.Minute;

        private static void StepUntil(SimWorld w, Func<bool> done, int max = 3 * 1440)
        {
            int guard = 0;
            while (!done() && guard++ < max) w.Step();
            Assert.Less(guard, max, "condition never met");
        }

        [Test]
        public void Winter_WorkerLeavesSoTheyReachTheTreeAtSunrise()
        {
            // Home (50,50), tree (150,50): 100 m = about 57.5 min. Sunrise 08:00.
            var w = NewWorld(12, 21, 18, Weekday.Monday, 150, 50);
            Citizen c = AddSleeper(w, 50, 50, 7);
            w.Tasks.Create(Fell(30000), 7, new TreeId(1));
            StepUntil(w, () => c.Activity == Activity.Walking);
            int leave = MinuteOfDay(w);
            Assert.That(leave, Is.EqualTo(480 - 58).Within(4), "leaves about an hour before sunrise, in the dark");
            StepUntil(w, () => c.Activity == Activity.Working);
            Assert.That(MinuteOfDay(w), Is.EqualTo(480).Within(4), "arrives at sunrise");
        }

        [Test]
        public void Summer_ShortNight_LongCommuteLeavesLaterToKeepMinimumSleep()
        {
            // Home (10,10), tree (190,190): about 254 m = 146 min. Summer night is 8 h, so 2 x 146 min of walking
            // would leave under 6 h at home: the worker leaves later and starts after sunrise (04:00).
            var w = NewWorld(6, 21, 22, Weekday.Monday, 190, 190);
            Citizen c = AddSleeper(w, 10, 10, 7);
            w.Tasks.Create(Fell(30000), 7, new TreeId(1));
            long arrivedHome = w.WorkDay.WindowOf(w, WorkDay.DayStart(w)).End + 147; // sunset today + commute
            StepUntil(w, () => c.Activity == Activity.Walking);
            long departTick = w.Tick;
            Assert.That(departTick - arrivedHome, Is.GreaterThanOrEqualTo(360 - 2), "at least MinSleepHours at home");
            Assert.That(departTick - arrivedHome, Is.LessThanOrEqualTo(360 + 4), "but no more than needed");
            StepUntil(w, () => c.Activity == Activity.Working);
            Assert.Greater(MinuteOfDay(w), 4 * 60 + 10, "work starts after sunrise");
        }

        [Test]
        public void Summer_ShortCommute_StillArrivesAtSunrise()
        {
            var w = NewWorld(6, 21, 22, Weekday.Monday, 60, 10);
            Citizen c = AddSleeper(w, 10, 10, 7);
            w.Tasks.Create(Fell(30000), 7, new TreeId(1));
            StepUntil(w, () => c.Activity == Activity.Working);
            Assert.That(MinuteOfDay(w), Is.EqualTo(240).Within(4));
        }

        [Test]
        public void Weekday_WorkStopsAtSunset_AndWorkerWalksHome()
        {
            var w = NewWorld(12, 21, 9, Weekday.Monday, 60, 50);
            Citizen c = AddSleeper(w, 50, 50, 7);
            c.Activity = Activity.MillingAbout;
            var t = w.Tasks.Create(Fell(30000), 7, new TreeId(1));
            StepUntil(w, () => MinuteOfDay(w) == 15 * 60 + 58);
            Assert.AreEqual(Activity.Working, c.Activity);
            StepUntil(w, () => MinuteOfDay(w) == 16 * 60 + 3);
            Assert.AreEqual(Activity.Walking, c.Activity);
            Assert.AreEqual(TaskState.Active, t.State);
        }

        [Test]
        public void Sunday_WorkStopsSundayMealMinutesEarly()
        {
            var w = NewWorld(12, 21, 9, Weekday.Sunday, 60, 50);
            Citizen c = AddSleeper(w, 50, 50, 7);
            c.Activity = Activity.MillingAbout;
            w.Tasks.Create(Fell(30000), 7, new TreeId(1));
            StepUntil(w, () => MinuteOfDay(w) == 13 * 60 + 58);
            Assert.AreEqual(Activity.Working, c.Activity);
            StepUntil(w, () => MinuteOfDay(w) == 14 * 60 + 3);
            Assert.AreEqual(Activity.Walking, c.Activity, "16:00 sunset minus 120 minutes");
        }

        [Test]
        public void SameWorkerResumesTheSameTaskNextMorning_WithProgressAndReservation()
        {
            var w = NewWorld(12, 21, 9, Weekday.Monday, 60, 50);
            Citizen c = AddSleeper(w, 50, 50, 7);
            c.Activity = Activity.MillingAbout;
            var t = w.Tasks.Create(Fell(600), 7, new TreeId(1));
            StepUntil(w, () => MinuteOfDay(w) == 23 * 60);
            Assert.AreEqual(Activity.Sleeping, c.Activity);
            Assert.That(c.X, Is.EqualTo(50).Within(0.1));
            Assert.AreEqual(TaskState.Active, t.State);
            Assert.AreEqual(c.Id, t.AssigneeId);
            Assert.AreEqual(t.Id, w.Trees.Get(new TreeId(1)).ReservedBy);
            int progress = t.StepProgress;
            Assert.Greater(progress, 0);

            StepUntil(w, () => c.Activity == Activity.Working);
            Assert.That(MinuteOfDay(w), Is.GreaterThanOrEqualTo(7 * 60), "next morning, in time for sunrise");
            Assert.That(c.X, Is.EqualTo(60).Within(1.0), "walked back to the tree first");
            Assert.AreEqual(c.Id, t.AssigneeId);
            Assert.GreaterOrEqual(t.StepProgress, progress);

            StepUntil(w, () => t.State == TaskState.Done);
            Assert.AreEqual(c.Id, t.AssigneeId, "one worker for the whole task");
        }

        [Test]
        public void AssignedWorkerWithNoTask_MillsNearHome()
        {
            var w = NewWorld(12, 21, 9, Weekday.Monday, 60, 50);
            Citizen c = AddSleeper(w, 50, 50, 7);
            c.Activity = Activity.MillingAbout;
            for (int i = 0; i < 300; i++)
            {
                w.Step();
                double dx = c.X - 50, dz = c.Z - 50;
                Assert.LessOrEqual(Math.Sqrt(dx * dx + dz * dz), w.Tuning.MillRadiusM + 1.0);
            }
            Assert.AreEqual(Activity.MillingAbout, c.Activity);
        }

        [Test]
        public void UnassignedWorker_KeepsTheM7Routine_SleepsAtNightAndTakesNoNightTask()
        {
            var w = NewWorld(12, 21, 22, Weekday.Monday, 60, 50);
            Citizen c = AddSleeper(w, 50, 50, null);
            var def = new TaskDef("task.help", new[] { TaskStep.MoveTo(), TaskStep.Work(10) }, EligibilityPolicy.HelpersOnly);
            var t = w.Tasks.Create(def, 7, new TreeId(1));
            w.Step(120);
            Assert.AreEqual(TaskState.Open, t.State);
            Assert.AreEqual(Activity.Sleeping, c.Activity);
            Assert.IsTrue(w.WorkDay.OnShift(w, c));
        }

        [Test]
        public void ChildOfAssignedFamily_IsNotOnTheWorkDay()
        {
            var w = NewWorld(12, 21, 22, Weekday.Monday, 60, 50);
            Family f = w.People.AddFamily(1, "F", 50, 50);
            f.WorkplaceId = 7;
            Citizen kid = w.People.AddCitizen(1, "K", Sex.Male, -100, 1, 50, 50);
            Assert.IsFalse(WorkDay.AppliesTo(w, kid));
        }
    }
}
