using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Markets
{
    /// <summary>Camp food handover (T-464, CMP-20..22, MK-24 step 2, decision 0038).</summary>
    public class FoodHandoverTests
    {
        private const long Adult = -4_000_000;
        private static PointMm P(long x, long z) => new PointMm(x, z);
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private sealed class Fx
        {
            public SimWorld W; public MapData Map; public int Granary, GranaryOp;
            public LedgerLocation GranaryStore => W.Inventory.FindFoodStore(Granary);
        }

        // Flat map, a started camp (family 1 with two adults, 25 meat in the camp food store), a market area and a built
        // granary (placed and built through the real command and ticks). Supplying is not switched on yet.
        private static Fx Make(int campMeat = 25)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8, BushCount = 0, StartingMeat = campMeat });
            var layout = new StartLayout(P(140000, 140000), 0, P(0, 0), P(6000, 0), P(-6000, 0), new PointMm[0],
                new[] { new StartFamily("Ash", P(-8000, -4000), new[] { new StartCitizen("A", Sex.Male, Adult), new StartCitizen("B", Sex.Female, Adult) }) }, 2);
            StartLayoutApplier.Apply(w, layout);
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 80);
            Assert.IsTrue(w.Execute(new DesignateArea(AreaShape.Straight(new[] { M(10, 10), M(30, 10), M(30, 30), M(10, 30) }), AreaTypes.MarketId)).IsAccepted);
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(60, 60), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int fp = (int)r.Created[0];
            for (int i = 0; i < 6000 && !w.IsBuilt(fp); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(fp));
            var f = new Fx { W = w, Map = map, Granary = fp, GranaryOp = w.Operations.FindByModule(fp).Id };
            f.GranaryStore.Capacity = 500;
            return f;
        }

        private static void Supply(Fx f, bool on) => Assert.IsTrue(f.W.Execute(new SetGranarySupplies(f.GranaryOp, on)).IsAccepted);

        private static int CampFamily(Fx f) => f.W.People.Families.First().Id;

        [Test]
        public void Trigger_BuiltGranaryNotSupplying_FlagStaysClear()
        {
            Fx f = Make();
            f.W.Step(5);
            Assert.IsFalse(f.W.FoodHandedOver);
            Assert.IsNotNull(f.W.Camp.FoodStoreLocation);
        }

        [Test]
        public void Trigger_BuiltGranarySuppliesMarket_FlagSetWithoutWaitingForStallStock()
        {
            Fx f = Make();
            Supply(f, true);
            f.W.Step(1);
            Assert.IsTrue(f.W.FoodHandedOver, "set on the first tick, no stall built or stocked");
            Assert.AreEqual(0, f.W.Markets.Current.StallFootprintIds.Count(s => f.W.IsBuilt(s)), "no stall exists yet");
        }

        [Test]
        public void NoReversal_SwitchingSupplyOff_FlagStaysSet()
        {
            Fx f = Make();
            Supply(f, true);
            f.W.Step(1);
            Supply(f, false);
            f.W.Step(5);
            Assert.IsTrue(f.W.FoodHandedOver);
        }

        [Test]
        public void NoReversal_GranaryDeconstructionMarked_FlagStaysSet()
        {
            Fx f = Make();
            Supply(f, true);
            f.W.Step(1);
            f.W.Execute(new RemoveModule(f.Granary));
            f.W.Step(5);
            Assert.IsTrue(f.W.FoodHandedOver);
        }

        [Test]
        public void BeforeHandover_CampStoreIsNotACollectionSource()
        {
            Fx f = Make();
            Assert.IsNull(LordsLegacy.Sim.Markets.StoreFamilySystem.ChooseForagerSource(f.W, f.GranaryStore, out _));
            Assert.IsTrue(f.W.Execute(new AssignFamily(f.GranaryOp, CampFamily(f))).IsAccepted);
            f.W.Step(5);
            Assert.AreEqual(25, f.W.Camp.FoodStoreLocation.Total, "nothing was fetched");
        }

        [Test]
        public void EmptyingCampStore_FullPipeline_FamilyCarriesFoodToGranary_StoreRemoved_TotalsStayCorrect()
        {
            Fx f = Make();
            int rationsBefore = FoodStats.TotalRations(f.W);
            Supply(f, true);
            Assert.IsTrue(f.W.Execute(new AssignFamily(f.GranaryOp, CampFamily(f))).IsAccepted);
            for (int i = 0; i < 20000 && (f.W.Camp.FoodStoreLocation != null || f.GranaryStore.Total < 25); i++)
            {
                f.W.Step(1);
                Assert.AreEqual(rationsBefore, FoodStats.TotalRations(f.W), "top bar total constant at tick " + f.W.Tick);
            }
            Assert.IsNull(f.W.Camp.FoodStoreLocation, "emptied camp store is removed");
            Assert.AreEqual(25, f.GranaryStore.Total, "all 25 items now sit in the granary");
            Assert.AreEqual(25, FoodStats.CountsByType(f.W).Sum(c => c.Count));
            Assert.AreEqual("", f.W.Inventory.Audit() ?? "");
            Assert.IsTrue(f.W.FoodHandedOver);
        }

        [Test]
        public void EmptyCampStore_AfterHandover_RemovedAtOnce()
        {
            Fx f = Make(0);
            Supply(f, true);
            f.W.Step(1);
            Assert.IsNull(f.W.Camp.FoodStoreLocation);
        }

        [Test]
        public void SaveRoundTrip_FlagAndRemovedCampStore_SurviveAndSecondWriteIsIdentical()
        {
            Fx f = Make(0);
            Supply(f, true);
            f.W.Step(2);
            byte[] first = SimSave.Write(f.W, "s", 1, f.Map.MapId, f.Map.ContentHash);
            SimWorld back = SimSave.Read(first, f.Map);
            Assert.IsTrue(back.FoodHandedOver);
            Assert.IsNull(back.Camp.FoodStoreLocation);
            Assert.IsNull(SaveDiff.FirstDifference(first, SimSave.Write(back, "s", 1, f.Map.MapId, f.Map.ContentHash)));
        }

        [Test]
        public void SaveRoundTrip_FlagClearWithCampStore_StaysClear()
        {
            Fx f = Make();
            byte[] first = SimSave.Write(f.W, "s", 1, f.Map.MapId, f.Map.ContentHash);
            SimWorld back = SimSave.Read(first, f.Map);
            Assert.IsFalse(back.FoodHandedOver);
            Assert.AreEqual(25, back.Camp.FoodStoreLocation.Total);
        }

        [Test]
        public void SaveVersion_IsFifteen_VersionFourteenBytesAreRefused()
        {
            Assert.AreEqual(18, SaveWriter.CurrentVersion);
            byte[] bytes = { (byte)'L', (byte)'L', (byte)'S', (byte)'V', 14, 0, 0, 0, 0, 0, 0, 0 };
            var e = Assert.Throws<SaveFormatException>(() => SaveReader.ReadHeaderOnly(bytes));
            StringAssert.Contains("Unknown save format version 14", e.Message);
        }
    }
}
