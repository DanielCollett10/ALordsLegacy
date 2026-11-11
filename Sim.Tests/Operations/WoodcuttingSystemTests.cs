using System.Collections.Generic;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>Fell tasks from the woodcutting operation (T-276).</summary>
    public class WoodcuttingSystemTests
    {
        private const long AdultBirth = -4_000_000;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        // 200 m flat map, 8:00 start. Trees have a 16 m trunk (4 logs).
        private static SimWorld NewWorld(params (float x, float z)[] trees)
        {
            var list = new List<MapTree>();
            for (int i = 0; i < trees.Length; i++) list.Add(new MapTree(new TreeId(i + 1), trees[i].x, trees[i].z, 16));
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), list.ToArray());
            return SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
        }

        // Building area x 90..120, z 90..110 (it clears trees inside, so trees go outside it).
        // Hut centre (100,100) -> access point (100,97). Storage beside it.
        private static Operation Setup(SimWorld w, int workers = 1)
        {
            CommandResult r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) }), AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Operation op = w.Operations.FindByArea((int)r.Created[0]);
            w.Footprints.Add(1, ModuleTypes.WoodcuttingHut, M(100, 100), 0);
            w.Operations.AddModule(op.Id, 1);
            w.Footprints.Add(2, ModuleTypes.LogStorage, M(110, 100), 0);
            w.Operations.AddModule(op.Id, 2);
            w.People.AddFamily(1, "F", 100, 100);
            for (int i = 0; i < workers; i++)
                w.People.AddCitizen(10 + i, "C" + i, Sex.Male, AdultBirth, 1, 100, 100);
            Assert.IsTrue(w.Execute(new AssignFamily(op.Id, 1)).IsAccepted);
            return op;
        }

        private static List<TaskRecord> Fell(SimWorld w, Operation op, TaskState? state = null)
        {
            var r = new List<TaskRecord>();
            foreach (TaskRecord t in w.Tasks.ByOwner(op.Id))
                if (t.Def.Id == WoodcuttingSystem.FellTaskId && (state == null || t.State == state)) r.Add(t);
            return r;
        }

        [Test]
        public void DefaultSystems_IncludeWoodcuttingSystem()
        {
            bool found = false;
            foreach (var s in SimSystems.CreateDefault()) found |= s is WoodcuttingSystem;
            Assert.IsTrue(found);
        }

        [Test]
        public void Working_OneOpenTaskPerIdleWorker_TargetsNearestTreeToAccessPoint()
        {
            SimWorld w = NewWorld((100f, 85f), (125f, 100f), (80f, 97f));
            Operation op = Setup(w, workers: 1);
            w.Step(1);
            List<TaskRecord> tasks = Fell(w, op);
            Assert.AreEqual(1, tasks.Count);
            Assert.AreEqual(1, tasks[0].Target.Value.Value); // (100,85) is 12 m from (100,97)
            Assert.AreEqual(tasks[0].Id, w.Trees.Get(new TreeId(1)).ReservedBy, "reserved from creation");
            Assert.AreEqual(TaskState.Open, tasks[0].State);
        }

        [Test]
        public void TwoWorkers_GetTwoTasks_NearestFirstThenNext()
        {
            SimWorld w = NewWorld((100f, 85f), (125f, 100f), (80f, 97f));
            Operation op = Setup(w, workers: 2);
            w.Step(1);
            List<TaskRecord> tasks = Fell(w, op);
            Assert.AreEqual(2, tasks.Count);
            Assert.AreEqual(1, tasks[0].Target.Value.Value);
            Assert.AreEqual(3, tasks[1].Target.Value.Value); // (80,97): 20 m beats (125,100): ~25.5 m
        }

        [Test]
        public void ManyTicks_NoDuplicateOpenTasks()
        {
            SimWorld w = NewWorld((100f, 85f), (125f, 100f));
            Operation op = Setup(w, workers: 1);
            w.Step(5);
            Assert.AreEqual(1, Fell(w, op, TaskState.Open).Count);
        }

        [Test]
        public void DrawnWorkArea_Respected_NearerTreeOutsideIsIgnored()
        {
            SimWorld w = NewWorld((100f, 85f), (150f, 150f));
            Operation op = Setup(w);
            CommandResult r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(140, 140), M(160, 140), M(160, 160), M(140, 160) }), AreaTypes.WoodcuttingWorkId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Assert.IsTrue(w.Execute(new SetWorkArea(op.Id, (int)r.Created[0])).IsAccepted);
            w.Step(1);
            List<TaskRecord> tasks = Fell(w, op);
            Assert.AreEqual(1, tasks.Count);
            Assert.AreEqual(2, tasks[0].Target.Value.Value);
        }

        [Test]
        public void NoTrees_NoTasks()
        {
            SimWorld w = NewWorld();
            Operation op = Setup(w);
            w.Step(20);
            Assert.AreEqual(OperationState.NoTrees, OperationStatus.Of(w, op));
            Assert.AreEqual(0, Fell(w, op).Count);
        }

        [Test]
        public void Paused_NoTasks_AndOpenOnesAreCancelledWithReservationReleased()
        {
            SimWorld w = NewWorld((100f, 85f));
            Operation op = Setup(w);
            w.Step(1);
            TaskRecord t = Fell(w, op)[0];
            w.Execute(new PauseOperation(op.Id));
            w.Step(1);
            Assert.AreEqual(TaskState.Cancelled, t.State);
            Assert.IsNull(w.Trees.Get(new TreeId(1)).ReservedBy);
            w.Step(5);
            Assert.AreEqual(1, Fell(w, op).Count, "no new task while paused");
        }

        [Test]
        public void NoWorkers_ChildOnly_NoTasks()
        {
            SimWorld w = NewWorld((100f, 85f));
            Operation op = Setup(w, workers: 0);
            w.People.AddCitizen(50, "kid", Sex.Male, -100, 1, 100, 100);
            w.Step(5);
            Assert.AreEqual(0, Fell(w, op).Count);
        }

        [Test]
        public void FullCycle_TreeFelledThenStump_LogsInOwnedPileAtTree()
        {
            SimWorld w = NewWorld((100f, 85f));
            Operation op = Setup(w);
            var tree = w.Trees.Get(new TreeId(1));
            bool sawFelled = false;
            for (int i = 0; i < 600 && tree.State != TreeState.Stump; i++)
            {
                w.Step(1);
                sawFelled |= tree.State == TreeState.Felled;
                if (tree.State == TreeState.Felled) Assert.AreEqual(0, OperationStatus.LogLoad(w, op), "no logs before buck");
            }
            Assert.IsTrue(sawFelled);
            Assert.AreEqual(TreeState.Stump, tree.State);
            Assert.AreEqual(4, OperationStatus.LogLoad(w, op)); // floor(16 / 4)
            LedgerLocation pile = null;
            foreach (LedgerLocation l in w.Inventory.Locations) if (l.Kind == LocationKind.GroundPile) pile = l;
            Assert.IsNotNull(pile);
            Assert.AreEqual(op.Id, pile.OwnerOperationId);
            Assert.Less(pile.Position.DistanceTo(M(100, 85)), 10);
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void StorageFull_StopsAtTwentyLogs()
        {
            SimWorld w = NewWorld((100f, 85f), (101f, 85f), (102f, 85f), (103f, 85f), (104f, 85f), (105f, 85f), (106f, 85f));
            Operation op = Setup(w);
            for (int i = 0; i < 3000; i++) w.Step(1);
            Assert.AreEqual(20, OperationStatus.LogLoad(w, op));
            Assert.AreEqual(OperationState.StorageFull, OperationStatus.Of(w, op));
            Assert.AreEqual(0, Fell(w, op, TaskState.Open).Count);
            int stumps = 0;
            foreach (var _ in w.Trees.Stumps) stumps++;
            Assert.AreEqual(5, stumps);
        }

        [Test]
        public void Determinism_SameInputsSameResult()
        {
            string Run()
            {
                SimWorld w = NewWorld((100f, 85f), (105f, 86f), (96f, 88f));
                Operation op = Setup(w, workers: 2);
                w.Step(700);
                var sb = new System.Text.StringBuilder();
                foreach (TaskRecord t in w.Tasks.All)
                    sb.Append(t.Id).Append(':').Append(t.State).Append(':').Append(t.Target).Append(':').Append(t.AssigneeId).Append(';');
                foreach (var c in w.People.Citizens) sb.Append(c.X).Append(',').Append(c.Z).Append(';');
                sb.Append(OperationStatus.LogLoad(w, op)).Append(w.Inventory.Version);
                return sb.ToString();
            }
            Assert.AreEqual(Run(), Run());
        }
    }
}
