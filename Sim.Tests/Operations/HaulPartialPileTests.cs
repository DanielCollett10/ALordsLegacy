using System.Collections.Generic;
using System.Linq;
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
    /// <summary>Stranded sub-load log piles must still be hauled when the operation is storage-full (T-490).</summary>
    public class HaulPartialPileTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        // Trunk 8 m = 2 logs per tree. Storage holds 20 logs.
        private static SimWorld NewWorld(int trees, int oxen)
        {
            var list = new List<MapTree>();
            for (int i = 0; i < trees; i++)
                list.Add(new MapTree(new TreeId(i + 1), 92f + (i % 5) * 4f, 86f + (i / 5) * 3f, 8));
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), list.ToArray());
            SimWorld w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
            CommandResult r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(96, 96), M(114, 96), M(114, 112), M(96, 112) }), AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Operation op = w.Operations.FindByArea((int)r.Created[0]);
            w.Footprints.Add(1, ModuleTypes.WoodcuttingHut, M(100, 100), 0);
            w.Operations.AddModule(op.Id, 1);
            w.Footprints.Add(2, ModuleTypes.LogStorage, M(110, 104), 0);
            w.Operations.AddModule(op.Id, 2);
            w.People.AddFamily(1, "F", 100, 100);
            w.People.AddCitizen(10, "C", Sex.Male, -4_000_000, 1, 100, 100);
            Assert.IsTrue(w.Execute(new AssignFamily(op.Id, 1)).IsAccepted);
            for (int i = 0; i < oxen; i++)
            {
                Ox ox = w.People.AddOx(50 + i, "O" + i, 105, 104);
                ox.State = OxState.Resting(105000, 104000);
            }
            return w;
        }

        private static Operation Op(SimWorld w)
        {
            foreach (Operation o in w.Operations.All) return o;
            return null;
        }

        private static int GroundLogs(SimWorld w, Operation op)
        {
            int n = 0;
            foreach (LedgerLocation l in w.Inventory.Locations)
                if (l.Kind == LocationKind.GroundPile && l.OwnerOperationId == op.Id) n += l.CountOf(Items.Log.Id);
            return n;
        }

        private static List<TaskRecord> Hauls(SimWorld w, Operation op)
        {
            var r = new List<TaskRecord>();
            foreach (TaskRecord t in w.Tasks.ByOwner(op.Id)) if (t.Def.Id == HaulSystem.HaulTaskId) r.Add(t);
            return r;
        }

        private static LedgerLocation Store(SimWorld w) =>
            w.Inventory.GetOrAddStorage(2, w.Footprints.Get(2).Type);

        [Test]
        public void ManyTwoLogTrees_StorageFills_EveryLogEndsInStorage_NoneOnGround_TreesStillStand()
        {
            SimWorld w = NewWorld(14, 2);
            Operation op = Op(w);
            w.Step(1);
            Assert.AreEqual(OperationState.Working, OperationStatus.Of(w, op), "state at start");
            w.Step(8000);
            Assert.AreEqual(20, Store(w).CountOf(Items.Log.Id));
            Assert.AreEqual(0, GroundLogs(w, op));
            Assert.IsTrue(w.Trees.AnyStandingIn(OperationStatus.WorkRegion(w, op)), "trees still stand");
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void StorageFullFromSubLoadPilesOnly_OpensHaulsForThem_WithStandingTree()
        {
            SimWorld w = NewWorld(1, 2);
            Operation op = Op(w);
            for (int i = 0; i < 10; i++) w.Inventory.ProduceOnGround(M(90 + i * 3, 90), op.Id, Items.Log, 2);
            Assert.IsTrue(OperationStatus.IsStorageFull(w, op));
            w.Step(1);
            Assert.AreEqual(10, Hauls(w, op).Count);
            foreach (TaskRecord t in Hauls(w, op)) Assert.AreEqual(2, t.Def.Steps[2].Count);
        }

        [Test]
        public void FullLoadPile_StillWaitsForOx_AndPartialHaulNeverReservesMoreThanThePile()
        {
            SimWorld w = NewWorld(1, 0);
            Operation op = Op(w);
            LedgerLocation big = w.Inventory.ProduceOnGround(M(90, 90), op.Id, Items.Log, 4);
            for (int i = 0; i < 8; i++) w.Inventory.ProduceOnGround(M(95 + i * 3, 94), op.Id, Items.Log, 2);
            w.Step(3);
            foreach (TaskRecord t in Hauls(w, op))
            {
                Assert.IsTrue(t.IsWaitingForOx);
                Assert.AreEqual(TaskState.Open, t.State);
            }
            Assert.AreEqual(4, big.ReservedOf(Items.Log.Id));
            foreach (LedgerLocation l in w.Inventory.Locations)
                if (l.Kind == LocationKind.GroundPile)
                    Assert.LessOrEqual(l.ReservedOf(Items.Log.Id), l.CountOf(Items.Log.Id));
            Assert.IsNull(w.Inventory.Audit());
        }
    }
}
