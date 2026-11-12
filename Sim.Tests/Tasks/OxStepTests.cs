using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class OxStepTests
    {
        private const int Op = 7;

        private sealed class Rig
        {
            public SimWorld W;
            public Citizen C;
            public Ox O;
            public LedgerLocation Pile, Store;
            public TaskRecord T;
        }

        // Handler at (100,100); ox resting at its home (110,100) beside a 4-log pile; an unlimited store at (140,100).
        private static Rig Build(params TaskStep[] extraAfterLoad)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
            w.People.AddFamily(1, "F", 100, 100);
            Citizen c = w.People.AddCitizen(1, "C", Sex.Male, -4_000_000, 1, 100, 100);
            Ox o = w.People.AddOx(1, "O", 110, 100);
            o.State = OxState.Resting(110000, 100000);
            LedgerLocation pile = w.Inventory.ProduceOnGround(PointMm.FromMetres(110, 100), Op, Items.Log, 4);
            LedgerLocation store = w.Inventory.AddStockpile(PointMm.FromMetres(140, 100));
            return new Rig { W = w, C = c, O = o, Pile = pile, Store = store };
        }

        private static void Start(Rig r, params TaskStep[] steps)
        {
            r.T = r.W.Tasks.Create(new TaskDef("task.haul_test", steps, EligibilityPolicy.AssignedFamilyOnly,
                RequiredResource.Ox), Op);
            r.W.Tasks.SetOx(r.T.Id, r.O.Id, false);
            r.W.Tasks.Assign(r.T.Id, r.C.Id);
            r.W.Tasks.Start(r.T.Id);
            r.O.State = OxState.Reserved(r.T.Id);
        }

        private static double Dist(IMovable a, double x, double z) =>
            System.Math.Sqrt((a.X - x) * (a.X - x) + (a.Z - z) * (a.Z - z));

        private static void RunUntil(Rig r, System.Func<bool> done, int max = 800)
        {
            int n = 0;
            while (!done() && n++ < max) r.W.Step();
            Assert.Less(n, max, "timed out");
        }

        private static TaskStep[] FullHaul(Rig r) => new[]
        {
            TaskStep.FetchOx(),
            TaskStep.LoadOx(r.Pile.Id, 4, 10),
            TaskStep.LeadTo(new PathPoint(140, 100)),
            TaskStep.UnloadOx(r.Store.Id, 4, 10),
            TaskStep.ReturnOx(),
        };

        [Test]
        public void FullHaul_FetchLoadLeadUnloadReturn_MovesLogsAndRestsTheOx()
        {
            Rig r = Build();
            Start(r, FullHaul(r));
            LedgerLocation carrier = null;

            RunUntil(r, () => r.O.State.Kind == OxStateKind.Led);
            Assert.AreEqual(r.C.Id, r.O.State.HandlerId);
            Assert.IsNotNull(r.O.Follow);

            RunUntil(r, () => r.T.CurrentStep == 2);
            carrier = r.W.OxCarrier(r.O);
            Assert.AreEqual(4, carrier.CountOf(Items.Log.Id));
            Assert.AreEqual(0, r.Pile.CountOf(Items.Log.Id));

            RunUntil(r, () => r.T.CurrentStep == 3);
            Assert.That(r.C.X, Is.EqualTo(140).Within(1.0));
            Assert.That(Dist(r.C, r.O.X, r.O.Z), Is.EqualTo(1.5).Within(1e-6));

            RunUntil(r, () => r.T.State == TaskState.Done);
            Assert.AreEqual(4, r.Store.CountOf(Items.Log.Id));
            Assert.AreEqual(0, carrier.CountOf(Items.Log.Id));
            Assert.AreEqual(OxStateKind.Resting, r.O.State.Kind);
            Assert.IsNull(r.O.Follow);
            Assert.That(r.O.X, Is.EqualTo(110).Within(1e-6));
            Assert.IsNull(r.W.Inventory.Audit());
        }

        [Test]
        public void LoadAndUnload_WorkForTheirMinutesBeforeTransferring()
        {
            Rig r = Build();
            Start(r, TaskStep.FetchOx(), TaskStep.LoadOx(r.Pile.Id, 4, 10));
            RunUntil(r, () => r.T.CurrentStep == 1);
            LedgerLocation carrier = r.W.OxCarrier(r.O);
            r.W.Step(9);
            Assert.AreEqual(0, carrier.CountOf(Items.Log.Id));
            Assert.AreEqual(Activity.Working, r.C.Activity);
            r.W.Step(2);
            Assert.AreEqual(4, carrier.CountOf(Items.Log.Id));
            Assert.AreEqual(TaskState.Done, r.T.State);
        }

        [Test]
        public void LeadTo_OxFollowsAtOxSpeed()
        {
            Rig r = Build();
            Start(r, TaskStep.FetchOx(), TaskStep.LeadTo(new PathPoint(150, 100)));
            RunUntil(r, () => r.T.CurrentStep == 1);
            r.W.Step(2);
            double x0 = r.C.X, z0 = r.C.Z;
            r.W.Step(10);
            Assert.That(Dist(r.C, x0, z0), Is.EqualTo(10 * r.W.Tuning.OxSpeedMPerGameMinute).Within(1e-6));
            Assert.That(Dist(r.C, r.O.X, r.O.Z), Is.EqualTo(1.5).Within(1e-6));
        }

        [Test]
        public void OxRemovedMidTask_CancelsAndDropsLogsAsGroundPileAtTheOx()
        {
            Rig r = Build();
            Start(r, FullHaul(r));
            RunUntil(r, () => r.T.CurrentStep == 2);
            r.W.Step(10);
            double ox = r.O.X, oz = r.O.Z;
            LedgerLocation carrier = r.W.OxCarrier(r.O);
            r.W.People.RemoveOx(r.O.Id);
            r.W.Step(2);

            Assert.AreEqual(TaskState.Cancelled, r.T.State);
            Assert.AreEqual(0, carrier.CountOf(Items.Log.Id));
            LedgerLocation dropped = null;
            foreach (LedgerLocation l in r.W.Inventory.Locations)
                if (l.Kind == LocationKind.GroundPile && l.CountOf(Items.Log.Id) == 4 && l.OwnerOperationId == Op) dropped = l;
            Assert.IsNotNull(dropped);
            Assert.That(dropped.Position.DistanceTo(PointMm.FromMetres(ox, oz)), Is.LessThan(3000));
            Assert.IsFalse(r.W.Tasks.IsBusy(r.C.Id));
            Assert.IsNull(r.W.Inventory.Audit());
        }

        [Test]
        public void OxUnassignedMidTask_CancelsDropsLogsAndFreesTheOx()
        {
            Rig r = Build();
            Start(r, FullHaul(r));
            r.W.Tasks.SetOx(r.T.Id, r.O.Id, true); // it was assigned to the operation; now nothing owns it
            RunUntil(r, () => r.T.State == TaskState.Cancelled, 5);
            Assert.AreEqual(OxStateKind.Free, r.O.State.Kind);
            Assert.IsNull(r.O.Follow);
            Assert.AreEqual(4, r.Pile.CountOf(Items.Log.Id));
        }

        [Test]
        public void OxRemovedAfterLoad_LogsDropWhereTheOxLastStood()
        {
            Rig r = Build();
            Start(r, TaskStep.FetchOx(), TaskStep.LoadOx(r.Pile.Id, 4, 10), TaskStep.LeadTo(new PathPoint(150, 100)));
            RunUntil(r, () => r.T.CurrentStep == 2);
            r.W.Step(5);
            double ox = r.O.X;
            r.W.People.RemoveOx(r.O.Id);
            r.W.Step(2);
            Assert.AreEqual(TaskState.Cancelled, r.T.State);
            double total = 0;
            foreach (LedgerLocation l in r.W.Inventory.Locations)
                if (l.Kind == LocationKind.GroundPile && l.Position.DistanceTo(PointMm.FromMetres(ox, 100)) < 3000)
                    total += l.CountOf(Items.Log.Id);
            Assert.AreEqual(4, total);
        }

        [Test]
        public void LoadFromMissingPile_CancelsTheTask()
        {
            Rig r = Build();
            Start(r, TaskStep.FetchOx(), TaskStep.LoadOx(9999, 4, 3));
            RunUntil(r, () => r.T.IsFinished);
            Assert.AreEqual(TaskState.Cancelled, r.T.State);
            Assert.AreEqual(OxStateKind.Free, r.O.State.Kind);
        }

        [Test]
        public void OxStepWithoutAnOx_CancelsTheTask()
        {
            Rig r = Build();
            r.T = r.W.Tasks.Create(new TaskDef("task.no_ox", new[] { TaskStep.FetchOx() }, EligibilityPolicy.AssignedFamilyOnly), Op);
            r.W.Tasks.Assign(r.T.Id, r.C.Id);
            r.W.Tasks.Start(r.T.Id);
            r.W.Step(2);
            Assert.AreEqual(TaskState.Cancelled, r.T.State);
        }

        [Test]
        public void Factories_RejectBadArguments()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => TaskStep.LoadOx(1, 0, 5));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => TaskStep.UnloadOx(1, 2, 0));
        }
    }
}
