using System.Linq;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Markets;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Markets
{
    /// <summary>task.stock_stall: creation, steps, reservations, failure and save (T-461, MK-24, MK-25, MK-27).</summary>
    public class StoreFamilySystemTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private sealed class Fixture
        {
            public SimWorld W; public MapData Map; public int Granary, GranaryOp, Stall;
            public LedgerLocation GranaryStore => W.Inventory.FindFoodStore(Granary);
            public LedgerLocation StallStore => W.Inventory.FindFoodStore(Stall);
            public TaskRecord Task => W.Tasks.All.FirstOrDefault(t => t.Def.Id == StoreFamilySystem.StockStallTaskId && !t.IsFinished);
        }

        private static Fixture Make(int fedMeat, int fedBerries, Weekday start = Weekday.Monday)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8, BushCount = 0, StartWeekday = start });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 40);
            w.People.AddFamily(2, "F2", 40, 40);
            for (int i = 0; i < 2; i++) w.People.AddCitizen(20 + i, "C" + i, Sex.Male, AdultBirth, 2, 40, 40);
            w.People.AddFamily(3, "F3", 40, 40); // unassigned helpers: they build the granary and the stall
            for (int i = 0; i < 2; i++) w.People.AddCitizen(30 + i, "H" + i, Sex.Male, AdultBirth, 3, 40, 40);
            Ox ox = w.People.AddOx(50, "O", 40, 44);
            ox.State = OxState.Resting(40000, 44000);
            var shape = AreaShape.Straight(new[] { M(10, 10), M(30, 10), M(30, 30), M(10, 30) });
            Assert.IsTrue(w.Execute(new DesignateArea(shape, AreaTypes.MarketId)).IsAccepted);
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(60, 60), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int fp = (int)r.Created[0];
            for (int i = 0; i < 6000 && !w.IsBuilt(fp); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(fp));
            int op = w.Operations.FindByModule(fp).Id;
            Assert.IsTrue(w.Execute(new SetGranarySupplies(op, true)).IsAccepted);
            var f = new Fixture { W = w, Map = map, Granary = fp, GranaryOp = op };
            f.GranaryStore.Capacity = 500;
            if (fedMeat > 0) w.Inventory.Produce(f.GranaryStore, Items.Meat, fedMeat);
            if (fedBerries > 0) w.Inventory.Produce(f.GranaryStore, Items.Berries, fedBerries);
            return f;
        }

        // Assigns the family and waits (ticks only) until its stall is built; no store task may start before.
        private static void StaffAndBuildStall(Fixture f)
        {
            Assert.IsTrue(f.W.Execute(new AssignFamily(f.GranaryOp, 2)).IsAccepted);
            f.W.Step(2);
            f.Stall = f.W.Markets.Current.StallOf(2);
            Assert.AreNotEqual(0, f.Stall);
            for (int i = 0; i < 6000 && !f.W.IsBuilt(f.Stall); i++) f.W.Step(1);
            Assert.IsTrue(f.W.IsBuilt(f.Stall));
        }

        private static void ClearTasks(Fixture f)
        {
            foreach (TaskRecord t in f.W.Tasks.All.ToList())
                if (t.Def.Id == StoreFamilySystem.StockStallTaskId && !t.IsFinished) f.W.Tasks.Cancel(t.Id);
        }

        private static TaskRecord OpenTask(Fixture f)
        {
            // Dispatch may already have taken the task: any live stock task counts.
            f.W.Step(1);
            return f.Task;
        }

        [Test]
        public void StallWithRoomAndGranaryWithFood_OpensStockTask_WithTheSixSteps()
        {
            Fixture f = Make(20, 20);
            StaffAndBuildStall(f);
            TaskRecord t = OpenTask(f);
            Assert.IsNotNull(t);
            Assert.AreEqual(f.GranaryOp, t.OwnerOperationId);
            var kinds = t.Def.Steps.Select(s => s.Kind).ToArray();
            Assert.AreEqual(new[] { StepKind.WalkTo, StepKind.Work, StepKind.CollectItems, StepKind.WalkTo, StepKind.Work, StepKind.DepositItems }, kinds);
            Assert.AreEqual(10, t.Def.Steps[1].DurationTicks, "load is 10 minutes");
            Assert.AreEqual(10, t.Def.Steps[4].DurationTicks, "unload is 10 minutes");
            Assert.AreEqual(10, t.Def.Steps[2].Count, "up to HandCarryItems");
            Assert.AreEqual(f.GranaryStore.Id, t.Def.Steps[2].LocationId);
            Assert.AreEqual(f.StallStore.Id, t.Def.Steps[5].LocationId);
        }

        [Test]
        public void GranaryWithoutFood_OpensNoTask()
        {
            Fixture f = Make(0, 0);
            StaffAndBuildStall(f);
            f.W.Step(3);
            Assert.IsNull(f.Task);
        }

        [Test]
        public void GranaryNotSupplyingTheMarket_OpensNoTask()
        {
            Fixture f = Make(20, 20);
            StaffAndBuildStall(f);
            f.W.Step(1);
            foreach (TaskRecord t in f.W.Tasks.All.ToList()) f.W.Tasks.Cancel(t.Id);
            Assert.IsTrue(f.W.Execute(new SetGranarySupplies(f.GranaryOp, false)).IsAccepted);
            f.W.Step(3);
            Assert.IsNull(f.Task);
        }

        [Test]
        public void StallWithOnlyThreeFreeSlots_PlansThreeItems()
        {
            Fixture f = Make(20, 20);
            StaffAndBuildStall(f);
            ClearTasks(f);
            f.W.Inventory.Produce(f.StallStore, Items.Meat, f.StallStore.Capacity - 3);
            TaskRecord t = OpenTask(f);
            Assert.IsNotNull(t);
            Assert.AreEqual(3, t.Def.Steps[2].Count, "min(10, stall room)");
        }

        [Test]
        public void FullStall_OpensNoTask()
        {
            Fixture f = Make(20, 20);
            StaffAndBuildStall(f);
            ClearTasks(f);
            f.W.Inventory.Produce(f.StallStore, Items.Meat, f.StallStore.Capacity);
            f.W.Step(3);
            Assert.IsNull(f.Task);
        }

        [Test]
        public void Proportions_SplitInProportionToStock_AndSumToTheRequest()
        {
            Fixture f = Make(30, 10);
            var share = StoreFamilySystem.Proportions(f.GranaryStore, 10);
            Assert.AreEqual(10, share.Sum(kv => kv.Value));
            Assert.AreEqual(8, share.First(kv => kv.Key == Items.Meat).Value, "75% meat of 10 rounds by largest remainder: 7.5 -> 8");
            Assert.AreEqual(2, share.First(kv => kv.Key == Items.Berries).Value);
            Assert.AreEqual(0, StoreFamilySystem.Proportions(f.GranaryStore, 0).Count);
            Assert.AreEqual(40, StoreFamilySystem.Proportions(f.GranaryStore, 99).Sum(kv => kv.Value), "never more than the stock");
        }

        [Test]
        public void FullPipeline_ThroughCommandsAndTicks_MovesTenItemsGranaryToStall_InProportion()
        {
            Fixture f = Make(30, 10);
            StaffAndBuildStall(f);
            int total = 40;
            for (int i = 0; i < 3000 && f.StallStore.Total == 0; i++) f.W.Step(1);
            // The fixture's family is homeless with no camp, so it dosses at the market (CMP-25) and may finish more
            // than one load before the loop notices: whole loads of ten only.
            Assert.GreaterOrEqual(f.StallStore.Total, 10, "ten items reached the stall");
            Assert.AreEqual(0, f.StallStore.Total % 10, "whole loads");
            Assert.AreEqual(total - f.GranaryStore.Total, f.StallStore.Total + CarrierTotal(f.W), "ledger conserved");
            Assert.Greater(f.StallStore.CountOf(Items.Meat.Id), f.StallStore.CountOf(Items.Berries.Id), "meat-heavy stock gives a meat-heavy load");
            Assert.AreEqual("", f.W.Inventory.Audit() ?? "");
        }

        private static int CarrierTotal(SimWorld w) => w.Inventory.Locations.Where(l => l.Kind == LocationKind.Carrier).Sum(l => l.Total);

        [Test]
        public void CollectNeverTakesFoodReservedForAnAllotment()
        {
            Fixture f = Make(10, 0);
            StaffAndBuildStall(f);
            int allot = AllotmentSystem(f, 7);
            Assert.AreNotEqual(0, allot);
            for (int i = 0; i < 3000 && f.StallStore.Total == 0 && f.Task != null; i++) f.W.Step(1);
            Assert.AreEqual(3, f.StallStore.Total, "only the 3 unreserved items move (MK-27)");
            Assert.AreEqual(7, f.GranaryStore.Total);
            Assert.AreEqual(7, f.GranaryStore.ReservedOf(Items.Meat.Id), "the allotment is untouched");
        }

        private static int AllotmentSystem(Fixture f, int n)
        {
            return f.W.Inventory.ReserveStock(f.GranaryStore, Items.Meat, n, LordsLegacy.Sim.Food.AllotmentSystem.OwnerFor(2));
        }

        [Test]
        public void LoadingReservesStock_ThenTransferConsumesIt()
        {
            Fixture f = Make(10, 0);
            StaffAndBuildStall(f);
            bool sawReservation = false;
            for (int i = 0; i < 3000 && f.StallStore.Total == 0; i++)
            {
                f.W.Step(1);
                TaskRecord t = f.Task;
                if (t != null && t.CarrierId.HasValue && f.W.Inventory.Get(t.CarrierId.Value).Total > 0)
                {
                    sawReservation |= f.W.Inventory.Reservations.Any(r => r.Owner == StoreFamilySystem.OwnerFor(t.Id) && r.Kind == ReservationKind.Capacity);
                    Assert.AreEqual(0, f.GranaryStore.ReservedOf(Items.Meat.Id), "stock reservation consumed by the transfer");
                }
            }
            Assert.IsTrue(sawReservation, "room at the stall is reserved while the carrier walks");
            Assert.IsFalse(f.W.Inventory.Reservations.Any(r => r.Owner <= -1_000_000), "all stock-trip reservations are gone at the end");
        }

        [Test]
        public void StallGone_MidWalk_CarriedFoodDropsOnTheGround_AndReservationsAreFreed()
        {
            Fixture f = Make(10, 0);
            StaffAndBuildStall(f);
            TaskRecord t = null;
            for (int i = 0; i < 3000; i++)
            {
                f.W.Step(1);
                t = f.Task;
                if (t != null && t.CarrierId.HasValue) break;
            }
            Assert.IsNotNull(t);
            Assert.AreEqual(10, f.W.Inventory.Get(t.CarrierId.Value).Total);
            int id = t.Id;
            f.W.Tasks.Cancel(id);
            f.W.Step(1);
            Assert.AreEqual(0, f.W.Inventory.Get(t.CarrierId.Value).Total, "carrier emptied");
            Assert.AreEqual(10, f.W.Inventory.Locations.Where(l => l.Kind == LocationKind.GroundPile).Sum(l => l.Total));
            Assert.IsFalse(f.W.Inventory.Reservations.Any(r => r.Owner == StoreFamilySystem.OwnerFor(id)));
        }

        [Test]
        public void Save_CarrierMidWalkWithFood_RoundTrips_AndTheTripFinishesAfterLoad()
        {
            Fixture f = Make(10, 10);
            StaffAndBuildStall(f);
            TaskRecord t = null;
            for (int i = 0; i < 3000; i++)
            {
                f.W.Step(1);
                t = f.Task;
                if (t != null && t.CarrierId.HasValue && t.CurrentStep == 3) break;
            }
            Assert.IsNotNull(t);
            Assert.AreEqual(3, t.CurrentStep, "mid-walk to the stall");
            byte[] bytes = SimSave.Write(f.W, "s", 1, f.Map.MapId, f.Map.ContentHash);
            SimWorld back = SimSave.Read(bytes, f.Map);
            Assert.AreEqual(bytes, SimSave.Write(back, "s", 1, f.Map.MapId, f.Map.ContentHash), "second write is identical");
            TaskRecord bt = back.Tasks.Get(t.Id);
            Assert.AreEqual(StoreFamilySystem.StockStallTaskId, bt.Def.Id);
            Assert.AreEqual(3, bt.CurrentStep);
            Assert.AreEqual(10, back.Inventory.Get(bt.CarrierId.Value).Total, "food on the carrier is real ledger stock");
            LedgerLocation stall = back.Inventory.FindFoodStore(f.Stall);
            int before = stall.Total;
            for (int i = 0; i < 3000 && back.Tasks.Get(t.Id).State != TaskState.Done; i++) back.Step(1);
            Assert.AreEqual(TaskState.Done, back.Tasks.Get(t.Id).State);
            Assert.GreaterOrEqual(stall.Total, before + 10, "the loaded task delivered its 10 (the other worker's trip may add more)");
            Assert.AreEqual(0, back.Inventory.Get(bt.CarrierId.Value).Total, "carrier empty after unloading");
        }

        private const long NineBirth = -2_500_000; // about 9.5 game years
        private const long SevenBirth = -1_900_000; // about 7 game years

        [Test]
        public void TwoDayRun_SaturdayAndSunday_StoreFamilyStocksTheStallBothDays_AndOthersNeverTakeStockTasks()
        {
            Fixture f = Make(300, 0, Weekday.Saturday);
            f.W.People.AddCitizen(24, "Kid", Sex.Male, NineBirth, 2, 40, 40);
            f.W.People.AddCitizen(25, "Small", Sex.Male, SevenBirth, 2, 40, 40);
            StaffAndBuildStall(f);
            long dayStart = LordsLegacy.Sim.People.WorkDay.DayStart(f.W);
            int saturdayTotal = -1;
            bool kidWorked = false, smallWorked = false;
            while (f.W.Tick < dayStart + 2 * 1440)
            {
                f.W.Step(1);
                if (f.W.Tick == dayStart + 1440)
                {
                    saturdayTotal = f.StallStore.Total;
                    f.W.Inventory.Consume(f.StallStore, Items.Meat, f.StallStore.CountOf(Items.Meat.Id)); // shoppers empty the stall overnight
                }
                foreach (TaskRecord t in f.W.Tasks.All)
                {
                    if (t.Def.Id != StoreFamilySystem.StockStallTaskId || !t.AssigneeId.HasValue) continue;
                    int who = t.AssigneeId.Value;
                    Assert.AreEqual(2, f.W.People.FindCitizen(who).FamilyId, "only the store family takes stock tasks");
                    kidWorked |= who == 24; smallWorked |= who == 25;
                }
            }
            Assert.Greater(saturdayTotal, 0, "stock moved on Saturday");
            Assert.Greater(f.StallStore.Total, 0, "the emptied stall was restocked on Sunday (MK-26)");
            Assert.IsTrue(kidWorked, "a 9 year old takes stock tasks (MK-24, light work)");
            Assert.IsFalse(smallWorked, "a 7 year old does not");
            Assert.AreEqual("", f.W.Inventory.Audit() ?? "");
        }

        [Test]
        public void SundayRun_NonStoreFamily_NeverTakesAStockTask()
        {
            Fixture f = Make(300, 0, Weekday.Sunday);
            StaffAndBuildStall(f);
            for (int i = 0; i < 600; i++)
            {
                f.W.Step(1);
                foreach (TaskRecord t in f.W.Tasks.All)
                    if (t.Def.Id == StoreFamilySystem.StockStallTaskId && t.AssigneeId.HasValue)
                        Assert.AreNotEqual(3, f.W.People.FindCitizen(t.AssigneeId.Value).FamilyId, "helper family never carries stock");
            }
            Assert.Greater(f.StallStore.Total, 0, "the store family still works on Sunday");
        }

        [Test]
        public void CatalogKnowsTheTaskDef()
        {
            Assert.IsTrue(TaskDefCatalog.TryGet(StoreFamilySystem.StockStallTaskId, out var e, out var r));
            Assert.AreEqual(EligibilityPolicy.AssignedFamilyOnly, e);
            Assert.AreEqual(RequiredResource.None, r);
        }
    }
}
