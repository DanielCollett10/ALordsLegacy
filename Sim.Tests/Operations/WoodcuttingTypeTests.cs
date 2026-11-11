using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Operations;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>Woodcutting area, module and operation type definitions (woodcutting.md WC-01, WC-02, WC-05, WC-06).</summary>
    public class WoodcuttingTypeTests
    {
        [Test]
        public void WoodcuttingArea_IsBuildingKind_WithSpecValues()
        {
            AreaTypeDef t = AreaTypes.Find("area.woodcutting");
            Assert.IsNotNull(t);
            Assert.AreSame(AreaTypes.Woodcutting, t);
            Assert.AreEqual(AreaKind.Building, t.Kind);
            Assert.AreEqual("Woodcutter", t.NamePrefix);
            Assert.AreEqual(150, t.MinAreaM2);
            Assert.AreEqual(5000, t.MaxAreaM2);
            Assert.AreEqual(10.0, t.MaxSlopeDegrees);
            Assert.IsTrue(t.ClearsTrees);
        }

        [Test]
        public void WoodcuttingWorkArea_IsWorkKind_WithSpecValues()
        {
            AreaTypeDef t = AreaTypes.Find("area.work_woodcutting");
            Assert.IsNotNull(t);
            Assert.AreSame(AreaTypes.WoodcuttingWork, t);
            Assert.AreEqual(AreaKind.Work, t.Kind);
            Assert.AreEqual(100, t.MinAreaM2);
            Assert.AreEqual(40000, t.MaxAreaM2);
            Assert.IsNull(t.MaxSlopeDegrees);
            Assert.IsFalse(t.ClearsTrees);
            Assert.IsFalse(t.BlocksRoads);
        }

        [Test]
        public void DefaultAreaCatalog_ContainsBothWoodcuttingTypes_AndLookupsWork()
        {
            var cat = AreaTypes.DefaultCatalog;
            Assert.AreSame(AreaTypes.Woodcutting, cat.Find("area.woodcutting"));
            Assert.AreSame(AreaTypes.WoodcuttingWork, cat.Find("area.work_woodcutting"));
            Assert.IsNull(cat.Find("area.woodcutting_hut"));
            Assert.AreEqual(4, cat.All.Count);
        }

        [Test]
        public void Hut_Is6x4_BlocksMovement_ClearsTrees()
        {
            FootprintTypeDef t = ModuleTypes.Find("module.woodcutting_hut");
            Assert.IsNotNull(t);
            Assert.AreSame(ModuleTypes.WoodcuttingHut, t);
            Assert.AreEqual(6000, t.WidthMm);
            Assert.AreEqual(4000, t.DepthMm);
            Assert.IsTrue(t.BlocksMovement);
            Assert.IsTrue(t.ClearsTrees);
            Assert.AreEqual(0, t.LogCapacity);
            Assert.AreEqual(0, t.OxCapacity);
        }

        [Test]
        public void LogStorage_Is8x6_Walkable_ClearsTrees_Holds20Logs()
        {
            FootprintTypeDef t = ModuleTypes.Find("module.log_storage");
            Assert.IsNotNull(t);
            Assert.AreSame(ModuleTypes.LogStorage, t);
            Assert.AreEqual(8000, t.WidthMm);
            Assert.AreEqual(6000, t.DepthMm);
            Assert.IsFalse(t.BlocksMovement);
            Assert.IsTrue(t.ClearsTrees);
            Assert.AreEqual(20, t.LogCapacity);
            Assert.AreEqual(0, t.OxCapacity);
        }

        [Test]
        public void OxenStand_Is3x3_Walkable_ClearsTrees_Holds1Ox()
        {
            FootprintTypeDef t = ModuleTypes.Find("module.oxen_stand");
            Assert.IsNotNull(t);
            Assert.AreSame(ModuleTypes.OxenStand, t);
            Assert.AreEqual(3000, t.WidthMm);
            Assert.AreEqual(3000, t.DepthMm);
            Assert.IsFalse(t.BlocksMovement);
            Assert.IsTrue(t.ClearsTrees);
            Assert.AreEqual(0, t.LogCapacity);
            Assert.AreEqual(1, t.OxCapacity);
        }

        [Test]
        public void ModuleLookup_UnknownOrNullId_ReturnsNull()
        {
            Assert.IsNull(ModuleTypes.Find("module.nope"));
            Assert.IsNull(ModuleTypes.Find(null));
            Assert.AreEqual(3, ModuleTypes.All.Count);
        }

        [Test]
        public void WoodcuttingOperation_HasSpecValues()
        {
            OperationTypeDef op = OperationTypes.Find("operation.woodcutting");
            Assert.IsNotNull(op);
            Assert.AreSame(OperationTypes.Woodcutting, op);
            Assert.AreEqual("area.woodcutting", op.BuildingAreaTypeId);
            Assert.AreEqual("area.work_woodcutting", op.WorkAreaTypeId);
            Assert.AreEqual("module.woodcutting_hut", op.HutTypeId);
            Assert.AreEqual(1, op.HutCount);
            Assert.AreEqual("module.log_storage", op.LogStorageTypeId);
            Assert.AreEqual(1, op.MinLogStorageCount);
            Assert.AreEqual("module.oxen_stand", op.OxenStandTypeId);
            Assert.AreEqual(1, op.MaxOxenStands);
            Assert.AreEqual(2, op.FamilySlots);
            Assert.AreEqual(420, op.DefaultWorkRadiusM);
        }

        [Test]
        public void WoodcuttingOperation_ReferencesOnlyDefinedAreaAndModuleTypes()
        {
            OperationTypeDef op = OperationTypes.Woodcutting;
            Assert.IsNotNull(AreaTypes.DefaultCatalog.Find(op.BuildingAreaTypeId));
            Assert.AreEqual(AreaKind.Building, AreaTypes.DefaultCatalog.Find(op.BuildingAreaTypeId).Kind);
            Assert.IsNotNull(AreaTypes.DefaultCatalog.Find(op.WorkAreaTypeId));
            Assert.AreEqual(AreaKind.Work, AreaTypes.DefaultCatalog.Find(op.WorkAreaTypeId).Kind);
            Assert.IsNotNull(ModuleTypes.Find(op.HutTypeId));
            Assert.IsNotNull(ModuleTypes.Find(op.LogStorageTypeId));
            Assert.IsNotNull(ModuleTypes.Find(op.OxenStandTypeId));
        }

        [Test]
        public void OperationLookup_UnknownOrNullId_ReturnsNull()
        {
            Assert.IsNull(OperationTypes.Find("operation.nope"));
            Assert.IsNull(OperationTypes.Find(null));
            Assert.AreEqual(1, OperationTypes.All.Count);
        }
    }
}
