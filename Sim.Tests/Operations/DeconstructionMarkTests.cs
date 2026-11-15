using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>MarkForDeconstruction / CancelDeconstruction (T-393, CN-50..57).</summary>
    public class DeconstructionMarkTests
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

        private static int Place(SimWorld w, Operation op, string type, double x) =>
            (int)w.Execute(new PlaceModule(op.Id, type, M(x, 110), 0)).Created[0];

        [Test]
        public void Mark_UnbuiltSite_CancelsLikeRemove_ReleasesReservationsAndPilesDeliveredLogs()
        {
            SimWorld w = World(10, out LedgerLocation sp);
            Operation op = Designate(w);
            Place(w, op, ModuleTypes.LogStorageId, 130);
            int hut = Place(w, op, ModuleTypes.WoodcuttingHutId, 110);
            w.Inventory.Produce(w.Inventory.GetOrAddSite(hut), Items.Log, 3);

            CommandResult r = w.Execute(new MarkForDeconstruction(hut));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Assert.IsNull(w.Footprints.Get(hut));
            Assert.IsNull(w.Construction.Get(hut));
            Assert.AreEqual(0, w.Deconstruction.Count);
            Assert.AreEqual(0, w.Inventory.Reservations.Sum(x => x.Count));
            Assert.AreEqual(10, sp.AvailableOf(Items.Log.Id));
            int unowned = w.Inventory.Locations
                .Where(l => l.Kind == LocationKind.GroundPile && l.OwnerOperationId == null).Sum(l => l.Total);
            Assert.AreEqual(3, unowned);
        }

        [Test]
        public void Mark_BuiltStorage_ZeroMinuteRecipe_IsRemovedAtOnce_T394()
        {
            SimWorld w = World(0, out _);
            Operation op = Designate(w);
            int storage = Place(w, op, ModuleTypes.LogStorageId, 110);
            Assert.AreEqual(ModuleTypes.LogStorage.LogCapacity, OperationStatus.LogCapacity(w, op));

            Assert.IsTrue(w.Execute(new MarkForDeconstruction(storage)).IsAccepted);
            Assert.IsNull(w.Footprints.Get(storage));
            Assert.AreEqual(0, w.Deconstruction.Count);
            Assert.AreEqual(0, OperationStatus.LogCapacity(w, op));
            Assert.AreEqual(0, OperationStatus.StorageModules(w, op).Count);
        }

        [Test]
        public void Mark_BuiltOxenStand_RecordsEntry_FootprintStaysUntilWorkDone()
        {
            SimWorld w = World(1, out _);
            Operation op = Designate(w);
            int stand = Place(w, op, ModuleTypes.OxenStandId, 110);
            w.Construction.Remove(stand);
            Assert.IsTrue(w.Execute(new MarkForDeconstruction(stand)).IsAccepted);
            Assert.IsNotNull(w.Deconstruction.Get(stand));
            Assert.IsNotNull(w.Footprints.Get(stand));
        }

        [Test]
        public void Mark_BuiltHut_IsMissingHut_AndUnassignsFamiliesInSameApply()
        {
            SimWorld w = World(10, out _);
            Operation op = Designate(w);
            int hut = Place(w, op, ModuleTypes.WoodcuttingHutId, 110);
            w.Construction.Remove(hut);
            w.People.AddFamily(1, "F", 0, 0);
            Assert.IsTrue(w.Execute(new AssignFamily(op.Id, 1)).IsAccepted);
            Assert.IsNotNull(OperationStatus.Hut(w, op));

            Assert.IsTrue(w.Execute(new MarkForDeconstruction(hut)).IsAccepted);
            Assert.AreEqual(OperationState.MissingHut, OperationStatus.Of(w, op));
            Assert.AreEqual(0, op.FamilyIds.Count);
            Assert.IsNull(w.People.FindFamily(1).WorkplaceId);
        }

        [Test]
        public void Mark_BuiltOxenStand_HoldsNoOx()
        {
            SimWorld w = World(10, out _);
            Operation op = Designate(w);
            int stand =Place(w, op, ModuleTypes.OxenStandId, 110);
            w.Construction.Remove(stand);
            var pool = new OxPool(w);
            var ox = w.People.AddOx(1, "Ox", 110, 110);
            w.Operations.AssignOx(op.Id, 1);
            pool.RestSpot(ox, out int before);
            Assert.AreEqual(stand, before);

            Assert.IsTrue(w.Execute(new MarkForDeconstruction(stand)).IsAccepted);
            pool.RestSpot(ox, out int after);
            Assert.AreNotEqual(stand, after);
        }

        [Test]
        public void Mark_BuiltStorageHoldingReservedLogs_IsRefused_Cn34()
        {
            SimWorld w = World(0, out _);
            Operation op = Designate(w);
            int storage = Place(w, op, ModuleTypes.LogStorageId, 110);
            w.Inventory.Produce(w.Inventory.GetOrAddStorage(storage, 100), Items.Log, 10);
            Place(w, op, ModuleTypes.WoodcuttingHutId, 125);

            CommandResult r = w.Execute(new MarkForDeconstruction(storage));
            Assert.IsTrue(r.IsRejected);
            StringAssert.StartsWith("Logs here are reserved for ", r.Text);
            Assert.AreEqual(0, w.Deconstruction.Count);
        }

        [Test]
        public void Mark_Twice_AndUnknown_AreRejected()
        {
            SimWorld w = World(0, out _);
            Operation op = Designate(w);
            int storage = Place(w, op, ModuleTypes.LogStorageId, 110);
            Assert.IsTrue(w.Execute(new MarkForDeconstruction(storage)).IsAccepted);
            Assert.IsTrue(w.Execute(new MarkForDeconstruction(storage)).IsRejected);
            Assert.IsTrue(w.Execute(new MarkForDeconstruction(999)).IsRejected);
        }

        [Test]
        public void Cancel_RestoresModuleAtOnce_AndRejectsWhenNotMarked()
        {
            SimWorld w = World(1, out _);
            Operation op = Designate(w);
            int stand = Place(w, op, ModuleTypes.OxenStandId, 110);
            w.Construction.Remove(stand);
            Assert.IsTrue(w.Execute(new CancelDeconstruction(stand)).IsRejected);
            w.Execute(new MarkForDeconstruction(stand));
            Assert.AreEqual(1, w.Deconstruction.Count);

            Assert.IsTrue(w.Execute(new CancelDeconstruction(stand)).IsAccepted);
            Assert.AreEqual(0, w.Deconstruction.Count);
            Assert.IsTrue(w.IsWorking(stand));
        }

        [Test]
        public void SaveVersion_IsThree_PreviousVersionBytesAreRefusedWithSv33Message()
        {
            Assert.AreEqual(3, SaveWriter.CurrentVersion);
            byte[] bytes = { (byte)'L', (byte)'L', (byte)'S', (byte)'V', 2, 0, 0, 0, 0, 0, 0, 0 };
            var e = Assert.Throws<SaveFormatException>(() => SaveReader.ReadHeaderOnly(bytes));
            StringAssert.Contains("Unknown save format version 2 (this build reads version 3).", e.Message);
        }
    }
}
