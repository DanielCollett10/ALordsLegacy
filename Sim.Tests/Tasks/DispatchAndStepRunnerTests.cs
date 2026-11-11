using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class DispatchAndStepRunnerTests
    {
        private const long AdultBirth = -4_000_000; // about 15 years at the default ageing rate
        private const long ChildBirth = -100;

        // 200 m flat map, 8:00 start (daytime), trees at the given positions (ids 1..n).
        private static SimWorld NewWorld(params (float x, float z)[] trees)
        {
            var list = new List<MapTree>();
            for (int i = 0; i < trees.Length; i++) list.Add(new MapTree(new TreeId(i + 1), trees[i].x, trees[i].z, 16));
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), list.ToArray());
            return SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
        }

        private static Citizen AddWorker(SimWorld w, int familyId, int citizenId, double x, double z,
            int? workplace = null, long birth = AdultBirth)
        {
            if (w.People.FindFamily(familyId) == null)
                w.People.AddFamily(familyId, "F" + familyId, x, z).WorkplaceId = workplace;
            return w.People.AddCitizen(citizenId, "C" + citizenId, Sex.Male, birth, familyId, x, z);
        }

        private static TaskDef Light(EligibilityPolicy e = EligibilityPolicy.AssignedFamilyOnly, int workTicks = 5) =>
            new TaskDef("task.fake", new[] { TaskStep.MoveTo(), TaskStep.Work(workTicks) }, e);

        private static TaskDef Heavy(EligibilityPolicy e = EligibilityPolicy.AssignedFamilyOnly) =>
            new TaskDef("task.heavy", new[] { TaskStep.MoveTo(), TaskStep.Work(3, heavy: true) }, e);

        private static void Run(SimWorld w, int ticks) => w.Step(ticks);

        [Test]
        public void DefaultSystems_DispatchAndStepsSitBetweenRoutineAndMovement()
        {
            var list = SimSystems.CreateDefault();
            int r = -1, d = -1, s = -1, m = -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is RoutineSystem) r = i;
                if (list[i] is DispatchSystem) d = i;
                if (list[i] is StepRunnerSystem) s = i;
                if (list[i] is MovementSystem) m = i;
            }
            Assert.That(r, Is.GreaterThanOrEqualTo(0));
            Assert.That(d, Is.GreaterThan(r));
            Assert.That(s, Is.GreaterThan(d));
            Assert.That(m, Is.GreaterThan(s));
        }

        [Test]
        public void OpenTask_IsNotAssignedBeforeTheTenMinuteDispatchTick()
        {
            var w = NewWorld((110f, 100f));
            AddWorker(w, 1, 1, 100, 100, workplace: 7);
            var t = w.Tasks.Create(Light(), 7, new TreeId(1));
            Run(w, 9);
            Assert.AreEqual(TaskState.Open, t.State);
            Run(w, 1);
            Assert.AreEqual(TaskState.Active, t.State);
        }

        [Test]
        public void Task_RunsMoveThenWorkToDone_AndReleasesTheTree()
        {
            var w = NewWorld((110f, 100f));
            var c = AddWorker(w, 1, 1, 100, 100, workplace: 7);
            var t = w.Tasks.Create(Light(workTicks: 5), 7, new TreeId(1));
            Run(w, 10);
            Assert.AreEqual(Activity.Walking, c.Activity);
            Assert.AreEqual(t.Id, w.Trees.Get(new TreeId(1)).ReservedBy);

            int guard = 0;
            while (c.Activity == Activity.Walking && guard++ < 100) Run(w, 1);
            Assert.AreEqual(Activity.Working, c.Activity);
            Assert.AreEqual(1, t.CurrentStep);
            Assert.That(c.X, Is.EqualTo(110).Within(1.0));

            // The first tick of work was already counted when Working first showed.
            Assert.AreEqual(1, t.StepProgress);
            Run(w, 3);
            Assert.AreEqual(TaskState.Active, t.State, "5 ticks of work are not done after 4");
            Run(w, 1);
            Assert.AreEqual(TaskState.Done, t.State);
            Assert.IsNull(w.Trees.Get(new TreeId(1)).ReservedBy);
            Assert.IsFalse(w.Tasks.IsBusy(1));
        }

        [Test]
        public void HigherPriority_WinsOverNearerTask()
        {
            var w = NewWorld((105f, 100f), (150f, 100f));
            AddWorker(w, 1, 1, 100, 100, workplace: 7);
            var near = w.Tasks.Create(Light(), 7, new TreeId(1), priority: 0);
            var far = w.Tasks.Create(Light(), 7, new TreeId(2), priority: 5);
            Run(w, 10);
            Assert.AreEqual(TaskState.Active, far.State);
            Assert.AreEqual(TaskState.Open, near.State);
        }

        [Test]
        public void SamePriority_NearestTaskIsChosen()
        {
            var w = NewWorld((150f, 100f), (105f, 100f));
            AddWorker(w, 1, 1, 100, 100, workplace: 7);
            var far = w.Tasks.Create(Light(), 7, new TreeId(1));
            var near = w.Tasks.Create(Light(), 7, new TreeId(2));
            Run(w, 10);
            Assert.AreEqual(TaskState.Active, near.State);
            Assert.AreEqual(TaskState.Open, far.State);
        }

        [Test]
        public void Workers_AreServedInIdOrder()
        {
            var w = NewWorld((110f, 100f), (190f, 100f));
            // Citizen 1 is far from tree 1 but picks first; citizen 2 stands beside it.
            var a = AddWorker(w, 1, 1, 100, 140, workplace: 7);
            var b = AddWorker(w, 2, 2, 109, 100, workplace: 7);
            var t = w.Tasks.Create(Light(), 7, new TreeId(1));
            Run(w, 10);
            Assert.AreEqual(a.Id, t.AssigneeId);
            Assert.IsNotNull(b);
        }

        [Test]
        public void Child_CannotTakeHeavyTask_ButCanTakeLight()
        {
            var w = NewWorld((105f, 100f));
            AddWorker(w, 1, 1, 100, 100, workplace: 7, birth: ChildBirth);
            var heavy = w.Tasks.Create(Heavy(), 7, new TreeId(1));
            Run(w, 20);
            Assert.AreEqual(TaskState.Open, heavy.State, "PPL-44");

            var light = w.Tasks.Create(Light(), 7);
            // No target: the light task is assignable, then fails at its walk; it must have been picked up.
            Run(w, 10);
            Assert.AreNotEqual(TaskState.Open, light.State);
        }

        [Test]
        public void AdultWorker_TakesHeavyTask()
        {
            var w = NewWorld((105f, 100f));
            AddWorker(w, 1, 1, 100, 100, workplace: 7);
            var heavy = w.Tasks.Create(Heavy(), 7, new TreeId(1));
            Run(w, 10);
            Assert.AreEqual(TaskState.Active, heavy.State);
        }

        [Test]
        public void AssignedFamilyOnly_IsNotTakenByUnassignedOrOtherWorkplaceFamilies()
        {
            var w = NewWorld((105f, 100f));
            AddWorker(w, 1, 1, 100, 100, workplace: null);
            AddWorker(w, 2, 2, 100, 100, workplace: 9);
            var t = w.Tasks.Create(Light(), 7, new TreeId(1));
            Run(w, 30);
            Assert.AreEqual(TaskState.Open, t.State);
            AddWorker(w, 3, 3, 100, 100, workplace: 7);
            Run(w, 10);
            Assert.AreEqual(3, t.AssigneeId);
        }

        [Test]
        public void HelpersOnly_IsTakenByUnassignedFamily_NeverByAssignedFamily()
        {
            var w = NewWorld((105f, 100f));
            AddWorker(w, 1, 1, 100, 100, workplace: 7); // PPL-45
            var t = w.Tasks.Create(Light(EligibilityPolicy.HelpersOnly), 7, new TreeId(1));
            Run(w, 30);
            Assert.AreEqual(TaskState.Open, t.State);
            AddWorker(w, 2, 2, 100, 100, workplace: null);
            Run(w, 10);
            Assert.AreEqual(2, t.AssigneeId);
        }

        [Test]
        public void AssignedThenHelpers_PrefersTheAssignedFamilyEvenWithLowerId()
        {
            var w = NewWorld((105f, 100f));
            AddWorker(w, 1, 1, 100, 100, workplace: null);   // helper, lower id
            AddWorker(w, 2, 2, 100, 100, workplace: 7);       // assigned
            var t = w.Tasks.Create(Light(EligibilityPolicy.AssignedThenHelpers), 7, new TreeId(1));
            Run(w, 10);
            Assert.AreEqual(2, t.AssigneeId);
        }

        [Test]
        public void AssignedThenHelpers_FallsBackToHelper()
        {
            var w = NewWorld((105f, 100f));
            AddWorker(w, 1, 1, 100, 100, workplace: null);
            var t = w.Tasks.Create(Light(EligibilityPolicy.AssignedThenHelpers), 7, new TreeId(1));
            Run(w, 10);
            Assert.AreEqual(1, t.AssigneeId);
        }

        [Test]
        public void WorkerInTheMiddleOfATree_IsNotIdle_AndTakesNothingElse()
        {
            var w = NewWorld((101f, 100f), (102f, 100f));
            var c = AddWorker(w, 1, 1, 100, 100, workplace: 7);
            var first = w.Tasks.Create(Light(workTicks: 60), 7, new TreeId(1));
            Run(w, 10);
            var second = w.Tasks.Create(Light(), 7, new TreeId(2), priority: 99);
            Run(w, 20); // two more dispatch ticks while the first is still being worked
            Assert.AreEqual(TaskState.Active, first.State);
            Assert.AreEqual(Activity.Working, c.Activity);
            Assert.IsTrue(w.Tasks.IsBusy(c.Id));
            Assert.IsFalse(DispatchSystem.IsIdle(w, c));
            Assert.AreEqual(TaskState.Open, second.State);
        }

        [Test]
        public void TargetTreeGoneMidWalk_CancelsTask_ReleasesReservation_AndFreesWorker()
        {
            var w = NewWorld((150f, 100f), (160f, 100f));
            var c = AddWorker(w, 1, 1, 100, 100, workplace: 7);
            var t = w.Tasks.Create(Light(), 7, new TreeId(1));
            Run(w, 15);
            Assert.AreEqual(TaskState.Active, t.State);
            w.Trees.SetState(new TreeId(1), TreeState.Stump);
            Run(w, 1);
            Assert.AreEqual(TaskState.Cancelled, t.State);
            Assert.IsNull(w.Trees.Get(new TreeId(1)).ReservedBy);
            Assert.IsFalse(w.Tasks.IsBusy(c.Id));
            Assert.IsNull(c.Move);
        }

        [Test]
        public void TreeRemovedMidWork_CancelsTask()
        {
            var w = NewWorld((101f, 100f));
            AddWorker(w, 1, 1, 100, 100, workplace: 7);
            var t = w.Tasks.Create(Light(workTicks: 50), 7, new TreeId(1));
            Run(w, 20);
            Assert.AreEqual(TaskState.Active, t.State);
            w.Trees.Remove(new TreeId(1));
            Run(w, 1);
            Assert.AreEqual(TaskState.Cancelled, t.State);
            Assert.AreEqual(0, t.Reservations.Count);
        }

        [Test]
        public void NoRoute_CancelsTask_AndReleasesReservation()
        {
            // A tree far outside the map cannot be reached.
            var w = NewWorld((5000f, 5000f));
            var c = AddWorker(w, 1, 1, 100, 100, workplace: 7);
            var t = w.Tasks.Create(Light(), 7, new TreeId(1));
            Run(w, 20);
            Assert.AreEqual(TaskState.Cancelled, t.State);
            Assert.IsNull(w.Trees.Get(new TreeId(1)).ReservedBy);
            Assert.IsFalse(w.Tasks.IsBusy(c.Id));
        }

        [Test]
        public void OpenTask_WithGoneTarget_IsCancelledByDispatcher()
        {
            var w = NewWorld((105f, 100f));
            AddWorker(w, 1, 1, 100, 100, workplace: 7);
            var t = w.Tasks.Create(Light(), 7, new TreeId(1));
            w.Trees.Remove(new TreeId(1));
            Run(w, 10);
            Assert.AreEqual(TaskState.Cancelled, t.State);
        }

        [Test]
        public void TreeReservedByAnotherTask_IsLeftOpen()
        {
            var w = NewWorld((105f, 100f));
            AddWorker(w, 1, 1, 100, 100, workplace: 7);
            var a = w.Tasks.Create(Light(EligibilityPolicy.HelpersOnly), 7, new TreeId(1)); // nobody eligible; holds the tree
            Assert.IsTrue(w.Tasks.Reserve(a.Id, new TreeId(1)));
            var b = w.Tasks.Create(Light(), 7, new TreeId(1));
            Run(w, 10);
            Assert.AreEqual(TaskState.Open, b.State);
        }

        [Test]
        public void TwoWorkers_TakeTwoTasks_NotTheSame()
        {
            var w = NewWorld((105f, 100f), (106f, 100f));
            AddWorker(w, 1, 1, 100, 100, workplace: 7);
            AddWorker(w, 2, 2, 100, 100, workplace: 7);
            var a = w.Tasks.Create(Light(), 7, new TreeId(1));
            var b = w.Tasks.Create(Light(), 7, new TreeId(2));
            Run(w, 10);
            Assert.AreNotEqual(a.AssigneeId, b.AssigneeId);
            Assert.AreEqual(TaskState.Active, a.State);
            Assert.AreEqual(TaskState.Active, b.State);
        }
    }
}
