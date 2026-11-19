using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Foraging;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Operations.Workshops;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Thatch;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>Collecting dry sheaves into the thatcher's hut store (T-575, MAT-32, MAT-35, MAT-39d, ST-42b).</summary>
    public class SheafCollectingTests
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

        private static Ctx Make(int adults)
        {
            var w = SimWorld.FromMapData(Map(), new SimTuning { StartHour = 8, StartMonth = 12, StartDay = 10, BushCount = 0,
                HazelStoolCount = 0, WillowStoolCount = 0, ReedBedCount = 0 });
            CommandResult r = w.Execute(new DesignateArea(Square(), AreaTypes.ThatchingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            var c = new Ctx { W = w, Op = w.Operations.FindByArea((int)r.Created[0]) };
            c.Hut = w.Footprints.Add(1, ModuleTypes.ThatcherHut, M(100, 100), 0);
            w.Operations.AddModule(c.Op.Id, 1);
            WorkshopBuildings.OnBuilt(w, c.Hut);
            c.Store = w.Inventory.FindGoodsStore(1);
            w.People.AddFamily(1, "F", 100, 100);
            for (int i = 0; i < adults; i++) w.People.AddCitizen(10 + i, "C" + i, Sex.Male, AdultBirth, 1, 100, 100);
            Assert.IsTrue(w.Execute(new AssignFamily(c.Op.Id, 1)).IsAccepted);
            return c;
        }

        private static List<TaskRecord> Collects(SimWorld w) => w.Tasks.All.Where(t => t.Def.Id == SheafCollecting.CollectTaskId).ToList();
        private static List<TaskRecord> Cuts(SimWorld w) => w.Tasks.All.Where(t => t.Def.Id == ThatchingSystem.CutReedTaskId).ToList();

        private static int Thatch(LedgerLocation l) => l.CountOf(Items.Thatch.Id);

        [Test]
        public void CollectTrip_IsOneSheaf_FourSteps_AndGoesBeforeCutting()
        {
            Ctx c = Make(1);
            c.W.Bushes.Add(105, 95, BushKind.ReedBed, 8);
            c.W.Inventory.ProduceOnGround(M(110, 95), c.Op.Id, Items.Thatch, 3);
            c.W.Step(1);
            TaskRecord t = Collects(c.W).Single();
            Assert.AreEqual(0, Cuts(c.W).Count, "the only cutter collects first");
            Assert.AreEqual(4, t.Def.Steps.Count);
            Assert.AreEqual(1, t.Def.Steps[1].Count, "1 sheaf per trip (MAT-35)");
            Assert.AreEqual(c.Store.Id, t.Def.Steps[3].LocationId);
            Assert.IsTrue(TaskDefCatalog.TryGet(SheafCollecting.CollectTaskId, out EligibilityPolicy e, out RequiredResource r));
            Assert.AreEqual(EligibilityPolicy.AssignedFamilyOnly, e);
        }

        [Test]
        public void TwoCutters_OneDrySheaf_OneCollectsAndOneCuts()
        {
            Ctx c = Make(2);
            c.W.Bushes.Add(105, 95, BushKind.ReedBed, 8);
            c.W.Inventory.ProduceOnGround(M(110, 95), c.Op.Id, Items.Thatch, 1);
            c.W.Step(1);
            Assert.AreEqual(1, Collects(c.W).Count);
            Assert.AreEqual(1, Cuts(c.W).Count);
        }

        [Test]
        public void Sheaves_AreCarriedOneByOne_UntilPileEmpty_ThenCuttingResumes()
        {
            Ctx c = Make(1);
            c.W.Bushes.Add(105, 95, BushKind.ReedBed, 8);
            c.W.Inventory.ProduceOnGround(M(110, 95), c.Op.Id, Items.Thatch, 3);
            for (int i = 0; i < 600 && Thatch(c.Store) < 3; i++) c.W.Step(1);
            Assert.AreEqual(3, Thatch(c.Store));
            Assert.AreEqual(3, Collects(c.W).Count(t => t.State == TaskState.Done), "one task per sheaf");
            Assert.AreEqual(0, SheafCollecting.DryCount(c.W, c.Op));
            c.W.Step(5);
            Assert.GreaterOrEqual(Cuts(c.W).Count, 1, "cutting starts once nothing is left to collect");
            Assert.IsNull(c.W.Inventory.Audit());
        }

        [Test]
        public void StoreFull_CuttingAndCollectingBothStop_PilesAndBedsWait()
        {
            Ctx c = Make(2);
            BushRecord bed = c.W.Bushes.Add(105, 95, BushKind.ReedBed, 8);
            c.W.Inventory.Produce(c.Store, Items.Thatch, 40);
            LedgerLocation pile = c.W.Inventory.ProduceOnGround(M(110, 95), c.Op.Id, Items.Thatch, 5);
            c.W.Step(300);
            Assert.AreEqual(0, Collects(c.W).Count);
            Assert.AreEqual(0, Cuts(c.W).Count);
            Assert.AreEqual(40, Thatch(c.Store));
            Assert.AreEqual(5, Thatch(pile));
            Assert.AreEqual(8, bed.Baskets);
            Assert.AreEqual(OperationState.StorageFull, ThatchingStatus.Of(c.W, c.Op));
            Assert.IsNull(c.W.Inventory.Audit());
        }

        [Test]
        public void OneSpaceLeft_OnlyOneSheafIsCollected_NeverOverfills()
        {
            Ctx c = Make(2);
            c.W.Inventory.Produce(c.Store, Items.Thatch, 39);
            LedgerLocation pile = c.W.Inventory.ProduceOnGround(M(110, 95), c.Op.Id, Items.Thatch, 3);
            c.W.Step(1);
            Assert.AreEqual(1, Collects(c.W).Count, "room counts trips on their way");
            c.W.Step(300);
            Assert.AreEqual(40, Thatch(c.Store));
            Assert.AreEqual(2, Thatch(pile));
            Assert.IsNull(c.W.Inventory.Audit());
        }

        [Test]
        public void NoReedBeds_CollectingContinues()
        {
            Ctx c = Make(1);
            c.W.Inventory.ProduceOnGround(M(110, 95), c.Op.Id, Items.Thatch, 2);
            Assert.AreEqual(OperationState.NoBushes, ThatchingStatus.Of(c.W, c.Op));
            c.W.Step(300);
            Assert.AreEqual(2, Thatch(c.Store));
        }

        [Test]
        public void PausedHut_DoesNotCollect()
        {
            Ctx c = Make(1);
            c.Op.Paused = true;
            c.W.Inventory.ProduceOnGround(M(110, 95), c.Op.Id, Items.Thatch, 2);
            c.W.Step(100);
            Assert.AreEqual(0, Collects(c.W).Count);
            Assert.AreEqual(0, Thatch(c.Store));
        }

        [Test]
        public void CollectEndingMidCarry_DropsTheSheafAsAnOperationPile()
        {
            Ctx c = Make(1);
            LedgerLocation pile = c.W.Inventory.ProduceOnGround(M(112, 95), c.Op.Id, Items.Thatch, 1);
            c.W.Step(1);
            TaskRecord t = Collects(c.W).Single();
            for (int i = 0; i < 300 && !(t.CurrentStep >= 2 && t.CarrierId.HasValue); i++) c.W.Step(1);
            Assert.AreEqual(0, Thatch(pile), "the sheaf is in hand");
            c.W.Inventory.Produce(c.Store, Items.Thatch, 40); // the hut fills up: the deposit cannot happen
            for (int i = 0; i < 300 && !t.IsFinished; i++) c.W.Step(1);
            Assert.AreEqual(TaskState.Cancelled, t.State);
            Assert.AreEqual(40, Thatch(c.Store));
            Assert.AreEqual(1, SheafCollecting.DryCount(c.W, c.Op), "the sheaf lies in an operation pile again");
            Assert.AreEqual(0, c.W.Inventory.Locations.Count(l => l.Kind == LocationKind.GroundPile && l.OwnerOperationId == null && Thatch(l) > 0));
            Assert.IsNull(c.W.Inventory.Audit());
        }

        [Test]
        public void HutStore_IsAConstructionSourceForThatch_DryPilesAreNot()
        {
            Ctx c = Make(1);
            c.W.Inventory.Produce(c.Store, Items.Thatch, 7);
            c.W.Inventory.ProduceOnGround(M(110, 95), c.Op.Id, Items.Thatch, 5);
            Assert.AreEqual(7, ConstructionSources.Available(c.W, Items.Thatch));
            List<int> ids = ConstructionSources.TryReserve(c.W, Items.Thatch, 7, M(100, 100), 99);
            Assert.IsNotNull(ids);
            Assert.AreEqual(0, c.Store.AvailableOf(Items.Thatch.Id));
            Assert.IsNull(ConstructionSources.TryReserve(c.W, Items.Thatch, 1, M(100, 100), 98), "operation piles are no source");
        }

        [Test]
        public void YardNeverCollectsTheHutStore_ST42b()
        {
            Ctx c = Make(1);
            Footprint yardFp = c.W.Footprints.Add(50, ModuleTypes.StorageYard, M(60, 60), 0);
            Operation yard = c.W.Operations.Create(OperationTypes.StorageYardOpId, 0);
            c.W.Operations.AddModule(yard.Id, yardFp.Id);
            WorkshopBuildings.OnBuilt(c.W, yardFp);
            c.W.Inventory.Produce(c.Store, Items.Thatch, 20);
            Assert.IsNull(new YardWorkshopSource().NextTrip(c.W, yard));
        }

        [Test]
        public void FullPipeline_CutStookCureDry_ThenCollectedHome_ByRealTicks()
        {
            Ctx c = Make(1);
            c.W.Bushes.Add(105, 95, BushKind.ReedBed, 8);
            c.W.Step(8 * 1440 + 600);
            Assert.AreEqual(0, c.W.Curing.Count, "the stook dried");
            Assert.AreEqual(8, Thatch(c.Store), "all eight sheaves came home");
            Assert.AreEqual(0, SheafCollecting.DryCount(c.W, c.Op));
            Assert.AreEqual(8, Collects(c.W).Count(t => t.State == TaskState.Done));
            Assert.IsNull(c.W.Inventory.Audit());
            Assert.IsNull(c.W.Curing.Audit());
        }
    }
}
