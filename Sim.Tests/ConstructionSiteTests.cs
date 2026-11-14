using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class ConstructionSiteTests
    {
        private static SimWorld NewWorld() => new SimWorld(new SimTuning());

        [Test]
        public void Hut_Recipe_Is8Logs960Minutes()
        {
            var r = ModuleTypes.WoodcuttingHut.ConstructionRecipe;
            Assert.AreEqual(8, r.Logs);
            Assert.AreEqual(960, r.BuildWorkerMinutes);
            Assert.AreEqual(8, r.Items[Items.Log.Id]);
        }

        [Test]
        public void OxenStand_Recipe_Is1Log60Minutes()
        {
            var r = ModuleTypes.OxenStand.ConstructionRecipe;
            Assert.AreEqual(1, r.Logs);
            Assert.AreEqual(60, r.BuildWorkerMinutes);
        }

        [Test]
        public void LogStorage_Recipe_IsEmpty()
        {
            Assert.IsTrue(ModuleTypes.LogStorage.ConstructionRecipe.IsEmpty);
            Assert.AreEqual(0, ModuleTypes.LogStorage.ConstructionRecipe.Items.Count);
        }

        [Test]
        public void Camp_Recipe_IsEmpty()
        {
            Assert.IsTrue(FootprintTypeDef.Camp.ConstructionRecipe.IsEmpty);
        }

        [Test]
        public void Recipe_NegativeValues_Throw()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new ConstructionRecipe(-1, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new ConstructionRecipe(0, -1));
        }

        [Test]
        public void MaxBuildersPerSite_DefaultsTo4()
        {
            Assert.AreEqual(4, new SimTuning().MaxBuildersPerSite);
        }

        [Test]
        public void IsBuilt_NoRecord_IsTrue_AndUnknownIdIsTrue()
        {
            var w = NewWorld();
            Assert.IsTrue(w.IsBuilt(1));
            Assert.IsTrue(w.IsBuilt(999));
        }

        [Test]
        public void IsBuilt_WithSite_IsFalse_UntilRemoved()
        {
            var w = NewWorld();
            var loc = w.Inventory.GetOrAddSite(7);
            var site = w.Construction.Add(new ConstructionSite(7, loc.Id));
            Assert.IsFalse(w.IsBuilt(7));
            Assert.IsTrue(w.IsBuilt(8));
            Assert.AreEqual(ConstructionState.Delivering, site.State);
            Assert.AreEqual(ConstructionPriority.Normal, site.Priority);
            Assert.AreSame(site, w.Construction.Get(7));
            Assert.IsTrue(w.Construction.Remove(7));
            Assert.IsTrue(w.IsBuilt(7));
            Assert.IsFalse(w.Construction.Remove(7));
        }

        [Test]
        public void Construction_DuplicateSite_Throws()
        {
            var w = NewWorld();
            w.Construction.Add(new ConstructionSite(3, 1));
            Assert.Throws<System.InvalidOperationException>(() => w.Construction.Add(new ConstructionSite(3, 2)));
        }

        [Test]
        public void SiteLocation_IsReusedPerFootprint_AndKeyedByFootprintId()
        {
            var w = NewWorld();
            var a = w.Inventory.GetOrAddSite(5);
            Assert.AreEqual(LocationKind.Site, a.Kind);
            Assert.AreEqual(5, a.ModuleId);
            Assert.AreSame(a, w.Inventory.GetOrAddSite(5));
            Assert.AreSame(a, w.Inventory.FindSite(5));
            Assert.IsNull(w.Inventory.FindSite(6));
            Assert.AreNotSame(a, w.Inventory.GetOrAddSite(6));
        }

        [Test]
        public void SiteLocation_HoldsLogs_AndAuditCoversIt()
        {
            var w = NewWorld();
            var site = w.Inventory.GetOrAddSite(5);
            w.Inventory.Produce(site, Items.Log, 3);
            Assert.AreEqual(3, w.Inventory.TotalOf(Items.Log));
            Assert.IsNull(w.Inventory.Audit());
            // A count slipped in behind the ledger's back must be caught.
            site.Counts[Items.Log.Id] = 4;
            Assert.IsNotNull(w.Inventory.Audit());
        }

        [Test]
        public void SiteLocation_NonPositiveCount_FailsAudit()
        {
            var w = NewWorld();
            var site = w.Inventory.GetOrAddSite(5);
            site.Counts[Items.Log.Id] = 0;
            Assert.IsNotNull(w.Inventory.Audit());
        }
    }
}
