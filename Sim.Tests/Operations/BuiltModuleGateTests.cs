using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>Only built modules count for an operation (T-343, CN-04, CN-04b).</summary>
    public class BuiltModuleGateTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static Operation NewOp(SimWorld w)
        {
            CommandResult r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) }), AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return w.Operations.FindByArea((int)r.Created[0]);
        }

        private static int Add(SimWorld w, Operation op, int id, FootprintTypeDef type, double x, bool built)
        {
            w.Footprints.Add(id, type, M(x, 100), 0);
            w.Operations.AddModule(op.Id, id);
            if (!built) w.Construction.Add(new ConstructionSite(id, w.Inventory.GetOrAddSite(id).Id));
            return id;
        }

        private static void Finish(SimWorld w, int id) => w.Construction.Remove(id);

        [Test]
        public void Status_UnbuiltHut_IsMissingHut_ThenBuilt_MovesOn()
        {
            var w = new SimWorld(new SimTuning());
            Operation op = NewOp(w);
            Add(w, op, 1, ModuleTypes.WoodcuttingHut, 100, false);
            Add(w, op, 2, ModuleTypes.LogStorage, 110, true);
            Assert.AreEqual(OperationState.MissingHut, OperationStatus.Of(w, op));
            Finish(w, 1);
            Assert.AreNotEqual(OperationState.MissingHut, OperationStatus.Of(w, op));
            Assert.AreNotEqual(OperationState.MissingStorage, OperationStatus.Of(w, op));
        }

        [Test]
        public void Status_UnbuiltStorage_IsMissingStorage_ThenBuilt_MovesOn()
        {
            var w = new SimWorld(new SimTuning());
            Operation op = NewOp(w);
            Add(w, op, 1, ModuleTypes.WoodcuttingHut, 100, true);
            Add(w, op, 2, ModuleTypes.LogStorage, 110, false);
            Assert.AreEqual(OperationState.MissingStorage, OperationStatus.Of(w, op));
            Finish(w, 2);
            Assert.AreNotEqual(OperationState.MissingStorage, OperationStatus.Of(w, op));
        }

        [Test]
        public void LogCapacity_CountsBuiltStoragesOnly()
        {
            var w = new SimWorld(new SimTuning());
            Operation op = NewOp(w);
            Add(w, op, 2, ModuleTypes.LogStorage, 110, true);
            Add(w, op, 3, ModuleTypes.LogStorage, 112, false);
            Assert.AreEqual(ModuleTypes.LogStorage.LogCapacity, OperationStatus.LogCapacity(w, op));
            Finish(w, 3);
            Assert.AreEqual(2 * ModuleTypes.LogStorage.LogCapacity, OperationStatus.LogCapacity(w, op));
        }

        [Test]
        public void LogLoad_IgnoresLogsInUnbuiltStorage()
        {
            var w = new SimWorld(new SimTuning());
            Operation op = NewOp(w);
            Add(w, op, 2, ModuleTypes.LogStorage, 110, true);
            int unbuilt = Add(w, op, 3, ModuleTypes.LogStorage, 112, false);
            w.Inventory.Produce(w.Inventory.GetOrAddStorage(2, ModuleTypes.LogStorage), Items.Log, 3);
            w.Inventory.Produce(w.Inventory.GetOrAddStorage(unbuilt, ModuleTypes.LogStorage), Items.Log, 5);
            Assert.AreEqual(3, OperationStatus.LogLoad(w, op));
        }

        [Test]
        public void OxPool_RestSpot_AssignedOx_SkipsUnbuiltStand_UsesBuiltStand()
        {
            var w = new SimWorld(new SimTuning());
            Operation op = NewOp(w);
            int stand = Add(w, op, 4, ModuleTypes.OxenStand, 105, false);
            Ox ox = w.People.AddOx(50, "O", 100, 100);
            w.Operations.AssignOx(op.Id, ox.Id);
            new OxPool(w).RestSpot(ox, out int id);
            Assert.AreEqual(OxState.NoStand, id);
            Finish(w, stand);
            new OxPool(w).RestSpot(ox, out id);
            Assert.AreEqual(stand, id);
        }

        [Test]
        public void OxPool_RestSpot_PoolOx_SkipsUnbuiltStand_UsesBuiltStand()
        {
            var w = new SimWorld(new SimTuning());
            Operation op = NewOp(w);
            int stand = Add(w, op, 4, ModuleTypes.OxenStand, 105, false);
            Ox ox = w.People.AddOx(50, "O", 100, 100);
            new OxPool(w).RestSpot(ox, out int id);
            Assert.AreEqual(OxState.NoStand, id);
            Finish(w, stand);
            new OxPool(w).RestSpot(ox, out id);
            Assert.AreEqual(stand, id);
        }

        [Test]
        public void AssignFamily_UnbuiltHut_Refused_ThenAcceptedOnceBuilt()
        {
            var w = new SimWorld(new SimTuning());
            Operation op = NewOp(w);
            Add(w, op, 1, ModuleTypes.WoodcuttingHut, 100, false);
            w.People.AddFamily(1, "F", 100, 100);
            CommandResult r = w.Execute(new AssignFamily(op.Id, 1));
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual(CommandRejectCode.NotAllowed, r.Reason);
            StringAssert.Contains("Hut not built yet", r.Text);
            Assert.IsNull(w.People.FindFamily(1).WorkplaceId);
            Finish(w, 1);
            Assert.IsTrue(w.Execute(new AssignFamily(op.Id, 1)).IsAccepted);
        }
    }
}
