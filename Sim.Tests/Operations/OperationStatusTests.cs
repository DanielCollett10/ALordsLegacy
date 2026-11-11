using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>Operation records, creation/deletion with the area, and computed status (T-268).</summary>
    public class OperationStatusTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static AreaShape Square(double x, double z, double s) =>
            AreaShape.Straight(new[] { M(x, z), M(x + s, z), M(x + s, z + s), M(x, z + s) });

        private static SimWorld World() => new SimWorld(new SimTuning());

        private static Operation Designate(SimWorld w, double x = 100, double z = 100)
        {
            CommandResult r = w.Execute(new DesignateArea(Square(x, z, 20), AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return w.Operations.FindByArea((int)r.Created[0]);
        }

        private static int AddModule(SimWorld w, Operation op, FootprintTypeDef type, double x, double z)
        {
            int id = 1;
            foreach (int existing in w.Footprints.Ids) id = existing + 1;
            w.Footprints.Add(id, type, M(x, z), 0);
            w.Operations.AddModule(op.Id, id);
            return id;
        }

        private static void AddFamily(SimWorld w, Operation op, int id, double birthYearsAgo)
        {
            w.People.AddFamily(id, "F" + id, 0, 0);
            w.People.AddCitizen(id * 10, "C" + id, Sex.Male, (long)(-birthYearsAgo / 2 * Citizen.TicksPerYear), id, 0, 0);
            w.Operations.AssignFamily(op.Id, id);
        }

        /// <summary>Hut, storage, adult family, one tree near the hut.</summary>
        private static Operation Ready(SimWorld w, out int storageId)
        {
            Operation op = Designate(w);
            AddModule(w, op, ModuleTypes.WoodcuttingHut, 105, 105);
            storageId = AddModule(w, op, ModuleTypes.LogStorage, 112, 112);
            AddFamily(w, op, 1, 30);
            w.Trees.Add(new TreeId(1), 150f, 150f, 5);
            return op;
        }

        [Test]
        public void Designate_WoodcuttingArea_CreatesOperationWithDefaults()
        {
            SimWorld w = World();
            Operation op = Designate(w);
            Assert.IsNotNull(op);
            Assert.AreEqual(OperationTypes.WoodcuttingId, op.TypeId);
            Assert.AreEqual(1, w.Operations.Count);
            Assert.IsEmpty(op.ModuleIds);
            Assert.IsEmpty(op.FamilyIds);
            Assert.IsEmpty(op.OxIds);
            Assert.IsNull(op.WorkAreaId);
            Assert.IsFalse(op.Paused);
        }

        [Test]
        public void Designate_OtherAreaType_CreatesNoOperation()
        {
            SimWorld w = World();
            w.Execute(new DesignateArea(Square(100, 100, 20), AreaTypes.TestId));
            Assert.AreEqual(0, w.Operations.Count);
        }

        [Test]
        public void DeleteArea_RemovesOperationAndItsModules()
        {
            SimWorld w = World();
            Operation op = Designate(w);
            AddModule(w, op, ModuleTypes.WoodcuttingHut, 105, 105);
            AddModule(w, op, ModuleTypes.LogStorage, 112, 112);
            Assert.IsTrue(w.Execute(new DeleteArea(op.AreaId)).IsAccepted);
            Assert.AreEqual(0, w.Operations.Count);
            Assert.AreEqual(0, w.Footprints.Count);
        }

        [Test]
        public void Version_BumpsOnEveryChange()
        {
            SimWorld w = World();
            Operation op = Designate(w);
            int v = w.Operations.Version;
            w.Operations.AddModule(op.Id, 5); Assert.Greater(w.Operations.Version, v); v = w.Operations.Version;
            w.Operations.AssignFamily(op.Id, 1); Assert.Greater(w.Operations.Version, v); v = w.Operations.Version;
            w.Operations.AssignOx(op.Id, 1); Assert.Greater(w.Operations.Version, v); v = w.Operations.Version;
            w.Operations.SetWorkArea(op.Id, 3); Assert.Greater(w.Operations.Version, v); v = w.Operations.Version;
            w.Operations.SetPaused(op.Id, true); Assert.Greater(w.Operations.Version, v); v = w.Operations.Version;
            w.Operations.SetPaused(op.Id, true); Assert.AreEqual(v, w.Operations.Version, "no-op must not bump");
            w.Operations.UnassignFamily(op.Id, 1); Assert.Greater(w.Operations.Version, v);
        }

        [Test]
        public void Status_Paused_ReportedFirst()
        {
            SimWorld w = World();
            Operation op = Designate(w); // also has no hut etc.
            w.Operations.SetPaused(op.Id, true);
            Assert.AreEqual(OperationState.Paused, OperationStatus.Of(w, op));
        }

        [Test]
        public void Status_NoModules_MissingHutBeforeMissingStorage()
        {
            SimWorld w = World();
            Operation op = Designate(w);
            Assert.AreEqual(OperationState.MissingHut, OperationStatus.Of(w, op));
        }

        [Test]
        public void Status_HutOnly_MissingStorage()
        {
            SimWorld w = World();
            Operation op = Designate(w);
            AddModule(w, op, ModuleTypes.WoodcuttingHut, 105, 105);
            Assert.AreEqual(OperationState.MissingStorage, OperationStatus.Of(w, op));
        }

        [Test]
        public void Status_NoFamilies_NoWorkersBeforeNoTrees()
        {
            SimWorld w = World();
            Operation op = Designate(w);
            AddModule(w, op, ModuleTypes.WoodcuttingHut, 105, 105);
            AddModule(w, op, ModuleTypes.LogStorage, 112, 112);
            Assert.AreEqual(OperationState.NoWorkers, OperationStatus.Of(w, op));
        }

        [Test]
        public void Status_OnlyChildren_NoWorkers()
        {
            SimWorld w = World();
            Operation op = Designate(w);
            AddModule(w, op, ModuleTypes.WoodcuttingHut, 105, 105);
            AddModule(w, op, ModuleTypes.LogStorage, 112, 112);
            AddFamily(w, op, 1, 13.9); // aged years, already includes the ageing rate
            Assert.AreEqual(OperationState.NoWorkers, OperationStatus.Of(w, op));
        }

        [Test]
        public void Status_AdultAtExactly14_IsWorker()
        {
            SimWorld w = World();
            Operation op = Designate(w);
            AddModule(w, op, ModuleTypes.WoodcuttingHut, 105, 105);
            AddModule(w, op, ModuleTypes.LogStorage, 112, 112);
            AddFamily(w, op, 1, 14);
            w.Trees.Add(new TreeId(1), 150f, 150f, 5);
            Assert.AreEqual(OperationState.Working, OperationStatus.Of(w, op));
        }

        [Test]
        public void Status_NoTreesInDefaultCircle_NoTrees()
        {
            SimWorld w = World();
            Operation op = Ready(w, out _);
            w.Trees.Add(new TreeId(2), 900f, 900f, 5);
            w.Trees.SetState(new TreeId(1), TreeState.Stump);
            Assert.AreEqual(OperationState.NoTrees, OperationStatus.Of(w, op));
        }

        [Test]
        public void Status_TreeOutsideWorkPolygon_NoTrees_InsideWorking()
        {
            SimWorld w = World();
            Operation op = Ready(w, out _);
            CommandResult r = w.Execute(new DesignateArea(Square(300, 300, 20), AreaTypes.WoodcuttingWorkId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            w.Operations.SetWorkArea(op.Id, (int)r.Created[0]);
            Assert.AreEqual(OperationState.NoTrees, OperationStatus.Of(w, op));
            w.Trees.Add(new TreeId(3), 310f, 310f, 5);
            Assert.AreEqual(OperationState.Working, OperationStatus.Of(w, op));
        }

        [Test]
        public void Status_Ready_Working()
        {
            SimWorld w = World();
            Operation op = Ready(w, out _);
            Assert.AreEqual(OperationState.Working, OperationStatus.Of(w, op));
        }

        [Test]
        public void StorageFull_LogsInStorageAtCapacity_StorageFull()
        {
            SimWorld w = World();
            Operation op = Ready(w, out int storageId);
            w.Inventory.Produce(w.Inventory.GetOrAddStorage(storageId), Items.Log, 19);
            Assert.AreEqual(OperationState.Working, OperationStatus.Of(w, op));
            w.Inventory.Produce(w.Inventory.GetOrAddStorage(storageId), Items.Log, 1);
            Assert.AreEqual(20, OperationStatus.LogLoad(w, op));
            Assert.AreEqual(OperationState.StorageFull, OperationStatus.Of(w, op));
        }

        [Test]
        public void LogLoad_CountsOwnGroundPilesOnly()
        {
            SimWorld w = World();
            Operation op = Ready(w, out int storageId);
            w.Inventory.Produce(w.Inventory.GetOrAddStorage(storageId), Items.Log, 5);
            w.Inventory.ProduceOnGround(M(120, 120), op.Id, Items.Log, 7);
            w.Inventory.ProduceOnGround(M(500, 500), op.Id + 1, Items.Log, 100);
            w.Inventory.ProduceOnGround(M(120, 120), null, Items.Log, 50);
            Assert.AreEqual(12, OperationStatus.LogLoad(w, op));
        }

        [Test]
        public void StorageFull_GroundPilesPlusStorage_CountTowardsCapacity()
        {
            SimWorld w = World();
            Operation op = Ready(w, out int storageId);
            w.Inventory.Produce(w.Inventory.GetOrAddStorage(storageId), Items.Log, 12);
            w.Inventory.ProduceOnGround(M(120, 120), op.Id, Items.Log, 8);
            Assert.AreEqual(OperationState.StorageFull, OperationStatus.Of(w, op));
        }

        [Test]
        public void StorageFull_TwoStorages_CapacityAdds()
        {
            SimWorld w = World();
            Operation op = Ready(w, out int s1);
            AddModule(w, op, ModuleTypes.LogStorage, 90, 90);
            w.Inventory.Produce(w.Inventory.GetOrAddStorage(s1), Items.Log, 20);
            Assert.AreEqual(OperationState.Working, OperationStatus.Of(w, op));
            Assert.AreEqual(40, OperationStatus.LogCapacity(w, op));
        }

        [Test]
        public void Order_PausedBeatsEverything_StorageFullIsLast()
        {
            SimWorld w = World();
            Operation op = Ready(w, out int storageId);
            w.Inventory.Produce(w.Inventory.GetOrAddStorage(storageId), Items.Log, 20);
            w.Trees.SetState(new TreeId(1), TreeState.Stump);
            Assert.AreEqual(OperationState.NoTrees, OperationStatus.Of(w, op));
            w.Operations.UnassignFamily(op.Id, 1);
            Assert.AreEqual(OperationState.NoWorkers, OperationStatus.Of(w, op));
            w.Operations.SetPaused(op.Id, true);
            Assert.AreEqual(OperationState.Paused, OperationStatus.Of(w, op));
        }
    }
}
