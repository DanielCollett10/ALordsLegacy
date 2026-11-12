using System;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    /// <summary>Reserve, release, transfer, carriers, storage pools (T-286).</summary>
    public class LedgerReservationTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static Ledger L(out LedgerLocation pile, out LedgerLocation store, int cap = 20, int logs = 10)
        {
            var l = new Ledger();
            pile = l.GetOrAddGroundPile(M(0, 0), null);
            store = l.GetOrAddStorage(1, cap);
            l.Produce(pile, Items.Log, logs);
            return l;
        }

        [Test]
        public void NewKinds_CarrierHasCapacity_StockpileIsUnlimited()
        {
            var l = new Ledger();
            var ox = l.AddCarrier(4);
            var sp = l.AddStockpile();
            Assert.AreEqual(LocationKind.Carrier, ox.Kind);
            Assert.AreEqual(4, ox.Capacity);
            Assert.AreEqual(LocationKind.Stockpile, sp.Kind);
            Assert.AreEqual(int.MaxValue, sp.Capacity);
            l.Produce(sp, Items.Log, 100000);
            Assert.AreEqual(int.MaxValue, sp.FreeSpace);
            Assert.Throws<InvalidOperationException>(() => l.Produce(ox, Items.Log, 5));
            Assert.IsNull(l.Audit());
        }

        [Test]
        public void Storage_CapacityComesFromModuleType()
        {
            var l = new Ledger();
            var s = l.GetOrAddStorage(7, ModuleTypes.LogStorage);
            Assert.AreEqual(20, s.Capacity);
            Assert.Throws<InvalidOperationException>(() => l.Produce(s, Items.Log, 21));
        }

        [Test]
        public void ReserveStock_CountsReservedAndAvailable()
        {
            var l = L(out var pile, out _);
            int id = l.ReserveStock(pile, Items.Log, 4, 1);
            Assert.Greater(id, 0);
            Assert.AreEqual(10, pile.CountOf("item.log"));
            Assert.AreEqual(4, pile.ReservedOf("item.log"));
            Assert.AreEqual(6, pile.AvailableOf("item.log"));
            Assert.AreEqual(0, l.ReserveStock(pile, Items.Log, 7, 2), "only 6 available");
            Assert.IsNull(l.Audit());
        }

        [Test]
        public void ReserveCapacity_CountsIncomingAndRefusesWhenFull()
        {
            var l = L(out _, out var store, cap: 5);
            Assert.Greater(l.ReserveCapacity(store, Items.Log, 3, 1), 0);
            Assert.AreEqual(3, store.IncomingOf("item.log"));
            Assert.AreEqual(2, store.FreeSpace);
            Assert.AreEqual(0, l.ReserveCapacity(store, Items.Log, 3, 2));
            Assert.Greater(l.ReserveCapacity(store, Items.Log, 2, 2), 0);
            Assert.AreEqual(0, store.FreeSpace);
            Assert.IsNull(l.Audit());
        }

        [Test]
        public void ReserveThenTransfer_ConsumesBothReservations()
        {
            var l = L(out var pile, out var store);
            int a = l.ReserveStock(pile, Items.Log, 4, 1);
            int b = l.ReserveCapacity(store, Items.Log, 4, 1);
            Assert.IsTrue(l.Transfer(pile, store, Items.Log, 4, 1));
            Assert.AreEqual(6, pile.CountOf("item.log"));
            Assert.AreEqual(4, store.CountOf("item.log"));
            Assert.AreEqual(0, pile.ReservedOf("item.log"));
            Assert.AreEqual(0, store.IncomingOf("item.log"));
            Assert.IsNull(l.GetReservation(a));
            Assert.IsNull(l.GetReservation(b));
            Assert.AreEqual(10, l.TotalOf(Items.Log));
            Assert.IsNull(l.Audit());
        }

        [Test]
        public void Transfer_PartialOfReservation_LeavesRemainder()
        {
            var l = L(out var pile, out var store);
            int a = l.ReserveStock(pile, Items.Log, 5, 1);
            l.ReserveCapacity(store, Items.Log, 5, 1);
            Assert.IsTrue(l.Transfer(pile, store, Items.Log, 2, 1));
            Assert.AreEqual(3, l.GetReservation(a).Count);
            Assert.AreEqual(3, pile.ReservedOf("item.log"));
            Assert.AreEqual(3, store.IncomingOf("item.log"));
            Assert.IsNull(l.Audit());
        }

        [Test]
        public void Transfer_CannotTakeStockReservedByAnotherOwner()
        {
            var l = L(out var pile, out var store);
            l.ReserveStock(pile, Items.Log, 8, 1);
            Assert.IsFalse(l.Transfer(pile, store, Items.Log, 5, 2), "only 2 unreserved");
            Assert.AreEqual(10, pile.CountOf("item.log"));
            Assert.IsTrue(l.Transfer(pile, store, Items.Log, 2, 2));
            Assert.IsNull(l.Audit());
        }

        [Test]
        public void Transfer_IntoFullStore_FailsAtomically()
        {
            var l = L(out var pile, out var store, cap: 3);
            Assert.IsFalse(l.Transfer(pile, store, Items.Log, 4));
            Assert.AreEqual(10, pile.CountOf("item.log"));
            Assert.AreEqual(0, store.Total);
            Assert.IsTrue(l.Transfer(pile, store, Items.Log, 3));
            Assert.IsFalse(l.Transfer(pile, store, Items.Log, 1));
            Assert.AreEqual(3, store.Total);
            Assert.IsNull(l.Audit());
        }

        [Test]
        public void Transfer_FailureAfterStockOk_DoesNotConsumeStockReservation()
        {
            var l = L(out var pile, out var store, cap: 2);
            int a = l.ReserveStock(pile, Items.Log, 4, 1);
            Assert.IsFalse(l.Transfer(pile, store, Items.Log, 4, 1));
            Assert.AreEqual(4, l.GetReservation(a).Count);
            Assert.AreEqual(4, pile.ReservedOf("item.log"));
        }

        [Test]
        public void Transfer_MoreThanHeld_Fails()
        {
            var l = L(out var pile, out var store);
            Assert.IsFalse(l.Transfer(pile, store, Items.Log, 11));
            Assert.IsFalse(l.Transfer(pile, pile, Items.Log, 1));
            Assert.AreEqual(10, pile.Total);
        }

        [Test]
        public void Transfer_OtherOwnersCapacityReservation_BlocksUnreservedMove()
        {
            var l = L(out var pile, out var store, cap: 5);
            l.ReserveCapacity(store, Items.Log, 5, 9);
            Assert.IsFalse(l.Transfer(pile, store, Items.Log, 1, 1));
            Assert.IsTrue(l.Transfer(pile, store, Items.Log, 1, 9));
        }

        [Test]
        public void Release_FreesReservation()
        {
            var l = L(out var pile, out var store, cap: 5);
            int a = l.ReserveStock(pile, Items.Log, 4, 1);
            int b = l.ReserveCapacity(store, Items.Log, 5, 1);
            Assert.IsTrue(l.Release(a));
            Assert.IsTrue(l.Release(b));
            Assert.IsFalse(l.Release(a));
            Assert.AreEqual(10, pile.AvailableOf("item.log"));
            Assert.AreEqual(5, store.FreeSpace);
            Assert.IsNull(l.Audit());
        }

        [Test]
        public void CancelOwner_ReleasesAllOfThatOwnerOnly()
        {
            var l = L(out var pile, out var store);
            l.ReserveStock(pile, Items.Log, 2, 1);
            l.ReserveCapacity(store, Items.Log, 2, 1);
            l.ReserveStock(pile, Items.Log, 3, 2);
            Assert.AreEqual(2, l.CancelOwner(1));
            Assert.AreEqual(3, pile.ReservedOf("item.log"));
            Assert.AreEqual(0, store.IncomingOf("item.log"));
            Assert.AreEqual(0, l.CancelOwner(1));
            Assert.IsNull(l.Audit());
        }

        [Test]
        public void CapacityNeverExceeded_ManyReservationsAndTransfers()
        {
            var l = L(out var pile, out var store, cap: 7, logs: 30);
            for (int owner = 1; owner <= 10; owner++)
            {
                l.ReserveStock(pile, Items.Log, 3, owner);
                l.ReserveCapacity(store, Items.Log, 3, owner);
                l.Transfer(pile, store, Items.Log, 3, owner);
                Assert.LessOrEqual(store.Total + store.IncomingTotal, 7);
                Assert.IsNull(l.Audit());
            }
            Assert.AreEqual(6, store.Total);
            Assert.AreEqual(30, l.TotalOf(Items.Log));
        }

        [Test]
        public void ReserveWithBadArguments_Throws()
        {
            var l = L(out var pile, out _);
            Assert.Throws<ArgumentOutOfRangeException>(() => l.ReserveStock(pile, Items.Log, 0, 1));
            Assert.Throws<ArgumentNullException>(() => l.ReserveCapacity(null, Items.Log, 1, 1));
        }

        [Test]
        public void HaulChain_PileToOxToStore_EveryLogInOnePlace()
        {
            var l = L(out var pile, out var store, cap: 20, logs: 6);
            var ox = l.AddCarrier(4);
            Assert.IsTrue(l.ReserveStock(pile, Items.Log, 4, 1) > 0);
            Assert.IsTrue(l.ReserveCapacity(ox, Items.Log, 4, 1) > 0);
            Assert.IsTrue(l.Transfer(pile, ox, Items.Log, 4, 1));
            Assert.IsTrue(l.ReserveStock(ox, Items.Log, 4, 1) > 0);
            Assert.IsTrue(l.ReserveCapacity(store, Items.Log, 4, 1) > 0);
            Assert.IsTrue(l.Transfer(ox, store, Items.Log, 4, 1));
            Assert.AreEqual(2, pile.Total);
            Assert.AreEqual(0, ox.Total);
            Assert.AreEqual(4, store.Total);
            Assert.AreEqual(6, l.TotalOf(Items.Log));
            Assert.IsNull(l.Audit());
        }

        // ---- operation pool -------------------------------------------------

        private static SimWorld Op(out Operation op, double x)
        {
            var w = new SimWorld(new SimTuning());
            op = Designate(w, x);
            return w;
        }

        private static Operation Designate(SimWorld w, double x)
        {
            var sq = AreaShape.Straight(new[] { M(x, 100), M(x + 20, 100), M(x + 20, 120), M(x, 120) });
            CommandResult r = w.Execute(new DesignateArea(sq, AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return w.Operations.FindByArea((int)r.Created[0]);
        }

        private static int AddStorage(SimWorld w, Operation op, double x)
        {
            int id = 1;
            foreach (int e in w.Footprints.Ids) id = e + 1;
            w.Footprints.Add(id, ModuleTypes.LogStorage, M(x, 112), 0);
            w.Operations.AddModule(op.Id, id);
            return id;
        }

        [Test]
        public void OperationPool_SumsOwnStorageModulesOnly()
        {
            SimWorld w = Op(out Operation a, 100);
            Operation b = Designate(w, 400);
            int sa1 = AddStorage(w, a, 110);
            int sa2 = AddStorage(w, a, 130);
            int sb = AddStorage(w, b, 410);
            w.Inventory.Produce(w.Inventory.GetOrAddStorage(sa1, ModuleTypes.LogStorage), Items.Log, 5);
            w.Inventory.Produce(w.Inventory.GetOrAddStorage(sa2, ModuleTypes.LogStorage), Items.Log, 7);
            w.Inventory.Produce(w.Inventory.GetOrAddStorage(sb, ModuleTypes.LogStorage), Items.Log, 100 - 90);
            w.Inventory.ProduceOnGround(M(105, 105), a.Id, Items.Log, 50);
            var ox = w.Inventory.AddCarrier(4);
            w.Inventory.Produce(ox, Items.Log, 3);

            w.Inventory.ReserveStock(w.Inventory.FindStorage(sa1), Items.Log, 2, 1);
            w.Inventory.ReserveCapacity(w.Inventory.FindStorage(sa2), Items.Log, 4, 1);

            OperationPool pa = w.OperationPool(a.Id, Items.Log);
            Assert.AreEqual(12, pa.Total);
            Assert.AreEqual(2, pa.Reserved);
            Assert.AreEqual(10, pa.Available);
            Assert.AreEqual(4, pa.Incoming);
            Assert.AreEqual(40, pa.Capacity);
            Assert.AreEqual(10, w.OperationPool(b.Id, Items.Log).Total);
            Assert.AreEqual(0, w.OperationPool(999, Items.Log).Total);
            Assert.IsNull(w.Inventory.Audit());
        }
    }
}
