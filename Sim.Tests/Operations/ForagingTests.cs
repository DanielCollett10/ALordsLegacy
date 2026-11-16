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
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>Foraging camp: operation type, hut store, forage task and delivery (T-423, FD-22..24, FD-23b).</summary>
    public class ForagingTests
    {
        private const long AdultBirth = -4_000_000;   // about 15 game years
        private const long NineBirth = -2_500_000;    // about 9.5 game years
        private const long FiveBirth = -1_300_000;    // about 5 game years
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static MapData Map() =>
            new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);

        private sealed class Ctx
        {
            public SimWorld W;
            public MapData Map;
            public Operation Op;
            public Footprint Hut;
            public LedgerLocation HutStore;
        }

        // Flat 200 m map, 8:00 start, no scattered bushes. Area x 90..120, z 90..110, hut (100,100) with its 30 item store.
        private static Ctx Make(params long[] births)
        {
            MapData map = Map();
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8, BushCount = 0 });
            CommandResult r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) }), AreaTypes.ForagingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            var c = new Ctx { W = w, Map = map, Op = w.Operations.FindByArea((int)r.Created[0]) };
            c.Hut = w.Footprints.Add(1, ModuleTypes.ForagerHut, M(100, 100), 0);
            w.Operations.AddModule(c.Op.Id, 1);
            c.HutStore = w.Inventory.AddFoodStore(w.Tuning.ForagerHutCapacity, 1);
            w.People.AddFamily(1, "F", 100, 100);
            for (int i = 0; i < births.Length; i++) w.People.AddCitizen(10 + i, "C" + i, Sex.Male, births[i], 1, 100, 100);
            Assert.IsTrue(w.Execute(new AssignFamily(c.Op.Id, 1)).IsAccepted);
            return c;
        }

        private static Footprint AddGranary(Ctx c, int id, double x, double z, int items = 0)
        {
            Footprint g = c.W.Footprints.Add(id, ModuleTypes.Granary, M(x, z), 0);
            LedgerLocation s = c.W.Inventory.AddFoodStore(100, id);
            if (items > 0) c.W.Inventory.Produce(s, Items.Berries, items);
            return g;
        }

        private static int Foods(LedgerLocation l) => l.CountOf(Items.Berries.Id) + l.CountOf(Items.Mushrooms.Id);

        private static List<TaskRecord> Forage(SimWorld w) =>
            w.Tasks.All.Where(t => t.Def.Id == ForagingSystem.ForageTaskId).ToList();

        // ---- type and tuning ----

        [Test]
        public void ForagingType_HasHutOnlyRecipeEightLogs480Minutes_Circle300_DrawnAreaAllowed()
        {
            OperationTypeDef t = OperationTypes.Find(OperationTypes.ForagingId);
            Assert.AreSame(OperationTypes.Foraging, t);
            Assert.AreEqual(300, t.DefaultWorkRadiusM);
            Assert.AreEqual(1, t.HutCount);
            Assert.AreEqual(0, t.MinLogStorageCount);
            Assert.AreEqual(ModuleTypes.ForagerHutId, t.HutTypeId);
            Assert.AreEqual(8, ModuleTypes.ForagerHut.ConstructionRecipe.Logs);
            Assert.AreEqual(480, ModuleTypes.ForagerHut.ConstructionRecipe.BuildWorkerMinutes);
            Assert.AreSame(OperationTypes.Foraging, OperationTypes.FindByBuildingArea(AreaTypes.ForagingId));
            Assert.AreEqual(AreaKind.Work, AreaTypes.Find(t.WorkAreaTypeId).Kind);
            Assert.AreEqual(8, LordsLegacy.Sim.Construction.BuildCost.MinimumFor(t));
        }

        [Test]
        public void Tuning_Defaults()
        {
            var t = new SimTuning();
            Assert.AreEqual(8, t.ForagerMinAge);
            Assert.AreEqual(30, t.ForagerHutCapacity);
            Assert.AreEqual(30, t.GatherMinutesPerBasket);
            Assert.AreEqual(10, t.HandCarryItems);
        }

        [Test]
        public void Tuning_RoundTripsThroughCoreSection()
        {
            MapData map = Map();
            var w = SimWorld.FromMapData(map, new SimTuning { ForagerMinAge = 9, ForagerHutCapacity = 12, GatherMinutesPerBasket = 7, HandCarryItems = 4 });
            SimWorld back = SimSave.Read(SimSave.Write(w, "s", 1, map.MapId, map.ContentHash), map);
            Assert.AreEqual(9, back.Tuning.ForagerMinAge);
            Assert.AreEqual(12, back.Tuning.ForagerHutCapacity);
            Assert.AreEqual(7, back.Tuning.GatherMinutesPerBasket);
            Assert.AreEqual(4, back.Tuning.HandCarryItems);
        }

        [Test]
        public void TaskForage_IsInTheCatalog_AssignedFamilyOnly()
        {
            Assert.IsTrue(TaskDefCatalog.TryGet("task.forage", out EligibilityPolicy e, out RequiredResource r));
            Assert.AreEqual(EligibilityPolicy.AssignedFamilyOnly, e);
            Assert.AreEqual(RequiredResource.None, r);
        }

        // ---- designation (bt-3) ----

        private static List<ModuleSpec> HutOnly() => new List<ModuleSpec> { new ModuleSpec(ModuleTypes.ForagerHutId, M(100, 100), 0) };

        private static AreaShape Square() => AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) });

        [Test]
        public void DesignateOperation_HutOnly_IsAccepted_AndHutHasA30ItemFoodStore()
        {
            var w = SimWorld.FromMapData(Map(), new SimTuning { BushCount = 0 });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(0, 0)), Items.Log, 8);
            CommandResult r = w.Execute(new DesignateOperation(Square(), OperationTypes.ForagingId, HutOnly()));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int hutId = (int)r.Created[1];
            LedgerLocation store = w.Inventory.FindFoodStore(hutId);
            Assert.NotNull(store);
            Assert.AreEqual(30, store.Capacity);
            Assert.IsFalse(w.IsBuilt(hutId));
        }

        [Test]
        public void DesignateOperation_WithoutHut_IsRefused()
        {
            var w = SimWorld.FromMapData(Map(), new SimTuning { BushCount = 0 });
            CommandResult r = w.Execute(new DesignateOperation(Square(), OperationTypes.ForagingId, new List<ModuleSpec>()));
            Assert.IsTrue(r.IsRejected);
            StringAssert.Contains("Hut", r.Text);
        }

        [Test]
        public void DesignateOperation_LogStorageIsNotAllowedInAForagingCamp()
        {
            var w = SimWorld.FromMapData(Map(), new SimTuning { BushCount = 0 });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(0, 0)), Items.Log, 8);
            var list = HutOnly();
            list.Add(new ModuleSpec(ModuleTypes.LogStorageId, M(110, 100), 0));
            Assert.IsTrue(w.Execute(new DesignateOperation(Square(), OperationTypes.ForagingId, list)).IsRejected);
        }

        [Test]
        public void ForagerHutNotBuilt_NoDeliveryTarget_CampShowsMissingHut_NoTasks()
        {
            var w = SimWorld.FromMapData(Map(), new SimTuning { StartHour = 8, BushCount = 0 });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(0, 0)), Items.Log, 8);
            CommandResult r = w.Execute(new DesignateOperation(Square(), OperationTypes.ForagingId, HutOnly()));
            Operation op = w.Operations.FindByArea((int)r.Created[0]);
            w.Bushes.Add(105, 95, BushKind.Berries, 3);
            Assert.AreEqual(OperationState.MissingHut, OperationStatus.Of(w, op));
            w.Step(60);
            Assert.AreEqual(0, Forage(w).Count);
        }

        // ---- gathering and delivery ----

        [Test]
        public void Adult_PicksThreeBaskets_DeliversToOwnHut()
        {
            Ctx c = Make(AdultBirth);
            c.W.Bushes.Add(105, 95, BushKind.Berries, 3);
            c.W.Step(400);
            Assert.AreEqual(3, c.HutStore.CountOf(Items.Berries.Id));
            Assert.IsNull(c.W.Inventory.Audit());
            TaskRecord done = Forage(c.W).First(t => t.State == TaskState.Done);
            Assert.AreEqual(1 + 3 + 2, done.Def.Steps.Count);
            Assert.IsFalse(done.Def.Steps.Any(s => s.Heavy));
        }

        [Test]
        public void MushroomBush_GivesMushrooms()
        {
            Ctx c = Make(AdultBirth);
            c.W.Bushes.Add(105, 95, BushKind.Mushrooms, 2);
            c.W.Step(400);
            Assert.AreEqual(2, c.HutStore.CountOf(Items.Mushrooms.Id));
        }

        [Test]
        public void Child9_Forages_Child5_DoesNot()
        {
            Ctx c = Make(NineBirth);
            c.W.Bushes.Add(105, 95, BushKind.Berries, 3);
            c.W.Step(400);
            Assert.AreEqual(3, Foods(c.HutStore));

            Ctx d = Make(FiveBirth);
            d.W.Bushes.Add(105, 95, BushKind.Berries, 3);
            d.W.Step(400);
            Assert.AreEqual(0, Foods(d.HutStore));
            Assert.AreEqual(0, Forage(d.W).Count, "no forager of age 8 or more in the family");
            Assert.AreEqual(OperationState.NoWorkers, OperationStatus.Of(d.W, d.Op));
        }

        [Test]
        public void YoungChildInAMixedFamily_NeverTakesTheTask()
        {
            Ctx c = Make(FiveBirth, AdultBirth);
            c.W.Bushes.Add(105, 95, BushKind.Berries, 3);
            c.W.Step(400);
            foreach (TaskRecord t in Forage(c.W)) Assert.AreNotEqual(10, t.AssigneeId, "the 5-year-old must not forage");
            Assert.AreEqual(3, Foods(c.HutStore));
        }

        [Test]
        public void CarryLimit_CapsBasketsPerTrip()
        {
            Ctx c = Make(AdultBirth);
            c.W.Tuning.HandCarryItems = 2;
            c.W.Bushes.Add(105, 95, BushKind.Berries, 3);
            c.W.Step(1);
            TaskRecord t = Forage(c.W).First();
            Assert.AreEqual(2, t.Def.Steps.Count(s => s.Kind == StepKind.Work));
            Assert.AreEqual(2, t.Def.Steps.Last().Count);
        }

        [Test]
        public void GranaryNextToTheBush_GetsNothing_HutReceivesTheFood()
        {
            Ctx c = Make(AdultBirth);
            Footprint g = AddGranary(c, 2, 110, 70);
            c.W.Bushes.Add(112, 60, BushKind.Berries, 3);   // bush sits next to the granary (decision 0044)
            c.W.Step(500);
            Assert.AreEqual(0, Foods(c.W.Inventory.FindFoodStore(g.Id)), "foragers never carry to a granary");
            Assert.AreEqual(3, Foods(c.HutStore));
        }

        [Test]
        public void TwoGranariesWithRoom_NearerThanHut_GetNothing()
        {
            Ctx c = Make(AdultBirth);
            AddGranary(c, 2, 110, 70);
            AddGranary(c, 3, 104, 98);
            c.W.Bushes.Add(112, 60, BushKind.Berries, 3);
            c.W.Step(500);
            Assert.AreEqual(0, Foods(c.W.Inventory.FindFoodStore(2)));
            Assert.AreEqual(0, Foods(c.W.Inventory.FindFoodStore(3)));
            Assert.AreEqual(3, Foods(c.HutStore));
        }

        [Test]
        public void HutFull_WithGranaryWithRoom_StorageFull_NoNewTasks()
        {
            Ctx c = Make(AdultBirth);
            c.W.Inventory.Produce(c.HutStore, Items.Meat, 30);
            AddGranary(c, 2, 160, 160);
            BushRecord b = c.W.Bushes.Add(105, 95, BushKind.Berries, 3);
            c.W.Step(700);
            Assert.AreEqual(OperationState.StorageFull, OperationStatus.Of(c.W, c.Op));
            Assert.AreEqual(0, Forage(c.W).Count, "a granary with room does not open a forage task (FD-23b)");
            Assert.AreEqual(0, Foods(c.W.Inventory.FindFoodStore(2)));
            Assert.AreEqual(3, b.Baskets);
        }

        [Test]
        public void UnbuiltGranary_IsNotATarget()
        {
            Ctx c = Make(AdultBirth);
            CommandResult r = c.W.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(110, 70), 0));
            if (r.IsAccepted) Assert.IsFalse(c.W.IsBuilt((int)r.Created[0]));
            c.W.Bushes.Add(112, 60, BushKind.Berries, 3);
            c.W.Step(500);
            Assert.AreEqual(3, Foods(c.HutStore), "an unbuilt granary takes nothing");
        }

        [Test]
        public void NeverDeliversToTheCampFoodStore()
        {
            Ctx c = Make(AdultBirth);
            LedgerLocation camp = c.W.Inventory.AddFoodStore(M(100, 100));
            c.W.Bushes.Add(105, 95, BushKind.Berries, 3);
            c.W.Step(400);
            Assert.AreEqual(0, Foods(camp));
            Assert.AreEqual(3, Foods(c.HutStore));
        }

        [Test]
        public void HutFullAndGranaryFull_StorageFull_NoGathering_BushUntouched()
        {
            Ctx c = Make(AdultBirth);
            c.W.Inventory.Produce(c.HutStore, Items.Meat, 30);
            AddGranary(c, 2, 160, 160, 100);
            BushRecord b = c.W.Bushes.Add(105, 95, BushKind.Berries, 3);
            c.W.Step(300);
            Assert.AreEqual(OperationState.StorageFull, OperationStatus.Of(c.W, c.Op));
            Assert.AreEqual(0, Forage(c.W).Count);
            Assert.AreEqual(3, b.Baskets);
        }

        [Test]
        public void HutHoldsOnlyFood_RefusesLogs()
        {
            Ctx c = Make(AdultBirth);
            Assert.Throws<System.InvalidOperationException>(() => c.W.Inventory.Produce(c.HutStore, Items.Log, 1));
        }

        [Test]
        public void TwoForagers_NeverOverfillTheHutStore()
        {
            Ctx c = Make(AdultBirth, AdultBirth);
            c.W.Inventory.Produce(c.HutStore, Items.Meat, 27);
            c.W.Bushes.Add(105, 95, BushKind.Berries, 3);
            c.W.Bushes.Add(106, 95, BushKind.Berries, 3);
            c.W.Step(700);
            Assert.LessOrEqual(c.HutStore.Total, 30);
            Assert.AreEqual(30, c.HutStore.Total);
            Assert.IsNull(c.W.Inventory.Audit());
        }

        // ---- cancel and persistence ----

        [Test]
        public void CancelMidCarry_DropsFoodOnTheGround_ReleasesTheBush()
        {
            Ctx c = Make(AdultBirth);
            BushRecord b = c.W.Bushes.Add(105, 95, BushKind.Berries, 3);
            TaskRecord t = null;
            for (int i = 0; i < 600 && (t == null || !t.CarrierId.HasValue || c.W.Inventory.Get(t.CarrierId.Value).Total == 0); i++)
            {
                c.W.Step(1);
                t = Forage(c.W).FirstOrDefault();
            }
            Assert.NotNull(t);
            Assert.IsTrue(c.W.Inventory.Get(t.CarrierId.Value).Total > 0, "carrying");
            Assert.IsTrue(c.W.Execute(new UnassignFamily(c.Op.Id, 1)).IsAccepted);
            c.W.Step(2);
            Assert.AreEqual(0, c.W.Inventory.Get(t.CarrierId.Value).Total);
            Assert.AreEqual(0, c.HutStore.Total);
            Assert.IsNull(b.ReservedBy);
            Assert.IsNull(c.W.Inventory.Audit());
            Assert.Greater(c.W.Inventory.Locations.Where(l => l.Kind == LocationKind.GroundPile).Sum(l => l.Total), 0);
        }

        [Test]
        public void RoundTripMidCarry_ThenFinishesDelivering()
        {
            Ctx c = Make(AdultBirth);
            c.W.Bushes.Add(105, 95, BushKind.Berries, 3);
            TaskRecord t = null;
            for (int i = 0; i < 600; i++)
            {
                c.W.Step(1);
                t = Forage(c.W).FirstOrDefault();
                if (t != null && t.CarrierId.HasValue && c.W.Inventory.Get(t.CarrierId.Value).Total >= 2) break;
            }
            Assert.IsNotNull(t);
            int carried = c.W.Inventory.Get(t.CarrierId.Value).Total;
            Assert.GreaterOrEqual(carried, 2);

            byte[] bytes = SimSave.Write(c.W, "s", 1, c.Map.MapId, c.Map.ContentHash);
            SimWorld back = SimSave.Read(bytes, c.Map);
            TaskRecord bt = back.Tasks.Get(t.Id);
            Assert.AreEqual(t.BushId, bt.BushId);
            Assert.AreEqual(t.CarrierId, bt.CarrierId);
            Assert.AreEqual(t.CurrentStep, bt.CurrentStep);
            Assert.AreEqual(carried, back.Inventory.Get(bt.CarrierId.Value).Total);
            Assert.AreEqual(t.Id, back.Bushes.All.First().ReservedBy);
            Assert.IsNull(back.Inventory.Audit());

            back.Step(600);
            LedgerLocation store = back.Inventory.FindFoodStore(1);
            Assert.AreEqual(3, Foods(store));
            Assert.AreEqual(0, back.Inventory.Get(bt.CarrierId.Value).Total);
        }

        [Test]
        public void SaveVersion_IsEleven_VersionTenBytesAreRefused()
        {
            Assert.AreEqual(13, SaveWriter.CurrentVersion);
            byte[] bytes = { (byte)'L', (byte)'L', (byte)'S', (byte)'V', 10, 0, 0, 0, 0, 0, 0, 0 };
            var e = Assert.Throws<SaveFormatException>(() => SaveReader.ReadHeaderOnly(bytes));
            StringAssert.Contains("version 10 (this build reads version 13)", e.Message);
        }

        // ---- the whole pipeline through real commands and ticks ----

        [Test]
        public void FullPipeline_Designate_Deliver_Build_Assign_Forage_Store()
        {
            MapData map = Map();
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8, BushCount = 0 });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(95, 95)), Items.Log, 8);
            w.People.AddFamily(2, "Builders", 100, 100);
            for (int i = 0; i < 4; i++) w.People.AddCitizen(20 + i, "B" + i, Sex.Male, AdultBirth, 2, 100, 100);
            for (int i = 0; i < 2; i++) w.People.AddOx(50 + i, "O" + i, 100, 120).State = OxState.Resting(100000, 120000);

            CommandResult r = w.Execute(new DesignateOperation(Square(), OperationTypes.ForagingId, HutOnly()));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Operation op = w.Operations.FindByArea((int)r.Created[0]);
            int hutId = (int)r.Created[1];
            for (int i = 0; i < 4 * 1440 && !w.IsBuilt(hutId); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(hutId), "the hut gets built by helpers");

            w.People.AddFamily(1, "Foragers", 100, 100);
            w.People.AddCitizen(10, "Forager", Sex.Female, AdultBirth, 1, 100, 100);
            w.People.AddCitizen(11, "Kid", Sex.Male, NineBirth, 1, 100, 100);
            Assert.IsTrue(w.Execute(new AssignFamily(op.Id, 1)).IsAccepted);
            w.Bushes.Add(105, 95, BushKind.Berries, 3);
            w.Bushes.Add(95, 105, BushKind.Mushrooms, 3);

            for (int i = 0; i < 3 * 1440; i++) w.Step(1);
            LedgerLocation store = w.Inventory.FindFoodStore(hutId);
            Assert.Greater(Foods(store), 0, "foragers fill the hut store");
            Assert.LessOrEqual(Foods(store), 30);
            Assert.IsNull(w.Inventory.Audit());
        }
    }
}
