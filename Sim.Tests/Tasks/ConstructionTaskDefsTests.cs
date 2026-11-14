using System.Linq;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Tasks
{
    /// <summary>Construction task definitions (CN-20, CN-24, CN-30, CN-41; T-344).</summary>
    public class ConstructionTaskDefsTests
    {
        private const long AdultBirth = -4_000_000; // about 15 years at the default ageing rate
        private const long ChildBirth = -100;       // newborn

        // Flat 200 m map, no trees, 08:00 start (daytime, on shift for the dispatcher's 10-minute tick).
        private static SimWorld NewWorld()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            return SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
        }

        private static Citizen AddHelper(SimWorld w, int familyId, int citizenId, long birth)
        {
            if (w.People.FindFamily(familyId) == null) w.People.AddFamily(familyId, "F" + familyId, 100, 100);
            return w.People.AddCitizen(citizenId, "C" + citizenId, Sex.Male, birth, familyId, 100, 100);
        }

        private static Citizen AddAssigned(SimWorld w, int familyId, int citizenId, int workplaceId)
        {
            Family f = w.People.AddFamily(familyId, "F" + familyId, 100, 100);
            f.WorkplaceId = workplaceId;
            return w.People.AddCitizen(citizenId, "C" + citizenId, Sex.Male, AdultBirth, familyId, 100, 100);
        }

        private static PathPoint P(double x, double z) => new PathPoint(x, z);

        private static TaskDef Deliver() => ConstructionTaskDefs.Deliver(P(110, 100), 1, P(150, 100), 2, 4, 10, 10);

        private static TaskDef Build() => ConstructionTaskDefs.Build(30);

        [Test]
        public void DeliverDef_IsHelpersOnly_NeedsAnOx_AndIsHeavy()
        {
            TaskDef d = Deliver();
            Assert.AreEqual("task.deliver_materials", d.Id);
            Assert.AreEqual(EligibilityPolicy.HelpersOnly, d.Eligibility);
            Assert.AreEqual(RequiredResource.Ox, d.Required);
            Assert.IsTrue(d.Steps.Any(s => s.Heavy), "the load and unload work is heavy");
        }

        [Test]
        public void BuildDef_IsHelpersOnly_NeedsNoOx_AndIsHeavy()
        {
            TaskDef d = Build();
            Assert.AreEqual("task.build", d.Id);
            Assert.AreEqual(EligibilityPolicy.HelpersOnly, d.Eligibility);
            Assert.AreEqual(RequiredResource.None, d.Required);
            Assert.IsTrue(d.Steps.Any(s => s.Heavy && s.Kind == StepKind.Work), "the build work is heavy");
        }

        [Test]
        public void BuildDef_WorkStepLastsTheGivenMinutes()
        {
            TaskDef d = ConstructionTaskDefs.Build(42);
            Assert.AreEqual(StepKind.Work, d.Steps[d.Steps.Count - 1].Kind);
            Assert.AreEqual(42, d.Steps[d.Steps.Count - 1].DurationTicks);
        }

        [Test]
        public void DeliverDef_StepsAreFetchLeadLoadLeadUnloadReturn()
        {
            TaskDef d = Deliver();
            StepKind[] kinds = d.Steps.Select(s => s.Kind).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                StepKind.FetchOx, StepKind.LeadTo, StepKind.LoadOx, StepKind.LeadTo, StepKind.UnloadOx, StepKind.ReturnOx,
            }, kinds);
        }

        [Test]
        public void Build_IsNotOfferedToAChildUnder14()
        {
            var w = NewWorld();
            Citizen child = AddHelper(w, 1, 1, ChildBirth);
            TaskRecord t = w.Tasks.Create(Build(), 9);
            w.Step(10);
            Assert.AreEqual(TaskState.Open, t.State, "a child under 14 cannot take a heavy task");
            Assert.IsNull(t.AssigneeId);
            Assert.IsFalse(w.Tasks.IsBusy(child.Id));
        }

        [Test]
        public void Deliver_IsNotOfferedToAChildUnder14()
        {
            var w = NewWorld();
            AddHelper(w, 1, 1, ChildBirth);
            w.People.AddOx(1, "Ox", 100, 100);
            TaskRecord t = w.Tasks.Create(Deliver(), 9);
            w.Tasks.SetOx(t.Id, 1, false);
            w.Step(10);
            Assert.AreEqual(TaskState.Open, t.State, "a child under 14 cannot take a heavy task");
        }

        [Test]
        public void Build_IsNotOfferedToAnAssignedFamily()
        {
            var w = NewWorld();
            Citizen c = AddAssigned(w, 1, 1, 9);
            TaskRecord t = w.Tasks.Create(Build(), 9);
            w.Step(10);
            Assert.AreEqual(TaskState.Open, t.State, "an assigned family never builds");
            Assert.IsNull(t.AssigneeId);
            Assert.IsFalse(w.Tasks.IsBusy(c.Id));
        }

        [Test]
        public void Deliver_IsNotOfferedToAnAssignedFamily()
        {
            var w = NewWorld();
            AddAssigned(w, 1, 1, 9);
            w.People.AddOx(1, "Ox", 100, 100);
            TaskRecord t = w.Tasks.Create(Deliver(), 9);
            w.Tasks.SetOx(t.Id, 1, false);
            w.Step(10);
            Assert.AreEqual(TaskState.Open, t.State, "an assigned family never delivers");
        }

        [Test]
        public void Build_IsTakenByAWorkingAgeHelper()
        {
            var w = NewWorld();
            Citizen c = AddHelper(w, 1, 1, AdultBirth);
            TaskRecord t = w.Tasks.Create(Build(), 9);
            w.Step(10);
            Assert.AreEqual(TaskState.Active, t.State);
            Assert.AreEqual(c.Id, t.AssigneeId);
        }

        [Test]
        public void Deliver_IsTakenByAWorkingAgeHelperWithAnOx()
        {
            var w = NewWorld();
            Citizen c = AddHelper(w, 1, 1, AdultBirth);
            w.People.AddOx(1, "Ox", 100, 100);
            TaskRecord t = w.Tasks.Create(Deliver(), 9);
            w.Tasks.SetOx(t.Id, 1, false);
            w.Step(10);
            Assert.AreEqual(TaskState.Active, t.State);
            Assert.AreEqual(c.Id, t.AssigneeId);
        }

        [Test]
        public void TasksCreatedWithoutPriority_AreNormal()
        {
            var w = NewWorld();
            Assert.AreEqual(TaskPriority.Normal, w.Tasks.Create(Build(), 9).Priority);
            Assert.AreEqual(TaskPriority.Normal, w.Tasks.Create(Deliver(), 9).Priority);
        }

        [Test]
        public void VerbFor_DeliverNamesTheModule_AndBuildNamesTheModule()
        {
            Assert.AreEqual("Delivering logs to Hut", ConstructionTaskDefs.VerbFor(ConstructionTaskDefs.DeliverId, "Hut"));
            Assert.AreEqual("Building Hut", ConstructionTaskDefs.VerbFor(ConstructionTaskDefs.BuildId, "Hut"));
        }

        [Test]
        public void VerbFor_WithNoModuleName_OmitsTheModule()
        {
            Assert.AreEqual("Delivering logs", ConstructionTaskDefs.VerbFor(ConstructionTaskDefs.DeliverId, null));
            Assert.AreEqual("Building", ConstructionTaskDefs.VerbFor(ConstructionTaskDefs.BuildId, ""));
        }

        [Test]
        public void VerbFor_OtherDefinitionsGiveNull()
        {
            Assert.IsNull(ConstructionTaskDefs.VerbFor(WoodcuttingSystem.FellTaskId, "Hut"));
        }
    }
}
