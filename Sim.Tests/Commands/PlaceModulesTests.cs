using System.Collections.Generic;
using System.Linq;
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
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Commands
{
    /// <summary>PlaceModules: add several modules to an existing operation, all or nothing (T-387, BT-20..22).</summary>
    public class PlaceModulesTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static MapData Map() => new MapData("flat",
            new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]),
            new[] { new MapTree(new TreeId(1), 134f, 117f, 16), new MapTree(new TreeId(2), 190f, 190f, 16) });

        private static byte[] Bytes(SimWorld w, MapData map) => SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);

        // Operation with a hut and one storage (existing), 40 logs in stock; hut costs 8 so 32 are free.
        private static SimWorld World(MapData map, out Operation op, int logs = 40)
        {
            var w = SimWorld.FromMapData(map, new SimTuning());
            w.Inventory.Produce(w.Inventory.AddStockpile(M(0, 0)), Items.Log, logs);
            var area = AreaShape.Straight(new[] { M(100, 100), M(140, 100), M(140, 120), M(100, 120) });
            CommandResult r = w.Execute(new DesignateOperation(area, OperationTypes.WoodcuttingId, new List<ModuleSpec>
            {
                new ModuleSpec(ModuleTypes.WoodcuttingHutId, M(110, 110), 0),
                new ModuleSpec(ModuleTypes.LogStorageId, M(125, 110), 0),
            }));
            Assert.IsTrue(r.IsAccepted, r.Text);
            op = w.Operations.FindByArea((int)r.Created[0]);
            return w;
        }

        private static ModuleSpec Storage2() => new ModuleSpec(ModuleTypes.LogStorageId, M(134, 110), 0);
        private static ModuleSpec Stand() => new ModuleSpec(ModuleTypes.OxenStandId, M(135, 117), 0);

        private static void AssertRefusedUnchanged(SimWorld w, MapData map, PlaceModules cmd, string text)
        {
            byte[] before = Bytes(w, map);
            CommandResult r = w.Execute(cmd);
            Assert.IsTrue(r.IsRejected, "refused");
            StringAssert.Contains(text, r.Text);
            Assert.AreEqual(before, Bytes(w, map));
        }

        [Test]
        public void Validate_EmptyList_RefusesAndChangesNothing()
        {
            MapData map = Map();
            SimWorld w = World(map, out Operation op);
            AssertRefusedUnchanged(w, map, new PlaceModules(op.Id, new List<ModuleSpec>()), "at least one");
        }

        [Test]
        public void Validate_UnknownOperation_RefusesNotFound()
        {
            SimWorld w = World(Map(), out _);
            CommandResult r = new PlaceModules(999, new[] { Storage2() }).Validate(w);
            Assert.AreEqual(CommandRejectCode.NotFound, r.Reason);
        }

        [Test]
        public void Validate_SecondHut_RefusesAndChangesNothing()
        {
            MapData map = Map();
            SimWorld w = World(map, out Operation op);
            var hut = new ModuleSpec(ModuleTypes.WoodcuttingHutId, M(134, 117), 0);
            AssertRefusedUnchanged(w, map, new PlaceModules(op.Id, new[] { hut }), "Only 1 allowed");
        }

        [Test]
        public void Validate_InvalidGhostAfterGoodOne_RefusesWholeBatch()
        {
            MapData map = Map();
            SimWorld w = World(map, out Operation op);
            var outside = new ModuleSpec(ModuleTypes.OxenStandId, M(300, 300), 0);
            AssertRefusedUnchanged(w, map, new PlaceModules(op.Id, new[] { Storage2(), outside }), "inside");
        }

        [Test]
        public void Validate_OverlapsExistingModule_Refuses()
        {
            MapData map = Map();
            SimWorld w = World(map, out Operation op);
            var onStorage = new ModuleSpec(ModuleTypes.LogStorageId, M(125, 110), 0);
            AssertRefusedUnchanged(w, map, new PlaceModules(op.Id, new[] { onStorage }), "Overlaps");
        }

        [Test]
        public void Validate_GhostsOverlapEachOther_Refuses()
        {
            MapData map = Map();
            SimWorld w = World(map, out Operation op);
            AssertRefusedUnchanged(w, map, new PlaceModules(op.Id, new[] { Storage2(), Storage2() }), "Overlaps");
        }

        [Test]
        public void Validate_UnaffordableTotal_RefusesAndChangesNothing()
        {
            MapData map = Map();
            SimWorld w = World(map, out Operation op, logs: 8); // hut took all 8: the stand's 1 log is unaffordable
            AssertRefusedUnchanged(w, map, new PlaceModules(op.Id, new[] { Storage2(), Stand() }), "Not enough logs");
        }

        [Test]
        public void Apply_AddsAllFootprintsAndSites_InOneCommand()
        {
            MapData map = Map();
            SimWorld w = World(map, out Operation op);
            int logBefore = w.CommandLog.Count;
            int modulesBefore = op.ModuleIds.Count;
            CommandResult r = w.Execute(new PlaceModules(op.Id, new[] { Storage2(), Stand() }));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Assert.AreEqual(logBefore + 1, w.CommandLog.Count);
            Assert.AreEqual(2, r.Created.Length);
            Assert.AreEqual(modulesBefore + 2, op.ModuleIds.Count);
            Assert.IsNotNull(w.Footprints.Get((int)r.Created[0]));
            Assert.IsNull(w.Construction.Get((int)r.Created[0]), "storage is built at once");
            Assert.IsNotNull(w.Construction.Get((int)r.Created[1]), "stand needs a site");
            Assert.AreEqual(8 + 1, w.Inventory.Reservations.Sum(x => x.Count));
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void Apply_ExpandingKeepsOperationWorking_AndCapacityRisesOnlyOnceBuilt()
        {
            MapData map = Map();
            SimWorld w = World(map, out Operation op);
            // Make the operation Working: built hut, a family and a tree to fell.
            int hutId = op.ModuleIds[0];
            w.Construction.Remove(hutId);
            w.People.AddFamily(1, "F", 0, 0);
            w.People.AddCitizen(10, "C", Sex.Male, -40L * Citizen.TicksPerYear, 1, 0, 0);
            w.Operations.AssignFamily(op.Id, 1);
            w.Trees.Add(new TreeId(50), 112f, 115f, 5);
            Assert.AreEqual(OperationState.Working, OperationStatus.Of(w, op));
            int cap = OperationStatus.LogCapacity(w, op);

            CommandResult r = w.Execute(new PlaceModules(op.Id, new[] { Storage2(), Stand() }));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int newStorage = (int)r.Created[0];
            int newStand = (int)r.Created[1];

            // The stand is unbuilt: the operation keeps working, capacity unchanged by it.
            Assert.IsFalse(w.IsBuilt(newStand));
            Assert.AreEqual(OperationState.Working, OperationStatus.Of(w, op));

            // Simulate the new storage still being a site (BT-22, CN-04): capacity does not count it yet.
            w.Construction.Add(new LordsLegacy.Sim.Construction.ConstructionSite(newStorage, w.Inventory.GetOrAddSite(newStorage).Id));
            Assert.AreEqual(OperationState.Working, OperationStatus.Of(w, op));
            Assert.AreEqual(cap, OperationStatus.LogCapacity(w, op), "unbuilt storage adds no capacity");

            w.Construction.Remove(newStorage);
            Assert.AreEqual(OperationState.Working, OperationStatus.Of(w, op));
            Assert.AreEqual(cap + ModuleTypes.LogStorage.LogCapacity, OperationStatus.LogCapacity(w, op));
        }
    }
}
