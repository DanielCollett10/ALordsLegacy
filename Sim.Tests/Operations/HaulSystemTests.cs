using System.Collections.Generic;
using System.Text;
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
    /// <summary>Haul tasks: who hauls, where to, reservations (T-290).</summary>
    public class HaulSystemTests
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

        // Area x 90..120, z 90..110; hut (100,100); storage 1 (id 2) at (110,100).
        private static Operation Setup(SimWorld w, int workers = 1, int oxen = 1)
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
            for (int i = 0; i < oxen; i++)
            {
                Ox ox = w.People.AddOx(50 + i, "O" + i, 105, 104);
                ox.State = OxState.Resting(105000, 104000);
            }
            return op;
        }

        private static List<TaskRecord> Hauls(SimWorld w, Operation op)
        {
            var r = new List<TaskRecord>();
            foreach (TaskRecord t in w.Tasks.ByOwner(op.Id)) if (t.Def.Id == HaulSystem.HaulTaskId) r.Add(t);
            return r;
        }

        private static LedgerLocation Store(SimWorld w, int footprintId) =>
            w.Inventory.GetOrAddStorage(footprintId, w.Footprints.Get(footprintId).Type);

        [Test]
        public void DefaultSystems_IncludeHaulSystem()
        {
            bool found = false;
            foreach (var s in SimSystems.CreateDefault()) found |= s is HaulSystem;
            Assert.IsTrue(found);
        }

        [Test]
        public void FullLoadWaiting_OpensHaul_ReservingLogsCapacityAndOx()
        {
            SimWorld w = NewWorld();
            Operation op = Setup(w);
            LedgerLocation pile = w.Inventory.ProduceOnGround(M(95, 95), op.Id, Items.Log, 4);
            w.Step(1);

            TaskRecord t = Hauls(w, op)[0];
            Assert.AreEqual(4, pile.ReservedOf(Items.Log.Id));
            Assert.AreEqual(4, Store(w, 2).IncomingOf(Items.Log.Id));
            Assert.AreEqual(50, t.OxId.Value);
            Assert.AreEqual(OxStateKind.Reserved, w.People.FindOx(50).State.Kind);
            Assert.AreEqual(TaskState.Open, t.State);
            Assert.AreEqual(StepKind.FetchOx, t.Def.Steps[0].Kind);
            Assert.AreEqual(StepKind.LeadTo, t.Def.Steps[1].Kind);
            Assert.AreEqual(StepKind.LoadOx, t.Def.Steps[2].Kind);
            Assert.AreEqual(StepKind.LeadTo, t.Def.Steps[3].Kind);
            Assert.AreEqual(StepKind.UnloadOx, t.Def.Steps[4].Kind);
            Assert.AreEqual(StepKind.ReturnOx, t.Def.Steps[5].Kind);
        }

        [Test]
        public void ThreeLogsWaiting_StandingTreeRemains_NoHaulYet()
        {
            SimWorld w = NewWorld((100f, 85f));
            Operation op = Setup(w);
            w.Inventory.ProduceOnGround(M(95, 95), op.Id, Items.Log, 3);
            w.Step(1);
            Assert.AreEqual(0, Hauls(w, op).Count);
        }

        [Test]
        public void ThreeLogsWaiting_NoStandingTree_OpensShortHaul()
        {
            SimWorld w = NewWorld();
            Operation op = Setup(w);
            w.Inventory.ProduceOnGround(M(95, 95), op.Id, Items.Log, 3);
            w.Step(1);
            Assert.AreEqual(1, Hauls(w, op).Count);
            Assert.AreEqual(3, Hauls(w, op)[0].Def.Steps[2].Count);
        }

        [Test]
        public void EightLogsWaiting_OpensTwoLoads_NoMore()
        {
            SimWorld w = NewWorld();
            Operation op = Setup(w, oxen: 2);
            w.Inventory.ProduceOnGround(M(95, 95), op.Id, Items.Log, 8);
            w.Step(3);
            Assert.AreEqual(2, Hauls(w, op).Count);
        }

        [Test]
        public void NoOx_TaskWaits_StatusShowsWaitingForOx_AndFellingContinues()
        {
            SimWorld w = NewWorld((100f, 85f));
            Operation op = Setup(w, oxen: 0);
            w.Inventory.ProduceOnGround(M(95, 95), op.Id, Items.Log, 4);
            w.Step(30);
            TaskRecord haul = Hauls(w, op)[0];
            Assert.IsTrue(haul.IsWaitingForOx);
            Assert.AreEqual(TaskState.Open, haul.State);
            Assert.AreEqual(OperationState.Working, OperationStatus.Of(w, op));
            Assert.AreEqual(OperationState.WaitingForOx, OperationStatus.Reported(w, op));
            Assert.IsTrue(w.Trees.Get(new TreeId(1)).ReservedBy.HasValue, "the worker took the fell task, not the haul");
            Assert.AreEqual(TaskState.Active, w.Tasks.ByAssignee(10)[0].State);
            Assert.AreEqual(WoodcuttingSystem.FellTaskId, w.Tasks.ByAssignee(10)[0].Def.Id);
            Assert.AreEqual(4, w.Inventory.Get(haul.Def.Steps[2].LocationId).CountOf(Items.Log.Id));
        }

        [Test]
        public void OxArrivesLater_WaitingTaskGetsItAndRuns()
        {
            SimWorld w = NewWorld();
            Operation op = Setup(w, oxen: 0);
            w.Inventory.ProduceOnGround(M(95, 95), op.Id, Items.Log, 4);
            w.Step(15);
            Ox ox = w.People.AddOx(60, "late", 105, 104);
            ox.State = OxState.Resting(105000, 104000);
            for (int i = 0; i < 400 && Store(w, 2).CountOf(Items.Log.Id) < 4; i++) w.Step(1);
            Assert.AreEqual(4, Store(w, 2).CountOf(Items.Log.Id));
            Assert.IsFalse(OperationStatus.IsWaitingForOx(w, op));
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void OnlyOxBusy_SecondHaulWaits_ThenGetsTheOxBack()
        {
            SimWorld w = NewWorld();
            Operation op = Setup(w, workers: 2);
            w.Inventory.ProduceOnGround(M(95, 95), op.Id, Items.Log, 8);
            w.Step(2);
            Assert.AreEqual(1, Hauls(w, op).FindAll(t => t.IsWaitingForOx).Count);
            for (int i = 0; i < 1500 && Store(w, 2).CountOf(Items.Log.Id) < 8; i++) w.Step(1);
            Assert.AreEqual(8, Store(w, 2).CountOf(Items.Log.Id));
        }

        [Test]
        public void HaulGoesToTheWorkerNearestTheOx()
        {
            SimWorld w = NewWorld();
            Operation op = Setup(w, workers: 2);
            Citizen far = w.People.FindCitizen(10), near = w.People.FindCitizen(11);
            far.X = 60; near.X = 104; near.Z = 104;
            w.Inventory.ProduceOnGround(M(95, 95), op.Id, Items.Log, 4);
            w.Step(12);
            TaskRecord t = Hauls(w, op)[0];
            Assert.AreEqual(TaskState.Active, t.State);
            Assert.AreEqual(11, t.AssigneeId.Value);
        }

        [Test]
        public void WorkerMidTree_IsNotTakenForAHaul()
        {
            SimWorld w = NewWorld((100f, 85f));
            Operation op = Setup(w);
            for (int i = 0; i < 12; i++) w.Step(1); // the worker is now on the fell task
            TaskRecord fell = null;
            foreach (TaskRecord t in w.Tasks.ByOwner(op.Id)) if (t.Def.Id == WoodcuttingSystem.FellTaskId) fell = t;
            Assert.AreEqual(TaskState.Active, fell.State);
            w.Inventory.ProduceOnGround(M(95, 95), op.Id, Items.Log, 4);
            w.Step(25);
            Assert.AreEqual(TaskState.Active, fell.State, "still felling");
            Assert.AreEqual(TaskState.Open, Hauls(w, op)[0].State, "haul waits for the worker to finish the tree");
            Assert.IsNull(Hauls(w, op)[0].AssigneeId);
        }

        [Test]
        public void OneWorker_AlternatesFellAndHaul()
        {
            SimWorld w = NewWorld((100f, 85f), (104f, 85f), (96f, 85f));
            Operation op = Setup(w, workers: 1);
            var seq = new List<string>();
            int lastId = 0;
            for (int i = 0; i < 2500; i++)
            {
                w.Step(1);
                var held = w.Tasks.ByAssignee(10);
                if (held.Count == 1 && held[0].Id != lastId)
                {
                    lastId = held[0].Id;
                    seq.Add(held[0].Def.Id == HaulSystem.HaulTaskId ? "H" : "F");
                }
            }
            Assert.AreEqual(12, Store(w, 2).CountOf(Items.Log.Id));
            Assert.AreEqual("FHFHFH", string.Join("", seq));
        }

        [Test]
        public void TwoWorkers_OneHaulsWhileTheOtherFells()
        {
            SimWorld w = NewWorld((100f, 85f), (104f, 85f), (96f, 85f), (100f, 80f), (104f, 80f), (96f, 80f));
            Operation op = Setup(w, workers: 2);
            int overlap = 0;
            for (int i = 0; i < 2500; i++)
            {
                w.Step(1);
                int hauler = -1, feller = -1;
                foreach (TaskRecord t in w.Tasks.InState(TaskState.Active))
                {
                    if (t.Def.Id == HaulSystem.HaulTaskId) hauler = t.AssigneeId.Value;
                    else feller = t.AssigneeId.Value;
                }
                if (hauler >= 0 && feller >= 0) { overlap++; Assert.AreNotEqual(hauler, feller); }
            }
            Assert.Greater(overlap, 20, "one worker fells while the other hauls");
            Assert.AreEqual(20, Store(w, 2).CountOf(Items.Log.Id), "storage ends full");
        }

        [Test]
        public void NearestStorageWithSpace_IsChosen_ThenNextWhenFull()
        {
            SimWorld w = NewWorld();
            Operation op = Setup(w, oxen: 2);
            w.Footprints.Add(3, ModuleTypes.LogStorage, M(94, 100), 0);
            w.Operations.AddModule(op.Id, 3);
            LedgerLocation near = Store(w, 3), far = Store(w, 2);
            w.Inventory.ProduceOnGround(M(93, 95), op.Id, Items.Log, 4);
            w.Step(1);
            Assert.AreEqual(4, near.IncomingOf(Items.Log.Id));
            Assert.AreEqual(0, far.IncomingOf(Items.Log.Id));

            // Nearest is now full (counting what is reserved): the next load goes to the other storage.
            w.Inventory.Produce(near, Items.Log, 16);
            w.Inventory.ProduceOnGround(M(93, 95), op.Id, Items.Log, 4);
            w.Step(1);
            Assert.AreEqual(4, far.IncomingOf(Items.Log.Id));
            Assert.AreEqual(0, near.FreeSpace);
        }

        [Test]
        public void StorageWithRoomForOnlyPartOfALoad_OtherStorageTakesTheWholeLoad()
        {
            SimWorld w = NewWorld();
            Operation op = Setup(w);
            w.Footprints.Add(3, ModuleTypes.LogStorage, M(94, 100), 0);
            w.Operations.AddModule(op.Id, 3);
            w.Inventory.Produce(Store(w, 3), Items.Log, 18); // 2 free
            w.Inventory.ProduceOnGround(M(93, 95), op.Id, Items.Log, 4);
            w.Step(1);
            Assert.AreEqual(4, Store(w, 2).IncomingOf(Items.Log.Id));
            Assert.AreEqual(0, Store(w, 3).IncomingOf(Items.Log.Id));
        }

        [Test]
        public void NeverExceedsCapacity_AcrossLongRun_AndLedgerStaysConsistent()
        {
            SimWorld w = NewWorld((100f, 85f), (101f, 85f), (102f, 85f), (103f, 85f), (104f, 85f), (105f, 85f), (106f, 85f), (107f, 85f));
            Operation op = Setup(w, workers: 2, oxen: 2);
            LedgerLocation store = Store(w, 2);
            for (int i = 0; i < 4000; i++)
            {
                w.Step(1);
                Assert.LessOrEqual(store.Total, 20, "tick " + i);
                Assert.LessOrEqual(store.Total + store.IncomingTotal, 20, "tick " + i);
                if (i % 50 == 0) Assert.IsNull(w.Inventory.Audit(), "tick " + i);
            }
            Assert.AreEqual(20, store.CountOf(Items.Log.Id));
            Assert.AreEqual(OperationState.StorageFull, OperationStatus.Of(w, op));
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void CancelledOpenHaul_ReleasesReservationsAndOx()
        {
            SimWorld w = NewWorld();
            Operation op = Setup(w);
            LedgerLocation pile = w.Inventory.ProduceOnGround(M(95, 95), op.Id, Items.Log, 4);
            w.Step(1);
            TaskRecord t = Hauls(w, op)[0];
            Assert.IsTrue(w.Tasks.Cancel(t.Id));
            w.Step(1);
            Assert.AreEqual(TaskState.Cancelled, t.State);
            Assert.AreEqual(1, Hauls(w, op).FindAll(h => !h.IsFinished).Count, "a fresh haul replaces it");
            Assert.AreEqual(4, Store(w, 2).IncomingOf(Items.Log.Id), "old room released, new room reserved");
            Assert.AreEqual(4, pile.ReservedOf(Items.Log.Id));
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void PausedOperation_OpensNoHaul()
        {
            SimWorld w = NewWorld();
            Operation op = Setup(w);
            w.Execute(new PauseOperation(op.Id));
            w.Inventory.ProduceOnGround(M(95, 95), op.Id, Items.Log, 4);
            w.Step(3);
            Assert.AreEqual(0, Hauls(w, op).Count);
        }

        [Test]
        public void AssignedOx_IsPreferredOverPoolOx()
        {
            SimWorld w = NewWorld();
            Operation op = Setup(w, oxen: 2);
            Ox mine = w.People.FindOx(51);
            mine.X = 125; mine.Z = 108; // farther away than the pool ox
            Assert.IsTrue(w.Operations.AssignOx(op.Id, 51));
            w.Inventory.ProduceOnGround(M(95, 95), op.Id, Items.Log, 4);
            w.Step(1);
            Assert.AreEqual(51, Hauls(w, op)[0].OxId.Value);
            Assert.IsTrue(w.Tasks.Get(Hauls(w, op)[0].Id).OxWasAssigned);
        }

        [Test]
        public void FullHaul_DeliversLogs_FreesWorkerAndOx()
        {
            SimWorld w = NewWorld();
            Operation op = Setup(w);
            w.Inventory.ProduceOnGround(M(95, 95), op.Id, Items.Log, 4);
            for (int i = 0; i < 800 && Store(w, 2).CountOf(Items.Log.Id) < 4; i++) w.Step(1);
            w.Step(100);
            TaskRecord t = Hauls(w, op)[0];
            Assert.AreEqual(TaskState.Done, t.State);
            Assert.AreEqual(4, Store(w, 2).CountOf(Items.Log.Id));
            Assert.IsFalse(w.Tasks.IsBusy(10));
            Assert.AreEqual(OxStateKind.Resting, w.People.FindOx(50).State.Kind);
            Assert.AreEqual(0, Store(w, 2).IncomingOf(Items.Log.Id));
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void Determinism_SameInputsSameResult()
        {
            string Run()
            {
                SimWorld w = NewWorld((100f, 85f), (105f, 86f), (96f, 88f), (99f, 80f));
                Operation op = Setup(w, workers: 2, oxen: 2);
                w.Step(1500);
                var sb = new StringBuilder();
                foreach (TaskRecord t in w.Tasks.All)
                    sb.Append(t.Id).Append(':').Append(t.State).Append(':').Append(t.AssigneeId).Append(':').Append(t.OxId).Append(';');
                foreach (var c in w.People.Citizens) sb.Append(c.X).Append(',').Append(c.Z).Append(';');
                foreach (Ox o in w.People.Oxen) sb.Append(o.X).Append(',').Append(o.Z).Append(o.State.Kind).Append(';');
                sb.Append(Store(w, 2).Total).Append(':').Append(w.Inventory.Version);
                return sb.ToString();
            }
            Assert.AreEqual(Run(), Run());
        }
    }
}
