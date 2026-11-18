using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Operations.Workshops;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>StorageYardReport read model: stock, totals, families, computed status and the ST-10 market text (T-547).</summary>
    public class StorageYardReportTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);
        private static PointMm P(long x, long z) => new PointMm(x, z);
        private const long Adult = -4_000_000;

        // Camp centre (100,100) m, yard at (120,130) m, a loose pile at (110,120) m.
        private static readonly PointMm YardAt = M(120, 130);
        private static readonly PointMm PileAt = M(110, 120);

        private static StartLayout Layout() => new StartLayout(
            P(100000, 100000), 0, P(0, 0), P(6000, 0), P(-6000, 0), new PointMm[0],
            new[]
            {
                new StartFamily("Ash", P(-8000, -4000), new[] { new StartCitizen("A", Sex.Male, Adult), new StartCitizen("B", Sex.Female, Adult) }),
                new StartFamily("Birch", P(-8000, 4000), new[] { new StartCitizen("C", Sex.Female, Adult) }),
            }, 2);

        // The real start layout (camp General store, two families, two oxen), flat map, no starting planks.
        private static SimWorld Started()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8, StartingPlanks = 0 });
            StartLayoutApplier.Apply(w, Layout());
            Start.NoCampTests.UseHouseGoodsUp(w); // T-577: these tests count planks only
            return w;
        }

        // A yard placed through the real command and built through Sim ticks only (ST-03).
        private static int BuiltYard(SimWorld w, PointMm at)
        {
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.StorageYardId, at, 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int id = (int)r.Created[0];
            for (int i = 0; i < 6000 && !w.IsBuilt(id); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(id), "yard built through Sim ticks");
            return id;
        }

        // Both oxen are assigned to a second yard far away. FindFor never returns an ox owned by another operation,
        // so this yard has no free ox. Unassigning them (UnassignOx) frees them again.
        private static readonly PointMm FarYardAt = M(20, 180);

        private static int TieUpOxen(SimWorld w)
        {
            int other = BuiltYard(w, FarYardAt);
            int op = w.Operations.FindByModule(other).Id;
            foreach (Ox ox in w.People.Oxen.ToList())
                Assert.IsTrue(w.Execute(new AssignOx(op, ox.Id)).IsAccepted);
            return op;
        }

        private static int FamilyId(SimWorld w, string surname) => w.People.Families.First(f => f.Surname == surname).Id;

        private static void Staff(SimWorld w, int yard, string surname)
        {
            int op = w.Operations.FindByModule(yard).Id;
            Assert.IsTrue(w.Execute(new AssignFamily(op, FamilyId(w, surname))).IsAccepted);
        }

        private static IEnumerable<TaskRecord> OpenHauls(SimWorld w) =>
            w.Tasks.All.Where(t => !t.IsFinished && (t.Def.Id == HandHaulTaskDefs.HaulId || t.Def.Id == WorkshopSystem.FetchTaskId));

        private static StorageYardReport Report(SimWorld w, int yard) => StorageYardReport.Of(w, yard);


        [Test]
        public void NotAYard_ReturnsNull()
        {
            SimWorld w = Started();
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(50, 50), 0));
            Assert.IsNull(StorageYardReport.Of(w, (int)r.Created[0]), "a granary is not a yard");
            Assert.IsNull(StorageYardReport.Of(w, 987654), "unknown id");
        }

        [Test]
        public void JustPlaced_UnderConstruction_NoStoreYet()
        {
            SimWorld w = Started();
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.StorageYardId, YardAt, 0));
            int id = (int)r.Created[0];
            StorageYardReport rep = Report(w, id);
            Assert.IsFalse(rep.IsBuilt);
            Assert.AreEqual(StorageYardStatus.UnderConstruction, rep.Status);
            Assert.AreEqual("Under construction", rep.StatusText);
            Assert.AreEqual(0, rep.Total);
            Assert.AreEqual(0, rep.Capacity);
        }

        [Test]
        public void BuiltWithoutFamily_NoWorkers_FamilyTextNone()
        {
            SimWorld w = Started();
            int yard = BuiltYard(w, YardAt);
            StorageYardReport rep = Report(w, yard);
            Assert.AreEqual(StorageYardStatus.NoWorkers, rep.Status);
            Assert.AreEqual("No workers", rep.StatusText);
            Assert.AreEqual("None", rep.FamilyText);
            Assert.IsEmpty(rep.FamilyIds);
        }

        [Test]
        public void BuiltAndStaffedWithNothingToMove_Idle()
        {
            SimWorld w = Started();
            int yard = BuiltYard(w, YardAt);
            Staff(w, yard, "Ash");
            w.Step(1);
            StorageYardReport rep = Report(w, yard);
            Assert.AreEqual(StorageYardStatus.Idle, rep.Status);
            Assert.AreEqual("Idle", rep.StatusText);
            Assert.AreEqual("Ash", rep.FamilyText);
        }

        [Test]
        public void CampGoodsWaiting_MovingCampGoods_AfterThePlannerMakesTheTrip()
        {
            SimWorld w = Started();
            int yard = BuiltYard(w, YardAt);
            w.Inventory.Produce(w.Camp.GeneralStoreLocation, Items.Plank, 5);
            Staff(w, yard, "Ash");
            w.Step(1);
            Assert.AreEqual(StorageYardStatus.MovingCampGoods, Report(w, yard).Status);
            Assert.AreEqual("Moving camp goods", Report(w, yard).StatusText);
        }

        [Test]
        public void LoosePlanksOnTheGround_Collecting_NotCamp()
        {
            SimWorld w = Started();
            int yard = BuiltYard(w, YardAt);
            LedgerLocation pile = w.Inventory.AddGroundPile(PileAt, null);
            w.Inventory.Produce(pile, Items.Plank, 4);
            Staff(w, yard, "Ash");
            w.Step(1);
            StorageYardReport rep = Report(w, yard);
            Assert.AreEqual(StorageYardStatus.Collecting, rep.Status);
            Assert.AreEqual("Collecting", rep.StatusText);
        }

        [Test]
        public void FullYard_YardFull_NoTripMade()
        {
            SimWorld w = Started();
            int yard = BuiltYard(w, YardAt);
            LedgerLocation store = w.Inventory.FindGoodsStore(yard);
            w.Inventory.Produce(store, Items.Plank, store.Capacity);
            w.Inventory.Produce(w.Camp.GeneralStoreLocation, Items.Plank, 3);
            Staff(w, yard, "Ash");
            w.Step(1);
            Assert.AreEqual(StorageYardStatus.YardFull, Report(w, yard).Status);
            Assert.AreEqual("Yard full", Report(w, yard).StatusText);
            Assert.IsEmpty(OpenHauls(w).ToList(), "full yard: no trip reserved (ST-44)");
        }

        [Test]
        public void BeamsWithNoFreeOx_WaitingForOx_NoTaskMade()
        {
            SimWorld w = Started();
            int yard = BuiltYard(w, YardAt);
            LedgerLocation pile = w.Inventory.AddGroundPile(PileAt, null);
            w.Inventory.Produce(pile, Items.Beam, 4);
            TieUpOxen(w);
            Staff(w, yard, "Ash");
            w.Step(1);
            StorageYardReport rep = Report(w, yard);
            Assert.AreEqual(StorageYardStatus.WaitingForOx, rep.Status);
            Assert.AreEqual("Waiting for ox", rep.StatusText);
            Assert.IsEmpty(OpenHauls(w).Where(t => t.OwnerOperationId == w.Operations.FindByModule(yard).Id).ToList(),
                "nothing is opened for this yard until an ox is free");
        }

        [Test]
        public void BeamsWaitingThenOxFree_CollectingWithAnOxTask()
        {
            SimWorld w = Started();
            int yard = BuiltYard(w, YardAt);
            LedgerLocation pile = w.Inventory.AddGroundPile(PileAt, null);
            w.Inventory.Produce(pile, Items.Beam, 4);
            int other = TieUpOxen(w);
            Staff(w, yard, "Ash");
            w.Step(1);
            Assert.AreEqual(StorageYardStatus.WaitingForOx, Report(w, yard).Status);
            foreach (Ox ox in w.People.Oxen.ToList())
                Assert.IsTrue(w.Execute(new UnassignOx(other, ox.Id)).IsAccepted);
            w.Step(1);
            Assert.AreEqual(StorageYardStatus.Collecting, Report(w, yard).Status);
        }

        [Test]
        public void Pipeline_PlacedBuiltStaffedThenCampGoodsMovedByTicks_ShowsMovingThenIdle()
        {
            // Every state here is reached through real commands (PlaceStructure, AssignFamily) and Sim ticks.
            SimWorld w = Started();
            int yard = BuiltYard(w, YardAt);
            LedgerLocation camp = w.Camp.GeneralStoreLocation;
            w.Inventory.Produce(camp, Items.Plank, 5);
            Staff(w, yard, "Ash");
            LedgerLocation store = w.Inventory.FindGoodsStore(yard);

            bool sawMoving = false;
            for (int i = 0; i < 30000 && (camp.Total > 0 || OpenHauls(w).Any()); i++)
            {
                w.Step(1);
                if (Report(w, yard).Status == StorageYardStatus.MovingCampGoods) sawMoving = true;
            }

            Assert.IsTrue(sawMoving, "the panel showed Moving camp goods while the planks were carried");
            Assert.AreEqual(0, camp.Total, "camp General store emptied into the yard (ST-40)");
            StorageYardReport rep = Report(w, yard);
            Assert.AreEqual(5, store.CountOf(Items.Plank.Id));
            Assert.AreEqual(5, rep.StockOf(Items.Plank.Id));
            Assert.AreEqual(StorageYardStatus.Idle, rep.Status);
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void Stock_EveryGeneralItemInCatalogueOrder_ZerosIncluded_TotalAndCapacity()
        {
            SimWorld w = Started();
            int yard = BuiltYard(w, YardAt);
            LedgerLocation store = w.Inventory.FindGoodsStore(yard);
            w.Inventory.Produce(store, Items.Plank, 3);
            w.Inventory.Produce(store, Items.Beam, 2);

            StorageYardReport rep = Report(w, yard);
            string[] general = Items.All.Where(i => i.Category == ItemCategory.General).Select(i => i.Id).ToArray();
            CollectionAssert.AreEqual(general, rep.Stock.Select(kv => kv.Key).ToArray());
            Assert.AreEqual(3, rep.StockOf(Items.Plank.Id));
            Assert.AreEqual(2, rep.StockOf(Items.Beam.Id));
            Assert.AreEqual(0, rep.StockOf(Items.Basket.Id), "listed at zero");
            Assert.AreEqual(5, rep.Total);
            Assert.AreEqual(store.Capacity, rep.Capacity);
            Assert.Greater(rep.Capacity, 0);
        }

        [Test]
        public void VillageStock_IsTheT525Totals_PerItem_AndCampCountsToo()
        {
            SimWorld w = Started();
            int yard = BuiltYard(w, YardAt);
            w.Inventory.Produce(w.Inventory.FindGoodsStore(yard), Items.Plank, 3);
            w.Inventory.Produce(w.Camp.GeneralStoreLocation, Items.Plank, 4);
            w.Inventory.Produce(w.Inventory.FindGoodsStore(yard), Items.Beam, 2);

            StorageYardReport rep = Report(w, yard);
            Assert.AreEqual(7, rep.VillageStock.First(kv => kv.Key == Items.Plank.Id).Value);
            Assert.AreEqual(2, rep.VillageStock.First(kv => kv.Key == Items.Beam.Id).Value);
            Assert.AreEqual(GoodsStats.Planks(w), rep.VillageStock.First(kv => kv.Key == Items.Plank.Id).Value, "same as the top bar");
            Assert.AreEqual(GoodsStats.Beams(w), rep.VillageStock.First(kv => kv.Key == Items.Beam.Id).Value, "same as the top bar");
            Assert.AreEqual(3, rep.Stock.First(kv => kv.Key == Items.Plank.Id).Value, "the yard's own share is not the village total");
        }

        [Test]
        public void SuppliesMarket_NotAvailable_TextNoDemandYet()
        {
            SimWorld w = Started();
            int yard = BuiltYard(w, YardAt);
            StorageYardReport rep = Report(w, yard);
            Assert.IsFalse(rep.SuppliesMarketAvailable, "ST-10: no household buys General goods yet");
            Assert.AreEqual("No demand yet", rep.SuppliesMarketText);
        }

        [Test]
        public void Report_IsReadOnly_ChangesNothingInTheSim()
        {
            SimWorld w = Started();
            int yard = BuiltYard(w, YardAt);
            w.Inventory.Produce(w.Camp.GeneralStoreLocation, Items.Plank, 5);
            Staff(w, yard, "Ash");
            w.Step(1);
            int tasks = w.Tasks.All.Count();
            int stock = GoodsStats.Planks(w);
            long tick = w.Tick;
            int campBefore = w.Camp.GeneralStoreLocation.Total;

            StorageYardReport a = Report(w, yard);
            StorageYardReport b = Report(w, yard);

            Assert.AreEqual(tasks, w.Tasks.All.Count());
            Assert.AreEqual(stock, GoodsStats.Planks(w));
            Assert.AreEqual(tick, w.Tick);
            Assert.AreEqual(campBefore, w.Camp.GeneralStoreLocation.Total);
            Assert.AreEqual(a.Status, b.Status);
            Assert.AreEqual(a.Total, b.Total);
        }

        [Test]
        public void TextOf_EveryStatus_HasItsPanelText()
        {
            Assert.AreEqual("Under construction", StorageYardReport.TextOf(StorageYardStatus.UnderConstruction));
            Assert.AreEqual("No workers", StorageYardReport.TextOf(StorageYardStatus.NoWorkers));
            Assert.AreEqual("Waiting for ox", StorageYardReport.TextOf(StorageYardStatus.WaitingForOx));
            Assert.AreEqual("Moving camp goods", StorageYardReport.TextOf(StorageYardStatus.MovingCampGoods));
            Assert.AreEqual("Collecting", StorageYardReport.TextOf(StorageYardStatus.Collecting));
            Assert.AreEqual("Yard full", StorageYardReport.TextOf(StorageYardStatus.YardFull));
            Assert.AreEqual("Idle", StorageYardReport.TextOf(StorageYardStatus.Idle));
        }
    }
}
