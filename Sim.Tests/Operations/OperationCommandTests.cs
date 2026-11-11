using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>AssignFamily, UnassignFamily, Pause/Resume, SetWorkArea, UseDefaultWorkArea (T-270).</summary>
    public class OperationCommandTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);
        private static readonly TaskDef Fell = new TaskDef("task.fell",
            new[] { TaskStep.MoveTo(), TaskStep.Work(10, true) }, EligibilityPolicy.AssignedFamilyOnly);

        private static SimWorld World() => new SimWorld(new SimTuning());

        private static int Designate(SimWorld w, string type, double x0, double z0)
        {
            var shape = AreaShape.Straight(new[] { M(x0, z0), M(x0 + 20, z0), M(x0 + 20, z0 + 20), M(x0, z0 + 20) });
            CommandResult r = w.Execute(new DesignateArea(shape, type));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return (int)r.Created[0];
        }

        private static Operation Op(SimWorld w) =>
            w.Operations.FindByArea(Designate(w, AreaTypes.WoodcuttingId, 100, 100));

        private static void Family(SimWorld w, int id, int citizenId)
        {
            w.People.AddFamily(id, "F" + id, 0, 0);
            w.People.AddCitizen(citizenId, "C" + citizenId, Sex.Male, 0, id, 0, 0);
        }

        [Test]
        public void Assign_Valid_SetsWorkplaceAndListsFamily()
        {
            var w = World(); var op = Op(w); Family(w, 1, 10);
            Assert.IsTrue(w.Execute(new AssignFamily(op.Id, 1)).IsAccepted);
            Assert.AreEqual(op.Id, w.People.FindFamily(1).WorkplaceId);
            CollectionAssert.AreEqual(new[] { 1 }, op.FamilyIds);
        }

        [Test]
        public void Assign_UnknownOperation_NotFound_ChangesNothing()
        {
            var w = World(); Family(w, 1, 10);
            CommandResult r = w.Execute(new AssignFamily(99, 1));
            Assert.AreEqual(CommandRejectCode.NotFound, r.Reason);
            Assert.IsNull(w.People.FindFamily(1).WorkplaceId);
        }

        [Test]
        public void Assign_UnknownFamily_NotFound_ChangesNothing()
        {
            var w = World(); var op = Op(w); int v = w.Operations.Version;
            CommandResult r = w.Execute(new AssignFamily(op.Id, 5));
            Assert.AreEqual(CommandRejectCode.NotFound, r.Reason);
            Assert.AreEqual(v, w.Operations.Version);
            Assert.AreEqual(0, op.FamilyIds.Count);
        }

        [Test]
        public void Assign_SlotsFull_NotAllowed_ChangesNothing()
        {
            var w = World(); var op = Op(w);
            Family(w, 1, 10); Family(w, 2, 11); Family(w, 3, 12);
            w.Execute(new AssignFamily(op.Id, 1)); w.Execute(new AssignFamily(op.Id, 2));
            int v = w.Operations.Version;
            CommandResult r = w.Execute(new AssignFamily(op.Id, 3));
            Assert.AreEqual(CommandRejectCode.NotAllowed, r.Reason);
            Assert.IsNull(w.People.FindFamily(3).WorkplaceId);
            Assert.AreEqual(2, op.FamilyIds.Count);
            Assert.AreEqual(v, w.Operations.Version);
        }

        [Test]
        public void Assign_FamilyAlreadyAtWorkplace_NotAllowed_ChangesNothing()
        {
            var w = World(); var op = Op(w);
            var op2 = w.Operations.FindByArea(Designate(w, AreaTypes.WoodcuttingId, 300, 300));
            Family(w, 1, 10);
            w.Execute(new AssignFamily(op.Id, 1));
            CommandResult r = w.Execute(new AssignFamily(op2.Id, 1));
            Assert.AreEqual(CommandRejectCode.NotAllowed, r.Reason);
            Assert.AreEqual(op.Id, w.People.FindFamily(1).WorkplaceId);
            Assert.AreEqual(0, op2.FamilyIds.Count);
        }

        [Test]
        public void Unassign_ClearsWorkplaceAndList()
        {
            var w = World(); var op = Op(w); Family(w, 1, 10);
            w.Execute(new AssignFamily(op.Id, 1));
            Assert.IsTrue(w.Execute(new UnassignFamily(op.Id, 1)).IsAccepted);
            Assert.IsNull(w.People.FindFamily(1).WorkplaceId);
            Assert.AreEqual(0, op.FamilyIds.Count);
        }

        [Test]
        public void Unassign_CancelsMembersTasksAndReleasesReservations()
        {
            var w = World(); var op = Op(w); Family(w, 1, 10);
            w.Trees.Add(new TreeId(1), 0, 0, 10);
            w.Execute(new AssignFamily(op.Id, 1));
            TaskRecord t = w.Tasks.Create(Fell, op.Id, new TreeId(1));
            Assert.IsTrue(w.Tasks.Reserve(t.Id, new TreeId(1)));
            w.Tasks.Assign(t.Id, 10); w.Tasks.Start(t.Id);
            TaskRecord other = w.Tasks.Create(Fell, op.Id);
            w.Execute(new UnassignFamily(op.Id, 1));
            Assert.AreEqual(TaskState.Cancelled, t.State);
            Assert.IsNull(t.AssigneeId);
            Assert.IsTrue(w.Trees.TryGet(new TreeId(1), out var rec));
            Assert.AreNotEqual(t.Id, rec.ReservedBy);
            Assert.AreEqual(TaskState.Open, other.State);
        }

        [Test]
        public void Unassign_LeavesOtherFamiliesAndOtherOperationsTasks()
        {
            var w = World(); var op = Op(w); Family(w, 1, 10); Family(w, 2, 11);
            w.Execute(new AssignFamily(op.Id, 1)); w.Execute(new AssignFamily(op.Id, 2));
            TaskRecord mine = w.Tasks.Create(Fell, op.Id); w.Tasks.Assign(mine.Id, 11);
            TaskRecord foreign = w.Tasks.Create(Fell, 999); w.Tasks.Assign(foreign.Id, 10);
            w.Execute(new UnassignFamily(op.Id, 1));
            Assert.AreEqual(TaskState.Assigned, mine.State);
            Assert.AreEqual(TaskState.Assigned, foreign.State);
            Assert.AreEqual(op.Id, w.People.FindFamily(2).WorkplaceId);
        }

        [Test]
        public void Unassign_NotAssignedOrUnknown_RefusedChangesNothing()
        {
            var w = World(); var op = Op(w); Family(w, 1, 10);
            TaskRecord t = w.Tasks.Create(Fell, op.Id); w.Tasks.Assign(t.Id, 10);
            int v = w.Tasks.Version;
            Assert.AreEqual(CommandRejectCode.NotAllowed, w.Execute(new UnassignFamily(op.Id, 1)).Reason);
            Assert.AreEqual(CommandRejectCode.NotFound, w.Execute(new UnassignFamily(op.Id, 7)).Reason);
            Assert.AreEqual(CommandRejectCode.NotFound, w.Execute(new UnassignFamily(77, 1)).Reason);
            Assert.AreEqual(TaskState.Assigned, t.State);
            Assert.AreEqual(v, w.Tasks.Version);
        }

        [Test]
        public void PauseResume_TogglesAndRefusesRepeats()
        {
            var w = World(); var op = Op(w);
            Assert.AreEqual(CommandRejectCode.NotAllowed, w.Execute(new ResumeOperation(op.Id)).Reason);
            Assert.IsTrue(w.Execute(new PauseOperation(op.Id)).IsAccepted);
            Assert.IsTrue(op.Paused);
            int v = w.Operations.Version;
            Assert.AreEqual(CommandRejectCode.NotAllowed, w.Execute(new PauseOperation(op.Id)).Reason);
            Assert.AreEqual(v, w.Operations.Version);
            Assert.IsTrue(w.Execute(new ResumeOperation(op.Id)).IsAccepted);
            Assert.IsFalse(op.Paused);
        }

        [Test]
        public void PauseResume_UnknownOperation_NotFound()
        {
            var w = World();
            Assert.AreEqual(CommandRejectCode.NotFound, w.Execute(new PauseOperation(5)).Reason);
            Assert.AreEqual(CommandRejectCode.NotFound, w.Execute(new ResumeOperation(5)).Reason);
        }

        [Test]
        public void SetWorkArea_WorkWoodcuttingArea_Links()
        {
            var w = World(); var op = Op(w);
            int wa = Designate(w, AreaTypes.WoodcuttingWorkId, 200, 200);
            Assert.IsTrue(w.Execute(new SetWorkArea(op.Id, wa)).IsAccepted);
            Assert.AreEqual(wa, op.WorkAreaId);
        }

        [Test]
        public void SetWorkArea_WrongTypeOrUnknown_RefusedChangesNothing()
        {
            var w = World(); var op = Op(w);
            int other = Designate(w, AreaTypes.TestWorkId, 200, 200);
            int v = w.Operations.Version;
            Assert.AreEqual(CommandRejectCode.NotAllowed, w.Execute(new SetWorkArea(op.Id, other)).Reason);
            Assert.AreEqual(CommandRejectCode.NotAllowed, w.Execute(new SetWorkArea(op.Id, op.AreaId)).Reason);
            Assert.AreEqual(CommandRejectCode.NotFound, w.Execute(new SetWorkArea(op.Id, 999)).Reason);
            Assert.AreEqual(CommandRejectCode.NotFound, w.Execute(new SetWorkArea(999, other)).Reason);
            Assert.IsNull(op.WorkAreaId);
            Assert.AreEqual(v, w.Operations.Version);
        }

        [Test]
        public void UseDefaultWorkArea_ClearsLink_AndIsHarmlessWhenAlreadyDefault()
        {
            var w = World(); var op = Op(w);
            int wa = Designate(w, AreaTypes.WoodcuttingWorkId, 200, 200);
            w.Execute(new SetWorkArea(op.Id, wa));
            Assert.IsTrue(w.Execute(new UseDefaultWorkArea(op.Id)).IsAccepted);
            Assert.IsNull(op.WorkAreaId);
            Assert.IsTrue(w.Execute(new UseDefaultWorkArea(op.Id)).IsAccepted);
            Assert.AreEqual(CommandRejectCode.NotFound, w.Execute(new UseDefaultWorkArea(42)).Reason);
        }

        [Test]
        public void DeleteLinkedWorkArea_FallsBackToDefault_OnlyForLinkedOperations()
        {
            var w = World(); var op = Op(w);
            var op2 = w.Operations.FindByArea(Designate(w, AreaTypes.WoodcuttingId, 300, 300));
            int wa = Designate(w, AreaTypes.WoodcuttingWorkId, 200, 200);
            int wb = Designate(w, AreaTypes.WoodcuttingWorkId, 500, 500);
            w.Execute(new SetWorkArea(op.Id, wa)); w.Execute(new SetWorkArea(op2.Id, wb));
            Assert.IsTrue(w.Execute(new DeleteArea(wa)).IsAccepted);
            Assert.IsNull(op.WorkAreaId);
            Assert.AreEqual(wb, op2.WorkAreaId);
        }
    }
}
