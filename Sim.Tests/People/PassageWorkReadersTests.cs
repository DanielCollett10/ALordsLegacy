using System.Collections.Generic;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.People
{
    /// <summary>Work readers skip families in passage (T-499, decision 0045, GR-13).</summary>
    public class PassageWorkReadersTests
    {
        private const long AdultBirth = -4_000_000;

        private static SimWorld NewWorld()
        {
            var trees = new[] { new MapTree(new TreeId(1), 110f, 100f, 16) };
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), trees);
            return SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
        }

        private static Citizen Add(SimWorld w, FamilyPassage passage, int? workplace)
        {
            Family f = w.People.AddFamily(1, "F", 100, 100);
            f.WorkplaceId = workplace;
            f.Passage = passage;
            return w.People.AddCitizen(1, "C", Sex.Male, AdultBirth, 1, 100, 100);
        }

        private static readonly TaskDef Light =
            new TaskDef("task.fake", new[] { TaskStep.MoveTo(), TaskStep.Work(5) }, EligibilityPolicy.AssignedFamilyOnly);

        [TestCase(FamilyPassage.Leaving)]
        [TestCase(FamilyPassage.Arriving)]
        public void Dispatch_NoTaskGoesToMemberOfFamilyInPassage(FamilyPassage p)
        {
            var w = NewWorld();
            Add(w, p, 7);
            TaskRecord t = w.Tasks.Create(Light, 7, new TreeId(1));
            w.Step(30);
            Assert.AreEqual(TaskState.Open, t.State);
            Assert.IsFalse(w.Tasks.IsBusy(1));
        }

        [Test]
        public void Dispatch_PassageNone_StillGetsTask()
        {
            var w = NewWorld();
            Add(w, FamilyPassage.None, 7);
            TaskRecord t = w.Tasks.Create(Light, 7, new TreeId(1));
            w.Step(10);
            Assert.AreEqual(TaskState.Active, t.State);
        }

        [Test]
        public void Dispatch_OwnerFamilyTrip_NotGivenToLeavingFamily()
        {
            var w = NewWorld();
            Add(w, FamilyPassage.Leaving, null);
            var def = new TaskDef("task.trip", new[] { TaskStep.MoveTo(), TaskStep.Work(5) }, EligibilityPolicy.OwnerFamily);
            TaskRecord t = w.Tasks.Create(def, 1, new TreeId(1));
            w.Step(30);
            Assert.AreNotEqual(TaskState.Active, t.State);
            Assert.IsFalse(w.Tasks.IsBusy(1));
        }

        [Test]
        public void WorkDay_LeavingFamilyWorker_IsNeverOnShift_AndNoPlanIsCached()
        {
            var w = NewWorld();
            Citizen c = Add(w, FamilyPassage.Leaving, 7);
            Assert.IsFalse(w.WorkDay.OnShift(w, c));
            Assert.AreEqual(long.MaxValue, w.WorkDay.DepartTick(w, c));
            Assert.AreEqual(0, w.WorkDay.ExportPlans().Count);
            w.People.FindFamily(1).Passage = FamilyPassage.None;
            Assert.IsTrue(w.WorkDay.OnShift(w, c));
            Assert.AreEqual(1, w.WorkDay.ExportPlans().Count);
        }

        [Test]
        public void Routine_LeavingFamily_GetsNoMoveOrder_ButNoneDoes()
        {
            var w = NewWorld();
            Citizen c = Add(w, FamilyPassage.Leaving, null);
            w.Step(60);
            // The routine system gives no order; the departure system (T-502) may send the member to an exit.
            Assert.IsTrue(c.Move == null || LordsLegacy.Sim.Roads.MapExits.IsOnEdge(w,
                LordsLegacy.Sim.Geometry.PointMm.FromMetres(c.Move.TargetX, c.Move.TargetZ)), "only an exit order");

            var w2 = NewWorld();
            Citizen c2 = Add(w2, FamilyPassage.None, null);
            bool moved = false;
            for (int i = 0; i < 120 && !moved; i++) { w2.Step(1); moved = c2.Move != null; }
            Assert.IsTrue(moved, "control: a normal family mills about");
        }

        [Test]
        public void Routine_ArrivingFamily_GetsNoMoveOrder()
        {
            var w = NewWorld();
            Citizen c = Add(w, FamilyPassage.Arriving, null);
            w.Step(120);
            Assert.IsNull(c.Move);
        }

        private static SimWorld WorldWithOp(FamilyPassage p, out int opId)
        {
            var w = new SimWorld(new SimTuning());
            CommandResult r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { PointMm.FromMetres(90, 90), PointMm.FromMetres(120, 90),
                    PointMm.FromMetres(120, 110), PointMm.FromMetres(90, 110) }), AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            opId = w.Operations.FindByArea((int)r.Created[0]).Id;
            w.People.AddFamily(1, "F", 100, 100).Passage = p;
            return w;
        }

        [Test]
        public void AssignFamily_Leaving_Refused()
        {
            var w = WorldWithOp(FamilyPassage.Leaving, out int op);
            CommandResult r = w.Execute(new AssignFamily(op, 1));
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual(CommandRejectCode.NotAllowed, r.Reason);
            StringAssert.Contains("This family is leaving", r.Text);
            Assert.IsNull(w.People.FindFamily(1).WorkplaceId);
        }

        [Test]
        public void AssignFamily_Arriving_Refused()
        {
            var w = WorldWithOp(FamilyPassage.Arriving, out int op);
            CommandResult r = w.Execute(new AssignFamily(op, 1));
            Assert.IsTrue(r.IsRejected);
            StringAssert.Contains("This family is arriving", r.Text);
            Assert.IsNull(w.People.FindFamily(1).WorkplaceId);
        }
    }
}
