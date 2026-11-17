using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Growth;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Markets;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Markets
{
    /// <summary>A market stall's planks are carried by its own granary family, not odd-jobbers (T-518, PL-16, MK-24).</summary>
    public class StallCarryTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private sealed class Fx
        {
            public SimWorld W; public MapData Map; public int GranaryOp, Stall;
            public LedgerLocation Goods;
            public ConstructionSite Site => W.Construction.Get(Stall);
            public LedgerLocation SiteLoc => W.Inventory.FindSite(Stall);
        }

        // Granary built and supplying; family 2 (two adults) staffs a stall site that needs `planks` planks; family 3
        // (two odd-jobbers) stands right at the stall; 10 planks in a General goods store.
        private static Fx Make(int planks, bool staffed = true, bool savable = false)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8, BushCount = 0 });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 40);
            w.People.AddFamily(2, "F2", 40, 40);
            for (int i = 0; i < 2; i++) w.People.AddCitizen(20 + i, "C" + i, Sex.Male, AdultBirth, 2, 40, 40);
            w.People.AddFamily(3, "F3", 40, 40);
            for (int i = 0; i < 2; i++) w.People.AddCitizen(30 + i, "H" + i, Sex.Male, AdultBirth, 3, 40, 40);
            Ox ox = w.People.AddOx(50, "O", 40, 44);
            ox.State = OxState.Resting(40000, 44000);
            var shape = AreaShape.Straight(new[] { M(10, 10), M(30, 10), M(30, 30), M(10, 30) });
            Assert.IsTrue(w.Execute(new DesignateArea(shape, AreaTypes.MarketId)).IsAccepted);
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(60, 60), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int granary = (int)r.Created[0];
            for (int i = 0; i < 6000 && !w.IsBuilt(granary); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(granary));
            int op = w.Operations.FindByModule(granary).Id;
            Assert.IsTrue(w.Execute(new SetGranarySupplies(op, true)).IsAccepted);

            var f = new Fx { W = w, Map = map, GranaryOp = op, Goods = w.Inventory.AddGoodsStore(M(80, 80)) };
            w.Inventory.Produce(f.Goods, Items.Plank, 10);
            // A stall site made of planks (a stall costs logs today; planks are what this task carries).
            var type = new FootprintTypeDef(ModuleTypes.MarketStallId, 3000, 2000, false, false,
                recipe: new ConstructionRecipe(new Dictionary<string, int> { { Items.Plank.Id, planks } }, 100000), foodCapacity: 30);
            Footprint fp = w.Footprints.Add(w.Footprints.NextId(), type, M(20, 20), 0);
            w.Construction.Add(new ConstructionSite(fp.Id, w.Inventory.GetOrAddSite(fp.Id).Id));
            w.Construction.Get(fp.Id).ReservationIds.AddRange(SiteMaterials.TryReserveAll(w, type.ConstructionRecipe, fp.Centre, fp.Id));
            w.Inventory.AddFoodStore(type.FoodCapacity, fp.Id);
            f.Stall = fp.Id;
            Market m = w.Markets.Current;
            m.StallFootprintIds.Add(fp.Id);
            if (staffed)
            {
                m.SetFamily(fp.Id, 2);
                Assert.IsTrue(w.Execute(new AssignFamily(op, 2)).IsAccepted);
            }
            return f;
        }

        private static List<TaskRecord> Carries(SimWorld w) =>
            w.Tasks.All.Where(t => t.Def.Id == ConstructionTaskDefs.CarryId).ToList();

        private static int OnSite(Fx f) => ConstructionProgress.DeliveredOf(f.W, f.Site, Items.Plank.Id);

        [Test]
        public void StallCarry_OpensAssignedFamilyOnly_OwnedByTheGranaryOperation_NotHeavy()
        {
            Fx f = Make(2, staffed: true);
            f.W.Step(1);
            TaskRecord t = Carries(f.W).Single();
            Assert.AreEqual(EligibilityPolicy.AssignedFamilyOnly, t.Def.Eligibility);
            Assert.AreEqual(f.GranaryOp, t.OwnerOperationId);
            Assert.IsFalse(t.Def.Steps.Any(s => s.Heavy), "light, so age 8+ like stock_stall");
        }

        [Test]
        public void StallCarry_GoesOnlyToTheStallsGranaryFamily_OddJobberNearbyNeverTakesIt()
        {
            Fx f = Make(4, staffed: true);
            foreach (Citizen c in f.W.People.Citizens.Where(c => c.FamilyId == 3)) { c.X = 20; c.Z = 20; } // odd-jobbers at the stall
            var takers = new HashSet<int>();
            for (int i = 0; i < 4000 && OnSite(f) < 4; i++)
            {
                f.W.Step(1);
                foreach (TaskRecord t in Carries(f.W))
                    if (t.AssigneeId.HasValue) takers.Add(f.W.People.FindCitizen(t.AssigneeId.Value).FamilyId);
                Assert.IsNull(f.W.Inventory.Audit(), "tick " + i);
            }
            Assert.AreEqual(4, OnSite(f), "all planks delivered");
            CollectionAssert.AreEquivalent(new[] { 2 }, takers, "only the staffing family carried");
        }

        [Test]
        public void StallCarry_NobodyOfTheFamilyAround_OddJobberStillNeverTakesIt()
        {
            Fx f = Make(2, staffed: true);
            f.W.Step(1);
            foreach (Citizen c in f.W.People.Citizens.Where(c => c.FamilyId == 2).ToList()) f.W.People.RemoveCitizen(c.Id);
            foreach (Citizen c in f.W.People.Citizens.Where(c => c.FamilyId == 3)) { c.X = 20; c.Z = 20; }
            f.W.Step(300);
            TaskRecord t = Carries(f.W).Single(x => !x.IsFinished);
            Assert.AreEqual(TaskState.Open, t.State);
            Assert.IsNull(t.AssigneeId);
        }

        [Test]
        public void StallCarry_ChildOf8_Takes_ChildUnder8_DoesNot()
        {
            Fx f = Make(2, staffed: true);
            foreach (Citizen c in f.W.People.Citizens.Where(c => c.FamilyId == 2).ToList()) f.W.People.RemoveCitizen(c.Id);
            long under = f.W.Tick - (long)(7.0 * 365 * 1440 / f.W.Tuning.AgeingRate);
            f.W.People.AddCitizen(40, "Little", Sex.Male, under, 2, 20, 20);
            f.W.Step(300);
            Assert.AreEqual(TaskState.Open, Carries(f.W).Single(x => !x.IsFinished).State, "a child under 8 takes none");
            long over = f.W.Tick - (long)(9.0 * 365 * 1440 / f.W.Tuning.AgeingRate);
            f.W.People.AddCitizen(41, "Older", Sex.Male, over, 2, 20, 20);
            for (int i = 0; i < 3000 && OnSite(f) < 2; i++) f.W.Step(1);
            Assert.AreEqual(2, OnSite(f), "the 9-year-old carries");
        }

        [Test]
        public void UnstaffedStall_CarryGoesToOddJobbers()
        {
            Fx f = Make(2, staffed: false);
            f.W.Step(1);
            TaskRecord t = Carries(f.W).Single();
            Assert.AreEqual(EligibilityPolicy.HelpersOnly, t.Def.Eligibility);
            for (int i = 0; i < 4000 && OnSite(f) < 2; i++) f.W.Step(1);
            Assert.AreEqual(2, OnSite(f));
        }

        [Test]
        public void StallThatLosesItsFamily_OpenCarryReopensForOddJobbers_AuditClean()
        {
            Fx f = Make(2, staffed: true);
            foreach (Citizen c in f.W.People.Citizens.Where(c => c.FamilyId == 2).ToList()) f.W.People.RemoveCitizen(c.Id);
            f.W.Step(1);
            Assert.AreEqual(EligibilityPolicy.AssignedFamilyOnly, Carries(f.W).Single(x => !x.IsFinished).Def.Eligibility);
            Assert.IsTrue(f.W.Execute(new UnassignFamily(f.GranaryOp, 2)).IsAccepted);
            f.W.Markets.Current.SetFamily(f.Stall, 0);
            f.W.Step(2);
            Assert.AreEqual(EligibilityPolicy.HelpersOnly, Carries(f.W).Single(x => !x.IsFinished).Def.Eligibility);
            Assert.IsNull(f.W.Inventory.Audit());
            for (int i = 0; i < 4000 && OnSite(f) < 2; i++) f.W.Step(1);
            Assert.AreEqual(2, OnSite(f));
        }

        [Test]
        public void FamilyWithCarryAndStockTask_TakesTheCarryFirst_EvenWhenStockIsNearer()
        {
            Fx f = Make(2, staffed: true);
            // Keep the family's other member busy: only one worker is free, so the choice is visible.
            f.W.People.RemoveCitizen(21);
            Citizen worker = f.W.People.FindCitizen(20);
            worker.X = 80; worker.Z = 80; // right next to the stock task's walk target, far from the stall
            TaskDef stock = new TaskDef(StoreFamilySystem.StockStallTaskId,
                new[] { TaskStep.WalkTo(new PathPoint(80, 80)), TaskStep.Work(10, false) }, EligibilityPolicy.AssignedFamilyOnly);
            TaskRecord stockTask = f.W.Tasks.Create(stock, f.GranaryOp);
            f.W.Step(10);
            TaskRecord carry = Carries(f.W).Single();
            Assert.AreEqual(worker.Id, carry.AssigneeId, "the carry was taken first");
            Assert.AreEqual(TaskState.Open, stockTask.State, "stock_stall waits");
        }

        [Test]
        public void FamilyDeparture_MidStallCarry_DropsPlanks_ReleasesTheCarry_AuditPasses()
        {
            Fx f = Make(2, staffed: true);
            Family fam = f.W.People.FindFamily(2);
            TaskRecord t = null;
            for (int i = 0; i < 3000; i++)
            {
                f.W.Step(1);
                t = Carries(f.W).FirstOrDefault(x => x.State == TaskState.Active && x.CarrierId.HasValue &&
                    f.W.Inventory.Get(x.CarrierId.Value).CountOf(Items.Plank.Id) > 0);
                if (t != null) break;
            }
            Assert.IsNotNull(t, "a family member is walking with planks");
            Assert.AreEqual(2, f.W.People.FindCitizen(t.AssigneeId.Value).FamilyId);

            FamilyDeparture.Begin(f.W, 2);

            Assert.AreEqual(TaskState.Cancelled, t.State);
            Assert.AreEqual(0, f.W.Inventory.Get(t.CarrierId.Value).Total, "carrier emptied");
            Assert.AreEqual(0, f.W.Inventory.Reservations.Count(r => r.Owner == t.Id), "the task's reservations are released");
            Assert.IsNull(f.W.Inventory.Audit());
            Assert.AreEqual(10, f.W.Inventory.TotalOf(Items.Plank), "no plank lost");
            LedgerLocation pile = f.W.Inventory.Locations.First(l => l.Kind == LocationKind.GroundPile);
            Assert.AreEqual(f.Stall, pile.OwnerOperationId, "dropped on a pile owned by the site");
            Assert.AreEqual(2, pile.CountOf(Items.Plank.Id));

            // The leaving family is no longer a staffing family: odd-jobbers finish the stall's planks.
            for (int i = 0; i < 6000 && OnSite(f) < 2; i++)
            {
                f.W.Step(1);
                Assert.IsNull(f.W.Inventory.Audit(), "tick " + i);
            }
            Assert.AreEqual(2, OnSite(f));
        }

        [Test]
        public void SaveLoad_StallCarry_KeepsAssignedFamilyPolicy()
        {
            Fx f = Make(2, staffed: true);
            f.W.Step(1);
            // The test footprint type is not in the catalogue, so only the task def rebuild is checked.
            TaskDef def = TaskDefCatalog.Build(ConstructionTaskDefs.CarryId, Carries(f.W).Single().Def.Steps);
            Assert.AreEqual(EligibilityPolicy.AssignedFamilyOnly, def.Eligibility);
            TaskDef heavy = TaskDefCatalog.Build(ConstructionTaskDefs.CarryId,
                ConstructionTaskDefs.Carry(new PathPoint(0, 0), 1, Items.Plank.Id, new PathPoint(1, 1), 2, 2).Steps);
            Assert.AreEqual(EligibilityPolicy.HelpersOnly, heavy.Eligibility);
        }
    }
}
