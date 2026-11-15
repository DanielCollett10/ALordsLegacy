using LordsLegacy.Game.Tools;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Tests.EditMode
{
    /// <summary>OperationDraft rules BT-10..18 (T-389).</summary>
    public class OperationDraftTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld World(int logs)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning());
            if (logs > 0) w.Inventory.Produce(w.Inventory.AddStockpile(M(0, 0)), Items.Log, logs);
            return w;
        }

        private static AreaShape Square() =>
            AreaShape.Straight(new[] { M(100, 100), M(140, 100), M(140, 120), M(100, 120) });

        private static OperationDraft Locked(int logs)
        {
            var d = new OperationDraft(World(logs), OperationTypes.Woodcutting);
            string reason;
            Assert.IsTrue(d.LockArea(Square(), out reason), reason);
            return d;
        }

        private static bool Place(OperationDraft d, string type, double x, double z, out string reason)
        {
            Assert.IsTrue(d.PickModule(type));
            return d.AddGhost(M(x, z), 0, out reason);
        }

        [Test]
        public void StartsInAreaStage_LockMovesToModuleStage()
        {
            var d = new OperationDraft(World(40), OperationTypes.Woodcutting);
            Assert.AreEqual(DraftStage.AreaStage, d.Stage);
            Assert.IsNull(d.BuildCommand());
            string reason;
            Assert.IsTrue(d.LockArea(Square(), out reason));
            Assert.AreEqual(DraftStage.ModuleStage, d.Stage);
        }

        [Test]
        public void InvalidArea_DoesNotLock()
        {
            var d = new OperationDraft(World(40), OperationTypes.Woodcutting);
            string reason;
            Assert.IsFalse(d.LockArea(AreaShape.Straight(new[] { M(100, 100), M(101, 100) }), out reason));
            Assert.IsNotEmpty(reason);
            Assert.AreEqual(DraftStage.AreaStage, d.Stage);
        }

        [Test]
        public void AreaStage_CannotPickOrPlaceModules()
        {
            var d = new OperationDraft(World(40), OperationTypes.Woodcutting);
            Assert.IsFalse(d.PickModule(ModuleTypes.WoodcuttingHutId));
            string reason;
            Assert.IsFalse(d.AddGhost(M(110, 110), 0, out reason));
            Assert.AreEqual(0, d.Ghosts.Count);
        }

        [Test]
        public void AddGhost_NothingInHand_Refuses()
        {
            var d = Locked(40);
            string reason;
            Assert.IsFalse(d.AddGhost(M(110, 110), 0, out reason));
            Assert.IsNotEmpty(reason);
        }

        [Test]
        public void AddGhost_AddsAndCountsCost()
        {
            var d = Locked(40);
            string reason;
            Assert.IsTrue(Place(d, ModuleTypes.WoodcuttingHutId, 110, 110, out reason), reason);
            Assert.AreEqual(1, d.Ghosts.Count);
            Assert.AreEqual(8, d.RunningCost);
            Assert.AreEqual(40, d.Have);
        }

        [Test]
        public void AddGhost_OutsideArea_RefusesWithSimReason()
        {
            var d = Locked(40);
            string reason;
            Assert.IsFalse(Place(d, ModuleTypes.WoodcuttingHutId, 200, 200, out reason));
            Assert.AreEqual("Must sit fully inside the area", reason);
            Assert.AreEqual(0, d.Ghosts.Count);
        }

        [Test]
        public void AddGhost_OverlapsGhost_Refuses()
        {
            var d = Locked(40);
            string reason;
            Assert.IsTrue(Place(d, ModuleTypes.WoodcuttingHutId, 110, 110, out reason));
            Assert.IsFalse(Place(d, ModuleTypes.LogStorageId, 110, 110, out reason));
            Assert.AreEqual("Overlaps another module", reason);
        }

        [Test]
        public void AddGhost_SecondHut_RefusedByMaxCount()
        {
            var d = Locked(40);
            string reason;
            Assert.IsTrue(Place(d, ModuleTypes.WoodcuttingHutId, 110, 110, out reason));
            Assert.IsFalse(Place(d, ModuleTypes.WoodcuttingHutId, 130, 110, out reason));
            Assert.AreEqual("Only 1 allowed per operation", reason);
        }

        [Test]
        public void AddGhost_TooFewLogs_RefusesWithHaveAndNeed()
        {
            var d = Locked(5);
            string reason;
            Assert.IsFalse(Place(d, ModuleTypes.WoodcuttingHutId, 110, 110, out reason));
            Assert.AreEqual("Not enough logs (have 5, need 8)", reason);
            Assert.AreEqual(0, d.RunningCost);
        }

        [Test]
        public void RemoveGhostAt_RemovesOnlyGhostUnderPoint()
        {
            var d = Locked(40);
            string reason;
            Assert.IsTrue(Place(d, ModuleTypes.WoodcuttingHutId, 110, 110, out reason));
            Assert.IsTrue(Place(d, ModuleTypes.LogStorageId, 125, 110, out reason));
            Assert.IsTrue(d.RemoveGhostAt(M(110, 110)));
            Assert.AreEqual(1, d.Ghosts.Count);
            Assert.AreEqual(ModuleTypes.LogStorageId, d.Ghosts[0].Type.Id);
            Assert.AreEqual(0, d.RunningCost);
        }

        [Test]
        public void RemoveGhostAt_EmptyGround_DoesNothing()
        {
            var d = Locked(40);
            string reason;
            Assert.IsTrue(Place(d, ModuleTypes.WoodcuttingHutId, 110, 110, out reason));
            Assert.IsFalse(d.RemoveGhostAt(M(135, 118)));
            Assert.AreEqual(1, d.Ghosts.Count);
        }

        [Test]
        public void MissingText_NamesWhatIsMissing_AndCanConfirmFollows()
        {
            var d = Locked(40);
            string reason;
            Assert.AreEqual("Place a Hut", d.MissingText);
            Assert.IsFalse(d.CanConfirm);
            Assert.IsTrue(Place(d, ModuleTypes.WoodcuttingHutId, 110, 110, out reason));
            Assert.AreEqual("Place a Log storage", d.MissingText);
            Assert.IsFalse(d.CanConfirm);
            Assert.IsTrue(Place(d, ModuleTypes.LogStorageId, 125, 110, out reason));
            Assert.AreEqual("", d.MissingText);
            Assert.IsTrue(d.CanConfirm);
        }

        [Test]
        public void CanConfirm_FalseBeforeAreaLocked()
        {
            var d = new OperationDraft(World(40), OperationTypes.Woodcutting);
            Assert.IsFalse(d.CanConfirm);
        }

        [Test]
        public void Escape_WithModuleInHand_DropsOnlyTheModule()
        {
            var d = Locked(40);
            string reason;
            Assert.IsTrue(Place(d, ModuleTypes.WoodcuttingHutId, 110, 110, out reason));
            Assert.AreEqual(DraftEscape.DroppedModule, d.Escape());
            Assert.IsNull(d.ModuleInHand);
            Assert.AreEqual(1, d.Ghosts.Count);
            Assert.AreEqual(DraftStage.ModuleStage, d.Stage);
        }

        [Test]
        public void Escape_WithEmptyHand_SignalsCancel_AndCancelClearsEverything()
        {
            var d = Locked(40);
            string reason;
            Assert.IsTrue(Place(d, ModuleTypes.WoodcuttingHutId, 110, 110, out reason));
            d.Escape();
            Assert.AreEqual(DraftEscape.Cancel, d.Escape());
            d.Cancel();
            Assert.AreEqual(0, d.Ghosts.Count);
            Assert.AreEqual(DraftStage.AreaStage, d.Stage);
            Assert.IsNull(d.BuildCommand());
        }

        [Test]
        public void BuildCommand_ModulesInPlacementOrder()
        {
            var d = Locked(40);
            string reason;
            Assert.IsTrue(Place(d, ModuleTypes.LogStorageId, 125, 110, out reason));
            Assert.IsTrue(Place(d, ModuleTypes.WoodcuttingHutId, 110, 110, out reason));
            var cmd = d.BuildCommand();
            Assert.AreEqual(OperationTypes.WoodcuttingId, cmd.OperationTypeId);
            Assert.AreEqual(2, cmd.Modules.Count);
            Assert.AreEqual(ModuleTypes.LogStorageId, cmd.Modules[0].TypeId);
            Assert.AreEqual(ModuleTypes.WoodcuttingHutId, cmd.Modules[1].TypeId);
            Assert.AreEqual(M(110, 110), cmd.Modules[1].Centre);
            Assert.IsTrue(cmd.Validate(World(40)).IsAccepted);
        }
    }
}
