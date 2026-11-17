using System.Collections.Generic;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Construction
{
    /// <summary>Build progress, consumption, refunds and saving over every recipe item (T-516, decision 0046).</summary>
    public class MultiItemConstructionTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);
        private static int _id = 900;

        private static readonly FootprintTypeDef TwoLogsTwoPlanks = new FootprintTypeDef("test.mixed", 3000, 2000, false, false,
            recipe: new ConstructionRecipe(new Dictionary<string, int> { { Items.Log.Id, 2 }, { Items.Plank.Id, 2 } }, 100));
        private static readonly FootprintTypeDef TwoPlanks = new FootprintTypeDef("test.planks", 3000, 2000, false, false,
            recipe: new ConstructionRecipe(new Dictionary<string, int> { { Items.Plank.Id, 2 } }, 60));

        private static (SimWorld w, MapData map) Make()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            return (SimWorld.FromMapData(map, new SimTuning { StartHour = 8, BushCount = 0 }), map);
        }

        private static (ConstructionSite site, LedgerLocation loc, ConstructionRecipe recipe) Site(SimWorld w, FootprintTypeDef t)
        {
            Footprint fp = w.Footprints.Add(_id++, t, M(50, 50), 0);
            LedgerLocation loc = w.Inventory.GetOrAddSite(fp.Id);
            ConstructionSite site = w.Construction.Add(new ConstructionSite(fp.Id, loc.Id));
            return (site, loc, t.ConstructionRecipe);
        }

        [Test]
        public void TwoLogsTwoPlanks_OnlyLogsDelivered_CapsAtHalfTheMinutes()
        {
            var (w, _) = Make();
            var (site, loc, recipe) = Site(w, TwoLogsTwoPlanks);
            w.Inventory.Produce(loc, Items.Log, 2);
            Assert.AreEqual(50, ConstructionProgress.Cap(w, site, recipe));
            int worked = 0;
            while (ConstructionProgress.Work(w, site, recipe)) worked++;
            Assert.AreEqual(50, worked);
            Assert.AreEqual(1, site.ConsumedOf(Items.Log.Id),"half the minutes consume 1 of 2 logs");
            Assert.AreEqual(0, site.ConsumedOf(Items.Plank.Id));
            Assert.IsNotNull(w.Construction.Get(site.FootprintId), "not finished without planks");
        }

        [Test]
        public void TwoLogsTwoPlanks_AllDelivered_ConsumesEachItemAndFinishes()
        {
            var (w, _) = Make();
            var (site, loc, recipe) = Site(w, TwoLogsTwoPlanks);
            w.Inventory.Produce(loc, Items.Log, 2);
            w.Inventory.Produce(loc, Items.Plank, 2);
            Assert.AreEqual(100, ConstructionProgress.Cap(w, site, recipe));
            for (int i = 0; i < 100; i++) Assert.IsTrue(ConstructionProgress.Work(w, site, recipe));
            Assert.IsNull(w.Construction.Get(site.FootprintId));
            Assert.AreEqual(0, loc.CountOf(Items.Plank.Id));
            Assert.AreEqual(2, w.Inventory.ConsumedOf(Items.Plank.Id));
        }

        [Test]
        public void TwoLogsOnePlankDelivered_CapIsThreeQuartersOfTheMinutes()
        {
            var (w, _) = Make();
            var (site, loc, recipe) = Site(w, TwoLogsTwoPlanks);
            w.Inventory.Produce(loc, Items.Log, 2);
            w.Inventory.Produce(loc, Items.Plank, 1);
            Assert.AreEqual(75, ConstructionProgress.Cap(w, site, recipe));
        }

        [Test]
        public void DeconstructingBuiltTwoPlankStall_DropsOnePlankOnTheGround()
        {
            var (w, _) = Make();
            Footprint fp = w.Footprints.Add(_id++, TwoPlanks, M(50, 50), 0);
            w.Deconstruction.Add(new DeconstructionEntry(fp.Id));
            DeconstructionWork.Finish(w, fp.Id);
            LedgerLocation pile = w.Inventory.GetOrAddGroundPile(fp.Centre, null);
            Assert.AreEqual(1, pile.CountOf(Items.Plank.Id));
            Assert.AreEqual(0, pile.CountOf(Items.Log.Id));
            Assert.IsNull(w.Footprints.Get(fp.Id));
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void ConsumedCountsPerItem_SurviveSaveRoundTrip()
        {
            var (w, map) = Make();
            var (site, _, _) = Site(w, ModuleTypes.WoodcuttingHut);
            site.SetConsumed(Items.Log.Id, 3);
            site.SetConsumed(Items.Plank.Id, 2);
            byte[] bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(bytes, map);
            ConstructionSite b = back.Construction.Get(site.FootprintId);
            Assert.AreEqual(3, b.ConsumedOf(Items.Log.Id));
            Assert.AreEqual(2, b.ConsumedOf(Items.Plank.Id));
            Assert.AreEqual(bytes, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash));
            Assert.GreaterOrEqual(SaveWriter.CurrentVersion, 26);
        }
    }
}
