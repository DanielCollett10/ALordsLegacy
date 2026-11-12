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
    /// <summary>
    /// <see cref="OperationStatus.LogLoad"/> counts carriers and reserved incoming room without double counting (WC-25,
    /// WC-37, T-292). Also covers the storage-full stop, the second storage module and felling resuming.
    /// </summary>
    public class LogLoadTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld World() => new SimWorld(new SimTuning());

        private static Operation Designate(SimWorld w)
        {
            CommandResult r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) }), AreaTypes.WoodcuttingId));
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

        // Hut, one storage (20 logs), one adult family, one standing tree.
        private static Operation Ready(SimWorld w, out LedgerLocation store)
        {
            Operation op = Designate(w);
            AddModule(w, op, ModuleTypes.WoodcuttingHut, 105, 105);
            int storageId = AddModule(w, op, ModuleTypes.LogStorage, 112, 112);
            store = w.Inventory.GetOrAddStorage(storageId, ModuleTypes.LogStorage);
            w.People.AddFamily(1, "F", 0, 0);
            w.People.AddCitizen(10, "C", Sex.Male, -40L * Citizen.TicksPerYear, 1, 0, 0);
            w.Operations.AssignFamily(op.Id, 1);
            w.Trees.Add(new TreeId(1), 150f, 150f, 5);
            return op;
        }

        [Test]
        public void LogLoad_CountsLogsOnTheOperationsOxCarrier()
        {
            SimWorld w = World();
            Operation op = Ready(w, out _);
            Ox ox = w.People.AddOx(50, "O", 105, 104);
            Assert.IsTrue(w.Operations.AssignOx(op.Id, ox.Id));
            LedgerLocation carrier = w.OxCarrier(ox);
            LedgerLocation pile = w.Inventory.ProduceOnGround(M(95, 95), op.Id, Items.Log, 4);
            Assert.IsTrue(w.Inventory.Transfer(pile, carrier, Items.Log, 4));
            Assert.AreEqual(0, pile.Total);
            Assert.AreEqual(4, OperationStatus.LogLoad(w, op), "logs on the carrier are load");
        }

        [Test]
        public void LogLoad_IgnoresCarriersOfOxenNotUsedByTheOperation()
        {
            SimWorld w = World();
            Operation op = Ready(w, out _);
            Ox stranger = w.People.AddOx(60, "other", 105, 104);
            w.OxCarrier(stranger);
            w.Inventory.Produce(w.OxCarrier(stranger), Items.Log, 3);
            Assert.AreEqual(0, OperationStatus.LogLoad(w, op));
        }

        [Test]
        public void LogLoad_ReservedIncomingRoom_CountsAsLoad()
        {
            SimWorld w = World();
            Operation op = Ready(w, out LedgerLocation store);
            Assert.AreNotEqual(0, w.Inventory.ReserveCapacity(store, Items.Log, 5, 999));
            Assert.AreEqual(5, OperationStatus.LogLoad(w, op));
        }

        [Test]
        public void LogLoad_ReservedRoomForLogsInAPile_NotCountedTwice()
        {
            SimWorld w = World();
            Operation op = Ready(w, out LedgerLocation store);
            LedgerLocation pile = w.Inventory.ProduceOnGround(M(95, 95), op.Id, Items.Log, 4);
            Assert.AreNotEqual(0, w.Inventory.ReserveStock(pile, Items.Log, 4, 999));
            Assert.AreNotEqual(0, w.Inventory.ReserveCapacity(store, Items.Log, 4, 999));
            Assert.AreEqual(4, OperationStatus.LogLoad(w, op));
        }

        [Test]
        public void LogLoad_ReservedRoomForLogsOnACarrier_NotCountedTwice()
        {
            SimWorld w = World();
            Operation op = Ready(w, out LedgerLocation store);
            Ox ox = w.People.AddOx(50, "O", 105, 104);
            Assert.IsTrue(w.Operations.AssignOx(op.Id, ox.Id));
            LedgerLocation carrier = w.OxCarrier(ox);
            LedgerLocation pile = w.Inventory.ProduceOnGround(M(95, 95), op.Id, Items.Log, 4);
            Assert.AreNotEqual(0, w.Inventory.ReserveStock(pile, Items.Log, 4, 999));
            Assert.AreNotEqual(0, w.Inventory.ReserveCapacity(store, Items.Log, 4, 999));
            Assert.IsTrue(w.Inventory.Transfer(pile, carrier, Items.Log, 4, 999));
            Assert.AreEqual(4, OperationStatus.LogLoad(w, op));
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void LogLoad_ReservedRoomForLogsNotYetProduced_IsCounted()
        {
            // An early-fetch haul: room is reserved while the fell task is still producing the logs.
            SimWorld w = World();
            Operation op = Ready(w, out LedgerLocation store);
            w.Inventory.Produce(store, Items.Log, 15);
            Assert.AreNotEqual(0, w.Inventory.ReserveCapacity(store, Items.Log, 4, 999));
            Assert.AreEqual(19, OperationStatus.LogLoad(w, op));
            Assert.AreEqual(OperationState.Working, OperationStatus.Of(w, op));
        }

        [Test]
        public void StorageFull_HeldLogsPlusReservedRoomReachCapacity_StorageFull()
        {
            SimWorld w = World();
            Operation op = Ready(w, out LedgerLocation store);
            w.Inventory.Produce(store, Items.Log, 16);
            w.Inventory.ReserveCapacity(store, Items.Log, 3, 999);
            Assert.AreEqual(OperationState.Working, OperationStatus.Of(w, op), "19 of 20 counted");
            w.Inventory.ReserveCapacity(store, Items.Log, 1, 999);
            Assert.AreEqual(20, OperationStatus.LogLoad(w, op));
            Assert.AreEqual(OperationState.StorageFull, OperationStatus.Of(w, op));
        }

        [Test]
        public void SecondStorage_RaisesCapacityTo40_AndUnfullsTheOperation()
        {
            SimWorld w = World();
            Operation op = Ready(w, out LedgerLocation store);
            w.Inventory.Produce(store, Items.Log, 20);
            Assert.AreEqual(OperationState.StorageFull, OperationStatus.Of(w, op));
            int second = AddModule(w, op, ModuleTypes.LogStorage, 90, 90);
            LedgerLocation store2 = w.Inventory.GetOrAddStorage(second, ModuleTypes.LogStorage);
            Assert.AreEqual(40, OperationStatus.LogCapacity(w, op));
            Assert.AreEqual(20, OperationStatus.LogLoad(w, op));
            Assert.AreEqual(OperationState.Working, OperationStatus.Of(w, op));
            w.Inventory.Produce(store2, Items.Log, 20);
            Assert.AreEqual(OperationState.StorageFull, OperationStatus.Of(w, op));
        }

        [Test]
        public void RemovingLogsFromStorage_ClearsStorageFull()
        {
            SimWorld w = World();
            Operation op = Ready(w, out LedgerLocation store);
            w.Inventory.Produce(store, Items.Log, 20);
            Assert.AreEqual(OperationState.StorageFull, OperationStatus.Of(w, op));
            // Test hook: logs leave storage through the ledger into an unlimited stockpile.
            Assert.IsTrue(w.Inventory.Transfer(store, w.Inventory.AddStockpile(), Items.Log, 5));
            Assert.AreEqual(15, OperationStatus.LogLoad(w, op));
            Assert.AreEqual(OperationState.Working, OperationStatus.Of(w, op));
            Assert.IsNull(w.Inventory.Audit());
        }
    }
}
