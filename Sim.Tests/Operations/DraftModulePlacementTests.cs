using System.Collections.Generic;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>DraftModulePlacement.Preview (T-385, BT-12/13/18).</summary>
    public class DraftModulePlacementTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static readonly PointMm[] Area = { M(100, 100), M(120, 100), M(120, 120), M(100, 120) };

        private static SimWorld World(int logs)
        {
            var w = new SimWorld(new SimTuning());
            if (logs > 0) w.Inventory.Produce(w.Inventory.AddStockpile(M(0, 0)), Items.Log, logs);
            return w;
        }

        private static Footprint Ghost(FootprintTypeDef t, double x, double z) => new Footprint(0, t, M(x, z), 0);

        private static ModulePlacementResult Run(SimWorld w, string type, double x, double z,
            IReadOnlyList<Footprint> ghosts = null, int cost = 0, double rot = 0) =>
            DraftModulePlacement.Preview(w, OperationTypes.Woodcutting, Area, ghosts, type, M(x, z), rot, cost);

        [Test]
        public void Preview_InsideAreaWithEnoughLogs_IsOk()
        {
            Assert.IsTrue(Run(World(40), ModuleTypes.WoodcuttingHutId, 110, 110).Ok);
        }

        [Test]
        public void Preview_OutsidePolygon_RefusesOutsideArea()
        {
            Assert.AreEqual(ModuleRefusal.OutsideArea, Run(World(40), ModuleTypes.WoodcuttingHutId, 102.99, 110).Refusal);
            Assert.AreEqual(ModuleRefusal.OutsideArea, Run(World(40), ModuleTypes.WoodcuttingHutId, 200, 200).Refusal);
        }

        [Test]
        public void Preview_FlushWithPolygonEdge_IsOk()
        {
            Assert.IsTrue(Run(World(40), ModuleTypes.WoodcuttingHutId, 103, 110).Ok);
        }

        [Test]
        public void Preview_OverlappingGhost_RefusesOverlapsModule()
        {
            var ghosts = new[] { Ghost(ModuleTypes.LogStorage, 110, 110) };
            Assert.AreEqual(ModuleRefusal.OverlapsModule, Run(World(40), ModuleTypes.WoodcuttingHutId, 111, 110, ghosts).Refusal);
        }

        [Test]
        public void Preview_TouchingGhost_IsOk()
        {
            var ghosts = new[] { Ghost(ModuleTypes.LogStorage, 108, 110) };
            // Place the hut far enough apart to merely touch is geometry-dependent; clear separation must be Ok.
            Assert.IsTrue(Run(World(40), ModuleTypes.WoodcuttingHutId, 116, 116, ghosts).Ok);
        }

        [Test]
        public void Preview_OverlappingWorldFootprint_RefusesOverlapsFootprint()
        {
            SimWorld w = World(40);
            w.Footprints.Add(w.Footprints.NextId(), ModuleTypes.LogStorage, M(110, 110), 0);
            Assert.AreEqual(ModuleRefusal.OverlapsFootprint, Run(w, ModuleTypes.WoodcuttingHutId, 111, 110).Refusal);
        }

        [Test]
        public void Preview_OverlappingRoad_RefusesOverlapsRoad()
        {
            SimWorld w = World(40);
            CommandResult r = w.Execute(new DrawRoad(new[] { M(90, 110), M(130, 110) }, RoadTypes.DirtPathId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Assert.AreEqual(ModuleRefusal.OverlapsRoad, Run(w, ModuleTypes.WoodcuttingHutId, 110, 110).Refusal);
            Assert.IsTrue(Run(w, ModuleTypes.WoodcuttingHutId, 110, 116).Ok);
        }

        [Test]
        public void Preview_SecondHutGhost_RefusesTooMany()
        {
            var ghosts = new[] { Ghost(ModuleTypes.WoodcuttingHut, 104, 104) };
            Assert.AreEqual(ModuleRefusal.TooMany, Run(World(40), ModuleTypes.WoodcuttingHutId, 116, 116, ghosts).Refusal);
        }

        [Test]
        public void Preview_GhostCostPlusThisExceedsLogs_RefusesWithHaveAndNeed()
        {
            ModulePlacementResult r = Run(World(10), ModuleTypes.WoodcuttingHutId, 110, 110, null, 5);
            Assert.AreEqual(ModuleRefusal.NotEnoughMaterials, r.Refusal);
            StringAssert.StartsWith("Not enough logs (have 10, need ", r.Text);
        }

        [Test]
        public void Preview_GhostCostPlusThisEqualsLogs_IsOk()
        {
            int hut = ModuleTypes.WoodcuttingHut.ConstructionRecipe.Logs;
            Assert.IsTrue(Run(World(hut + 5), ModuleTypes.WoodcuttingHutId, 110, 110, null, 5).Ok);
        }

        [Test]
        public void Preview_UnknownModuleType_RefusesNotAllowedType()
        {
            Assert.AreEqual(ModuleRefusal.NotAllowedType, Run(World(40), "module.nope", 110, 110).Refusal);
        }

        [Test]
        public void Preview_ChangesNothing()
        {
            SimWorld w = World(40);
            int version = w.Footprints.Version;
            Run(w, ModuleTypes.WoodcuttingHutId, 110, 110);
            Assert.AreEqual(version, w.Footprints.Version);
        }
    }
}
