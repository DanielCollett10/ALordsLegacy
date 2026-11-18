using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
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
    /// <summary>Thatcher's hut and the reed cutting task (T-574, MAT-34, MAT-37, MAT-39b, MAT-39c).</summary>
    public class ThatchingTests
    {
        private const long AdultBirth = -4_000_000;
        private const long NineBirth = -2_500_000;
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

        private static List<TaskRecord> Tasks(SimWorld w) => w.Tasks.All.Where(t => t.Def.Id == ThatchingSystem.CutReedTaskId).ToList();

        [Test]
        public void ThatchingType_HasHut6x4_8Logs480Min_Circle300_AndStoreOf40Thatch()
        {
            OperationTypeDef t = OperationTypes.Find(OperationTypes.ThatchingId);
            Assert.AreSame(OperationTypes.Thatching, t);
            Assert.AreEqual(300, t.DefaultWorkRadiusM);
            Assert.AreEqual(ModuleTypes.ThatcherHutId, t.HutTypeId);
            Assert.AreEqual(8, ModuleTypes.ThatcherHut.ConstructionRecipe.Logs);
            Assert.AreEqual(480, ModuleTypes.ThatcherHut.ConstructionRecipe.BuildWorkerMinutes);
            Assert.AreEqual(6000, ModuleTypes.ThatcherHut.WidthMm);
            Assert.AreEqual(4000, ModuleTypes.ThatcherHut.DepthMm);
            Assert.AreSame(OperationTypes.Thatching, OperationTypes.FindByBuildingArea(AreaTypes.ThatchingId));
            Assert.AreEqual(AreaKind.Work, AreaTypes.Find(t.WorkAreaTypeId).Kind);
            Ctx c = Make(12, AdultBirth);
            Assert.AreEqual(40, c.Store.ItemCapOf(Items.Thatch.Id));
            Assert.IsTrue(TaskDefCatalog.TryGet(ThatchingSystem.CutReedTaskId, out EligibilityPolicy e, out RequiredResource r));
            Assert.AreEqual(EligibilityPolicy.AssignedFamilyOnly, e);
        }

        [Test]
        public void Winter_EightBundlesAt30Minutes_HeavyWork_ThenOneStookAtTheBed_BedReleased()
        {
            Ctx c = Make(12, AdultBirth);
            BushRecord bed = c.W.Bushes.Add(105, 95, BushKind.ReedBed, 8);
            c.W.Step(1);
            TaskRecord t = Tasks(c.W).Single();
            Assert.AreEqual(t.Id, bed.ReservedBy, "bed reserved while the task is open");
            Assert.AreEqual(1 + 8, t.Def.Steps.Count);
            Assert.IsTrue(t.Def.Steps.Skip(1).All(s => s.Heavy && s.DurationTicks == 30));
            c.W.Step(600);
            Assert.AreEqual(TaskState.Done, t.State);
            Assert.AreEqual(1, c.W.Curing.Count);
            StookRecord s1 = c.W.Curing.All.Single();
            Assert.AreEqual(8, s1.Count);
            Assert.AreEqual(c.Op.Id, s1.OperationId);
            Assert.AreEqual(M(105, 95), s1.Position);
            Assert.AreEqual(0, bed.Baskets);
            Assert.IsFalse(bed.ReservedBy.HasValue, "bed released");
            Assert.AreEqual(0, c.Store.CountOf(Items.Thatch.Id), "green bundles are not thatch");
        }

        [Test]
        public void Summer_BundlesTakeFourTimesAsLong()
        {
            Ctx c = Make(7, AdultBirth);
            c.W.Bushes.Add(105, 95, BushKind.ReedBed, 8);
            c.W.Step(1);
            TaskRecord t = Tasks(c.W).Single();
            Assert.IsTrue(t.Def.Steps.Skip(1).All(s => s.DurationTicks == 120));
            c.W.Step(300);
            Assert.AreEqual(0, c.W.Curing.Count, "winter pace would be done by now");
        }

        [Test]
        public void Summer_StillFinishesAndStandsOneStook_AcrossDays()
        {
            Ctx c = Make(7, AdultBirth);
            c.W.Bushes.Add(105, 95, BushKind.ReedBed, 8);
            c.W.Step(3 * 1440);
            Assert.GreaterOrEqual(c.W.Curing.CuringCount(c.Op.Id), 8);
            Assert.AreEqual(8, c.W.Curing.ForOperation(c.Op.Id)[0].Count);
        }

        [Test]
        public void SmallBed_CutsOnlyWhatIsThere_AndNoOtherCutterTakesAReservedBed()
        {
            Ctx c = Make(12, AdultBirth, AdultBirth);
            BushRecord a = c.W.Bushes.Add(105, 95, BushKind.ReedBed, 3);
            BushRecord b = c.W.Bushes.Add(112, 95, BushKind.ReedBed, 8);
            c.W.Step(1);
            List<TaskRecord> ts = Tasks(c.W);
            Assert.AreEqual(2, ts.Count);
            Assert.AreNotEqual(a.ReservedBy, b.ReservedBy);
            Assert.AreEqual(1 + 3, ts.First(t => t.Id == a.ReservedBy).Def.Steps.Count);
        }

        [Test]
        public void Child9_DoesNotCut_StatusNoWorkers()
        {
            Ctx c = Make(12, NineBirth);
            c.W.Bushes.Add(105, 95, BushKind.ReedBed, 8);
            c.W.Step(600);
            Assert.AreEqual(0, Tasks(c.W).Count);
            Assert.AreEqual(OperationState.NoWorkers, OperationStatus.Of(c.W, c.Op));
        }

        [Test]
        public void NoBeds_StatusNoBushes_AndNoTask()
        {
            Ctx c = Make(12, AdultBirth);
            Assert.AreEqual(OperationState.NoBushes, OperationStatus.Of(c.W, c.Op));
            c.W.Step(10);
            Assert.AreEqual(0, Tasks(c.W).Count);
        }

        [Test]
        public void Family_StaysAssignedAllYear()
        {
            Ctx c = Make(7, AdultBirth);
            c.W.Step(2 * 1440);
            Assert.Contains(1, c.Op.FamilyIds.ToList());
            Assert.AreEqual(c.Op.Id, c.W.People.FindFamily(1).WorkplaceId);
        }

        [Test]
        public void CancelledTask_ReleasesTheBed()
        {
            Ctx c = Make(12, AdultBirth);
            BushRecord bed = c.W.Bushes.Add(105, 95, BushKind.ReedBed, 8);
            c.W.Step(1);
            TaskRecord t = Tasks(c.W).Single();
            c.W.Step(30 * 3 + 40);
            Assert.AreEqual(TaskState.Active, t.State);
            c.W.Tasks.Cancel(t.Id);
            c.W.Step(2);
            Assert.AreNotEqual(t.Id, bed.ReservedBy, "bed freed when the task ended (a new task may take it)");
            Assert.AreEqual(5, bed.Baskets, "three bundles were cut before the cancel");
        }
    }
}
