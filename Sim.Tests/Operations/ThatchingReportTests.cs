using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using System.Linq;
using LordsLegacy.Sim.Foraging;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Operations.Workshops;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Thatch;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>Thatcher's hut status text, panel report and the grown grain flag (T-576, MAT-31, MAT-39e).</summary>
    public class ThatchingReportTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static MapData Map() =>
            new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);

        private static AreaShape Square() => AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) });

        private sealed class Ctx
        {
            public SimWorld W;
            public Operation Op;
            public Footprint Hut;
            public LedgerLocation Store;
        }

        // A thatcher's hut in a given start month (12 = winter, 6 = slow season), with one family of the given adults.
        private static Ctx Make(int startMonth, params long[] births)
        {
            var w = SimWorld.FromMapData(Map(), new SimTuning { StartHour = 8, StartMonth = startMonth, StartDay = 10, BushCount = 0,
                HazelStoolCount = 0, WillowStoolCount = 0, ReedBedCount = 0 });
            CommandResult r = w.Execute(new DesignateArea(Square(), AreaTypes.ThatchingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            var c = new Ctx { W = w, Op = w.Operations.FindByArea((int)r.Created[0]) };
            c.Hut = w.Footprints.Add(1, ModuleTypes.ThatcherHut, M(100, 100), 0);
            w.Operations.AddModule(c.Op.Id, 1);
            WorkshopBuildings.OnBuilt(w, c.Hut);
            c.Store = w.Inventory.FindGoodsStore(1);
            w.People.AddFamily(1, "F", 100, 100);
            for (int i = 0; i < births.Length; i++) w.People.AddCitizen(10 + i, "C" + i, Sex.Male, births[i], 1, 100, 100);
            Assert.IsTrue(w.Execute(new AssignFamily(c.Op.Id, 1)).IsAccepted);
            return c;
        }

        private static ThatchingReport Report(Ctx c) => ThatchingReport.Of(c.W, c.Hut.Id);

        [Test]
        public void Status_NoWorkers_WhenNoOneAged14()
        {
            Ctx c = Make(12);
            c.W.Bushes.Add(105, 95, BushKind.ReedBed, 8);
            Assert.AreEqual(OperationState.NoWorkers, ThatchingStatus.Of(c.W, c.Op));
            Assert.AreEqual("No workers", Report(c).StatusText);
        }

        [Test]
        public void Status_CuttingInWinter_TextIsCutting()
        {
            Ctx c = Make(12, AdultBirth);
            c.W.Bushes.Add(105, 95, BushKind.ReedBed, 8);
            Assert.AreEqual(OperationState.Working, ThatchingStatus.Of(c.W, c.Op));
            ThatchingReport r = Report(c);
            Assert.AreEqual(OperationState.Working, r.State);
            Assert.AreEqual("Cutting", r.StatusText);
        }

        [Test]
        public void Status_CuttingInSlowSeason_TextIsCuttingSlowSeason()
        {
            Ctx c = Make(6, AdultBirth);
            c.W.Bushes.Add(105, 95, BushKind.ReedBed, 8);
            Assert.AreEqual(OperationState.Working, ThatchingStatus.Of(c.W, c.Op));
            Assert.AreEqual("Cutting (slow season)", Report(c).StatusText);
        }

        [Test]
        public void Status_NoReedBeds_TextIsNoReedBeds()
        {
            Ctx c = Make(6, AdultBirth);
            Assert.AreEqual(OperationState.NoBushes, ThatchingStatus.Of(c.W, c.Op));
            Assert.AreEqual("No reed beds", Report(c).StatusText);
        }

        [Test]
        public void Status_StoreFull_TextIsStoreFull()
        {
            Ctx c = Make(6, AdultBirth);
            c.W.Bushes.Add(105, 95, BushKind.ReedBed, 8);
            c.W.Inventory.Produce(c.Store, Items.Thatch, 40);
            Assert.AreEqual(OperationState.StorageFull, ThatchingStatus.Of(c.W, c.Op));
            Assert.AreEqual("Store full", Report(c).StatusText);
        }

        [Test]
        public void Report_CountsStoreStooksBundlesDrySheavesAndNextReadyDay()
        {
            Ctx c = Make(6, AdultBirth);
            c.W.Inventory.Produce(c.Store, Items.Thatch, 3);
            c.W.Inventory.ProduceOnGround(M(110, 95), c.Op.Id, Items.Thatch, 2);
            long early = c.W.Tick + 3 * 1440, late = c.W.Tick + 7 * 1440;
            c.W.Curing.Add(M(105, 95), c.Op.Id, 8, late);
            c.W.Curing.Add(M(106, 95), c.Op.Id, 5, early);
            ThatchingReport r = Report(c);
            Assert.AreEqual(3, r.StoreCount);
            Assert.AreEqual(40, r.StoreCapacity);
            Assert.AreEqual(2, r.StooksCuring);
            Assert.AreEqual(13, r.BundlesCuring, "bundles of both stooks");
            Assert.AreEqual(2, r.DrySheaves, "dry sheaves in the operation's piles");
            Assert.IsNotNull(r.NextStookReadyAt);
            Assert.AreEqual(c.W.TimeAt(early).DayOfMonth, r.NextStookReadyAt.Value.DayOfMonth, "the earliest stook is next");
            Assert.AreEqual(c.W.TimeAt(early).Month, r.NextStookReadyAt.Value.Month);
            Assert.AreEqual("Stooks curing: 2 (13 bundles)", r.StooksText);
            Assert.AreEqual("Dry sheaves: 2", r.DryText);
            Assert.AreEqual("Store: 3 / 40", r.StoreText);
            Assert.IsTrue(r.NextStookText.StartsWith("Next stook ready: " + c.W.TimeAt(early).DayOfMonth + " "), r.NextStookText);
        }

        [Test]
        public void Report_NoStooks_CountsZero_NextReadyIsNull()
        {
            Ctx c = Make(6, AdultBirth);
            ThatchingReport r = Report(c);
            Assert.AreEqual(0, r.StooksCuring);
            Assert.AreEqual(0, r.BundlesCuring);
            Assert.AreEqual(0, r.DrySheaves);
            Assert.IsNull(r.NextStookReadyAt);
            Assert.AreEqual("No stook curing", r.NextStookText);
        }

        [Test]
        public void Report_NullForAnotherModule()
        {
            Ctx c = Make(6, AdultBirth);
            Footprint other = c.W.Footprints.Add(2, ModuleTypes.Granary, M(60, 60), 0);
            Assert.IsNull(ThatchingReport.Of(c.W, other.Id));
        }

        [Test]
        public void GrownGrainSource_IsAlwaysUnavailable_TextNeedsFarming()
        {
            Ctx c = Make(6, AdultBirth);
            Assert.IsFalse(GrownGrainSource.Available(c.W));
            Assert.AreEqual("Needs farming", GrownGrainSource.Text(c.W));
            ThatchingReport r = Report(c);
            Assert.IsFalse(r.GrainSourceAvailable);
            Assert.AreEqual("Needs farming", r.GrainSourceText);
        }

        [Test]
        public void Pipeline_CutToStookToDry_ReportFollowsEachStep()
        {
            // Reached only by the real task and Sim ticks: the bed is cut, the stook stands, then it dries (MAT-32, MAT-39c).
            Ctx c = Make(12, AdultBirth);
            c.W.Bushes.Add(105, 95, BushKind.ReedBed, 8);
            c.W.Step(1);
            Assert.AreEqual(0, Report(c).StooksCuring, "nothing curing before the first bundle");
            c.W.Step(600);
            ThatchingReport curing = Report(c);
            Assert.AreEqual(1, curing.StooksCuring);
            Assert.AreEqual(8, curing.BundlesCuring);
            Assert.AreEqual(0, curing.DrySheaves);
            Assert.AreEqual(c.W.TimeAt(c.W.Curing.All.Single().ReadyTick).DayOfMonth, curing.NextStookReadyAt.Value.DayOfMonth);
            for (int i = 0; i < 8 * 1440 && c.W.Curing.Count > 0; i++) c.W.Step(1);
            ThatchingReport dry = Report(c);
            Assert.AreEqual(0, dry.StooksCuring, "the stook has dried");
            Assert.AreEqual(8, dry.DrySheaves + dry.StoreCount, "all 8 sheaves are dry: in a pile or already in the store");
            Assert.AreEqual("No stook curing", dry.NextStookText);
        }
    }
}
