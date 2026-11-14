using System.Collections.Generic;
using System.Linq;
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

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>PlaceModule pays or refuses; RemoveModule on sites and reserved storage (T-342, CN-10..12, CN-32..34).</summary>
    public class PlaceRemoveConstructionTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld World(int logs, out LedgerLocation stockpile)
        {
            var w = new SimWorld(new SimTuning());
            stockpile = w.Inventory.AddStockpile(M(0, 0));
            if (logs > 0) w.Inventory.Produce(stockpile, Items.Log, logs);
            return w;
        }

        private static Operation Designate(SimWorld w)
        {
            var shape = AreaShape.Straight(new[] { M(100, 100), M(140, 100), M(140, 120), M(100, 120) });
            CommandResult r = w.Execute(new DesignateArea(shape, AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return w.Operations.FindByArea((int)r.Created[0]);
        }

        private static CommandResult Place(SimWorld w, Operation op, string type, double x) =>
            w.Execute(new PlaceModule(op.Id, type, M(x, 110), 0));

        private static int ReservedTotal(SimWorld w) => w.Inventory.Reservations.Sum(r => r.Count);

        [Test]
        public void Preview_NotEnoughLogs_RefusesWithHaveAndNeedText()
        {
            SimWorld w = World(5, out _);
            Operation op = Designate(w);
            ModulePlacementResult p = ModulePlacement.Preview(w, op.Id, ModuleTypes.WoodcuttingHutId, M(110, 110), 0);
            Assert.AreEqual(ModuleRefusal.NotEnoughMaterials, p.Refusal);
            Assert.AreEqual("Not enough logs (have 5, need 8)", p.Text);
        }

        [Test]
        public void Place_Hut_CreatesDeliveringSiteAndReservesLogsInOneApply()
        {
            SimWorld w = World(10, out LedgerLocation sp);
            Operation op = Designate(w);
            CommandResult r = Place(w, op, ModuleTypes.WoodcuttingHutId, 110);
            Assert.IsTrue(r.IsAccepted, r.Text);
            int id = (int)r.Created[0];
            Assert.IsNotNull(w.Footprints.Get(id));
            ConstructionSite site = w.Construction.Get(id);
            Assert.IsNotNull(site);
            Assert.AreEqual(ConstructionState.Delivering, site.State);
            Assert.AreEqual(8, site.ReservationIds.Sum(i => w.Inventory.GetReservation(i).Count));
            Assert.AreEqual(2, sp.AvailableOf(Items.Log.Id));
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void Place_RefusedHut_RejectsAndChangesNothing()
        {
            SimWorld w = World(7, out _);
            Operation op = Designate(w);
            CommandResult r = Place(w, op, ModuleTypes.WoodcuttingHutId, 110);
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual(0, w.Construction.Count);
            Assert.AreEqual(0, ReservedTotal(w));
            Assert.AreEqual(0, op.ModuleIds.Count);
        }

        [Test]
        public void Place_TwoSitesInARow_NeverReserveTheSameLogs()
        {
            SimWorld w = World(9, out LedgerLocation sp);
            Operation op = Designate(w);
            Assert.IsTrue(Place(w, op, ModuleTypes.WoodcuttingHutId, 110).IsAccepted); // 8 of 9
            CommandResult second = Place(w, op, ModuleTypes.OxenStandId, 130);          // needs 1, 1 left
            Assert.IsTrue(second.IsAccepted, second.Text);
            CommandResult third = Place(w, op, ModuleTypes.OxenStandId, 135);           // none left (also TooMany)
            Assert.IsTrue(third.IsRejected);
            Assert.AreEqual(9, ReservedTotal(w));
            Assert.AreEqual(0, sp.AvailableOf(Items.Log.Id));
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void Place_LogStorage_WithZeroLogs_IsBuiltAtOnceAndNeverASite()
        {
            SimWorld w = World(0, out _);
            Operation op = Designate(w);
            CommandResult r = Place(w, op, ModuleTypes.LogStorageId, 110);
            Assert.IsTrue(r.IsAccepted, r.Text);
            int id = (int)r.Created[0];
            Assert.IsTrue(w.IsBuilt(id));
            Assert.IsNull(w.Construction.Get(id));
            Assert.AreEqual(0, w.Construction.Count);
        }

        [Test]
        public void Place_RefusedHut_LeavesAlreadyPlacedStorageUntouched()
        {
            SimWorld w = World(0, out _);
            Operation op = Designate(w);
            int storage = (int)Place(w, op, ModuleTypes.LogStorageId, 110).Created[0];
            Assert.IsTrue(Place(w, op, ModuleTypes.WoodcuttingHutId, 125).IsRejected);
            Assert.IsNotNull(w.Footprints.Get(storage));
            CollectionAssert.AreEqual(new[] { storage }, op.ModuleIds);
            Assert.IsTrue(w.IsBuilt(storage));
        }

        [Test]
        public void Remove_UnbuiltSite_ReleasesReservationsAndDropsSiteLogsOnUnownedPile()
        {
            SimWorld w = World(10, out LedgerLocation sp);
            Operation op = Designate(w);
            Place(w, op, ModuleTypes.LogStorageId, 130); // takes footprint id 1 so the hut's id differs from the operation id
            int id = (int)Place(w, op, ModuleTypes.WoodcuttingHutId, 110).Created[0];
            Assert.AreNotEqual(op.Id, id);
            w.Inventory.Produce(w.Inventory.GetOrAddSite(id), Items.Log, 3);
            LedgerLocation ownedPile = w.Inventory.ProduceOnGround(M(111, 111), id, Items.Log, 2);

            CommandResult r = w.Execute(new RemoveModule(id));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Assert.IsNull(w.Construction.Get(id));
            Assert.IsNull(w.Footprints.Get(id));
            Assert.AreEqual(0, ReservedTotal(w));
            Assert.AreEqual(10, sp.AvailableOf(Items.Log.Id));
            Assert.AreEqual(0, w.Inventory.FindSite(id).Total);
            Assert.AreEqual(0, ownedPile.Total);
            int unowned = w.Inventory.Locations
                .Where(l => l.Kind == LocationKind.GroundPile && l.OwnerOperationId == null).Sum(l => l.Total);
            Assert.AreEqual(5, unowned);
            Assert.AreEqual(15, ConstructionSources.Available(w, Items.Log));
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void Remove_BuiltModule_IsUnchanged_NoRefund()
        {
            SimWorld w = World(0, out _);
            Operation op = Designate(w);
            int id = (int)Place(w, op, ModuleTypes.LogStorageId, 110).Created[0];
            CommandResult r = w.Execute(new RemoveModule(id));
            Assert.IsTrue(r.IsAccepted);
            Assert.IsNull(w.Footprints.Get(id));
            Assert.AreEqual(0, op.ModuleIds.Count);
            Assert.AreEqual(0, w.Inventory.TotalOf(Items.Log));
        }

        [Test]
        public void Remove_BuiltStorageHoldingReservedLogs_IsRefusedWithSiteName()
        {
            SimWorld w = World(0, out _);
            Operation op = Designate(w);
            int storage = (int)Place(w, op, ModuleTypes.LogStorageId, 110).Created[0];
            w.Inventory.Produce(w.Inventory.GetOrAddStorage(storage, 100), Items.Log, 10);
            int hut = (int)Place(w, op, ModuleTypes.WoodcuttingHutId, 125).Created[0];

            CommandResult r = w.Execute(new RemoveModule(storage));
            Assert.IsTrue(r.IsRejected);
            StringAssert.StartsWith("Logs here are reserved for ", r.Text);
            StringAssert.Contains(hut.ToString(), r.Text);
            Assert.IsNotNull(w.Footprints.Get(storage));

            // Once the site is removed the storage can go.
            Assert.IsTrue(w.Execute(new RemoveModule(hut)).IsAccepted);
            Assert.IsTrue(w.Execute(new RemoveModule(storage)).IsAccepted);
        }

        [Test]
        public void Remove_BuiltStorageHoldingUnreservedLogs_IsAllowed()
        {
            SimWorld w = World(0, out _);
            Operation op = Designate(w);
            int storage = (int)Place(w, op, ModuleTypes.LogStorageId, 110).Created[0];
            w.Inventory.Produce(w.Inventory.GetOrAddStorage(storage, 100), Items.Log, 10);
            Assert.IsTrue(w.Execute(new RemoveModule(storage)).IsAccepted);
        }
    }
}
