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
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>Site priority command (T-349, CN-30) and the dispatch order it feeds (CN-31).</summary>
    public class SitePriorityTests
    {
        private const long AdultBirth = -4_000_000;
        private const int Worker = 20;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private sealed class Ctx
        {
            public SimWorld W;
            public Operation Op;     // operation of the left area (holds the near hut)
            public Operation FarOp;  // operation of the right area (holds the far hut; one hut per operation)
            public int NearSite; // at x = 70: 10 m from a worker at x = 60
            public int FarSite;  // at x = 130: 70 m from that worker
        }

        // Flat map, a woodcutting area, a pile of 16 logs, one helper at (workerX, 100) and two huts (near and far).
        // With transfer, each hut has its 8 logs on site, so both are Building and offer build tasks. Without it, the
        // logs stay reserved in the pile and each site offers delivery tasks. Steps once so the tasks exist.
        private static Ctx Make(double workerX = 60, bool transfer = true)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
            var c = new Ctx { W = w, Op = DesignateWoodcutting(w, 50, 90), FarOp = DesignateWoodcutting(w, 110, 150) };
            LedgerLocation pile = w.Inventory.AddStockpile(M(45, 45));
            w.Inventory.Produce(pile, Items.Log, 16);
            w.People.AddFamily(2, "Helpers", 100, 100);
            w.People.AddCitizen(Worker, "H", Sex.Male, AdultBirth, 2, workerX, 100);

            c.NearSite = Place(c, c.Op, M(70, 100));
            c.FarSite = Place(c, c.FarOp, M(130, 100));
            if (transfer)
            {
                foreach (int id in new[] { c.NearSite, c.FarSite })
                {
                    LedgerLocation loc = w.Inventory.FindSite(id);
                    Assert.IsTrue(w.Inventory.Transfer(pile, loc, Items.Log, 8, id));
                    w.Construction.Get(id).ReservationIds.RemoveAll(rid => w.Inventory.GetReservation(rid) == null);
                }
            }
            w.Step(1); // Building sites open build tasks; Delivering sites open delivery tasks
            return c;
        }

        // A 40 x 30 m woodcutting area between x0 and x1 (z 85..115), and its operation.
        private static Operation DesignateWoodcutting(SimWorld w, double x0, double x1)
        {
            CommandResult r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(x0, 85), M(x1, 85), M(x1, 115), M(x0, 115) }), AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return w.Operations.FindByArea((int)r.Created[0]);
        }

        private static int Place(Ctx c, Operation op, PointMm centre)
        {
            CommandResult p = c.W.Execute(new PlaceModule(op.Id, ModuleTypes.WoodcuttingHutId, centre, 0));
            Assert.IsTrue(p.IsAccepted, p.Text);
            return (int)p.Created[0];
        }

        private static List<TaskRecord> Live(SimWorld w, int siteId, string defId) =>
            w.Tasks.All.Where(t => !t.IsFinished && t.Def.Id == defId && t.SiteFootprintId == siteId).ToList();

        // Runs to and past the next dispatch tick (every 10 game minutes, from tick 1 that is tick 10).
        private static void PastDispatch(SimWorld w) => w.Step(10);

        // A bare world (no operation, no footprints): one helper and two oxen, at x = 62 (near) and x = 150 (far).
        private static SimWorld OxWorld(double workerX)
        {
            var w = new SimWorld(new SimTuning { StartHour = 8 });
            w.People.AddFamily(2, "Helpers", 100, 100);
            w.People.AddCitizen(Worker, "H", Sex.Male, AdultBirth, 2, workerX, 100);
            w.People.AddOx(50, "NearOx", 62, 100);
            w.People.AddOx(51, "FarOx", 150, 100);
            return w;
        }

        // An Open delivery of one log, carried by the given ox (reserved for it), from the pile to site location 77.
        private static TaskRecord Delivery(SimWorld w, int pileLocationId, int siteLocationId, TaskPriority priority, int oxId)
        {
            TaskDef def = ConstructionTaskDefs.Deliver(new PathPoint(45, 45), pileLocationId, new PathPoint(70, 100),
                siteLocationId, 1, 1, 1);
            TaskRecord t = w.Tasks.Create(def, 0, null, priority);
            Assert.IsTrue(w.Tasks.SetOx(t.Id, oxId, false));
            w.People.FindOx(oxId).State = OxState.Reserved(t.Id);
            return t;
        }

        [Test]
        public void HighFarSite_IsTakenBeforeNormalNearSite()
        {
            Ctx c = Make();
            Assert.IsTrue(c.W.Execute(new SetSitePriority(c.FarSite, ConstructionPriority.High)).IsAccepted);
            PastDispatch(c.W);
            TaskRecord taken = c.W.Tasks.ByAssignee(Worker).Single();
            Assert.AreEqual(c.FarSite, taken.SiteFootprintId, "the High far site is taken before the Normal near one");
        }

        [Test]
        public void NormalNearSite_IsTakenWhenNoSiteIsHigh()
        {
            Ctx c = Make();
            PastDispatch(c.W);
            Assert.AreEqual(c.NearSite, c.W.Tasks.ByAssignee(Worker).Single().SiteFootprintId);
        }

        [TestCase(ConstructionPriority.Normal)]
        [TestCase(ConstructionPriority.High)]
        public void EqualPriority_NearerSiteWins_WorkerNearTheNearSite(ConstructionPriority both)
        {
            Ctx c = Make(workerX: 60);
            Assert.IsTrue(c.W.Execute(new SetSitePriority(c.NearSite, both)).IsAccepted);
            Assert.IsTrue(c.W.Execute(new SetSitePriority(c.FarSite, both)).IsAccepted);
            PastDispatch(c.W);
            Assert.AreEqual(c.NearSite, c.W.Tasks.ByAssignee(Worker).Single().SiteFootprintId);
        }

        [TestCase(ConstructionPriority.Normal)]
        [TestCase(ConstructionPriority.High)]
        public void EqualPriority_NearerSiteWins_WorkerNearTheFarSide(ConstructionPriority both)
        {
            Ctx c = Make(workerX: 140);
            Assert.IsTrue(c.W.Execute(new SetSitePriority(c.NearSite, both)).IsAccepted);
            Assert.IsTrue(c.W.Execute(new SetSitePriority(c.FarSite, both)).IsAccepted);
            PastDispatch(c.W);
            Assert.AreEqual(c.FarSite, c.W.Tasks.ByAssignee(Worker).Single().SiteFootprintId);
        }

        [Test]
        public void SetSitePriority_UpdatesTheSitesOpenBuildTasks_AndOnlyThose()
        {
            Ctx c = Make();
            Assert.IsTrue(c.W.Execute(new SetSitePriority(c.FarSite, ConstructionPriority.High)).IsAccepted);
            Assert.AreEqual(ConstructionPriority.High, c.W.Construction.Get(c.FarSite).Priority);
            Assert.AreEqual(ConstructionPriority.Normal, c.W.Construction.Get(c.NearSite).Priority);
            List<TaskRecord> far = Live(c.W, c.FarSite, ConstructionTaskDefs.BuildId);
            Assert.IsNotEmpty(far, "the far site has open build tasks");
            Assert.IsTrue(far.All(t => t.Priority == TaskPriority.High));
            Assert.IsTrue(Live(c.W, c.NearSite, ConstructionTaskDefs.BuildId).All(t => t.Priority == TaskPriority.Normal));
        }

        [Test]
        public void SetSitePriority_UpdatesOpenDeliveryTasks_AndOnlyThose()
        {
            Ctx c = Make(transfer: false);
            Assert.IsNotEmpty(Live(c.W, c.FarSite, ConstructionTaskDefs.DeliverId), "the far site has open deliveries");
            Assert.IsTrue(c.W.Execute(new SetSitePriority(c.FarSite, ConstructionPriority.High)).IsAccepted);
            Assert.IsTrue(Live(c.W, c.FarSite, ConstructionTaskDefs.DeliverId).All(t => t.Priority == TaskPriority.High));
            Assert.IsTrue(Live(c.W, c.NearSite, ConstructionTaskDefs.DeliverId).All(t => t.Priority == TaskPriority.Normal));
        }

        [Test]
        public void SetSitePriority_IsAcceptedWhilePaused()
        {
            Ctx c = Make();
            c.W.Pause();
            CommandResult r = c.W.Execute(new SetSitePriority(c.FarSite, ConstructionPriority.High));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Assert.IsTrue(Live(c.W, c.FarSite, ConstructionTaskDefs.BuildId).All(t => t.Priority == TaskPriority.High));
        }

        [Test]
        public void SetSitePriority_SameValue_IsAcceptedAndChangesNothing()
        {
            Ctx c = Make();
            CommandResult r = c.W.Execute(new SetSitePriority(c.NearSite, ConstructionPriority.Normal));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Assert.AreEqual(ConstructionPriority.Normal, c.W.Construction.Get(c.NearSite).Priority);
        }

        [Test]
        public void SetSitePriority_UnknownFootprint_IsNotFound()
        {
            Ctx c = Make();
            CommandResult r = c.W.Execute(new SetSitePriority(9999, ConstructionPriority.High));
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual(CommandRejectCode.NotFound, r.Reason);
        }

        [Test]
        public void SetSitePriority_BuiltFootprint_IsNotAllowed_AndChangesNothing()
        {
            Ctx c = Make();
            // A log storage has an empty recipe, so it never has a site: it is built at once.
            CommandResult storage = c.W.Execute(new PlaceModule(c.Op.Id, ModuleTypes.LogStorageId, M(60, 100), 0));
            Assert.IsTrue(storage.IsAccepted, storage.Text);
            int builtId = (int)storage.Created[0];
            Assert.IsTrue(c.W.IsBuilt(builtId));
            int tasksBefore = c.W.Tasks.Count;
            CommandResult r = c.W.Execute(new SetSitePriority(builtId, ConstructionPriority.High));
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual(CommandRejectCode.NotAllowed, r.Reason);
            Assert.AreEqual(tasksBefore, c.W.Tasks.Count);
        }

        [Test]
        public void SetSitePriority_OutOfRangeValue_IsInvalidArgument_AndChangesNothing()
        {
            Ctx c = Make();
            CommandResult r = c.W.Execute(new SetSitePriority(c.NearSite, (ConstructionPriority)7));
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual(CommandRejectCode.InvalidArgument, r.Reason);
            Assert.AreEqual(ConstructionPriority.Normal, c.W.Construction.Get(c.NearSite).Priority);
        }

        [Test]
        public void HighDelivery_FarOx_IsTakenBeforeNormalDelivery_NearOx()
        {
            SimWorld w = OxWorld(workerX: 60);
            int pile = w.Inventory.AddStockpile(M(45, 45)).Id;
            int site = w.Inventory.GetOrAddSite(77).Id;
            w.Construction.Add(new ConstructionSite(77, site));
            TaskRecord normalNear = Delivery(w, pile, site, TaskPriority.Normal, oxId: 50);
            TaskRecord highFar = Delivery(w, pile, site, TaskPriority.High, oxId: 51);
            PastDispatch(w);
            Assert.AreEqual(highFar.Id, w.Tasks.ByAssignee(Worker).Single().Id, "the High delivery is taken first");
            Assert.AreEqual(TaskState.Open, normalNear.State);
        }

        [Test]
        public void DeliveryOx_IsTakenByTheFreeWorkerNearestTheOx_EvenIfAnotherWorkerComesFirst()
        {
            // Worker 20 (id first) stands at x = 140, worker 21 at x = 60 next to the ox at x = 62.
            SimWorld w = OxWorld(workerX: 140);
            w.People.AddCitizen(21, "H2", Sex.Male, AdultBirth, 2, 60, 100);
            int pile = w.Inventory.AddStockpile(M(45, 45)).Id;
            int site = w.Inventory.GetOrAddSite(77).Id;
            w.Construction.Add(new ConstructionSite(77, site));
            TaskRecord t = Delivery(w, pile, site, TaskPriority.Normal, oxId: 50);
            PastDispatch(w);
            Assert.AreEqual(21, t.AssigneeId, "the worker nearest the ox takes the delivery (WC-21/22)");
        }
    }
}
