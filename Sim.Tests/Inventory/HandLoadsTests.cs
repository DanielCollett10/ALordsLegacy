using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Inventory
{
    [TestFixture]
    public class HandLoadsTests
    {
        [Test]
        public void Plank_DefaultsToHandCarryPlanks_TwoPerTrip()
        {
            var t = new SimTuning();
            Assert.AreEqual(2, HandLoads.For(Items.Plank, t));
            Assert.AreEqual(t.HandCarryPlanks, HandLoads.For(Items.Plank, t));
        }

        [Test]
        public void Plank_TunedHandCarryPlanks_IsUsed()
        {
            Assert.AreEqual(5, HandLoads.For(Items.Plank, new SimTuning { HandCarryPlanks = 5 }));
        }

        [Test]
        public void Beam_HasNoHandLoad_OxOnly()
        {
            Assert.IsNull(HandLoads.For(Items.Beam, new SimTuning()));
        }

        [Test]
        public void UnknownGeneralItem_DefaultsToHandCarryItems()
        {
            var rod = new ItemDef("item.rod", ItemCategory.General);
            Assert.AreEqual(10, HandLoads.For(rod, new SimTuning()));
        }

        [Test]
        public void UnknownGeneralItem_TunedHandCarryItems_IsUsed()
        {
            var rod = new ItemDef("item.rod", ItemCategory.General);
            Assert.AreEqual(4, HandLoads.For(rod, new SimTuning { HandCarryItems = 4 }));
        }

        [Test]
        public void Log_And_Food_HaveNoHandLoadHere()
        {
            var t = new SimTuning();
            Assert.IsNull(HandLoads.For(Items.Log, t), "logs go by ox");
            Assert.IsNull(HandLoads.For(Items.Meat, t), "food is not a General good");
        }

        [Test]
        public void NullItemOrTuning_GivesNull()
        {
            Assert.IsNull(HandLoads.For(null, new SimTuning()));
            Assert.IsNull(HandLoads.For(Items.Plank, null));
        }
    }

    [TestFixture]
    public class StorageYardTuningDefaultsTests
    {
        [Test]
        public void StorageYardDefaults_MatchDesignNumbers()
        {
            var t = new SimTuning();
            Assert.AreEqual(8.0, t.StorageYardWidthM, 1e-9, "ST-03 width");
            Assert.AreEqual(6.0, t.StorageYardDepthM, 1e-9, "ST-03 depth");
            Assert.AreEqual(8, t.StorageYardLogs, "ST-03 logs");
            Assert.AreEqual(480, t.StorageYardMinutes, "ST-03 build minutes");
            Assert.AreEqual(300, t.StorageYardCapacity, "ST-03 capacity");
            Assert.AreEqual(2, t.StorageYardFamilySlots, "ST-43 family slots");
            Assert.AreEqual(0.0, t.YardCollectThreshold, 1e-9, "ST-42 collect threshold: any free output");
        }
    }
}
