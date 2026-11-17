using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Markets;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Markets
{
    /// <summary>Market panel report, house market access, NoRoomAtMarket and stock-trip task lines (T-469, MK-40..44).</summary>
    public class MarketReportTests
    {
        private const long Adult = -4_000_000;
        private static PointMm P(long x, long z) => new PointMm(x, z);
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private sealed class Fx
        {
            public SimWorld W; public int Granary, GranaryOp, Family;
            public LedgerLocation GranaryStore => W.Inventory.FindFoodStore(Granary);
        }

        // Flat map, started camp (family "Ash", 25 meat in the camp store), market area (10..30), built granary.
        private static Fx Make()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8, BushCount = 0, StartingMeat = 25, MarketRangeM = 50 });
            var layout = new StartLayout(P(140000, 140000), 0, P(0, 0), P(6000, 0), P(-6000, 0), new PointMm[0],
                new[] { new StartFamily("Ash", P(-8000, -4000), new[] { new StartCitizen("A", Sex.Male, Adult), new StartCitizen("B", Sex.Female, Adult) }) }, 2);
            StartLayoutApplier.Apply(w, layout);
            w.People.AddFamily(7, "Bee", 40, 40); // odd-jobbers who build the stall
            for (int i = 0; i < 2; i++) w.People.AddCitizen(70 + i, "B" + i, Sex.Male, Adult, 7, 40, 40);
            Ox ox = w.People.AddOx(50, "O", 40, 44);
            ox.State = OxState.Resting(40000, 44000);
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 80);
            w.Inventory.Produce(w.Inventory.AddGoodsStore(M(20, 22)), Items.Plank, 10); // stalls cost 2 planks (PL-10)
            Assert.IsTrue(w.Execute(new DesignateArea(AreaShape.Straight(new[] { M(10, 10), M(30, 10), M(30, 30), M(10, 30) }), AreaTypes.MarketId)).IsAccepted);
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(60, 60), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int fp = (int)r.Created[0];
            for (int i = 0; i < 6000 && !w.IsBuilt(fp); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(fp));
            var f = new Fx { W = w, Granary = fp, GranaryOp = w.Operations.FindByModule(fp).Id, Family = w.People.Families.First().Id };
            f.GranaryStore.Capacity = 500;
            return f;
        }

        private static void SupplyAndAssign(Fx f)
        {
            Assert.IsTrue(f.W.Execute(new SetGranarySupplies(f.GranaryOp, true)).IsAccepted);
            Assert.IsTrue(f.W.Execute(new AssignFamily(f.GranaryOp, f.Family)).IsAccepted);
        }

        private static int House(SimWorld w, int id, double x, double z) => w.Footprints.Add(id, ModuleTypes.House, M(x, z), 0).Id;

        [Test]
        public void Report_UnknownMarket_IsNull()
        {
            Fx f = Make();
            Assert.IsNull(MarketReport.Of(f.W, 99));
        }

        [Test]
        public void Report_NoStalls_NameSlotsAndZeroes()
        {
            Fx f = Make();
            MarketReport r = MarketReport.Of(f.W, f.W.Markets.Current.Id);
            Assert.AreEqual("Market " + f.W.Markets.Current.Id, r.Name);
            Assert.Greater(r.Slots, 0);
            Assert.AreEqual(0, r.StallsBuilt);
            Assert.AreEqual(0, r.TotalFood);
            Assert.AreEqual(3, r.Stock.Count);
            CollectionAssert.IsEmpty(r.Stalls);
            CollectionAssert.IsEmpty(r.SupplyingGranaryIds);
        }

        [Test]
        public void Report_FullPipeline_StaffedStallThenUnstaffed_ShowsFamilyFoodGranaryAndDeconstructReason()
        {
            Fx f = Make();
            SupplyAndAssign(f);
            f.W.Step(2);
            Market m = f.W.Markets.Current;
            int stall = m.StallFootprintIds[0];
            for (int i = 0; i < 6000 && !f.W.IsBuilt(stall); i++) f.W.Step(1);
            Assert.IsTrue(f.W.IsBuilt(stall));
            LedgerLocation store = f.W.Inventory.FindFoodStore(stall);
            f.W.Inventory.Produce(store, Items.Meat, 3);
            f.W.Inventory.Produce(store, Items.Berries, 2);
            f.W.LastAllotmentServed = 4;
            f.W.LastAllotmentShort = 1;

            MarketReport r = MarketReport.Of(f.W, m.Id);
            Assert.AreEqual(1, r.StallsBuilt);
            Assert.AreEqual(5, r.TotalFood);
            Assert.AreEqual(3, r.StockOf(Items.Meat.Id));
            Assert.AreEqual(2, r.StockOf(Items.Berries.Id));
            Assert.AreEqual(0, r.StockOf(Items.Mushrooms.Id));
            Assert.AreEqual(1, r.Stalls.Count);
            StallLine line = r.Stalls[0];
            Assert.AreEqual("Ash", line.FamilyText);
            Assert.AreEqual(5, line.Items);
            Assert.IsFalse(line.CanDeconstruct);
            StringAssert.Contains("unassign them first", line.DeconstructReason);
            CollectionAssert.AreEqual(new[] { f.Granary }, r.SupplyingGranaryIds);
            Assert.AreEqual(4, r.ServedLastSunday);
            Assert.AreEqual(1, r.ShortLastSunday);

            Assert.IsTrue(f.W.Execute(new UnassignFamily(f.GranaryOp, f.Family)).IsAccepted);
            f.W.Step(2);
            line = MarketReport.Of(f.W, m.Id).Stalls[0];
            Assert.AreEqual("Unstaffed", line.FamilyText);
            Assert.AreEqual(5, line.Items, "an unstaffed stall keeps its food");
            Assert.IsTrue(line.CanDeconstruct);
            Assert.AreEqual("", line.DeconstructReason);
        }

        [Test]
        public void Report_GranaryNotSupplying_IsNotListed()
        {
            Fx f = Make();
            CollectionAssert.IsEmpty(MarketReport.Of(f.W, f.W.Markets.Current.Id).SupplyingGranaryIds);
        }

        [Test]
        public void GranaryReport_SuppliesMarketLine_FollowsTheToggle()
        {
            Fx f = Make();
            Assert.IsFalse(GranaryReport.Of(f.W, f.Granary).SuppliesMarket);
            Assert.AreEqual("Supplies market: No", GranaryReport.Of(f.W, f.Granary).SuppliesMarketText);
            Assert.IsTrue(f.W.Execute(new SetGranarySupplies(f.GranaryOp, true)).IsAccepted);
            Assert.IsTrue(GranaryReport.Of(f.W, f.Granary).SuppliesMarket);
            Assert.AreEqual("Supplies market: Yes", GranaryReport.Of(f.W, f.Granary).SuppliesMarketText);
        }

        [Test]
        public void AccessText_HouseInMarketRange_NotNearCamp_IsYes()
        {
            Fx f = Make();
            int h = House(f.W, 900, 20, 45);
            Assert.AreEqual("Yes", MarketRange.AccessText(f.W, h));
        }

        [Test]
        public void AccessText_HouseBeyondMarketRange_IsNo()
        {
            Fx f = Make();
            int h = House(f.W, 900, 20, 95);
            Assert.AreEqual("No", MarketRange.AccessText(f.W, h));
        }

        [Test]
        public void AccessText_HouseNearCamp_BeforeHandover_IsNoCampSupplies_AfterHandover_IsNo()
        {
            Fx f = Make();
            int h = House(f.W, 900, 140, 155);
            Assert.AreEqual("No (camp supplies)", MarketRange.AccessText(f.W, h));
            f.W.FoodHandedOver = true;
            Assert.AreEqual("No", MarketRange.AccessText(f.W, h));
        }

        [Test]
        public void AccessText_NoMarket_NotAHouse_AndUnknownId_AreNo()
        {
            Fx f = Make();
            int h = House(f.W, 900, 20, 45);
            Assert.IsTrue(f.W.Markets.RemoveByArea(f.W.Markets.Current.AreaId));
            Assert.AreEqual("No", MarketRange.AccessText(f.W, h));
            Assert.AreEqual("No", MarketRange.AccessText(f.W, f.Granary), "a granary is not a house");
            Assert.AreEqual("No", MarketRange.AccessText(f.W, 12345));
        }

        [Test]
        public void Status_FamilyWithNoFreeSlot_IsNoRoomAtMarket_OtherwiseNotPaused()
        {
            Fx f = Make();
            SupplyAndAssign(f);
            Operation op = f.W.Operations.Get(f.GranaryOp);
            Assert.AreNotEqual(OperationState.NoRoomAtMarket, OperationStatus.Of(f.W, op));
            f.W.Markets.Current.AddNoRoom(f.Family);
            Assert.AreEqual(OperationState.NoRoomAtMarket, OperationStatus.Of(f.W, op));
            Assert.AreEqual(OperationState.NoRoomAtMarket, OperationStatus.Reported(f.W, op));
            f.W.Markets.Current.ClearNoRoom();
            Assert.AreNotEqual(OperationState.NoRoomAtMarket, OperationStatus.Of(f.W, op));
        }

        [Test]
        public void TaskLines_FullPipeline_CollectingFoodThenCarryingFoodToStall()
        {
            Fx f = Make();
            SupplyAndAssign(f);
            var seen = new HashSet<string>();
            var order = new List<string>();
            for (int i = 0; i < 20000 && !(seen.Contains("Carrying food to stall") && seen.Contains("Collecting food")); i++)
            {
                f.W.Step(1);
                foreach (Citizen c in f.W.People.Citizens)
                {
                    string t = CitizenTaskText.Task(f.W, c);
                    if ((t == "Collecting food" || t == "Carrying food to stall") && seen.Add(t)) order.Add(t);
                }
            }
            CollectionAssert.AreEqual(new[] { "Collecting food", "Carrying food to stall" }, order);
        }
    }
}
