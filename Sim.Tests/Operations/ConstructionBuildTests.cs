using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>ConstructionSystem part 2: build tasks that progress as logs arrive (T-347, CN-23/23b/23c).</summary>
    public class ConstructionBuildTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private sealed class Ctx
        {
            public SimWorld W;
            public Operation Op;
            public LedgerLocation Pile;
            public int SiteId;
            public ConstructionSite Site => W.Construction.Get(SiteId);
            public LedgerLocation SiteLoc => W.Inventory.Get(W.Inventory.FindSite(SiteId).Id);
            public int OnSite => SiteLoc.CountOf(Items.Log.Id);
        }

        // Flat map, woodcutting area, 8 logs at a stockpile, one module placed (hut by default). Daytime start.
        private static Ctx Make(int helpers, int oxen, string moduleType = null, int maxBuilders = 4, int children = 0)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8, MaxBuildersPerSite = maxBuilders });
            CommandResult r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) }), AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            var c = new Ctx { W = w, Op = w.Operations.FindByArea((int)r.Created[0]) };
            c.Pile = w.Inventory.AddStockpile(M(95, 95));
            w.Inventory.Produce(c.Pile, Items.Log, 8);
            w.People.AddFamily(2, "Helpers", 100, 100);
            for (int i = 0; i < helpers; i++) w.People.AddCitizen(20 + i, "H" + i, Sex.Male, AdultBirth, 2, 100, 100);
            for (int i = 0; i < children; i++) w.People.AddCitizen(40 + i, "K" + i, Sex.Male, 0, 2, 100, 100);
            for (int i = 0; i < oxen; i++)
            {
                Ox ox = w.People.AddOx(50 + i, "O" + i, 100, 104);
                ox.State = OxState.Resting(100000, 104000);
            }
            CommandResult p = w.Execute(new PlaceModule(c.Op.Id, moduleType ?? ModuleTypes.WoodcuttingHutId, M(110, 100), 0));
            Assert.IsTrue(p.IsAccepted, p.Text);
            c.SiteId = (int)p.Created[0];
            return c;
        }

        // Moves n logs from the stockpile to the site as if delivered (consumes the site's reservation).
        private static void Deliver(Ctx c, int n)
        {
            Assert.IsTrue(c.W.Inventory.Transfer(c.Pile, c.SiteLoc, Items.Log, n, c.SiteId));
            c.Site.ReservationIds.RemoveAll(id => c.W.Inventory.GetReservation(id) == null);
        }

        private static List<TaskRecord> Builds(SimWorld w) =>
            w.Tasks.All.Where(t => t.Def.Id == ConstructionTaskDefs.BuildId).ToList();

        private static List<TaskRecord> Live(SimWorld w) => Builds(w).Where(t => !t.IsFinished).ToList();

        [Test]
        public void NoLogsOnSite_NoBuildTaskIsOpened()
        {
            Ctx c = Make(1, 0);
            c.W.Step(30);
            Assert.AreEqual(0, Builds(c.W).Count);
        }

        [Test]
        public void AllLogsOnSite_OpensUpToMaxBuilders()
        {
            Ctx c = Make(0, 0);
            Deliver(c, 8);
            c.W.Step(1);
            Assert.AreEqual(4, Live(c.W).Count);
            Assert.IsTrue(Live(c.W).All(t => t.SiteFootprintId == c.SiteId));
        }

        [Test]
        public void MaxBuildersTuning_LimitsTheOpenBuildTasks()
        {
            Ctx c = Make(0, 0, maxBuilders: 2);
            Deliver(c, 8);
            c.W.Step(3);
            Assert.AreEqual(2, Live(c.W).Count);
        }

        [Test]
        public void ChildUnder14_NeverTakesBuild()
        {
            Ctx c = Make(0, 0, children: 1);
            Deliver(c, 8);
            c.W.Step(200);
            Assert.IsTrue(Live(c.W).Count > 0);
            Assert.IsTrue(Live(c.W).All(t => t.State == TaskState.Open));
            Assert.AreEqual(0, c.Site.ProgressMinutes);
        }

        [Test]
        public void AssignedFamily_NeverTakesBuild()
        {
            Ctx c = Make(0, 0);
            Deliver(c, 8);
            c.W.People.AddFamily(1, "Assigned", 100, 100);
            c.W.People.AddCitizen(10, "A", Sex.Male, AdultBirth, 1, 100, 100);
            c.W.People.FindFamily(1).WorkplaceId = c.Op.Id;
            c.W.Step(200);
            Assert.IsTrue(Live(c.W).All(t => t.State == TaskState.Open));
            Assert.AreEqual(0, c.Site.ProgressMinutes);
        }

        [Test]
        public void Progress_IsCappedAtTheShareDelivered_AndBuilderIsFreedWithoutRetake()
        {
            Ctx c = Make(1, 0);
            Deliver(c, 2); // cap = 960 × 2 ÷ 8 = 240 (inside one working day)
            for (int i = 0; i < 300; i++)
            {
                c.W.Step(1);
                Assert.LessOrEqual(c.Site.ProgressMinutes, 240);
            }
            Assert.AreEqual(240, c.Site.ProgressMinutes);
            Assert.AreEqual(0, Live(c.W).Count, "no Build offered at the cap");
            Assert.IsFalse(c.W.Tasks.IsBusy(20));
            Assert.AreEqual(1, Builds(c.W).Count(t => t.State == TaskState.Done));

            // More logs arrive: Build is offered again and progress moves.
            c.W.Inventory.Produce(c.SiteLoc, Items.Log, 1); // 3 of 8 delivered -> cap 360
            c.W.Step(40);
            Assert.Greater(c.Site.ProgressMinutes, 240);
            Assert.LessOrEqual(c.Site.ProgressMinutes, 360);
        }

        [Test]
        public void BuilderAtTheCap_IsNotGivenBuildInTheSameTickOrTheNextDispatch()
        {
            Ctx c = Make(1, 0);
            Deliver(c, 1); // cap 120
            while (c.Site.ProgressMinutes < 120) c.W.Step(1);
            for (int i = 0; i < 25; i++)
            {
                c.W.Step(1);
                Assert.IsFalse(Live(c.W).Any(t => t.State != TaskState.Open), "tick +" + i);
                Assert.AreEqual(120, c.Site.ProgressMinutes);
            }
        }

        [Test]
        public void OneLogDelivered_TwoOddJobbers_OneBuildsAndOneDelivers()
        {
            Ctx c = Make(2, 1);
            Deliver(c, 1);
            bool sawDeliver = false, sawBuild = false;
            for (int i = 0; i < 400 && !(sawDeliver && sawBuild); i++)
            {
                c.W.Step(1);
                var held = new[] { 20, 21 }.SelectMany(id => c.W.Tasks.ByAssignee(id)).Where(t => !t.IsFinished).ToList();
                sawDeliver |= held.Any(t => t.Def.Id == ConstructionTaskDefs.DeliverId);
                sawBuild |= held.Any(t => t.Def.Id == ConstructionTaskDefs.BuildId);
                if (sawDeliver && sawBuild) Assert.AreEqual(2, held.Count);
            }
            Assert.IsTrue(sawDeliver, "one odd-jobber takes the Deliver");
            Assert.IsTrue(sawBuild, "the other builds");
        }

        [Test]
        public void CappedBuilder_WithADeliverOpen_TakesTheDeliver()
        {
            Ctx c = Make(1, 1);
            Deliver(c, 1); // 7 logs still to come
            bool delivered = false;
            for (int i = 0; i < 1500 && !delivered; i++)
            {
                c.W.Step(1);
                delivered = Delivers(c.W).Any(t => t.State == TaskState.Done);
            }
            Assert.IsTrue(delivered, "the lone builder, capped, hauled logs");
            Assert.Greater(c.OnSite + c.Site.LogsConsumed, 1);
        }

        private static List<TaskRecord> Delivers(SimWorld w) =>
            w.Tasks.All.Where(t => t.Def.Id == ConstructionTaskDefs.DeliverId).ToList();

        [Test]
        public void EachLogShare_ConsumesOneLog_AndTheJournalRecordsIt()
        {
            Ctx c = Make(1, 0);
            Deliver(c, 8);
            int prevOnSite = 8;
            while (c.Site != null && c.Site.ProgressMinutes < 960)
            {
                c.W.Step(1);
                if (c.Site == null) break;
                int expectedConsumed = c.Site.ProgressMinutes / 120; // 960 ÷ 8 logs
                Assert.AreEqual(expectedConsumed, c.Site.LogsConsumed, "at progress " + c.Site.ProgressMinutes);
                Assert.AreEqual(8 - expectedConsumed, c.OnSite);
                Assert.AreEqual(expectedConsumed, c.W.Inventory.ConsumedOf(Items.Log.Id));
                Assert.IsNull(c.W.Inventory.Audit());
                prevOnSite = c.OnSite;
            }
            Assert.IsNull(c.Site);
        }

        [Test]
        public void Hut_TwoBuilders_AllLogsOnSite_Completes_AfterFourHundredEightyGameMinutesOfWork()
        {
            Ctx c = Make(2, 0);
            Deliver(c, 8);
            int workTicks = 0, total = 0;
            for (int i = 0; i < 5000 && c.Site != null; i++)
            {
                int before = c.Site.ProgressMinutes;
                c.W.Step(1);
                int after = c.Site == null ? 960 : c.Site.ProgressMinutes;
                if (after > before) workTicks++;
                total += after - before;
            }
            Assert.IsNull(c.Site);
            Assert.IsTrue(c.W.IsBuilt(c.SiteId));
            Assert.AreEqual(960, total);
            Assert.GreaterOrEqual(workTicks, 480);
            Assert.LessOrEqual(workTicks, 500, "two builders from within a dispatch interval of the start");
            Assert.AreEqual(8, c.W.Inventory.ConsumedOf(Items.Log.Id));
            Assert.AreEqual(0, c.W.Inventory.TotalOf(Items.Log));
            Assert.IsNull(c.W.Inventory.Audit());
            Assert.AreEqual(0, Live(c.W).Count);
        }

        [Test]
        public void Hut_EndToEnd_FromTheStockpile_StockpileEndsEightLower()
        {
            Ctx c = Make(2, 1);
            for (int i = 0; i < 6000 && c.Site != null; i++) c.W.Step(1);
            Assert.IsNull(c.Site, "built");
            Assert.AreEqual(0, c.Pile.Total, "stockpile 8 lower");
            Assert.AreEqual(8, c.W.Inventory.ConsumedOf(Items.Log.Id));
            Assert.IsNull(c.W.Inventory.Audit());
        }

        [Test]
        public void OxenStand_OneLog_SixtyMinutes_CompletesFromOneDelivery()
        {
            Ctx c = Make(1, 1, ModuleTypes.OxenStandId);
            for (int i = 0; i < 3000 && c.Site != null; i++) c.W.Step(1);
            Assert.IsNull(c.Site);
            Assert.IsTrue(c.W.IsBuilt(c.SiteId));
            Assert.AreEqual(7, c.Pile.Total);
            Assert.AreEqual(1, c.W.Inventory.ConsumedOf(Items.Log.Id));
            Assert.IsNull(ConstructionProgress.Of(c.W, c.SiteId));
            Assert.IsNull(c.W.Inventory.Audit());
        }

        [Test]
        public void Progress_IsKeptOvernight_AndWorkResumesNextDay()
        {
            Ctx c = Make(1, 0);
            Deliver(c, 8);
            int idleRun = 0, longestIdle = 0, last = 0;
            for (int i = 0; i < 6000 && c.Site != null; i++)
            {
                c.W.Step(1);
                int p = c.Site == null ? 960 : c.Site.ProgressMinutes;
                Assert.GreaterOrEqual(p, last, "progress never goes back");
                if (p == last && p > 0 && p < 960) { idleRun++; longestIdle = System.Math.Max(longestIdle, idleRun); }
                else idleRun = 0;
                last = p;
            }
            Assert.GreaterOrEqual(longestIdle, 120, "the night stopped the work");
            Assert.IsNull(c.Site, "and it finished on a later day");
        }

        [Test]
        public void ProgressRead_ShowsStateDeliveredNeededAndFraction_AndIsNullWhenBuilt()
        {
            Ctx c = Make(0, 0);
            ConstructionProgress p0 = ConstructionProgress.Of(c.W, c.SiteId);
            Assert.AreEqual(ConstructionState.Delivering, p0.State);
            Assert.AreEqual(0, p0.LogsDelivered);
            Assert.AreEqual(8, p0.LogsNeeded);
            Assert.AreEqual(0.0, p0.Fraction);

            Deliver(c, 3);
            c.W.Step(1);
            c.Site.ProgressMinutes = 240;
            c.W.Inventory.Consume(c.SiteLoc, Items.Log, 1);
            c.Site.LogsConsumed = 1;
            ConstructionProgress p1 = ConstructionProgress.Of(c.W, c.SiteId);
            Assert.AreEqual(ConstructionState.Building, p1.State);
            Assert.AreEqual(3, p1.LogsDelivered, "includes the consumed log");
            Assert.AreEqual(0.25, p1.Fraction, 1e-9);

            c.W.Construction.Remove(c.SiteId);
            Assert.IsNull(ConstructionProgress.Of(c.W, c.SiteId));
            Assert.IsNull(ConstructionProgress.Of(c.W, 9999));
        }

        [Test]
        public void RemovedSite_CancelsItsBuildTasks()
        {
            Ctx c = Make(0, 0);
            Deliver(c, 8);
            c.W.Step(1);
            Assert.AreEqual(4, Live(c.W).Count);
            c.W.Construction.Remove(c.SiteId);
            c.W.Step(1);
            Assert.AreEqual(0, Live(c.W).Count);
        }
    }
}
