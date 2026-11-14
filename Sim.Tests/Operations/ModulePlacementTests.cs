using System;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>ModulePlacement.Preview, PlaceModule and RemoveModule (T-269, WC-03/04/07/08).</summary>
    public class ModulePlacementTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld World() => WorldWithLogs(100);

        private static SimWorld WorldWithLogs(int logs)
        {
            var w = new SimWorld(new SimTuning());
            if (logs > 0) w.Inventory.Produce(w.Inventory.AddStockpile(M(0, 0)), Items.Log, logs);
            return w;
        }

        // Area is the square (100,100)-(120,120).
        private static Operation Designate(SimWorld w)
        {
            var shape = AreaShape.Straight(new[] { M(100, 100), M(120, 100), M(120, 120), M(100, 120) });
            CommandResult r = w.Execute(new DesignateArea(shape, AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return w.Operations.FindByArea((int)r.Created[0]);
        }

        private static ModuleRefusal Refusal(SimWorld w, Operation op, string type, double x, double z, double rot = 0) =>
            ModulePlacement.Preview(w, op.Id, type, M(x, z), rot).Refusal;

        [Test]
        public void Preview_InsideArea_IsOk()
        {
            SimWorld w = World();
            Operation op = Designate(w);
            Assert.IsTrue(ModulePlacement.Preview(w, op.Id, ModuleTypes.WoodcuttingHutId, M(110, 110), 0).Ok);
        }

        [Test]
        public void Preview_FlushWithEdge_IsOk()
        {
            SimWorld w = World();
            Operation op = Designate(w);
            Assert.AreEqual(ModuleRefusal.None, Refusal(w, op, ModuleTypes.WoodcuttingHutId, 103, 110));
        }

        [Test]
        public void Preview_OneCentimetreOutside_IsOutsideArea()
        {
            SimWorld w = World();
            Operation op = Designate(w);
            Assert.AreEqual(ModuleRefusal.OutsideArea, Refusal(w, op, ModuleTypes.WoodcuttingHutId, 102.99, 110));
            Assert.AreEqual(ModuleRefusal.OutsideArea, Refusal(w, op, ModuleTypes.WoodcuttingHutId, 117.01, 110));
            Assert.AreEqual(ModuleRefusal.OutsideArea, Refusal(w, op, ModuleTypes.WoodcuttingHutId, 110, 101.99));
            Assert.AreEqual(ModuleRefusal.OutsideArea, Refusal(w, op, ModuleTypes.WoodcuttingHutId, 110, 118.01));
        }

        [Test]
        public void Preview_OneCentimetreInside_IsOk()
        {
            SimWorld w = World();
            Operation op = Designate(w);
            Assert.AreEqual(ModuleRefusal.None, Refusal(w, op, ModuleTypes.WoodcuttingHutId, 103.01, 110));
            Assert.AreEqual(ModuleRefusal.None, Refusal(w, op, ModuleTypes.WoodcuttingHutId, 110, 117.99));
        }

        [Test]
        public void Preview_FarOutside_IsOutsideArea()
        {
            SimWorld w = World();
            Operation op = Designate(w);
            Assert.AreEqual(ModuleRefusal.OutsideArea, Refusal(w, op, ModuleTypes.LogStorageId, 300, 300));
        }

        [Test]
        public void Preview_Rotated37Degrees_UsesRotatedCorners()
        {
            SimWorld w = World();
            Operation op = Designate(w);
            // Half extent along X of a 6x4 rectangle turned 37 degrees is 3cos37 + 2sin37 = 3.6000 m.
            Assert.AreEqual(ModuleRefusal.None, Refusal(w, op, ModuleTypes.WoodcuttingHutId, 103.61, 110, 37));
            Assert.AreEqual(ModuleRefusal.OutsideArea, Refusal(w, op, ModuleTypes.WoodcuttingHutId, 103.58, 110, 37));
            // Unrotated it would fit here, rotated it pokes out.
            Assert.AreEqual(ModuleRefusal.None, Refusal(w, op, ModuleTypes.WoodcuttingHutId, 103.2, 110, 0));
            Assert.AreEqual(ModuleRefusal.OutsideArea, Refusal(w, op, ModuleTypes.WoodcuttingHutId, 103.2, 110, 37));
        }

        [Test]
        public void Preview_OverlappingModule_IsOverlapsModule()
        {
            SimWorld w = World();
            Operation op = Designate(w);
            Assert.IsTrue(w.Execute(new PlaceModule(op.Id, ModuleTypes.WoodcuttingHutId, M(110, 110), 0)).IsAccepted);
            Assert.AreEqual(ModuleRefusal.OverlapsModule, Refusal(w, op, ModuleTypes.LogStorageId, 112, 110));
            Assert.AreEqual(ModuleRefusal.OverlapsModule, Refusal(w, op, ModuleTypes.LogStorageId, 110, 110, 37));
        }

        [Test]
        public void Preview_TouchingModule_IsOk()
        {
            SimWorld w = World();
            Operation op = Designate(w);
            w.Execute(new PlaceModule(op.Id, ModuleTypes.WoodcuttingHutId, M(110, 110), 0));
            // Hut spans x 107..113; storage 8 wide centred at 117 spans 113..121 (outside), use z instead.
            Assert.AreEqual(ModuleRefusal.None, Refusal(w, op, ModuleTypes.LogStorageId, 110, 115));
        }

        [Test]
        public void Preview_SecondHut_IsTooMany()
        {
            SimWorld w = World();
            Operation op = Designate(w);
            Assert.IsTrue(w.Execute(new PlaceModule(op.Id, ModuleTypes.WoodcuttingHutId, M(105, 105), 0)).IsAccepted);
            Assert.AreEqual(ModuleRefusal.TooMany, Refusal(w, op, ModuleTypes.WoodcuttingHutId, 115, 115));
            CommandResult r = w.Execute(new PlaceModule(op.Id, ModuleTypes.WoodcuttingHutId, M(115, 115), 0));
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual(1, op.ModuleIds.Count);
        }

        [Test]
        public void Preview_SecondOxenStand_IsTooMany_ButTwoStoragesAreFine()
        {
            SimWorld w = World();
            Operation op = Designate(w);
            w.Execute(new PlaceModule(op.Id, ModuleTypes.OxenStandId, M(102, 102), 0));
            Assert.AreEqual(ModuleRefusal.TooMany, Refusal(w, op, ModuleTypes.OxenStandId, 106, 102));
            w.Execute(new PlaceModule(op.Id, ModuleTypes.LogStorageId, M(110, 105), 0));
            Assert.AreEqual(ModuleRefusal.None, Refusal(w, op, ModuleTypes.LogStorageId, 110, 114));
        }

        [Test]
        public void Preview_UnknownType_IsNotAllowedType()
        {
            SimWorld w = World();
            Operation op = Designate(w);
            Assert.AreEqual(ModuleRefusal.NotAllowedType, Refusal(w, op, "module.nope", 110, 110));
            Assert.AreEqual(ModuleRefusal.NotAllowedType, Refusal(w, op, FootprintTypeDef.CampId, 110, 110));
            Assert.AreEqual(ModuleRefusal.NotAllowedType, Refusal(w, op, null, 110, 110));
        }

        [Test]
        public void Preview_UnknownOperation_IsUnknownOperation()
        {
            SimWorld w = World();
            Assert.AreEqual(ModuleRefusal.UnknownOperation,
                ModulePlacement.Preview(w, 99, ModuleTypes.WoodcuttingHutId, M(110, 110), 0).Refusal);
        }

        [Test]
        public void Preview_ChangesNothing()
        {
            SimWorld w = World();
            Operation op = Designate(w);
            int v = w.Footprints.Version, ov = w.Operations.Version;
            ModulePlacement.Preview(w, op.Id, ModuleTypes.WoodcuttingHutId, M(110, 110), 0);
            Assert.AreEqual(v, w.Footprints.Version);
            Assert.AreEqual(ov, w.Operations.Version);
        }

        [Test]
        public void Preview_ConcaveArea_NotchIsOutside()
        {
            SimWorld w = World();
            // L shape: 40x40 square minus the top-right 20x20 corner.
            var shape = AreaShape.Straight(new[]
            {
                M(200, 200), M(240, 200), M(240, 220), M(220, 220), M(220, 240), M(200, 240),
            });
            CommandResult r = w.Execute(new DesignateArea(shape, AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Operation op = w.Operations.FindByArea((int)r.Created[0]);
            Assert.AreEqual(ModuleRefusal.None, Refusal(w, op, ModuleTypes.LogStorageId, 210, 230));
            Assert.AreEqual(ModuleRefusal.OutsideArea, Refusal(w, op, ModuleTypes.LogStorageId, 220, 225));
        }

        [Test]
        public void Place_AddsFootprintAndListsItOnTheOperation()
        {
            SimWorld w = World();
            Operation op = Designate(w);
            CommandResult r = w.Execute(new PlaceModule(op.Id, ModuleTypes.LogStorageId, M(110, 110), 37));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int id = (int)r.Created[0];
            Footprint f = w.Footprints.Get(id);
            Assert.IsNotNull(f);
            Assert.AreEqual(ModuleTypes.LogStorageId, f.Type.Id);
            Assert.AreEqual(37, f.RotationDegrees);
            CollectionAssert.AreEqual(new[] { id }, op.ModuleIds);
        }

        [Test]
        public void Place_GivesEachModuleItsOwnId()
        {
            SimWorld w = World();
            Operation op = Designate(w);
            long a = w.Execute(new PlaceModule(op.Id, ModuleTypes.WoodcuttingHutId, M(105, 105), 0)).Created[0];
            long b = w.Execute(new PlaceModule(op.Id, ModuleTypes.LogStorageId, M(112, 112), 0)).Created[0];
            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void Place_RemovesTreesUnderModule_AndReportsTheirIds()
        {
            SimWorld w = World();
            Operation op = Designate(w);
            w.Trees.Add(new TreeId(1), 110f, 110f, 5);   // under the hut
            w.Trees.Add(new TreeId(2), 111f, 111f, 5);   // under the hut
            w.Trees.Add(new TreeId(3), 118f, 118f, 5);   // in the area, not under the hut
            w.Trees.Add(new TreeId(4), 300f, 300f, 5);
            CommandResult r = w.Execute(new PlaceModule(op.Id, ModuleTypes.WoodcuttingHutId, M(110, 110), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            CollectionAssert.AreEquivalent(new long[] { 1, 2 }, r.RemovedTreeIds);
            Assert.AreEqual(2, w.Trees.Count);
        }

        [Test]
        public void Place_Refused_ChangesNothingAndRemovesNoTrees()
        {
            SimWorld w = World();
            Operation op = Designate(w);
            w.Trees.Add(new TreeId(1), 102f, 110f, 5);
            CommandResult r = w.Execute(new PlaceModule(op.Id, ModuleTypes.WoodcuttingHutId, M(102.99, 110), 0));
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual(CommandRejectCode.NotAllowed, r.Reason);
            Assert.AreEqual(0, w.Footprints.Count);
            Assert.AreEqual(1, w.Trees.Count);
            Assert.IsEmpty(op.ModuleIds);
        }

        [Test]
        public void Place_UnknownOperation_IsNotFound()
        {
            SimWorld w = World();
            CommandResult r = w.Execute(new PlaceModule(5, ModuleTypes.WoodcuttingHutId, M(1, 1), 0));
            Assert.AreEqual(CommandRejectCode.NotFound, r.Reason);
        }

        [Test]
        public void Place_NonFiniteRotation_IsInvalidArgument()
        {
            SimWorld w = World();
            Operation op = Designate(w);
            CommandResult r = w.Execute(new PlaceModule(op.Id, ModuleTypes.WoodcuttingHutId, M(110, 110), double.NaN));
            Assert.AreEqual(CommandRejectCode.InvalidArgument, r.Reason);
        }

        [Test]
        public void Remove_TakesModuleOutOfOperationAndRegistry()
        {
            SimWorld w = World();
            Operation op = Designate(w);
            int id = (int)w.Execute(new PlaceModule(op.Id, ModuleTypes.WoodcuttingHutId, M(110, 110), 0)).Created[0];
            CommandResult r = w.Execute(new RemoveModule(id));
            Assert.IsTrue(r.IsAccepted);
            CollectionAssert.AreEqual(new long[] { id }, r.Removed);
            Assert.IsNull(w.Footprints.Get(id));
            Assert.IsEmpty(op.ModuleIds);
            // The hut slot is free again.
            Assert.IsTrue(w.Execute(new PlaceModule(op.Id, ModuleTypes.WoodcuttingHutId, M(110, 110), 0)).IsAccepted);
        }

        [Test]
        public void Remove_Unknown_IsNotFound()
        {
            SimWorld w = World();
            Assert.AreEqual(CommandRejectCode.NotFound, w.Execute(new RemoveModule(42)).Reason);
        }

        [Test]
        public void Remove_CampFootprint_IsNotFoundBecauseItIsNotAModule()
        {
            SimWorld w = World();
            w.Footprints.Add(1, FootprintTypeDef.Camp, M(50, 50), 0);
            Assert.AreEqual(CommandRejectCode.NotFound, w.Execute(new RemoveModule(1)).Reason);
            Assert.IsNotNull(w.Footprints.Get(1));
        }

        [Test]
        public void Place_HutOverAgent_PushesItToNearestWalkablePoint()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            SimWorld w = SimWorld.FromMapData(map, new SimTuning());
            w.Step();
            w.Inventory.Produce(w.Inventory.AddStockpile(M(0, 0)), Items.Log, 20);
            Operation op = Designate(w);
            w.People.AddFamily(1, "F", 110, 110);
            Citizen c = w.People.AddCitizen(1, "C", Sex.Male, 0, 1, 110, 110);
            Assert.IsTrue(w.Execute(new PlaceModule(op.Id, ModuleTypes.WoodcuttingHutId, M(110, 110), 0)).IsAccepted);
            w.Step();
            Assert.IsFalse(w.Nav.IsBlocked(w.Nav.CellX(c.X), w.Nav.CellZ(c.Z)));
            Assert.That(Math.Abs(c.X - 110) > 2.5 || Math.Abs(c.Z - 110) > 1.5, "citizen left the hut footprint");
        }

        [Test]
        public void Place_WalkableModule_DoesNotPushAgents()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            SimWorld w = SimWorld.FromMapData(map, new SimTuning());
            w.Step();
            Operation op = Designate(w);
            w.People.AddFamily(1, "F", 110, 110);
            Citizen c = w.People.AddCitizen(1, "C", Sex.Male, 0, 1, 110, 110);
            w.Execute(new PlaceModule(op.Id, ModuleTypes.LogStorageId, M(110, 110), 0));
            w.Step();
            Assert.AreEqual(110, c.X, 1e-9);
            Assert.AreEqual(110, c.Z, 1e-9);
        }
    }
}
