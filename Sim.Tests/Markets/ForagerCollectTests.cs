using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Markets;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Markets
{
    /// <summary>MK-24 step 2 (T-463): granary families collect from forager hut stores into the granary.</summary>
    public class ForagerCollectTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private sealed class Fx
        {
            public SimWorld W; public int Granary, GranaryOp;
            public LedgerLocation GranaryStore => W.Inventory.FindFoodStore(Granary);
            public List<TaskRecord> Trips => W.Tasks.All.Where(t => t.Def.Id == StoreFamilySystem.StockStallTaskId && !t.IsFinished).ToList();
        }

        private static Fx Make(bool market, int members = 2)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8, BushCount = 0 });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 80);
            w.Inventory.Produce(w.Inventory.AddGoodsStore(M(20, 22)), Items.Plank, 10); // stalls cost 2 planks (PL-10)
            w.People.AddFamily(2, "F2", 40, 40);
            for (int i = 0; i < members; i++) w.People.AddCitizen(20 + i, "C" + i, Sex.Male, AdultBirth, 2, 40, 40);
            w.People.AddFamily(3, "F3", 40, 40);
            for (int i = 0; i < 2; i++) w.People.AddCitizen(30 + i, "H" + i, Sex.Male, AdultBirth, 3, 40, 40);
            Ox ox = w.People.AddOx(50, "O", 40, 44);
            ox.State = OxState.Resting(40000, 44000);
            if (market)
                Assert.IsTrue(w.Execute(new DesignateArea(AreaShape.Straight(new[] { M(10, 10), M(30, 10), M(30, 30), M(10, 30) }), AreaTypes.MarketId)).IsAccepted);
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(60, 60), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int fp = (int)r.Created[0];
            for (int i = 0; i < 6000 && !w.IsBuilt(fp); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(fp));
            int op = w.Operations.FindByModule(fp).Id;
            if (market) Assert.IsTrue(w.Execute(new SetGranarySupplies(op, true)).IsAccepted);
            var f = new Fx { W = w, Granary = fp, GranaryOp = op };
            f.GranaryStore.Capacity = 500;
            return f;
        }

        // A built forager hut (own operation, no workers) with a food store holding `food` meat.
        private static LedgerLocation Hut(Fx f, double x, double z, int food)
        {
            var hut = new List<ModuleSpec> { new ModuleSpec(ModuleTypes.ForagerHutId, M(x, z), 0) };
            var square = AreaShape.Straight(new[] { M(x - 10, z - 10), M(x + 10, z - 10), M(x + 10, z + 10), M(x - 10, z + 10) });
            CommandResult d = f.W.Execute(new DesignateOperation(square, OperationTypes.ForagingId, hut));
            Assert.IsTrue(d.IsAccepted, d.Text);
            int fp = (int)d.Created[1];
            for (int i = 0; i < 6000 && !f.W.IsBuilt(fp); i++) f.W.Step(1);
            Assert.IsTrue(f.W.IsBuilt(fp));
            LedgerLocation store = f.W.Inventory.FindFoodStore(fp);
            Assert.IsNotNull(store);
            if (food > 0) f.W.Inventory.Produce(store, Items.Meat, food);
            return store;
        }

        private static void Assign(Fx f) => Assert.IsTrue(f.W.Execute(new AssignFamily(f.GranaryOp, 2)).IsAccepted);

        private static int SourceOf(TaskRecord t) => t.Def.Steps.First(s => s.Kind == StepKind.CollectItems).LocationId;

        [Test]
        public void NoMarket_FamilyCollectsFromHutIntoGranary_FullPipeline_TenItemsAndTenMinuteLoads()
        {
            Fx f = Make(false, 1);
            LedgerLocation hut = Hut(f, 100, 100, 20);
            Assign(f);
            f.W.Step(1);
            TaskRecord t = f.Trips.First();
            Assert.AreEqual(f.GranaryOp, t.OwnerOperationId);
            Assert.AreEqual(hut.Id, SourceOf(t));
            Assert.AreEqual(f.GranaryStore.Id, t.Def.Steps.Last().LocationId);
            Assert.AreEqual(10, t.Def.Steps[2].Count);
            Assert.AreEqual(10, t.Def.Steps[1].DurationTicks);
            Assert.AreEqual(10, t.Def.Steps[4].DurationTicks);
            for (int i = 0; i < 4000 && f.GranaryStore.Total == 0; i++) f.W.Step(1);
            Assert.AreEqual(10, f.GranaryStore.Total, "ten items reached the granary");
            Assert.AreEqual(10, hut.Total, "and ten left the hut");
            Assert.AreEqual("", f.W.Inventory.Audit() ?? "");
        }

        [Test]
        public void WithMarket_StallNotBuiltYet_CollectsFromHut()
        {
            Fx f = Make(true);
            Hut(f, 100, 100, 20);
            Assign(f); // stall is only queued, granary is empty: stocking is impossible
            f.W.Step(1);
            Assert.IsTrue(f.Trips.Any(t => t.Def.Steps.Last().LocationId == f.GranaryStore.Id));
        }

        [Test]
        public void StallStockingPossible_TakesPrecedence_NoCollectTrip()
        {
            Fx f = Make(true);
            Hut(f, 100, 100, 20);
            f.W.Inventory.Produce(f.GranaryStore, Items.Meat, 40);
            Assign(f);
            int stall = 0;
            for (int i = 0; i < 6000 && !(stall != 0 && f.W.IsBuilt(stall)); i++)
            {
                f.W.Step(1);
                stall = f.W.Markets.Current.StallOf(2);
            }
            Assert.IsTrue(f.W.IsBuilt(stall));
            foreach (TaskRecord t in f.Trips) f.W.Tasks.Cancel(t.Id);
            // The builders may still be finishing their build task (plank carries make it later): give them a few minutes.
            for (int i = 0; i < 300 && !f.Trips.Any(); i++) f.W.Step(1);
            Assert.IsNotEmpty(f.Trips);
            Assert.IsTrue(f.Trips.All(t => t.Def.Steps.Last().LocationId == f.W.Inventory.FindFoodStore(stall).Id), "all trips go to the stall");
        }

        [Test]
        public void FullestHutIsChosen_AtAnyDistance()
        {
            Fx f = Make(false);
            LedgerLocation near = Hut(f, 80, 60, 12);
            LedgerLocation far = Hut(f, 100, 100, 20);
            Assert.AreSame(far, StoreFamilySystem.ChooseForagerSource(f.W, f.GranaryStore, out int a));
            Assert.AreEqual(20, a);
            Assert.AreNotSame(near, far);
        }

        [Test]
        public void EqualHuts_TieGoesToTheNearest()
        {
            Fx f = Make(false);
            Hut(f, 100, 100, 15);
            LedgerLocation near = Hut(f, 80, 60, 15);
            Assert.AreSame(near, StoreFamilySystem.ChooseForagerSource(f.W, f.GranaryStore, out _));
        }

        [Test]
        public void EmptyHuts_ChooseNothing_AndOpenNoTask()
        {
            Fx f = Make(false);
            Hut(f, 100, 100, 0);
            Assign(f);
            f.W.Step(3);
            Assert.IsNull(StoreFamilySystem.ChooseForagerSource(f.W, f.GranaryStore, out int a));
            Assert.AreEqual(0, a);
            Assert.IsEmpty(f.Trips);
        }

        [Test]
        public void TwoWorkers_SplitOverHuts_PendingPromisesCounted()
        {
            Fx f = Make(false);
            LedgerLocation near = Hut(f, 80, 60, 12);
            LedgerLocation far = Hut(f, 100, 100, 20);
            Assign(f);
            // Builders of the second hut may still be finishing (2 logs per ox trip, T-491): give both workers a moment.
            for (int i = 0; i < 120 && f.Trips.Count < 2; i++) f.W.Step(1);
            var sources = f.Trips.Select(SourceOf).OrderBy(x => x).ToList();
            Assert.AreEqual(2, sources.Count);
            CollectionAssert.AreEquivalent(new[] { near.Id, far.Id }, sources, "second worker sees the first one's promise: 12 beats 10");
        }

        [Test]
        public void GranaryWithOnlyThreeFreeSlots_PlansThreeItems()
        {
            Fx f = Make(false, 1);
            Hut(f, 100, 100, 20);
            f.GranaryStore.Capacity = 3;
            Assign(f);
            f.W.Step(1);
            Assert.AreEqual(3, f.Trips.Single().Def.Steps[2].Count);
        }

        [Test]
        public void FullGranary_OpensNoCollectTrip()
        {
            Fx f = Make(false);
            Hut(f, 100, 100, 20);
            f.W.Inventory.Produce(f.GranaryStore, Items.Meat, 500);
            Assign(f);
            f.W.Step(3);
            Assert.IsEmpty(f.Trips);
        }

        [Test]
        public void LoadReservesGranaryRoom_AndFreesItAfterUnload()
        {
            Fx f = Make(false, 1);
            LedgerLocation hut = Hut(f, 100, 100, 20);
            Assign(f);
            bool sawRoomReserved = false;
            for (int i = 0; i < 4000 && f.GranaryStore.Total == 0; i++)
            {
                f.W.Step(1);
                if (f.W.Inventory.Reservations.Any(r => r.LocationId == f.GranaryStore.Id)) sawRoomReserved = true;
            }
            Assert.IsTrue(sawRoomReserved, "granary room reserved through the ledger while the load travels");
            f.W.Step(1);
            Assert.IsFalse(f.W.Inventory.Reservations.Any(r => r.LocationId == hut.Id || r.LocationId == f.GranaryStore.Id), "reservations gone");
        }

        [Test]
        public void EmptyingAFullHut_RestartsItsForagers()
        {
            Fx f = Make(false, 1);
            LedgerLocation hut = Hut(f, 100, 100, 0);
            f.W.Inventory.Produce(hut, Items.Meat, hut.Capacity);
            Operation forage = f.W.Operations.All.First(o => o.TypeId == OperationTypes.ForagingId);
            Assert.IsTrue(Foraging.ForagingStores.AllFull(f.W, forage));
            Assert.IsNull(Foraging.ForagingStores.HutStoreWithRoom(f.W, forage, out _));
            Assign(f);
            for (int i = 0; i < 6000 && Foraging.ForagingStores.AllFull(f.W, forage); i++) f.W.Step(1);
            Assert.IsFalse(Foraging.ForagingStores.AllFull(f.W, forage), "room appeared once a family emptied some of it");
            Assert.IsNotNull(Foraging.ForagingStores.HutStoreWithRoom(f.W, forage, out int room), "foragers may deliver again");
            Assert.Greater(room, 0);
        }

        [Test]
        public void SwitchingTheMarketOff_KeepsCollectTrips()
        {
            Fx f = Make(true);
            Hut(f, 100, 100, 20);
            Assign(f);
            f.W.Step(1);
            Assert.IsTrue(f.Trips.Count > 0);
            Assert.IsTrue(f.W.Execute(new SetGranarySupplies(f.GranaryOp, false)).IsAccepted);
            f.W.Step(1);
            Assert.IsTrue(f.Trips.Count > 0, "collect trips don't need the market");
        }
    }
}
