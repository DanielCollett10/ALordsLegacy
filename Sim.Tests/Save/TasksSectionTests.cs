using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Save
{
    public class TasksSectionTests
    {
        private static MapData Map() =>
            new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);

        private static byte[] Bytes(SimWorld w, MapData map) => SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);

        private static SimWorld Load(SimWorld w, MapData map)
        {
            byte[] bytes = Bytes(w, map);
            SimWorld back = SimSave.Read(bytes, map);
            Assert.AreEqual(bytes, Bytes(back, map), "second write is identical");
            return back;
        }

        private static TaskDef FellDef() => new TaskDef(WoodcuttingSystem.FellTaskId, new[]
        {
            TaskStep.MoveTo(), TaskStep.Work(5, true, "stage.fell"), TaskStep.Work(7, false, "stage.limb")
        }, EligibilityPolicy.AssignedFamilyOnly);

        private static TaskDef HaulDef() => new TaskDef(HaulSystem.HaulTaskId, new[]
        {
            TaskStep.FetchOx(), TaskStep.LeadTo(new PathPoint(30, 31)), TaskStep.LoadOx(4, 3, 6),
            TaskStep.LeadTo(new PathPoint(50, 51)), TaskStep.UnloadOx(5, 3, 8), TaskStep.ReturnOx()
        }, EligibilityPolicy.AssignedFamilyOnly, RequiredResource.Ox);

        private static (SimWorld w, TaskRecord fell, TaskRecord haul, TaskRecord deliver, TaskRecord open) Build(MapData map)
        {
            SimWorld w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
            w.People.AddFamily(1, "Ash", 20, 20);
            Citizen a = w.People.AddCitizen(1, "A", Sex.Male, -40000, 1, 20, 20);
            Citizen b = w.People.AddCitizen(2, "B", Sex.Female, -40000, 1, 22, 20);
            w.People.AddOx(1, "Bess", 25, 25);
            w.People.AddOx(2, "Dan", 26, 26);

            TaskRecord fell = w.Tasks.Create(FellDef(), 9, new TreeId(77));
            w.Tasks.Reserve(fell.Id, new TreeId(77));
            w.Tasks.Assign(fell.Id, a.Id);
            w.Tasks.Start(fell.Id);
            w.Tasks.CompleteStep(fell.Id);
            w.Tasks.AddProgress(fell.Id, 3);

            TaskRecord haul = w.Tasks.Create(HaulDef(), 9, null, TaskPriority.High);
            w.Tasks.SetAwaiting(haul.Id, fell.Id);
            w.Tasks.SetOx(haul.Id, 1, true);

            TaskRecord deliver = w.Tasks.Create(
                ConstructionTaskDefs.Deliver(new PathPoint(10, 11), 4, new PathPoint(60, 61), 5, 2, 3, 4),
                8, null, TaskPriority.High, "hut");
            w.Tasks.SetOx(deliver.Id, 2, false);
            w.Tasks.SetSite(deliver.Id, 12);
            w.Tasks.Assign(deliver.Id, b.Id);
            w.Tasks.Start(deliver.Id);
            w.Tasks.CompleteStep(deliver.Id); // FetchOx done, now leading to the pile
            w.Tasks.AddProgress(deliver.Id, 2);
            w.Tasks.SetWindingDown(deliver.Id);

            TaskRecord open = w.Tasks.Create(ConstructionTaskDefs.Build(30), 8, null, TaskPriority.Normal, "hut");
            return (w, fell, haul, deliver, open);
        }

        [Test]
        public void EveryTaskField_RoundTrips()
        {
            MapData map = Map();
            var (w, fell, haul, deliver, open) = Build(map);
            SimWorld back = Load(w, map);

            Assert.AreEqual(w.Tasks.Count, back.Tasks.Count);
            Assert.AreEqual(w.Tasks.Version, back.Tasks.Version);
            foreach (TaskRecord t in w.Tasks.All)
            {
                TaskRecord u = back.Tasks.Get(t.Id);
                Assert.AreEqual(t.Def.Id, u.Def.Id);
                Assert.AreEqual(t.Def.Eligibility, u.Def.Eligibility, "policy looked up by id");
                Assert.AreEqual(t.Def.Required, u.Def.Required);
                Assert.AreEqual(t.Def.Steps.Count, u.Def.Steps.Count);
                for (int i = 0; i < t.Def.Steps.Count; i++) Assert.AreEqual(t.Def.Steps[i], u.Def.Steps[i], "step " + i);
                Assert.AreEqual(t.State, u.State);
                Assert.AreEqual(t.OwnerOperationId, u.OwnerOperationId);
                Assert.AreEqual(t.AssigneeId, u.AssigneeId);
                Assert.AreEqual(t.Target, u.Target);
                Assert.AreEqual(t.Priority, u.Priority);
                Assert.AreEqual(t.Subject, u.Subject);
                Assert.AreEqual(t.OxId, u.OxId);
                Assert.AreEqual(t.OxWasAssigned, u.OxWasAssigned);
                Assert.AreEqual(t.SiteFootprintId, u.SiteFootprintId);
                Assert.AreEqual(t.CurrentStep, u.CurrentStep);
                Assert.AreEqual(t.StepProgress, u.StepProgress);
                Assert.AreEqual(t.AwaitsTaskId, u.AwaitsTaskId);
                Assert.AreEqual(t.WindingDown, u.WindingDown);
                CollectionAssert.AreEqual(t.Reservations, u.Reservations);
            }
            Assert.AreEqual(TaskState.Active, back.Tasks.Get(fell.Id).State);
            Assert.AreEqual(1, back.Tasks.Get(fell.Id).CurrentStep);
            Assert.AreEqual(3, back.Tasks.Get(fell.Id).StepProgress);
            Assert.AreEqual(fell.Id, back.Tasks.Get(haul.Id).AwaitsTaskId);
            Assert.AreEqual(1, back.Tasks.Get(deliver.Id).CurrentStep);
            Assert.IsTrue(back.Tasks.Get(deliver.Id).WindingDown);
        }

        [Test]
        public void IdCounter_ContinuesAfterLoad_AndBusySetIsRebuilt()
        {
            MapData map = Map();
            var (w, fell, haul, deliver, open) = Build(map);
            SimWorld back = Load(w, map);
            Assert.IsTrue(back.Tasks.IsBusy(1));
            Assert.IsTrue(back.Tasks.IsBusy(2));
            TaskRecord a = w.Tasks.Create(ConstructionTaskDefs.Build(5), 8);
            TaskRecord b = back.Tasks.Create(ConstructionTaskDefs.Build(5), 8);
            Assert.AreEqual(a.Id, b.Id, "new task id continues the counter");
            Assert.Greater(b.Id, open.Id);
        }

        [Test]
        public void FellHaulAndDeliver_ContinueSteppingIdenticallyAfterLoad()
        {
            MapData map = Map();
            var (w, fell, haul, deliver, open) = Build(map);
            SimWorld back = Load(w, map);
            for (int i = 0; i < 300; i++)
            {
                w.Tasks.AddProgress(fell.Id, 1);
                back.Tasks.AddProgress(fell.Id, 1);
                if (i == 100) { w.Tasks.CompleteStep(fell.Id); back.Tasks.CompleteStep(fell.Id); }
                if (i == 150) { w.Tasks.CompleteStep(fell.Id); back.Tasks.CompleteStep(fell.Id); }
            }
            // The board is driven directly: the step runner's own per-task state (walk orders, ox tracks) is not part of
            // this section.
            foreach (TaskRecord t in w.Tasks.All)
                Assert.AreEqual(t.State, back.Tasks.Get(t.Id).State, "task " + t.Id + " ended in the same state");
            Assert.AreEqual(Bytes(w, map), Bytes(back, map));
        }

        [Test]
        public void UnknownDefId_IsRejected()
        {
            MapData map = Map();
            SimWorld w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
            w.Tasks.Create(new TaskDef("task.mystery", new[] { TaskStep.MoveTo() }, EligibilityPolicy.AssignedFamilyOnly), 1);
            byte[] bytes = Bytes(w, map);
            Assert.Throws<SaveFormatException>(() => SimSave.Read(bytes, map));
        }

        [Test]
        public void EmptyBoard_RoundTrips()
        {
            MapData map = Map();
            SimWorld w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
            SimWorld back = Load(w, map);
            Assert.AreEqual(0, back.Tasks.Count);
        }
    }
}
