using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Foraging;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Kit;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Markets;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Kit
{
    /// <summary>T-565, decision 0052, MAT-50..54, ST-25: a borrowed kit basket doubles the forage and granary carry and is released when the task ends.</summary>
    public class KitLoanTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static MapData Map() =>
            new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);

        private static int Loans(SimWorld w) => w.Inventory.Reservations.Count(r => r.Owner <= -3_000_001 && r.Owner > -4_000_000);

        // ---- forager hut ----

        private sealed class Forage { public SimWorld W; public LedgerLocation Kit, Store; public Operation Op; }

        private static Forage MakeForage(int foragers, int baskets, int bushes)
        {
            var w = SimWorld.FromMapData(Map(), new SimTuning { StartHour = 8, BushCount = 0 });
            CommandResult r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) }), AreaTypes.ForagingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            var f = new Forage { W = w, Op = w.Operations.FindByArea((int)r.Created[0]) };
            Footprint hut = w.Footprints.Add(1, ModuleTypes.ForagerHut, M(100, 100), 0);
            w.Operations.AddModule(f.Op.Id, 1);
            f.Store = w.Inventory.AddFoodStore(w.Tuning.ForagerHutCapacity, 1);
            KitStores.OnBuilt(w, hut);
            f.Kit = KitStores.Find(w, 1);
            if (baskets > 0) w.Inventory.Produce(f.Kit, Items.Basket, baskets);
            w.People.AddFamily(1, "F", 100, 100);
            for (int i = 0; i < foragers; i++) w.People.AddCitizen(10 + i, "C" + i, Sex.Male, AdultBirth, 1, 100, 100);
            Assert.IsTrue(w.Execute(new AssignFamily(f.Op.Id, 1)).IsAccepted);
            for (int i = 0; i < bushes; i++) w.Bushes.Add(105 + 4 * i, 95, BushKind.Berries, 25);
            return f;
        }

        private static int[] PlannedBaskets(SimWorld w) =>
            w.Tasks.All.Where(t => t.Def.Id == ForagingSystem.ForageTaskId)
                .Select(t => t.Def.Steps.Single(s => s.Kind == StepKind.DepositForage).Count).OrderBy(n => n).ToArray();

        [Test]
        public void Forage_WithKitBasket_PlansDoubleLoad_AndReservesTheBasket()
        {
            Forage f = MakeForage(1, 1, 1);
            f.W.Step(1);
            Assert.AreEqual(new[] { 20 }, PlannedBaskets(f.W), "min(bush 25, 2 x 10, room 30, cap 20)");
            Assert.AreEqual(1, f.Kit.CountOf(Items.Basket.Id), "the basket never leaves the kit");
            Assert.AreEqual(0, f.Kit.AvailableOf(Items.Basket.Id), "but is reserved");
            Assert.AreEqual(1, Loans(f.W));
            Assert.IsNull(f.W.Inventory.Audit());
        }

        [Test]
        public void Forage_WithoutKitBasket_PlansTheNormalLoad()
        {
            Forage f = MakeForage(1, 0, 1);
            f.W.Step(1);
            Assert.AreEqual(new[] { 10 }, PlannedBaskets(f.W));
            Assert.AreEqual(0, Loans(f.W));
        }

        [Test]
        public void Forage_MoreTripsThanBaskets_TheRestCarryTheNormalLoad()
        {
            Forage f = MakeForage(2, 1, 2); // hut store room 30: the 20 trip leaves 10 for the other
            f.W.Step(1);
            Assert.AreEqual(new[] { 10, 20 }, PlannedBaskets(f.W));
            Assert.AreEqual(1, Loans(f.W));
        }

        [Test]
        public void Forage_FullPipeline_DoubleLoadReachesTheHut_AndLoanIsReleasedWhenTaskEnds()
        {
            Forage f = MakeForage(1, 1, 1);
            for (int i = 0; i < 3000 && f.Store.Total < 20; i++) f.W.Step(1);
            Assert.AreEqual(20, f.Store.CountOf(Items.Berries.Id), "20 picked and delivered in one trip");
            TaskRecord first = f.W.Tasks.All.First(t => t.Def.Id == ForagingSystem.ForageTaskId);
            for (int i = 0; i < 100 && !first.IsFinished; i++) f.W.Step(1);
            f.W.Step(3);
            Assert.IsTrue(first.IsFinished);
            Assert.IsFalse(KitLoans.Holds(f.W, first.Id), "released after the task is done");
            Assert.AreEqual(1, f.Kit.CountOf(Items.Basket.Id));
            Assert.IsNull(f.W.Inventory.Audit());
        }

        [Test]
        public void Forage_CancelledTask_ReleasesTheLoan()
        {
            Forage f = MakeForage(1, 1, 1);
            f.W.Step(1);
            TaskRecord t = f.W.Tasks.All.Single(x => x.Def.Id == ForagingSystem.ForageTaskId);
            f.W.Tasks.Cancel(t.Id);
            f.W.Step(1);
            Assert.AreEqual(1, f.Kit.CountOf(Items.Basket.Id));
            Assert.IsTrue(f.Kit.AvailableOf(Items.Basket.Id) == 1 || Loans(f.W) == 1, "a new trip may borrow it again, never two loans");
            Assert.IsFalse(f.W.Inventory.Reservations.Any(r => r.Owner == KitLoans.OwnerFor(t.Id)), "the cancelled task's loan is gone");
        }

        // ---- granary ----

        private sealed class Gran { public SimWorld W; public int Granary, Op; public LedgerLocation Store; public LedgerLocation Kit; }

        private static Gran MakeGranary(int baskets, int workers)
        {
            var w = SimWorld.FromMapData(Map(), new SimTuning { StartHour = 8, BushCount = 0, StartWeekday = Weekday.Monday });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 40);
            w.Inventory.Produce(w.Inventory.AddGoodsStore(M(20, 22)), Items.Plank, 10);
            w.People.AddFamily(2, "F2", 40, 40);
            for (int i = 0; i < workers; i++) w.People.AddCitizen(20 + i, "C" + i, Sex.Male, AdultBirth, 2, 40, 40);
            w.People.AddFamily(3, "F3", 40, 40);
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
            var g = new Gran { W = w, Granary = fp, Op = op, Store = w.Inventory.FindFoodStore(fp), Kit = KitStores.Find(w, fp) };
            g.Store.Capacity = 500;
            w.Inventory.Produce(g.Store, Items.Meat, 50);
            w.Inventory.Produce(g.Store, Items.Berries, 50);
            if (baskets > 0) w.Inventory.Produce(g.Kit, Items.Basket, baskets);
            Assert.IsTrue(w.Execute(new AssignFamily(op, 2)).IsAccepted);
            w.Step(2);
            int stall = w.Markets.Current.StallOf(2);
            for (int i = 0; i < 6000 && !w.IsBuilt(stall); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(stall));
            w.Inventory.FindFoodStore(stall).Capacity = 500;
            return g;
        }

        private static int[] StockLoads(SimWorld w) =>
            w.Tasks.All.Where(t => t.Def.Id == StoreFamilySystem.StockStallTaskId && !t.IsFinished)
                .Select(t => t.Def.Steps.Single(s => s.Kind == StepKind.CollectItems).Count).OrderBy(n => n).ToArray();

        [Test]
        public void Granary_WithKitBasket_PlansDoubleLoad_AndReservesTheBasket()
        {
            Gran g = MakeGranary(2, 1);
            g.W.Step(1);
            Assert.AreEqual(new[] { 20 }, StockLoads(g.W));
            Assert.AreEqual(2, g.Kit.CountOf(Items.Basket.Id));
            Assert.AreEqual(1, g.Kit.AvailableOf(Items.Basket.Id));
            Assert.AreEqual(1, Loans(g.W));
        }

        [Test]
        public void Granary_WithoutKitBasket_PlansTheNormalLoad()
        {
            Gran g = MakeGranary(0, 1);
            g.W.Step(1);
            Assert.AreEqual(new[] { 10 }, StockLoads(g.W));
            Assert.AreEqual(0, Loans(g.W));
        }

        [Test]
        public void Granary_MoreTripsThanBaskets_TheRestCarryTheNormalLoad()
        {
            Gran g = MakeGranary(1, 2);
            g.W.Step(1);
            Assert.AreEqual(new[] { 10, 20 }, StockLoads(g.W));
            Assert.AreEqual(1, Loans(g.W));
        }

        [Test]
        public void Granary_FullPipeline_StallGets20_AndLoanReleasedWhenTaskEnds()
        {
            Gran g = MakeGranary(1, 1);
            LedgerLocation stall = g.W.Inventory.FindFoodStore(g.W.Markets.Current.StallOf(2));
            int most = 0;
            for (int i = 0; i < 3000 && stall.Total < 20; i++) { g.W.Step(1); most = System.Math.Max(most, stall.Total); }
            Assert.GreaterOrEqual(stall.Total, 20, "one trip delivered the doubled load");
            // let the trip finish; the loan is released whatever happens next
            int trip = g.W.Tasks.All.First(t => t.Def.Id == StoreFamilySystem.StockStallTaskId).Id;
            for (int i = 0; i < 400 && !g.W.Tasks.Get(trip).IsFinished; i++) g.W.Step(1);
            g.W.Step(2);
            Assert.IsFalse(g.W.Inventory.Reservations.Any(r => r.Owner == KitLoans.OwnerFor(trip)), "loan released at task end");
            Assert.AreEqual(1, g.Kit.CountOf(Items.Basket.Id), "the basket never moved");
            Assert.IsNull(g.W.Inventory.Audit());
        }

        [Test]
        public void Granary_CancelledTask_ReleasesTheLoan()
        {
            Gran g = MakeGranary(2, 1);
            g.W.Step(1);
            TaskRecord t = g.W.Tasks.All.First(x => x.Def.Id == StoreFamilySystem.StockStallTaskId && !x.IsFinished);
            Assert.IsTrue(KitLoans.Holds(g.W, t.Id));
            g.W.Tasks.Cancel(t.Id);
            g.W.Step(1);
            Assert.IsFalse(KitLoans.Holds(g.W, t.Id));
            Assert.AreEqual(2, g.Kit.CountOf(Items.Basket.Id));
        }
    }
}
