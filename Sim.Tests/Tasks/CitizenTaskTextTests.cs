using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Tasks
{
    /// <summary>Citizen panel task text (T-283, PPL-14).</summary>
    public class CitizenTaskTextTests
    {
        private const long AdultBirth = -4_000_000;

        // 200 m flat map with one tree at (150, 50). Dec 21 (sunrise 08:00, sunset 16:00) at the given hour.
        private static SimWorld NewWorld(int hour)
        {
            var trees = new[] { new MapTree(new TreeId(1), 150, 50, 16) };
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), trees);
            return SimWorld.FromMapData(map, new SimTuning
            {
                StartMonth = 12, StartDay = 21, StartHour = hour, StartMinute = 0, StartWeekday = Weekday.Monday,
            });
        }

        // A woodcutter family whose workplace is operation 1, with one adult worker at the tree.
        private static Citizen SetUpWorker(SimWorld w)
        {
            Operation op = w.Operations.Create(OperationTypes.WoodcuttingId, 1);
            Family f = w.People.AddFamily(1, "Archer", 150, 50);
            f.WorkplaceId = op.Id;
            return w.People.AddCitizen(1, "Buck", Sex.Male, AdultBirth, 1, 150, 50, 0, Activity.Waiting);
        }

        // MoveTo, fell, then four buck steps: the same shape the woodcutting system builds.
        private static TaskDef FellWithFourBucks() => new TaskDef(WoodcuttingSystem.FellTaskId, new[]
        {
            TaskStep.MoveTo(),
            TaskStep.Work(10, true, "stage.fell"),
            TaskStep.Work(10, true, "stage.buck"),
            TaskStep.Work(10, true, "stage.buck"),
            TaskStep.Work(10, true, "stage.buck"),
            TaskStep.Work(10, true, "stage.buck"),
        }, EligibilityPolicy.AssignedFamilyOnly);

        [Test]
        public void IdleCitizenShowsActivity()
        {
            var w = NewWorld(12);
            Citizen c = SetUpWorker(w);
            c.Activity = Activity.MillingAbout;
            Assert.AreEqual("Milling about", CitizenTaskText.Task(w, c));
        }

        [Test]
        public void WorkplaceNameIsOperationTypeAndId()
        {
            var w = NewWorld(12);
            Citizen c = SetUpWorker(w);
            Assert.AreEqual("Woodcutter 1", CitizenTaskText.Workplace(w, c));
        }

        [Test]
        public void CitizenWithoutWorkplaceHasNone()
        {
            var w = NewWorld(12);
            w.People.AddFamily(2, "Brook", 10, 10);
            Citizen c = w.People.AddCitizen(2, "Cole", Sex.Male, AdultBirth, 2, 10, 10);
            Assert.AreEqual("none", CitizenTaskText.Workplace(w, c));
        }

        [Test]
        public void AssignedButNotStartedShowsWalkingToWork()
        {
            var w = NewWorld(12);
            Citizen c = SetUpWorker(w);
            TaskRecord t = w.Tasks.Create(FellWithFourBucks(), 1, new TreeId(1));
            w.Tasks.Assign(t.Id, c.Id);
            Assert.AreEqual("Walking to work", CitizenTaskText.Task(w, c));
        }

        [Test]
        public void FellingWorkerShowsBuckProgress()
        {
            var w = NewWorld(12);
            Citizen c = SetUpWorker(w);
            TaskRecord t = w.Tasks.Create(FellWithFourBucks(), 1, new TreeId(1));
            w.Tasks.Assign(t.Id, c.Id);
            w.Tasks.Start(t.Id);
            // Past MoveTo and fell: CurrentStep 2 is the first buck step, 3 the second.
            w.Tasks.CompleteStep(t.Id);
            w.Tasks.CompleteStep(t.Id);
            c.Activity = Activity.Working;
            Assert.AreEqual("Felling (Buck 1/4)", CitizenTaskText.Task(w, c));
            w.Tasks.CompleteStep(t.Id);
            Assert.AreEqual("Felling (Buck 2/4)", CitizenTaskText.Task(w, c));
        }

        [Test]
        public void FellStepAloneShowsOnlyTheVerb()
        {
            var w = NewWorld(12);
            Citizen c = SetUpWorker(w);
            TaskRecord t = w.Tasks.Create(FellWithFourBucks(), 1, new TreeId(1));
            w.Tasks.Assign(t.Id, c.Id);
            w.Tasks.Start(t.Id);
            w.Tasks.CompleteStep(t.Id); // now on the fell step
            c.Activity = Activity.Working;
            Assert.AreEqual("Felling", CitizenTaskText.Task(w, c));
        }

        [Test]
        public void WorkerOffShiftAtNightShowsPausedForTheNight()
        {
            var w = NewWorld(2);
            Citizen c = SetUpWorker(w);
            TaskRecord t = w.Tasks.Create(FellWithFourBucks(), 1, new TreeId(1));
            w.Tasks.Assign(t.Id, c.Id);
            w.Tasks.Start(t.Id);
            Assert.IsFalse(w.WorkDay.OnShift(w, c), "precondition: 02:00 is outside the shift");
            Assert.AreEqual("Paused for the night", CitizenTaskText.Task(w, c));
        }

        // An odd-job helper (a family with no workplace) at noon, working.
        private static Citizen SetUpHelper(SimWorld w)
        {
            w.People.AddFamily(2, "Brook", 150, 50);
            Citizen c = w.People.AddCitizen(2, "Cole", Sex.Male, AdultBirth, 2, 150, 50, 0, Activity.Working);
            return c;
        }

        [Test]
        public void DeliverTask_ShowsDeliveringLogsToTheModule()
        {
            var w = NewWorld(12);
            Citizen c = SetUpHelper(w);
            TaskRecord t = w.Tasks.Create(ConstructionDeliverDef(), 9, null, TaskPriority.Normal, "Hut");
            w.Tasks.Assign(t.Id, c.Id);
            w.Tasks.Start(t.Id);
            Assert.AreEqual("Delivering logs to Hut", CitizenTaskText.Task(w, c));
        }

        [Test]
        public void BuildTask_ShowsBuildingTheModule()
        {
            var w = NewWorld(12);
            Citizen c = SetUpHelper(w);
            TaskRecord t = w.Tasks.Create(ConstructionBuildDef(), 9, null, TaskPriority.Normal, "Hut");
            w.Tasks.Assign(t.Id, c.Id);
            w.Tasks.Start(t.Id);
            Assert.AreEqual("Building Hut", CitizenTaskText.Task(w, c));
        }

        [Test]
        public void ConstructionTask_WithNoModuleName_OmitsTheModule()
        {
            var w = NewWorld(12);
            Citizen c = SetUpHelper(w);
            TaskRecord t = w.Tasks.Create(ConstructionDeliverDef(), 9);
            w.Tasks.Assign(t.Id, c.Id);
            w.Tasks.Start(t.Id);
            Assert.AreEqual("Delivering logs", CitizenTaskText.Task(w, c));
        }

        private static TaskDef ConstructionDeliverDef() =>
            ConstructionTaskDefs.Deliver(new PathPoint(110, 50), 1, new PathPoint(150, 50), 2, 4, 10, 10);

        private static TaskDef ConstructionBuildDef() => ConstructionTaskDefs.Build(30);
    }
}
