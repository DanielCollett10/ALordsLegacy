using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Coppice;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Foraging;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Operations.Workshops;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>Coppice hut: operation type, rod store, cutting task, status, regrow (T-556, MAT-10..18).</summary>
    public class CoppiceTests
    {
        private const long AdultBirth = -4_000_000;
        private const long NineBirth = -2_500_000;
        private const long FiveBirth = -1_300_000;
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

        private static Ctx Make(params long[] births)
        {
            var w = SimWorld.FromMapData(Map(), new SimTuning { StartHour = 8, BushCount = 0, HazelStoolCount = 0, WillowStoolCount = 0 });
            CommandResult r = w.Execute(new DesignateArea(Square(), AreaTypes.CoppiceId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            var c = new Ctx { W = w, Op = w.Operations.FindByArea((int)r.Created[0]) };
            c.Hut = w.Footprints.Add(1, ModuleTypes.CoppiceHut, M(100, 100), 0);
            w.Operations.AddModule(c.Op.Id, 1);
            WorkshopBuildings.OnBuilt(w, c.Hut);
            c.Store = w.Inventory.FindGoodsStore(1);
            w.People.AddFamily(1, "F", 100, 100);
            for (int i = 0; i < births.Length; i++) w.People.AddCitizen(10 + i, "C" + i, Sex.Male, births[i], 1, 100, 100);
            Assert.IsTrue(w.Execute(new AssignFamily(c.Op.Id, 1)).IsAccepted);
            return c;
        }

        private static int Hazel(Ctx c) => c.Store.CountOf(Items.HazelRod.Id);
        private static int Willow(Ctx c) => c.Store.CountOf(Items.WillowRod.Id);
        private static List<TaskRecord> Tasks(SimWorld w) => w.Tasks.All.Where(t => t.Def.Id == CoppiceSystem.CoppiceTaskId).ToList();

        // ---- type ----

        [Test]
        public void CoppiceType_HasHutRecipe8Logs480Minutes_Circle300_And6x4Hut()
        {
            OperationTypeDef t = OperationTypes.Find(OperationTypes.CoppiceId);
            Assert.AreSame(OperationTypes.Coppice, t);
            Assert.AreEqual(300, t.DefaultWorkRadiusM);
            Assert.AreEqual(ModuleTypes.CoppiceHutId, t.HutTypeId);
            Assert.AreEqual(8, ModuleTypes.CoppiceHut.ConstructionRecipe.Logs);
            Assert.AreEqual(480, ModuleTypes.CoppiceHut.ConstructionRecipe.BuildWorkerMinutes);
            Assert.AreEqual(6000, ModuleTypes.CoppiceHut.WidthMm);
            Assert.AreEqual(4000, ModuleTypes.CoppiceHut.DepthMm);
            Assert.AreSame(OperationTypes.Coppice, OperationTypes.FindByBuildingArea(AreaTypes.CoppiceId));
            Assert.AreEqual(AreaKind.Work, AreaTypes.Find(t.WorkAreaTypeId).Kind);
        }

        [Test]
        public void HutStore_IsCappedFortyPerKind_AndTaskIsInCatalog()
        {
            Ctx c = Make(AdultBirth);
            Assert.AreEqual(40, c.Store.ItemCapOf(Items.HazelRod.Id));
            Assert.AreEqual(40, c.Store.ItemCapOf(Items.WillowRod.Id));
            Assert.IsTrue(TaskDefCatalog.TryGet(CoppiceSystem.CoppiceTaskId, out EligibilityPolicy e, out RequiredResource r));
            Assert.AreEqual(EligibilityPolicy.AssignedFamilyOnly, e);
        }

        // ---- cutting ----

        [Test]
        public void Adult_CutsTenRods_SixMinutesEach_DeliversToHutStore_StoolLeavesEmptyThenReleased()
        {
            Ctx c = Make(AdultBirth);
            BushRecord s = c.W.Bushes.Add(105, 95, BushKind.HazelStool, 10);
            c.W.Step(600);
            Assert.AreEqual(10, Hazel(c));
            Assert.AreEqual(0, s.Baskets);
            Assert.IsFalse(s.ReservedBy.HasValue);
            TaskRecord done = Tasks(c.W).First(t => t.State == TaskState.Done);
            Assert.AreEqual(1 + 10 + 2, done.Def.Steps.Count);
            Assert.IsFalse(done.Def.Steps.Any(x => x.Heavy));
            Assert.AreEqual(6, done.Def.Steps.First(x => x.Kind == StepKind.Work).DurationTicks);
            Assert.IsNull(c.W.Inventory.Audit());
        }

        [Test]
        public void Child9_Cuts_Child5_DoesNot()
        {
            Ctx a = Make(NineBirth);
            a.W.Bushes.Add(105, 95, BushKind.HazelStool, 10);
            a.W.Step(600);
            Assert.AreEqual(10, Hazel(a));
            Ctx b = Make(FiveBirth);
            b.W.Bushes.Add(105, 95, BushKind.HazelStool, 10);
            b.W.Step(600);
            Assert.AreEqual(0, Tasks(b.W).Count);
            Assert.AreEqual(OperationState.NoWorkers, OperationStatus.Of(b.W, b.Op));
        }

        // ---- kind choice ----

        [Test]
        public void ChoosesTheKindTheHutIsShortestOf()
        {
            Ctx c = Make(AdultBirth);
            c.W.Inventory.Produce(c.Store, Items.HazelRod, 5);
            c.W.Bushes.Add(101, 95, BushKind.HazelStool, 10);   // nearer
            c.W.Bushes.Add(112, 95, BushKind.WillowStool, 10);  // farther, but hut has no willow
            c.W.Step(1);
            TaskRecord t = Tasks(c.W).Single();
            Assert.AreEqual(BushKind.WillowStool, c.W.Bushes.All.First(b => b.ReservedBy == t.Id).Kind);
        }

        [Test]
        public void EqualStock_TakesHazelFirst_AndNearestStoolOfTheKind()
        {
            Ctx c = Make(AdultBirth);
            c.W.Bushes.Add(115, 95, BushKind.HazelStool, 10);
            BushRecord near = c.W.Bushes.Add(104, 95, BushKind.HazelStool, 10);
            c.W.Bushes.Add(101, 95, BushKind.WillowStool, 10);
            c.W.Step(1);
            Assert.AreEqual(Tasks(c.W).Single().Id, near.ReservedBy);
        }

        [Test]
        public void TwoCutters_ReserveDifferentStools()
        {
            Ctx c = Make(AdultBirth, AdultBirth);
            c.W.Bushes.Add(104, 95, BushKind.HazelStool, 10);
            c.W.Bushes.Add(105, 95, BushKind.HazelStool, 10);
            c.W.Step(1);
            Assert.AreEqual(2, c.W.Bushes.All.Count(b => b.ReservedBy.HasValue));
        }

        // ---- store full ----

        [Test]
        public void KindFull_OnlyStopsThatKind_CutsTheOtherAndStatusStaysWorking()
        {
            Ctx c = Make(AdultBirth);
            c.W.Inventory.Produce(c.Store, Items.HazelRod, 40);
            c.W.Bushes.Add(101, 95, BushKind.HazelStool, 10);
            c.W.Bushes.Add(112, 95, BushKind.WillowStool, 10);
            Assert.AreEqual(OperationState.Working, OperationStatus.Of(c.W, c.Op));
            c.W.Step(600);
            Assert.AreEqual(40, Hazel(c));
            Assert.AreEqual(10, Willow(c));
        }

        [Test]
        public void AllKindsFull_StatusIsStorageFull_NoTask()
        {
            Ctx c = Make(AdultBirth);
            c.W.Inventory.Produce(c.Store, Items.HazelRod, 40);
            c.W.Inventory.Produce(c.Store, Items.WillowRod, 40);
            c.W.Bushes.Add(101, 95, BushKind.HazelStool, 10);
            c.W.Bushes.Add(112, 95, BushKind.WillowStool, 10);
            Assert.AreEqual(OperationState.StorageFull, OperationStatus.Of(c.W, c.Op));
            c.W.Step(30);
            Assert.AreEqual(0, Tasks(c.W).Count);
        }

        [Test]
        public void RoomCapsTheCut_AfterPendingDeposits()
        {
            Ctx c = Make(AdultBirth, AdultBirth);
            c.W.Inventory.Produce(c.Store, Items.HazelRod, 35);
            c.W.Bushes.Add(101, 95, BushKind.HazelStool, 10);
            c.W.Bushes.Add(102, 95, BushKind.HazelStool, 10);
            c.W.Step(1);
            var counts = Tasks(c.W).Select(t => t.Def.Steps.Count(x => x.Kind == StepKind.Work)).OrderBy(x => x).ToList();
            Assert.AreEqual(new[] { 5 }, counts, "5 rods of room are promised once; the second cutter gets nothing");
        }

        // ---- other statuses ----

        [Test]
        public void Status_NoStools_Paused_MissingHut()
        {
            Ctx c = Make(AdultBirth);
            Assert.AreEqual(OperationState.NoStools, OperationStatus.Of(c.W, c.Op));
            c.W.Bushes.Add(105, 95, BushKind.HazelStool, 10);
            Assert.AreEqual(OperationState.Working, OperationStatus.Of(c.W, c.Op));
            c.W.Bushes.Add(300, 300, BushKind.Berries, 5); // not a stool: ignored
            c.Op.Paused = true;
            Assert.AreEqual(OperationState.Paused, OperationStatus.Of(c.W, c.Op));
            Ctx d = Make(AdultBirth);
            d.W.Operations.RemoveModule(d.Op.Id, 1);
            Assert.AreEqual(OperationState.MissingHut, OperationStatus.Of(d.W, d.Op));
        }

        [Test]
        public void StoolOutsideDefaultCircle_IsIgnored()
        {
            Ctx c = Make(AdultBirth);
            c.W.Bushes.Add(900, 900, BushKind.HazelStool, 10); // far outside the 300 m circle
            Assert.AreEqual(OperationState.NoStools, OperationStatus.Of(c.W, c.Op));
        }

        // ---- ended mid-carry ----

        [Test]
        public void CancelledMidCarry_DropsRodsAsOperationPileAtTheStool_StoolReleased()
        {
            Ctx c = Make(AdultBirth);
            BushRecord s = c.W.Bushes.Add(105, 95, BushKind.HazelStool, 10);
            TaskRecord t = null;
            for (int i = 0; i < 400 && (t == null || t.CarrierId == null || c.W.Inventory.Get(t.CarrierId.Value).Total < 3); i++)
            {
                c.W.Step(1);
                t = Tasks(c.W).FirstOrDefault();
            }
            Assert.NotNull(t);
            Assert.GreaterOrEqual(c.W.Inventory.Get(t.CarrierId.Value).Total, 3);
            c.W.Operations.RemoveModule(c.Op.Id, 1); // hut gone: the task cannot deliver and ends
            c.W.Tasks.Cancel(t.Id);
            c.W.Step(2);
            Assert.AreEqual(0, c.W.Inventory.Get(t.CarrierId.Value).Total);
            LedgerLocation pile = c.W.Inventory.Locations.First(l => l.Kind == LocationKind.GroundPile && l.CountOf(Items.HazelRod.Id) > 0);
            Assert.AreEqual(c.Op.Id, pile.OwnerOperationId);
            Assert.IsFalse(s.ReservedBy.HasValue);
            Assert.IsNull(c.W.Inventory.Audit());
        }

        [Test]
        public void WalkingHomeWhenItEnds_DropsAtTheHut()
        {
            Ctx c = Make(AdultBirth);
            BushRecord s = c.W.Bushes.Add(105, 95, BushKind.HazelStool, 2);
            TaskRecord t = null;
            for (int i = 0; i < 400; i++)
            {
                c.W.Step(1);
                t = Tasks(c.W).FirstOrDefault();
                if (t != null && t.CurrentStep == 3) break; // WalkTo hut
            }
            Assert.AreEqual(3, t.CurrentStep);
            c.W.Tasks.Cancel(t.Id);
            c.W.Step(2);
            LedgerLocation pile = c.W.Inventory.Locations.First(l => l.Kind == LocationKind.GroundPile && l.CountOf(Items.HazelRod.Id) == 2);
            Assert.AreEqual(c.Hut.Centre.XMetres, pile.Position.XMetres, 0.01);
        }

        // ---- regrow ----

        [Test]
        public void EmptiedStool_RegrowsAfterRegrowDays_AndIsCutAgain()
        {
            Ctx c = Make(AdultBirth);
            BushRecord s = c.W.Bushes.Add(105, 95, BushKind.HazelStool, 10);
            c.W.Step(600);
            Assert.AreEqual(0, s.Baskets);
            Assert.IsFalse(s.ReservedBy.HasValue);
            // The cutter would starve over 30 simulated days, so the regrow clock is driven at a chosen tick.
            long wait = 30L * 1440;
            Assert.AreEqual(0, c.W.Bushes.Regrow(s.EmptiedAtTick + wait - 1, c.W.Tuning), "not yet");
            Assert.AreEqual(1, c.W.Bushes.Regrow(s.EmptiedAtTick + wait, c.W.Tuning));
            Assert.AreEqual(10, s.Baskets);
            c.W.Step(1440);
            Assert.AreEqual(20, Hazel(c), "regrown stool is cut again");
        }

        // ---- save ----

        [Test]
        public void SaveMidTask_RoundTrips_AndFinishes()
        {
            Ctx c = Make(AdultBirth);
            c.W.Bushes.Add(105, 95, BushKind.HazelStool, 10);
            c.W.Step(120);
            MapData map = Map();
            SimWorld back = SimSave.Read(SimSave.Write(c.W, "s", 1, map.MapId, map.ContentHash), map);
            back.Step(600);
            Assert.AreEqual(10, back.Inventory.FindGoodsStore(1).CountOf(Items.HazelRod.Id));
            Assert.AreEqual(40, back.Inventory.FindGoodsStore(1).ItemCapOf(Items.HazelRod.Id));
        }

        // ---- full pipeline: designate, deliver, build, assign, cut, store ----

        [Test]
        public void FullPipeline_Designate_Build_Assign_Cut_Store_NoShortcuts()
        {
            var w = SimWorld.FromMapData(Map(), new SimTuning { StartHour = 8, BushCount = 0, HazelStoolCount = 0, WillowStoolCount = 0 });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(95, 95)), Items.Log, 8);
            w.People.AddFamily(2, "Builders", 100, 100);
            for (int i = 0; i < 4; i++) w.People.AddCitizen(20 + i, "B" + i, Sex.Male, AdultBirth, 2, 100, 100);
            for (int i = 0; i < 2; i++) w.People.AddOx(50 + i, "O" + i, 100, 120).State = OxState.Resting(100000, 120000);

            var hut = new List<ModuleSpec> { new ModuleSpec(ModuleTypes.CoppiceHutId, M(100, 100), 0) };
            CommandResult r = w.Execute(new DesignateOperation(Square(), OperationTypes.CoppiceId, hut));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Operation op = w.Operations.FindByArea((int)r.Created[0]);
            int hutId = (int)r.Created[1];
            for (int i = 0; i < 4 * 1440 && !w.IsBuilt(hutId); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(hutId), "the hut gets built by helpers");
            LedgerLocation store = w.Inventory.FindGoodsStore(hutId);
            Assert.NotNull(store, "built hut has its rod store");

            w.People.AddFamily(1, "Cutters", 100, 100);
            w.People.AddCitizen(10, "Cutter", Sex.Female, AdultBirth, 1, 100, 100);
            Assert.IsTrue(w.Execute(new AssignFamily(op.Id, 1)).IsAccepted);
            w.Bushes.Add(105, 95, BushKind.HazelStool, 10);
            w.Bushes.Add(95, 105, BushKind.WillowStool, 10);

            for (int i = 0; i < 3 * 1440; i++) w.Step(1);
            Assert.Greater(store.CountOf(Items.HazelRod.Id), 0);
            Assert.Greater(store.CountOf(Items.WillowRod.Id), 0, "the shorter kind is cut next");
            Assert.LessOrEqual(store.CountOf(Items.HazelRod.Id), 40);
            Assert.IsNull(w.Inventory.Audit());
        }
    }
}
