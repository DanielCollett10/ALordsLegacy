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
    /// <summary>
    /// Storage fills by ox haul and stops felling (WC-25, MVP-1-08/10/11, T-292); a second storage or removing logs
    /// lets felling resume. Uses the real haul path, so it runs for thousands of ticks.
    /// </summary>
    public class StorageFullFellingTests
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

        // Area x 90..120, z 90..110; hut (100,100); storage 1 (id 2) at (110,100). Two workers, two oxen.
        private static Operation Setup(SimWorld w)
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
            for (int i = 0; i < 2; i++) w.People.AddCitizen(10 + i, "C" + i, Sex.Male, AdultBirth, 1, 100, 100);
            Assert.IsTrue(w.Execute(new AssignFamily(op.Id, 1)).IsAccepted);
            for (int i = 0; i < 2; i++)
            {
                Ox ox = w.People.AddOx(50 + i, "O" + i, 105, 104);
                ox.State = OxState.Resting(105000, 104000);
            }
            return op;
        }

        private static int Stumps(SimWorld w)
        {
            int n = 0;
            foreach (var _ in w.Trees.Stumps) n++;
            return n;
        }

        private static int UnfinishedFell(SimWorld w, Operation op)
        {
            int n = 0;
            foreach (TaskRecord t in w.Tasks.ByOwner(op.Id))
                if (!t.IsFinished && t.Def.Id == WoodcuttingSystem.FellTaskId) n++;
            return n;
        }

        private static int OpenFell(SimWorld w, Operation op)
        {
            int n = 0;
            foreach (TaskRecord t in w.Tasks.ByOwner(op.Id))
                if (t.State == TaskState.Open && t.Def.Id == WoodcuttingSystem.FellTaskId) n++;
            return n;
        }

        [Test]
        public void HaulsFillStorage_ThenFellingStops_ThenRemovingLogsRestartsIt()
        {
            var trees = new List<(float x, float z)>();
            for (int i = 0; i < 12; i++) trees.Add((100f + i, 85f));
            SimWorld w = NewWorld(trees.ToArray());
            Operation op = Setup(w);
            LedgerLocation store = w.Inventory.GetOrAddStorage(2, ModuleTypes.LogStorage);

            for (int i = 0; i < 8000 && OperationStatus.LogLoad(w, op) < 20; i++) w.Step(1);
            Assert.AreEqual(20, OperationStatus.LogLoad(w, op));
            Assert.AreEqual(OperationState.StorageFull, OperationStatus.Of(w, op));
            Assert.AreEqual(0, OpenFell(w, op), "no new felling while full");
            for (int i = 0; i < 300; i++) w.Step(1); // tasks already under way finish, but no more start
            Assert.AreEqual(20, OperationStatus.LogLoad(w, op), "never overshoots while felling stops");
            Assert.AreEqual(0, OpenFell(w, op));
            Assert.IsNull(w.Inventory.Audit());

            int stumpsWhenFull = Stumps(w);
            Assert.Greater(stumpsWhenFull, 0);
            Assert.Less(stumpsWhenFull, 12, "felling stopped before the wood ran out");

            // Test hook: 5 logs leave storage (through the ledger into the unlimited stockpile).
            Assert.IsTrue(w.Inventory.Transfer(store, w.Inventory.AddStockpile(), Items.Log, 5));
            w.Step(1);
            Assert.AreEqual(OperationState.Working, OperationStatus.Of(w, op));
            Assert.Greater(UnfinishedFell(w, op), 0, "felling restarts");

            for (int i = 0; i < 8000 && Stumps(w) == stumpsWhenFull; i++) w.Step(1);
            Assert.Greater(Stumps(w), stumpsWhenFull, "more trees felled after logs were removed");
            // WC-25 starts a task while load + pending is still under capacity, so the last tree may cross 20 by up to
            // one yield; the excess waits on the ground. Storage itself never goes past its capacity.
            Assert.LessOrEqual(store.Total, 20);
            Assert.Less(OperationStatus.LogLoad(w, op), 20 + 4);
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void SecondStorage_RaisesCapacityTo40_FellingCarriesOn()
        {
            var trees = new List<(float x, float z)>();
            for (int i = 0; i < 12; i++) trees.Add((100f + i, 85f));
            SimWorld w = NewWorld(trees.ToArray());
            Operation op = Setup(w);
            for (int i = 0; i < 8000 && OperationStatus.LogLoad(w, op) < 20; i++) w.Step(1);
            Assert.AreEqual(OperationState.StorageFull, OperationStatus.Of(w, op));
            int stumpsWhenFull = Stumps(w);

            w.Footprints.Add(3, ModuleTypes.LogStorage, M(100, 107), 0);
            w.Operations.AddModule(op.Id, 3);
            Assert.AreEqual(40, OperationStatus.LogCapacity(w, op));
            w.Step(1);
            Assert.AreEqual(OperationState.Working, OperationStatus.Of(w, op));
            for (int i = 0; i < 8000 && OperationStatus.LogLoad(w, op) < 40; i++) w.Step(1);
            Assert.Greater(Stumps(w), stumpsWhenFull, "felling resumed into the new storage");
            Assert.AreEqual(40, OperationStatus.LogLoad(w, op), "fills the 40-log capacity, 10 trees' worth");
            for (int i = 0; i < 300; i++) w.Step(1); // the tenth tree is still being felled when the load reaches 40
            Assert.AreEqual(40, OperationStatus.LogLoad(w, op));
            Assert.AreEqual(OperationState.StorageFull, OperationStatus.Of(w, op));
            Assert.AreEqual(10, Stumps(w));
            Assert.IsNull(w.Inventory.Audit());
        }
    }
}
