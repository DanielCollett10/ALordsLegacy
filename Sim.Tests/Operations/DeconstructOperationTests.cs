using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>DeconstructOperation and the operation-area delete refusal (T-395, CN-58, HM-14b).</summary>
    public class DeconstructOperationTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static AreaShape Shape() =>
            AreaShape.Straight(new[] { M(100, 100), M(140, 100), M(140, 120), M(100, 120) });

        private static (SimWorld w, MapData map) Make()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            SimWorld w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(0, 0)), Items.Log, 20);
            return (w, map);
        }

        private static Operation Designate(SimWorld w)
        {
            CommandResult r = w.Execute(new DesignateArea(Shape(), AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return w.Operations.FindByArea((int)r.Created[0]);
        }

        private static int PlaceBuilt(SimWorld w, Operation op, string type, double x)
        {
            int id = (int)w.Execute(new PlaceModule(op.Id, type, M(x, 110), 0)).Created[0];
            w.Construction.Remove(id);
            return id;
        }

        [Test]
        public void DeconstructOperation_MarksEveryModule_AreaAndOperationGoAfterLastFinishes()
        {
            (SimWorld w, _) = Make();
            Operation op = Designate(w);
            int areaId = op.AreaId;
            int stand = PlaceBuilt(w, op, ModuleTypes.OxenStandId, 110);
            int storage = PlaceBuilt(w, op, ModuleTypes.LogStorageId, 130);

            Assert.IsTrue(w.Execute(new DeconstructOperation(op.Id)).IsAccepted);
            Assert.IsNull(w.Footprints.Get(storage), "zero-minute module goes at once");
            Assert.IsNotNull(w.Deconstruction.Get(stand));
            Assert.IsNotNull(w.Areas.Get(areaId), "area stays while a module remains");
            Assert.IsTrue(w.Deconstruction.IsOperationPending(op.Id));

            DeconstructionWork.Finish(w, stand);
            Assert.IsNull(w.Areas.Get(areaId));
            Assert.IsNull(w.Operations.Get(op.Id));
            Assert.IsNull(w.Footprints.Get(stand));
            Assert.IsFalse(w.Deconstruction.IsOperationPending(op.Id));
        }

        [Test]
        public void DeconstructOperation_AllZeroMinuteModules_RemovesAreaInSameCommand()
        {
            (SimWorld w, _) = Make();
            Operation op = Designate(w);
            int areaId = op.AreaId;
            PlaceBuilt(w, op, ModuleTypes.LogStorageId, 110);

            CommandResult r = w.Execute(new DeconstructOperation(op.Id));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Assert.IsNull(w.Areas.Get(areaId));
            Assert.IsNull(w.Operations.Get(op.Id));
        }

        [Test]
        public void DeconstructOperation_NoModules_RemovesAreaAndOperation()
        {
            (SimWorld w, _) = Make();
            Operation op = Designate(w);
            Assert.IsTrue(w.Execute(new DeconstructOperation(op.Id)).IsAccepted);
            Assert.IsNull(w.Operations.Get(op.Id));
        }

        [Test]
        public void DeconstructOperation_Unknown_IsRejected()
        {
            (SimWorld w, _) = Make();
            Assert.IsTrue(w.Execute(new DeconstructOperation(99)).IsRejected);
        }

        [Test]
        public void DeconstructOperation_ReservedLogsInStorage_RefusesWholeCommand_NothingMarked()
        {
            (SimWorld w, _) = Make();
            Operation op = Designate(w);
            int stand = PlaceBuilt(w, op, ModuleTypes.OxenStandId, 136);
            int storage = PlaceBuilt(w, op, ModuleTypes.LogStorageId, 110);
            w.Inventory.Produce(w.Inventory.GetOrAddStorage(storage, 100), Items.Log, 10);
            w.Execute(new PlaceModule(op.Id, ModuleTypes.WoodcuttingHutId, M(125, 110), 0));

            CommandResult r = w.Execute(new DeconstructOperation(op.Id));
            Assert.IsTrue(r.IsRejected);
            Assert.IsNull(w.Deconstruction.Get(stand));
            Assert.IsFalse(w.Deconstruction.IsOperationPending(op.Id));
        }

        [Test]
        public void CancelDeconstruction_OnPendingOperationModule_ClearsPendingFlag()
        {
            (SimWorld w, _) = Make();
            Operation op = Designate(w);
            int stand = PlaceBuilt(w, op, ModuleTypes.OxenStandId, 110);
            w.Execute(new DeconstructOperation(op.Id));
            Assert.IsTrue(w.Execute(new CancelDeconstruction(stand)).IsAccepted);
            Assert.IsFalse(w.Deconstruction.IsOperationPending(op.Id));
        }

        [Test]
        public void DeleteArea_OperationAreaWithModules_IsRefused_Cn58()
        {
            (SimWorld w, _) = Make();
            Operation op = Designate(w);
            Place(w, op);

            CommandResult r = w.Execute(new DeleteArea(op.AreaId));
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual("Deconstruct its buildings first", r.Text);
            Assert.IsNotNull(w.Areas.Get(op.AreaId));
            Assert.IsNotNull(w.Operations.Get(op.Id));
        }

        private static void Place(SimWorld w, Operation op) =>
            Assert.IsTrue(w.Execute(new PlaceModule(op.Id, ModuleTypes.OxenStandId, M(110, 110), 0)).IsAccepted);

        [Test]
        public void DeleteArea_OperationAreaWithoutModules_StillWorks()
        {
            (SimWorld w, _) = Make();
            Operation op = Designate(w);
            Assert.IsTrue(w.Execute(new DeleteArea(op.AreaId)).IsAccepted);
            Assert.IsNull(w.Operations.Get(op.Id));
        }

        [Test]
        public void DeleteArea_TestArea_IsUnaffected()
        {
            (SimWorld w, _) = Make();
            CommandResult d = w.Execute(new DesignateArea(Shape(), AreaTypes.TestId));
            Assert.IsTrue(d.IsAccepted, d.Text);
            Assert.IsTrue(w.Execute(new DeleteArea((int)d.Created[0])).IsAccepted);
            Assert.IsNull(w.Areas.Get((int)d.Created[0]));
        }

        [Test]
        public void PendingRemoval_RoundTripsThroughSave()
        {
            (SimWorld w, MapData map) = Make();
            Operation op = Designate(w);
            PlaceBuilt(w, op, ModuleTypes.OxenStandId, 110);
            w.Execute(new DeconstructOperation(op.Id));
            Assert.IsTrue(w.Deconstruction.IsOperationPending(op.Id));

            byte[] bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(bytes, map);
            Assert.IsTrue(back.Deconstruction.IsOperationPending(op.Id));
            Assert.AreEqual(bytes, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash));
        }

        [Test]
        public void SaveVersion_IsEight_VersionFourBytesAreRefused()
        {
            Assert.GreaterOrEqual(SaveWriter.CurrentVersion, 17);
            byte[] bytes = { (byte)'L', (byte)'L', (byte)'S', (byte)'V', 4, 0, 0, 0, 0, 0, 0, 0 };
            var e = Assert.Throws<SaveFormatException>(() => SaveReader.ReadHeaderOnly(bytes));
            StringAssert.Contains("Unknown save format version 4 (this build reads version " + SaveWriter.CurrentVersion + ").", e.Message);
        }
    }
}
