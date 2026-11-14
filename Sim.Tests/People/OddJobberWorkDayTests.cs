using System;
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
    /// <summary>Odd-jobbers with construction work keep the working day (T-346, CN-25/26).</summary>
    public class OddJobberWorkDayTests
    {
        private const long AdultBirth = -4_000_000;

        private static SimWorld NewWorld(int month, int day, int hour, Weekday wd)
        {
            var trees = new[] { new MapTree(new TreeId(1), 150, 50, 16) };
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), trees);
            return SimWorld.FromMapData(map, new SimTuning
            {
                StartMonth = month, StartDay = day, StartHour = hour, StartMinute = 0, StartWeekday = wd,
            });
        }

        // A helper family (no workplace) member at home.
        private static Citizen AddOddJobber(SimWorld w, Activity a)
        {
            w.People.AddFamily(1, "Odd", 50, 50);
            return w.People.AddCitizen(1, "J", Sex.Male, AdultBirth, 1, 50, 50, 0, a);
        }

        private static int Minute(SimWorld w) => w.Now.Hour * 60 + w.Now.Minute;

        private static void StepUntil(SimWorld w, Func<bool> done, int max = 3 * 1440)
        {
            int guard = 0;
            while (!done() && guard++ < max) w.Step();
            Assert.Less(guard, max, "condition never met");
        }

        private static TaskRecord Build(SimWorld w, int minutes = 30000) =>
            w.Tasks.Create(ConstructionTaskDefs.Build(minutes), 99);

        [Test]
        public void NoConstructionWork_WorkDayDoesNotApply_AndTheyMillAsBefore()
        {
            var w = NewWorld(12, 21, 9, Weekday.Monday);
            Citizen c = AddOddJobber(w, Activity.MillingAbout);
            Assert.IsFalse(WorkDay.AppliesTo(w, c));
            Assert.IsTrue(w.WorkDay.OnShift(w, c));
            StepUntil(w, () => c.Activity == Activity.MillingAbout && c.Move != null);
            Assert.AreEqual(Activity.MillingAbout, c.Activity);
        }

        [Test]
        public void OpenConstructionTask_AppliesToAdultOddJobber_NotToAChild()
        {
            var w = NewWorld(12, 21, 9, Weekday.Monday);
            Citizen adult = AddOddJobber(w, Activity.MillingAbout);
            Citizen child = w.People.AddCitizen(2, "K", Sex.Male, w.Tick - 5 * 525600L, 1, 50, 50, 0, Activity.MillingAbout);
            Build(w);
            Assert.IsTrue(WorkDay.AppliesTo(w, adult));
            Assert.IsFalse(WorkDay.AppliesTo(w, child));
        }

        [Test]
        public void Night_ConstructionTaskNotOfferedToOddJobber_UntilTheyAreOnShift()
        {
            var w = NewWorld(12, 21, 22, Weekday.Monday);
            Citizen c = AddOddJobber(w, Activity.Sleeping);
            TaskRecord t = Build(w);
            for (int i = 0; i < 200; i++)
            {
                w.Step();
                Assert.AreEqual(TaskState.Open, t.State, "never offered while off shift at minute " + Minute(w));
            }
            StepUntil(w, () => t.State != TaskState.Open);
            Assert.GreaterOrEqual(w.Tick, w.WorkDay.DepartTick(w, c));
            Assert.GreaterOrEqual(w.Tick, w.WorkDay.WindowOf(w, WorkDay.DayStart(w)).Sunrise - 1, "starts at sunrise, not at night");
        }

        [Test]
        public void Sunday_AfterShortDayEnd_TaskNotOffered()
        {
            // Dec 21: sunset 16:00, Sunday ends 14:00.
            var w = NewWorld(12, 21, 14, Weekday.Sunday);
            AddOddJobber(w, Activity.MillingAbout);
            TaskRecord t = Build(w);
            for (int i = 0; i < 60; i++) w.Step();
            Assert.AreEqual(TaskState.Open, t.State, "past the short Sunday end");
        }

        [Test]
        public void Daytime_ConstructionTaskIsOffered()
        {
            var w = NewWorld(12, 21, 10, Weekday.Monday);
            Citizen c = AddOddJobber(w, Activity.MillingAbout);
            TaskRecord t = Build(w);
            StepUntil(w, () => t.State == TaskState.Active, 60);
            Assert.AreEqual(c.Id, t.AssigneeId);
        }

        [Test]
        public void ActiveBuild_PausesAtSunset_WalksHome_AndResumesNextMorning()
        {
            var w = NewWorld(12, 21, 10, Weekday.Monday);
            Citizen c = AddOddJobber(w, Activity.MillingAbout);
            TaskRecord t = Build(w);
            StepUntil(w, () => c.Activity == Activity.Working);
            long end = w.WorkDay.WindowOf(w, WorkDay.DayStart(w)).End;
            StepUntil(w, () => w.Tick > end + 5);
            Assert.AreEqual(TaskState.Active, t.State, "paused, not cancelled");
            int progress = t.StepProgress;
            Assert.AreNotEqual(Activity.Working, c.Activity);
            StepUntil(w, () => c.Activity == Activity.Sleeping);
            Assert.AreEqual(progress, t.StepProgress, "no progress at night");
            StepUntil(w, () => c.Activity == Activity.Working);
            Assert.GreaterOrEqual(w.Tick, w.WorkDay.WindowOf(w, WorkDay.DayStart(w)).Sunrise);
            Assert.Less(Minute(w), 12 * 60);
            StepUntil(w, () => t.StepProgress > progress);
        }

        [Test]
        public void Summer_SecondDay_BuildResumesAtSunriseAfterNightAtHome()
        {
            var w = NewWorld(6, 21, 10, Weekday.Monday);
            Citizen c = AddOddJobber(w, Activity.MillingAbout);
            Build(w);
            StepUntil(w, () => c.Activity == Activity.Working);
            StepUntil(w, () => c.Activity == Activity.Sleeping);
            StepUntil(w, () => Minute(w) == 3 * 60 + 30);
            Assert.AreEqual(Activity.Sleeping, c.Activity, "still asleep before sunrise (04:00)");
            StepUntil(w, () => c.Activity == Activity.Working);
            Assert.GreaterOrEqual(Minute(w), 4 * 60, "working from sunrise");
            Assert.LessOrEqual(Minute(w), 4 * 60 + 15);
        }

        [Test]
        public void TaskCancelled_OddJobberGoesBackToMilling()
        {
            var w = NewWorld(12, 21, 10, Weekday.Monday);
            Citizen c = AddOddJobber(w, Activity.MillingAbout);
            TaskRecord t = Build(w);
            StepUntil(w, () => c.Activity == Activity.Working);
            w.Tasks.Cancel(t.Id);
            Assert.IsFalse(WorkDay.AppliesTo(w, c));
            StepUntil(w, () => c.Activity == Activity.MillingAbout);
        }
    }
}
