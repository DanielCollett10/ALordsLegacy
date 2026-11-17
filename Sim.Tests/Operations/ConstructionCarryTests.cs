using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>Non-log recipe items are carried to sites on foot by odd-jobbers (T-517, PL-16, decision 0046).</summary>
    public class ConstructionCarryTests
    {
        private const long AdultBirth = -4_000_000;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private sealed class Ctx
        {
            public SimWorld W;
            public MapData Map;
            public LedgerLocation Store;
            public int SiteId;
            public ConstructionSite Site => W.Construction.Get(SiteId);
            public LedgerLocation SiteLoc => W.Inventory.Get(W.Inventory.FindSite(SiteId).Id);
        }

        // Flat 200 m map. Goods store with 10 planks at (sx, sz); a plank-only site at (110,100) needing `planks`;
        // one odd-jobber at (100,100). Dec 21 (sunrise 08:00, sunset 16:00) when a start minute is given.
        private static Ctx Build(int planks, double sx = 110, double sz = 130, int startMinute = -1, int helpers = 1,
            bool savable = false)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var tuning = new SimTuning { StartHour = 8, BushCount = 0 };
            if (startMinute >= 0)
            {
                tuning.StartMonth = 12; tuning.StartDay = 21;
                tuning.StartHour = startMinute / 60; tuning.StartMinute = startMinute % 60;
            }
            var w = SimWorld.FromMapData(map, tuning);
            var c = new Ctx { W = w, Map = map, Store = w.Inventory.AddGoodsStore(M(sx, sz)) };
            w.Inventory.Produce(c.Store, Items.Plank, 10);
            w.People.AddFamily(2, "Helpers", 100, 100);
            for (int i = 0; i < helpers; i++) w.People.AddCitizen(20 + i, "H" + i, Sex.Male, AdultBirth, 2, 100, 100);

            // A savable world needs a catalogue footprint type: the hut (logs only, never delivered here) with one
            // hand-made carry task for 2 planks, reserved by the task itself.
            if (savable)
            {
                Footprint hut = w.Footprints.Add(700, ModuleTypes.WoodcuttingHut, M(110, 100), 0);
                LedgerLocation hutLoc = w.Inventory.GetOrAddSite(hut.Id);
                w.Construction.Add(new ConstructionSite(hut.Id, hutLoc.Id));
                TaskDef def = ConstructionTaskDefs.Carry(new PathPoint(sx, sz), c.Store.Id, Items.Plank.Id,
                    new PathPoint(hut.AccessPoint.XMetres, hut.AccessPoint.ZMetres), hutLoc.Id, 2);
                TaskRecord t = w.Tasks.Create(def, DeconstructionWork.HouseOwnerId, null, TaskPriority.Normal, hut.Type.Id);
                w.Tasks.SetSite(t.Id, hut.Id);
                Assert.AreNotEqual(0, w.Inventory.ReserveStock(c.Store, Items.Plank, 2, t.Id));
                c.SiteId = hut.Id;
                return c;
            }
            var type = new FootprintTypeDef("test.carry" + planks, 3000, 2000, false, false,
                recipe: new ConstructionRecipe(new Dictionary<string, int> { { Items.Plank.Id, planks } }, 100000));
            Footprint fp = w.Footprints.Add(700 + planks, type, M(110, 100), 0);
            LedgerLocation loc = w.Inventory.GetOrAddSite(fp.Id);
            ConstructionSite site = w.Construction.Add(new ConstructionSite(fp.Id, loc.Id));
            List<int> ids = SiteMaterials.TryReserveAll(w, type.ConstructionRecipe, fp.Centre, fp.Id);
            Assert.IsNotNull(ids);
            site.ReservationIds.AddRange(ids);
            c.SiteId = fp.Id;
            return c;
        }

        private static List<TaskRecord> Carries(SimWorld w) =>
            w.Tasks.All.Where(t => t.Def.Id == ConstructionTaskDefs.CarryId).ToList();

        private static int OnSite(Ctx c) => ConstructionProgress.DeliveredOf(c.W, c.Site, Items.Plank.Id);

        private static long SunsetTick(SimWorld w) => w.WorkDay.WindowOf(w, WorkDay.DayStart(w)).End;

        private static LedgerLocation CarrierOf(SimWorld w, TaskRecord t) =>
            t.CarrierId.HasValue ? w.Inventory.Get(t.CarrierId.Value) : null;

        private static bool Carrying(SimWorld w) =>
            Carries(w).Any(t => !t.IsFinished && (CarrierOf(w, t)?.CountOf(Items.Plank.Id) ?? 0) > 0);

        [Test]
        public void TwoPlanks_OpensOneCarryTask_WithWalkCollectWalkDepositSteps()
        {
            Ctx c = Build(2);
            c.W.Step(1);
            List<TaskRecord> ts = Carries(c.W);
            Assert.AreEqual(1, ts.Count);
            TaskRecord t = ts[0];
            CollectionAssert.AreEqual(new[] { StepKind.WalkTo, StepKind.CollectItems, StepKind.WalkTo, StepKind.DepositItems },
                t.Def.Steps.Select(s => s.Kind).ToArray());
            Assert.AreEqual(c.Store.Id, t.Def.Steps[1].LocationId);
            Assert.AreEqual(Items.Plank.Id, t.Def.Steps[1].ItemId);
            Assert.AreEqual(2, t.Def.Steps[1].Count);
            Assert.AreEqual(c.Site.LedgerLocationId, t.Def.Steps[3].LocationId);
            Assert.AreEqual(EligibilityPolicy.HelpersOnly, t.Def.Eligibility);
            Assert.AreEqual(0, c.W.Tasks.All.Count(x => x.Def.Id == ConstructionTaskDefs.DeliverId), "no ox delivery for planks");
            Assert.IsNull(c.W.Inventory.Audit());
        }

        [Test]
        public void FivePlanks_OpensThreeCarryTasks_OfTwoTwoOne()
        {
            Ctx c = Build(5);
            c.W.Step(1);
            CollectionAssert.AreEqual(new[] { 2, 2, 1 }, Carries(c.W).Select(t => t.Def.Steps[1].Count).ToArray());
            Assert.AreEqual(0, c.Site.ReservationIds.Sum(i => c.W.Inventory.GetReservation(i).Count), "all handed to tasks");
            Assert.AreEqual(5, c.W.Inventory.Reservations.Sum(r => r.Count));
            Assert.IsNull(c.W.Inventory.Audit());
        }

        [Test]
        public void HandCarryPlanksTuning_ChangesTripSize()
        {
            Ctx c = Build(5);
            c.W.Tuning.HandCarryPlanks = 3;
            c.W.Step(1);
            CollectionAssert.AreEqual(new[] { 3, 2 }, Carries(c.W).Select(t => t.Def.Steps[1].Count).ToArray());
        }

        [Test]
        public void OddJobber_WalksCollectsWalksDeposits_SiteTurnsBuildingAtFirstPlank_AuditCleanEveryTick()
        {
            Ctx c = Build(2);
            Assert.AreEqual(ConstructionState.Delivering, c.Site.State);
            bool sawCarry = false;
            for (int i = 0; i < 3000 && OnSite(c) < 2; i++)
            {
                c.W.Step(1);
                Assert.IsNull(c.W.Inventory.Audit(), "tick " + i);
                if (Carrying(c.W))
                {
                    sawCarry = true;
                    Assert.AreEqual(2, CarrierOf(c.W, Carries(c.W)[0]).CountOf(Items.Plank.Id));
                    Assert.AreEqual(8, c.Store.CountOf(Items.Plank.Id), "taken from the store at collect");
                    Assert.AreEqual(ConstructionState.Delivering, c.Site.State, "nothing on site yet");
                }
                if (OnSite(c) > 0) Assert.AreEqual(ConstructionState.Building, c.Site.State);
            }
            Assert.IsTrue(sawCarry);
            Assert.AreEqual(2, OnSite(c));
            Assert.AreEqual(ConstructionState.Building, c.Site.State);
            Assert.AreEqual(TaskState.Done, Carries(c.W)[0].State);
            Assert.AreEqual(8, c.Store.CountOf(Items.Plank.Id));
            Assert.AreEqual(0, c.W.Inventory.Reservations.Count());
        }

        [Test]
        public void FivePlanks_TwoHelpers_AllDeliveredThroughRealTicks()
        {
            Ctx c = Build(5, helpers: 2);
            for (int i = 0; i < 4000 && OnSite(c) < 5; i++) c.W.Step(1);
            Assert.AreEqual(5, OnSite(c));
            Assert.AreEqual(3, Carries(c.W).Count(t => t.State == TaskState.Done));
            Assert.AreEqual(5, c.Store.CountOf(Items.Plank.Id));
            Assert.IsNull(c.W.Inventory.Audit());
        }

        [Test]
        public void ChildUnder14_NeverTakesCarry()
        {
            Ctx c = Build(2, helpers: 0);
            c.W.People.AddCitizen(11, "K", Sex.Male, 0, 2, 100, 100);
            c.W.Step(300);
            Assert.AreEqual(1, Carries(c.W).Count);
            Assert.IsTrue(Carries(c.W).All(t => t.State == TaskState.Open));
        }

        [Test]
        public void CancelledOpenCarry_PlanksReReservedAndDeliveredAnyway()
        {
            Ctx c = Build(4, helpers: 0);
            c.W.Step(1);
            Assert.IsTrue(c.W.Tasks.Cancel(Carries(c.W)[0].Id));
            c.W.Step(1);
            Assert.AreEqual(2, Carries(c.W).Count(t => !t.IsFinished), "a fresh task replaces the cancelled one");
            Assert.AreEqual(4, c.W.Inventory.Reservations.Sum(r => r.Count));
            Assert.IsNull(c.W.Inventory.Audit());
            c.W.People.AddCitizen(20, "late", Sex.Male, AdultBirth, 2, 100, 100);
            for (int i = 0; i < 4000 && OnSite(c) < 4; i++) c.W.Step(1);
            Assert.AreEqual(4, OnSite(c));
            Assert.IsNull(c.W.Inventory.Audit());
        }

        [Test]
        public void CancelledMidWalk_CarriedPlanksDropToSitePile_ReservedForSite_AuditClean_ThenDelivered()
        {
            Ctx c = Build(2);
            for (int i = 0; i < 3000 && !Carrying(c.W); i++) c.W.Step(1);
            Assert.IsTrue(Carrying(c.W));
            TaskRecord t = Carries(c.W)[0];
            Assert.IsTrue(c.W.Tasks.Cancel(t.Id)); // from outside the runner
            c.W.Step(1);
            Assert.IsNull(c.W.Inventory.Audit());
            LedgerLocation pile = c.W.Inventory.Locations.First(l => l.Kind == LocationKind.GroundPile);
            Assert.AreEqual(c.SiteId, pile.OwnerOperationId);
            Assert.AreEqual(2, pile.CountOf(Items.Plank.Id));
            Assert.AreEqual(0, CarrierOf(c.W, t).Total);
            Assert.AreEqual(10, c.W.Inventory.TotalOf(Items.Plank), "no plank lost");
            for (int i = 0; i < 4000 && OnSite(c) < 2; i++)
            {
                c.W.Step(1);
                Assert.IsNull(c.W.Inventory.Audit(), "tick " + i);
            }
            Assert.AreEqual(2, OnSite(c), "the site's pile is picked up and delivered");
        }

        [Test]
        public void RemovedSite_CancelsOpenCarries_AuditClean()
        {
            Ctx c = Build(4, helpers: 0);
            c.W.Step(1);
            Assert.AreEqual(2, Carries(c.W).Count);
            c.W.Construction.Remove(c.SiteId);
            c.W.Step(2);
            Assert.IsTrue(Carries(c.W).All(t => t.State == TaskState.Cancelled));
            Assert.AreEqual(0, c.W.Inventory.Reservations.Count(r => r.Kind == ReservationKind.Stock));
            Assert.IsNull(c.W.Inventory.Audit());
        }

        [Test]
        public void SunsetWindDown_DropsSiteOwnedPileWhereCarrierStands_NothingLostAndAuditClean()
        {
            // Source 90 m away: the walk there takes ~50 minutes, so the helper is under way at sunset (16:00).
            Ctx c = Build(2, 100, 190, startMinute: 15 * 60);
            for (int i = 0; i < 400 && c.W.Tick < SunsetTick(c.W) - 1; i++) c.W.Step(1);
            TaskRecord t = Carries(c.W).First();
            Assert.AreEqual(TaskState.Active, t.State);
            for (int i = 0; i < 30 && !t.IsFinished; i++)
            {
                c.W.Step(1);
                Assert.IsNull(c.W.Inventory.Audit(), "audit each tick");
            }
            Assert.AreEqual(TaskState.Cancelled, t.State, "wound down at sunset");
            Assert.AreEqual(10, c.W.Inventory.TotalOf(Items.Plank));
            Assert.AreEqual(0, OnSite(c));
            Assert.AreEqual(1, Carries(c.W).Count(x => !x.IsFinished), "the plank reservation reopened as a fresh task");
            Assert.AreEqual(2, c.W.Inventory.Reservations.Sum(r => r.Count));
        }

        [Test]
        public void SunsetWindDown_WhileCarrying_DropsOnSiteOwnedPile_AndNextDayDelivers()
        {
            // Source 80 m from the helper and 80 m from the site: ~46 minutes each leg, so the collect happens before
            // sunset (16:00) and the walk to the site is still under way when it comes.
            Ctx probe = Build(2, 110, 180, startMinute: 12 * 60);
            int minutes = 0;
            while (!Carrying(probe.W) && minutes < 2000) { probe.W.Step(1); minutes++; }
            Assert.Less(minutes, 120, "probe collects");
            Ctx c = Build(2, 110, 180, startMinute: 16 * 60 - minutes - 3);
            for (int i = 0; i < 400 && !Carrying(c.W) && c.W.Tick < SunsetTick(c.W); i++) c.W.Step(1);
            Assert.IsTrue(Carrying(c.W), "collected before sunset");
            TaskRecord t = Carries(c.W)[0];
            for (int i = 0; i < 60 && t.State != TaskState.Cancelled; i++)
            {
                c.W.Step(1);
                Assert.IsNull(c.W.Inventory.Audit(), "audit each tick");
            }
            Assert.AreEqual(TaskState.Cancelled, t.State);
            LedgerLocation pile = c.W.Inventory.Locations.First(l => l.Kind == LocationKind.GroundPile);
            Assert.AreEqual(c.SiteId, pile.OwnerOperationId);
            Assert.AreEqual(2, pile.CountOf(Items.Plank.Id));
            Assert.AreEqual(0, CarrierOf(c.W, t).Total);
            Assert.AreEqual(10, c.W.Inventory.TotalOf(Items.Plank));
            for (int i = 0; i < 3000 && OnSite(c) < 2; i++) c.W.Step(1);
            Assert.AreEqual(2, OnSite(c), "delivered the next day");
            Assert.IsNull(c.W.Inventory.Audit());
        }

        [Test]
        public void SaveLoad_MidWalkWithPlanksOnTheCarrier_RoundTripsAndFinishesDelivery()
        {
            Ctx c = Build(2, savable: true);
            for (int i = 0; i < 3000 && !Carrying(c.W); i++) c.W.Step(1);
            Assert.IsTrue(Carrying(c.W));
            c.W.Step(3);
            byte[] bytes = SimSave.Write(c.W, "s", 1, c.Map.MapId, c.Map.ContentHash);
            SimWorld back = SimSave.Read(bytes, c.Map);
            Assert.AreEqual(bytes, SimSave.Write(back, "s", 1, c.Map.MapId, c.Map.ContentHash), "second write is identical");

            TaskRecord a = Carries(c.W)[0], b = Carries(back)[0];
            Assert.AreEqual(a.CurrentStep, b.CurrentStep);
            Assert.AreEqual(a.CarrierId, b.CarrierId);
            Assert.AreEqual(TaskState.Active, b.State);
            Assert.AreEqual(Items.Plank.Id, b.Def.Steps[3].ItemId);
            Assert.AreEqual(2, b.Def.Steps[3].Count);
            Assert.AreEqual(EligibilityPolicy.HelpersOnly, b.Def.Eligibility);
            Assert.AreEqual(2, CarrierOf(back, b).CountOf(Items.Plank.Id));
            Assert.IsNull(back.Inventory.Audit());

            LedgerLocation siteLoc = back.Inventory.FindSite(c.SiteId);
            for (int i = 0; i < 3000 && siteLoc.CountOf(Items.Plank.Id) < 2; i++)
            {
                back.Step(1);
                Assert.IsNull(back.Inventory.Audit(), "tick " + i);
            }
            Assert.AreEqual(2, siteLoc.CountOf(Items.Plank.Id), "the loaded world finishes the walk and deposits");
        }

        [Test]
        public void TasksSection_StepItemIdAndTuning_RoundTrip()
        {
            Ctx c = Build(5, savable: true);
            c.W.Tuning.HandCarryPlanks = 3;
            c.W.Step(1);
            byte[] bytes = SimSave.Write(c.W, "s", 1, c.Map.MapId, c.Map.ContentHash);
            SimWorld back = SimSave.Read(bytes, c.Map);
            Assert.AreEqual(3, back.Tuning.HandCarryPlanks);
            CollectionAssert.AreEqual(Carries(c.W).Select(t => t.Def.Steps[1].Count).ToArray(),
                Carries(back).Select(t => t.Def.Steps[1].Count).ToArray());
            Assert.IsTrue(Carries(back).All(t => t.Def.Steps[1].ItemId == Items.Plank.Id && t.Def.Steps[3].ItemId == Items.Plank.Id));
        }
    }
}
