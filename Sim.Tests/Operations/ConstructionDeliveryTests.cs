using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Forestry;
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
    /// <summary>ConstructionSystem part 1: delivering a site's reserved logs by ox (T-345, CN-20..23).</summary>
    public class ConstructionDeliveryTests
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
        }

        // Flat 200 m map, area x 90..120 z 90..110, 8 logs at a stockpile (95,95), hut placed at (110,100) (8 logs).
        private static Ctx Build(int helpers = 1, int oxen = 1, int childHelpers = 0, bool placeHut = true)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
            CommandResult r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) }), AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            var c = new Ctx { W = w, Op = w.Operations.FindByArea((int)r.Created[0]) };
            c.Pile = w.Inventory.AddStockpile(M(95, 95));
            w.Inventory.Produce(c.Pile, Items.Log, 8);
            w.People.AddFamily(2, "Helpers", 100, 100);
            for (int i = 0; i < helpers; i++) w.People.AddCitizen(20 + i, "H" + i, Sex.Male, AdultBirth, 2, 100, 100);
            for (int i = 0; i < childHelpers; i++) w.People.AddCitizen(40 + i, "K" + i, Sex.Male, 0, 2, 100, 100);
            for (int i = 0; i < oxen; i++)
            {
                Ox ox = w.People.AddOx(50 + i, "O" + i, 100, 104);
                ox.State = OxState.Resting(100000, 104000);
            }
            if (placeHut)
            {
                CommandResult p = w.Execute(new PlaceModule(c.Op.Id, ModuleTypes.WoodcuttingHutId, M(110, 100), 0));
                Assert.IsTrue(p.IsAccepted, p.Text);
                c.SiteId = (int)p.Created[0];
            }
            return c;
        }

        private static List<TaskRecord> Delivers(SimWorld w) =>
            w.Tasks.All.Where(t => t.Def.Id == ConstructionTaskDefs.DeliverId).ToList();

        private static int Reserved(SimWorld w) => w.Inventory.Reservations.Sum(r => r.Count);

        [Test]
        public void DefaultSystems_IncludeConstructionSystem()
        {
            Assert.IsTrue(SimSystems.CreateDefault().Any(s => s is ConstructionSystem));
        }

        [Test]
        public void EightLogs_OpensTwoTasksOfFour_WithCn21Steps_AndLedgerStaysClean()
        {
            Ctx c = Build(oxen: 2);
            c.W.Step(1);
            List<TaskRecord> ds = Delivers(c.W);
            Assert.AreEqual(2, ds.Count);
            foreach (TaskRecord t in ds)
            {
                Assert.AreEqual(4, t.Def.Steps[2].Count);
                Assert.AreEqual(c.Pile.Id, t.Def.Steps[2].LocationId);
                Assert.AreEqual(c.Site.LedgerLocationId, t.Def.Steps[4].LocationId);
                var kinds = t.Def.Steps.Select(s => s.Kind).ToArray();
                CollectionAssert.AreEqual(new[] { StepKind.FetchOx, StepKind.LeadTo, StepKind.LoadOx, StepKind.LeadTo,
                    StepKind.UnloadOx, StepKind.ReturnOx }, kinds);
                Assert.AreEqual(TaskPriority.Normal, t.Priority);
                Assert.IsTrue(t.OxId.HasValue);
            }
            Assert.AreEqual(8, Reserved(c.W));
            Assert.AreEqual(0, c.Site.ReservationIds.Sum(i => c.W.Inventory.GetReservation(i).Count));
            Assert.IsNull(c.W.Inventory.Audit());
        }

        [Test]
        public void HighPrioritySite_TasksAreHigh()
        {
            Ctx c = Build();
            c.Site.Priority = ConstructionPriority.High;
            c.W.Step(1);
            Assert.IsTrue(Delivers(c.W).All(t => t.Priority == TaskPriority.High));
        }

        [Test]
        public void FullDelivery_EightLogs_TwoTrips_AuditCleanEveryTick_BuildingFromFirstLog()
        {
            Ctx c = Build();
            bool sawFirstLog = false;
            for (int i = 0; i < 3000 && c.W.Construction.Get(c.SiteId).State == ConstructionState.Delivering || i < 3000 && ConstructionProgress.Of(c.W, c.SiteId).LogsDelivered < 8; i++)
            {
                c.W.Step(1);
                Assert.IsNull(c.W.Inventory.Audit(), "tick " + i);
                int onSite = ConstructionProgress.Of(c.W, c.SiteId).LogsDelivered; // incl. logs the builders used up (T-347)
                if (onSite > 0)
                {
                    sawFirstLog = true;
                    Assert.AreEqual(ConstructionState.Building, c.Site.State, "Building once a log is on site");
                }
                else Assert.AreEqual(ConstructionState.Delivering, c.Site.State);
            }
            Assert.IsTrue(sawFirstLog);
            c.W.Step(200);
            Assert.AreEqual(8, ConstructionProgress.Of(c.W, c.SiteId).LogsDelivered);
            Assert.AreEqual(2, Delivers(c.W).Count(t => t.State == TaskState.Done));
            Assert.AreEqual(2, Delivers(c.W).Count);
            Assert.AreEqual(0, Reserved(c.W));
            Assert.AreEqual(0, c.Pile.Total);
            Assert.AreEqual(OxStateKind.Resting, c.W.People.FindOx(50).State.Kind);
            Assert.IsNull(c.W.Inventory.Audit());
        }

        [Test]
        public void AssignedFamily_NeverTakesDelivery()
        {
            Ctx c = Build(helpers: 0);
            c.W.People.AddFamily(1, "Assigned", 100, 100);
            c.W.People.AddCitizen(10, "A", Sex.Male, AdultBirth, 1, 100, 100);
            c.W.People.FindFamily(1).WorkplaceId = c.Op.Id; // the hut is only a site, so AssignFamily would refuse
            c.W.Step(300);
            Assert.IsTrue(Delivers(c.W).Count > 0);
            Assert.IsTrue(Delivers(c.W).All(t => t.State == TaskState.Open));
            Assert.AreEqual(0, c.W.Inventory.Get(c.Site.LedgerLocationId).CountOf(Items.Log.Id));
        }

        [Test]
        public void ChildUnder14_NeverTakesDelivery()
        {
            Ctx c = Build(helpers: 0, childHelpers: 1);
            c.W.Step(300);
            Assert.IsTrue(Delivers(c.W).Count > 0);
            Assert.IsTrue(Delivers(c.W).All(t => t.State == TaskState.Open));
        }

        [Test]
        public void NoOx_TaskWaitsOpen_ThenOxArrivesAndDelivers()
        {
            Ctx c = Build(oxen: 0);
            c.W.Step(20);
            Assert.IsTrue(Delivers(c.W).All(t => t.State == TaskState.Open && !t.OxId.HasValue));
            Ox ox = c.W.People.AddOx(60, "late", 100, 104);
            ox.State = OxState.Resting(100000, 104000);
            for (int i = 0; i < 3000 && c.W.Inventory.Get(c.Site.LedgerLocationId).CountOf(Items.Log.Id) < 8; i++) c.W.Step(1);
            Assert.AreEqual(8, c.W.Inventory.Get(c.Site.LedgerLocationId).CountOf(Items.Log.Id));
            Assert.IsNull(c.W.Inventory.Audit());
        }

        [Test]
        public void CancelledOpenTask_LogsReturnToSiteAndAreDeliveredAnyway()
        {
            Ctx c = Build(oxen: 0);
            c.W.Step(1);
            TaskRecord first = Delivers(c.W)[0];
            Assert.IsTrue(c.W.Tasks.Cancel(first.Id));
            c.W.Step(1);
            Assert.AreEqual(8, Reserved(c.W));
            Assert.IsNull(c.W.Inventory.Audit());
            Assert.AreEqual(2, Delivers(c.W).Count(t => !t.IsFinished), "a fresh task replaces the cancelled one");
            Ox ox = c.W.People.AddOx(60, "late", 100, 104);
            ox.State = OxState.Resting(100000, 104000);
            for (int i = 0; i < 3000 && c.W.Inventory.Get(c.Site.LedgerLocationId).CountOf(Items.Log.Id) < 8; i++) c.W.Step(1);
            Assert.AreEqual(8, c.W.Inventory.Get(c.Site.LedgerLocationId).CountOf(Items.Log.Id));
            Assert.IsNull(c.W.Inventory.Audit());
        }

        [Test]
        public void NoSites_OpensNothing()
        {
            Ctx c = Build(placeHut: false);
            c.W.Step(5);
            Assert.AreEqual(0, Delivers(c.W).Count);
        }
    }
}
