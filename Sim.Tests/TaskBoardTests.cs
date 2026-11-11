using System;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class TaskBoardTests
    {
        private static readonly TaskDef Fell = new TaskDef("task.fell",
            new[] { TaskStep.MoveTo(), TaskStep.Work(10, true) }, EligibilityPolicy.AssignedFamilyOnly);

        private static SimWorld World()
        {
            var w = new SimWorld(new SimTuning());
            w.Trees.Add(new TreeId(1), 0, 0, 10);
            w.Trees.Add(new TreeId(2), 5, 0, 10);
            return w;
        }

        [Test]
        public void Def_NoSteps_Throws() =>
            Assert.Throws<ArgumentException>(() => new TaskDef("x", new TaskStep[0], EligibilityPolicy.HelpersOnly));

        [Test]
        public void Work_ZeroDuration_Throws() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => TaskStep.Work(0));

        [Test]
        public void Def_KeepsPolicyResourceAndHeavyFlags()
        {
            var d = new TaskDef("t", new[] { TaskStep.MoveTo(), TaskStep.Work(3, true) },
                EligibilityPolicy.HelpersOnly, RequiredResource.Ox);
            Assert.AreEqual(EligibilityPolicy.HelpersOnly, d.Eligibility);
            Assert.AreEqual(RequiredResource.Ox, d.Required);
            Assert.IsFalse(d.Steps[0].Heavy);
            Assert.IsTrue(d.Steps[1].Heavy);
            Assert.AreEqual(3, d.Steps[1].DurationTicks);
        }

        [Test]
        public void Create_IsOpenWithNoAssignee_AndBumpsVersion()
        {
            var w = World();
            int v = w.Tasks.Version;
            var t = w.Tasks.Create(Fell, 7, new TreeId(1), 5);
            Assert.AreEqual(TaskState.Open, t.State);
            Assert.IsNull(t.AssigneeId);
            Assert.AreEqual(5, t.Priority);
            Assert.Greater(w.Tasks.Version, v);
        }

        [Test]
        public void FullLifecycle_OpenAssignedActiveDone()
        {
            var b = World().Tasks;
            var t = b.Create(Fell, 1);
            Assert.IsTrue(b.Assign(t.Id, 9));
            Assert.AreEqual(TaskState.Assigned, t.State);
            Assert.IsTrue(b.Start(t.Id));
            Assert.AreEqual(TaskState.Active, t.State);
            Assert.IsTrue(b.AddProgress(t.Id, 4));
            Assert.AreEqual(4, t.StepProgress);
            Assert.IsTrue(b.CompleteStep(t.Id));
            Assert.AreEqual(1, t.CurrentStep);
            Assert.AreEqual(0, t.StepProgress);
            Assert.AreEqual(TaskState.Active, t.State);
            Assert.IsTrue(b.CompleteStep(t.Id));
            Assert.AreEqual(TaskState.Done, t.State);
        }

        [Test]
        public void IllegalTransitions_AreRefused()
        {
            var b = World().Tasks;
            var t = b.Create(Fell, 1);
            Assert.IsFalse(b.Start(t.Id), "Open cannot start");
            Assert.IsFalse(b.CompleteStep(t.Id));
            Assert.IsFalse(b.AddProgress(t.Id, 1));
            Assert.IsTrue(b.Assign(t.Id, 1));
            Assert.IsFalse(b.Assign(t.Id, 2), "already assigned");
            Assert.IsFalse(b.Assign(999, 1));
            b.Cancel(t.Id);
            Assert.IsFalse(b.Start(t.Id));
            Assert.IsFalse(b.Cancel(t.Id), "cancel twice");
        }

        [Test]
        public void Cancel_ReleasesReservations_AndClearsAssignee()
        {
            var w = World();
            var t = w.Tasks.Create(Fell, 1, new TreeId(1));
            Assert.IsTrue(w.Tasks.Reserve(t.Id, new TreeId(1)));
            Assert.IsTrue(w.Tasks.Reserve(t.Id, new TreeId(2)));
            w.Tasks.Assign(t.Id, 3);
            Assert.IsNotNull(w.Trees.Get(new TreeId(1)).ReservedBy);
            Assert.IsTrue(w.Tasks.Cancel(t.Id));
            Assert.AreEqual(TaskState.Cancelled, t.State);
            Assert.IsNull(w.Trees.Get(new TreeId(1)).ReservedBy);
            Assert.IsNull(w.Trees.Get(new TreeId(2)).ReservedBy);
            Assert.AreEqual(0, t.Reservations.Count);
            Assert.IsNull(t.AssigneeId);
        }

        [Test]
        public void Cancel_ActiveTask_ReleasesReservation()
        {
            var w = World();
            var t = w.Tasks.Create(Fell, 1, new TreeId(1));
            w.Tasks.Reserve(t.Id, new TreeId(1));
            w.Tasks.Assign(t.Id, 3);
            w.Tasks.Start(t.Id);
            Assert.IsTrue(w.Tasks.Cancel(t.Id));
            Assert.IsNull(w.Trees.Get(new TreeId(1)).ReservedBy);
        }

        [Test]
        public void Cancel_DoesNotReleaseTreeReservedByAnotherTask()
        {
            var w = World();
            var a = w.Tasks.Create(Fell, 1);
            var c = w.Tasks.Create(Fell, 1);
            Assert.IsTrue(w.Tasks.Reserve(a.Id, new TreeId(1)));
            Assert.IsFalse(w.Tasks.Reserve(c.Id, new TreeId(1)), "already held");
            w.Tasks.Cancel(c.Id);
            Assert.AreEqual(a.Id, w.Trees.Get(new TreeId(1)).ReservedBy);
        }

        [Test]
        public void Done_ReleasesReservations()
        {
            var w = World();
            var d = new TaskDef("one", new[] { TaskStep.Work(1) }, EligibilityPolicy.HelpersOnly);
            var t = w.Tasks.Create(d, 1);
            w.Tasks.Reserve(t.Id, new TreeId(2));
            w.Tasks.Assign(t.Id, 1); w.Tasks.Start(t.Id); w.Tasks.CompleteStep(t.Id);
            Assert.AreEqual(TaskState.Done, t.State);
            Assert.IsNull(w.Trees.Get(new TreeId(2)).ReservedBy);
        }

        [Test]
        public void Reserve_OnFinishedOrUnknownTask_Fails()
        {
            var w = World();
            var t = w.Tasks.Create(Fell, 1);
            w.Tasks.Cancel(t.Id);
            Assert.IsFalse(w.Tasks.Reserve(t.Id, new TreeId(1)));
            Assert.IsFalse(w.Tasks.Reserve(42, new TreeId(1)));
            var t2 = w.Tasks.Create(Fell, 1);
            Assert.IsFalse(w.Tasks.Reserve(t2.Id, new TreeId(99)));
        }

        [Test]
        public void Queries_ByOwnerAndAssignee()
        {
            var b = World().Tasks;
            var a = b.Create(Fell, 1);
            var c = b.Create(Fell, 2);
            var d = b.Create(Fell, 1);
            b.Assign(a.Id, 10); b.Assign(c.Id, 10); b.Assign(d.Id, 11);
            CollectionAssert.AreEqual(new[] { a, d }, b.ByOwner(1));
            CollectionAssert.AreEqual(new[] { a, c }, b.ByAssignee(10));
            b.Cancel(a.Id);
            CollectionAssert.AreEqual(new[] { c }, b.ByAssignee(10));
            Assert.IsEmpty(b.ByOwner(99));
            Assert.IsEmpty(b.ByAssignee(99));
        }

        [Test]
        public void Version_BumpsOnEachChange_NotOnRefusal()
        {
            var b = World().Tasks;
            var t = b.Create(Fell, 1);
            int v = b.Version;
            b.Start(t.Id);
            Assert.AreEqual(v, b.Version);
            b.Assign(t.Id, 1);
            Assert.AreEqual(v + 1, b.Version);
        }

        [Test]
        public void World_TasksNeverNull_AndSameInstance()
        {
            var w = new SimWorld(new SimTuning());
            Assert.IsNotNull(w.Tasks);
            Assert.AreSame(w.Tasks, w.Tasks);
        }
    }
}
