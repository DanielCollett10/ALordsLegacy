using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    /// <summary>Construction material sources and nearest-first reservation (T-341, CN-11..13).</summary>
    public class ConstructionSourcesTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld NewWorld() => new SimWorld(new SimTuning());

        private static Operation Designate(SimWorld w, double x)
        {
            var sq = AreaShape.Straight(new[] { M(x, 100), M(x + 20, 100), M(x + 20, 120), M(x, 120) });
            CommandResult r = w.Execute(new DesignateArea(sq, AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return w.Operations.FindByArea((int)r.Created[0]);
        }

        private static int AddStorage(SimWorld w, Operation op, double x, int logs)
        {
            int id = 1;
            foreach (int e in w.Footprints.Ids) id = e + 1;
            w.Footprints.Add(id, ModuleTypes.LogStorage, M(x, 112), 0);
            w.Operations.AddModule(op.Id, id);
            if (logs > 0) w.Inventory.Produce(w.Inventory.GetOrAddStorage(id, ModuleTypes.LogStorage), Items.Log, logs);
            return id;
        }

        [Test]
        public void Available_SumsStockpileBuiltStorageAndUnownedPiles()
        {
            var w = NewWorld();
            Operation op = Designate(w, 100);
            w.Inventory.Produce(w.Inventory.AddStockpile(M(0, 0)), Items.Log, 5);
            AddStorage(w, op, 110, 7);
            w.Inventory.ProduceOnGround(M(50, 50), null, Items.Log, 3);
            Assert.AreEqual(15, ConstructionSources.Available(w, Items.Log));
        }

        [Test]
        public void Available_SubtractsReserved()
        {
            var w = NewWorld();
            var sp = w.Inventory.AddStockpile(M(0, 0));
            w.Inventory.Produce(sp, Items.Log, 10);
            w.Inventory.ReserveStock(sp, Items.Log, 4, 99);
            Assert.AreEqual(6, ConstructionSources.Available(w, Items.Log));
        }

        [Test]
        public void Available_OperationOwnedPile_IsNotASource()
        {
            var w = NewWorld();
            Operation op = Designate(w, 100);
            w.Inventory.ProduceOnGround(M(105, 105), op.Id, Items.Log, 9);
            Assert.AreEqual(0, ConstructionSources.Available(w, Items.Log));
        }

        [Test]
        public void Available_UnbuiltStorage_IsIgnored()
        {
            var w = NewWorld();
            Operation op = Designate(w, 100);
            int id = AddStorage(w, op, 110, 6);
            w.Construction.Add(new ConstructionSite(id, w.Inventory.GetOrAddSite(id).Id));
            Assert.AreEqual(0, ConstructionSources.Available(w, Items.Log));
        }

        [Test]
        public void Available_SiteStoreAndCarrier_AreNotSources()
        {
            var w = NewWorld();
            w.Inventory.Produce(w.Inventory.GetOrAddSite(5), Items.Log, 4);
            w.Inventory.Produce(w.Inventory.AddCarrier(8), Items.Log, 4);
            Assert.AreEqual(0, ConstructionSources.Available(w, Items.Log));
        }

        [Test]
        public void TryReserve_CombinesTwoSources()
        {
            var w = NewWorld();
            var sp = w.Inventory.AddStockpile(M(0, 0));
            w.Inventory.Produce(sp, Items.Log, 5);
            var pile = w.Inventory.ProduceOnGround(M(10, 0), null, Items.Log, 5);
            var ids = ConstructionSources.TryReserve(w, Items.Log, 8, M(0, 0), 42);
            Assert.IsNotNull(ids);
            Assert.AreEqual(2, ids.Count);
            Assert.AreEqual(0, sp.AvailableOf(Items.Log.Id));
            Assert.AreEqual(2, pile.AvailableOf(Items.Log.Id));
            foreach (int id in ids) Assert.AreEqual(42, w.Inventory.GetReservation(id).Owner);
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void TryReserve_TakesNearestFirst()
        {
            var w = NewWorld();
            var far = w.Inventory.AddStockpile(M(100, 0));
            w.Inventory.Produce(far, Items.Log, 10);
            var near = w.Inventory.ProduceOnGround(M(5, 0), null, Items.Log, 10);
            var ids = ConstructionSources.TryReserve(w, Items.Log, 4, M(0, 0), 1);
            Assert.AreEqual(1, ids.Count);
            Assert.AreEqual(6, near.AvailableOf(Items.Log.Id));
            Assert.AreEqual(10, far.AvailableOf(Items.Log.Id));
        }

        [Test]
        public void TryReserve_NearestUsesStorageModulePosition()
        {
            var w = NewWorld();
            Operation op = Designate(w, 100);
            var sp = w.Inventory.AddStockpile(M(0, 0));
            w.Inventory.Produce(sp, Items.Log, 10);
            int id = AddStorage(w, op, 110, 10);
            var ids = ConstructionSources.TryReserve(w, Items.Log, 3, M(110, 112), 1);
            Assert.AreEqual(1, ids.Count);
            Assert.AreEqual(7, w.Inventory.FindStorage(id).AvailableOf(Items.Log.Id));
            Assert.AreEqual(10, sp.AvailableOf(Items.Log.Id));
        }

        [Test]
        public void TryReserve_NotEnough_ReservesNothingAndReturnsNull()
        {
            var w = NewWorld();
            var sp = w.Inventory.AddStockpile(M(0, 0));
            w.Inventory.Produce(sp, Items.Log, 3);
            var pile = w.Inventory.ProduceOnGround(M(5, 0), null, Items.Log, 3);
            Assert.IsNull(ConstructionSources.TryReserve(w, Items.Log, 7, M(0, 0), 1));
            Assert.AreEqual(3, sp.AvailableOf(Items.Log.Id));
            Assert.AreEqual(3, pile.AvailableOf(Items.Log.Id));
            Assert.IsEmpty(new System.Collections.Generic.List<Reservation>(w.Inventory.Reservations));
        }

        [Test]
        public void TryReserve_ZeroOrNegative_ReturnsNull()
        {
            var w = NewWorld();
            w.Inventory.Produce(w.Inventory.AddStockpile(M(0, 0)), Items.Log, 3);
            Assert.IsNull(ConstructionSources.TryReserve(w, Items.Log, 0, M(0, 0), 1));
            Assert.IsNull(ConstructionSources.TryReserve(w, Items.Log, -2, M(0, 0), 1));
        }

        [Test]
        public void TryReserve_NoSources_ReturnsNull()
        {
            Assert.IsNull(ConstructionSources.TryReserve(NewWorld(), Items.Log, 1, M(0, 0), 1));
        }

        [Test]
        public void TryReserve_IgnoresUnbuiltStorageAndOperationPiles()
        {
            var w = NewWorld();
            Operation op = Designate(w, 100);
            int id = AddStorage(w, op, 110, 6);
            w.Construction.Add(new ConstructionSite(id, w.Inventory.GetOrAddSite(id).Id));
            w.Inventory.ProduceOnGround(M(105, 105), op.Id, Items.Log, 9);
            Assert.IsNull(ConstructionSources.TryReserve(w, Items.Log, 1, M(110, 112), 1));
        }
    }
}
