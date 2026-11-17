using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
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

namespace LordsLegacy.Sim.Tests.Workshops
{
    /// <summary>WorkshopSystem: fetching inputs by ox, construction first for oxen (T-523, HW-03, HW-06).</summary>
    public class WorkshopSystemTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static (SimWorld w, MapData map) Make(int oxen = 1)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
            for (int fam = 2; fam <= 4; fam++)
            {
                w.People.AddFamily(fam, "F" + fam, 40, 40);
                for (int i = 0; i < 2; i++) w.People.AddCitizen(fam * 10 + i, "C" + fam + i, Sex.Male, AdultBirth, fam, 40, 40);
            }
            for (int i = 0; i < oxen; i++) w.People.AddOx(50 + i, "O" + i, 40, 44).State = OxState.Resting(40000, 44000);
            return (w, map);
        }

        // A built workshop with its stock and store and family 3 working there.
        private static (int fp, Operation op) Workshop(SimWorld w, string type, double x, double z, int family = 3)
        {
            int id = w.Footprints.Add(100 + w.Operations.Count, ModuleTypes.Find(type), M(x, z), 0).Id;
            Operation op = w.Operations.Create(OperationTypes.WorkshopOperationFor(type), 0);
            w.Operations.AddModule(op.Id, id);
            WorkshopBuildings.OnBuilt(w, w.Footprints.Get(id));
            Assert.IsTrue(w.Execute(new AssignFamily(op.Id, family)).IsAccepted);
            return (id, op);
        }

        private static LedgerLocation Logs(SimWorld w, int n)
        {
            LedgerLocation p = w.Inventory.AddStockpile(M(20, 20));
            w.Inventory.Produce(p, Items.Log, n);
            return p;
        }

        private static TaskRecord[] Fetches(SimWorld w) =>
            w.Tasks.All.Where(t => t.Def.Id == WorkshopSystem.FetchTaskId).ToArray();

        [Test]
        public void DefaultSystems_WorkshopSystemRunsAfterConstructionSystem()
        {
            var list = SimSystems.CreateDefault().ToList();
            int c = list.FindIndex(s => s is ConstructionSystem);
            int ws = list.FindIndex(s => s is WorkshopSystem);
            Assert.GreaterOrEqual(c, 0);
            Assert.AreEqual(c + 1, ws);
        }

        [Test]
        public void HewingStation_EmptyStockFreeOx_FetchesOneLogToStock()
        {
            var (w, _) = Make();
            var (fp, _) = Workshop(w, ModuleTypes.HewingStationId, 60, 60);
            LedgerLocation pile = Logs(w, 4);
            LedgerLocation stock = w.Inventory.FindWorkshopStock(fp);
            w.Step(1);
            TaskRecord t = Fetches(w).First();
            Assert.AreEqual(w.Tuning.OxLoadLogs, t.Def.Steps[2].Count);
            Assert.AreEqual(Items.Log.Id, t.Def.Steps[2].ItemId);
            Assert.AreEqual(pile.Id, t.Def.Steps[2].LocationId);
            Assert.AreEqual(stock.Id, t.Def.Steps[4].LocationId);
            for (int i = 0; i < 600 && w.Tasks.Get(t.Id).State != TaskState.Done; i++) { w.Step(1); Assert.IsNull(w.Inventory.Audit()); }
            Assert.GreaterOrEqual(stock.CountOf(Items.Log.Id), w.Tuning.OxLoadLogs);
            Assert.AreEqual(TaskState.Done, w.Tasks.Get(t.Id).State);
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void Sawpit_FetchesOneBeamFromHewingStore()
        {
            var (w, _) = Make();
            var (hew, _) = Workshop(w, ModuleTypes.HewingStationId, 60, 60);
            var (saw, _) = Workshop(w, ModuleTypes.SawpitId, 70, 70, family: 4);
            w.Inventory.Produce(w.Inventory.FindGoodsStore(hew), Items.Beam, 2);
            LedgerLocation stock = w.Inventory.FindWorkshopStock(saw);
            w.Step(1);
            TaskRecord t = Fetches(w).Single();
            Assert.AreEqual(Items.Beam.Id, t.Def.Steps[2].ItemId);
            Assert.AreEqual(w.Tuning.OxLoadBeams, t.Def.Steps[2].Count);
            for (int i = 0; i < 600 && stock.CountOf(Items.Beam.Id) == 0; i++) w.Step(1);
            Assert.GreaterOrEqual(stock.CountOf(Items.Beam.Id), w.Tuning.OxLoadBeams);
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void NoFetch_WhenStockHasNoRoomForALoad()
        {
            var (w, _) = Make();
            var (fp, _) = Workshop(w, ModuleTypes.HewingStationId, 60, 60);
            Logs(w, 4);
            LedgerLocation stock = w.Inventory.FindWorkshopStock(fp);
            w.Inventory.Produce(stock, Items.Log, stock.Capacity);
            w.Step(5);
            Assert.IsEmpty(Fetches(w));
        }

        [Test]
        public void NoFetch_WhenNoSourceHasFreeInput()
        {
            var (w, _) = Make();
            Workshop(w, ModuleTypes.HewingStationId, 60, 60);
            w.Step(5);
            Assert.IsEmpty(Fetches(w), "no logs anywhere");
            LedgerLocation pile = Logs(w, 2);
            w.Inventory.ReserveStock(pile, Items.Log, 2, 999);
            w.Step(5);
            Assert.IsEmpty(Fetches(w), "all logs reserved");
        }

        [Test]
        public void NoFetch_WhenNoFreeOx()
        {
            var (w, _) = Make(oxen: 0);
            Workshop(w, ModuleTypes.HewingStationId, 60, 60);
            Logs(w, 4);
            w.Step(5);
            Assert.IsEmpty(Fetches(w));
        }

        [Test]
        public void ConstructionDelivery_GetsTheOnlyFreeOxFirst_ThenFetchRuns()
        {
            var (w, _) = Make();
            var (fp, _) = Workshop(w, ModuleTypes.HewingStationId, 70, 70);
            Logs(w, 12);
            // A sawpit site reserves 6 logs; the other 6 stay free for the workshop.
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.SawpitId, M(50, 50), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            LedgerLocation stock = w.Inventory.FindWorkshopStock(fp);

            w.Step(1);
            TaskRecord deliver = w.Tasks.All.First(t => t.Def.Id == ConstructionTaskDefs.DeliverId);
            Assert.IsTrue(deliver.OxId.HasValue, "the delivery holds the only ox");
            Assert.IsEmpty(Fetches(w), "no ox left for a fetch in the same tick");

            bool fetchSeen = false;
            for (int i = 0; i < 3000 && stock.CountOf(Items.Log.Id) == 0; i++)
            {
                w.Step(1);
                if (Fetches(w).Length > 0 && !fetchSeen)
                {
                    fetchSeen = true;
                }
            }
            Assert.IsTrue(fetchSeen, "the fetch ran once the ox was free");
            Assert.Greater(stock.CountOf(Items.Log.Id), 0);
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void OxAssignedToWoodcutting_IsNeverUsedByFetchOrDelivery()
        {
            var (w, _) = Make(oxen: 1);
            CommandResult area = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(10, 10), M(30, 10), M(30, 30), M(10, 30) }), AreaTypes.WoodcuttingId));
            Assert.IsTrue(area.IsAccepted, area.Text);
            Operation wood = w.Operations.FindByArea((int)area.Created[0]);
            Assert.IsTrue(w.Operations.AssignOx(wood.Id, 50));
            var (fp, _) = Workshop(w, ModuleTypes.HewingStationId, 70, 70);
            Logs(w, 12);
            Assert.IsTrue(w.Execute(new PlaceStructure(ModuleTypes.SawpitId, M(50, 50), 0)).IsAccepted);
            for (int i = 0; i < 200; i++)
            {
                w.Step(1);
                foreach (TaskRecord t in w.Tasks.All.Where(x => x.OxId.HasValue))
                    Assert.AreEqual(wood.Id, t.OwnerOperationId, "only the owning operation's tasks may hold the ox");
            }
            Assert.IsEmpty(Fetches(w).Where(t => t.OxId.HasValue));
            Assert.IsFalse(w.Tasks.All.Any(t => t.Def.Id == ConstructionTaskDefs.DeliverId && t.OxId.HasValue));
            Assert.AreEqual(0, w.Inventory.FindWorkshopStock(fp).Total);
        }

        [Test]
        public void SunsetWindDown_MidFetch_LeavesWorkshopOwnedPile_AuditPasses()
        {
            var (w, _) = Make();
            w.Tuning.HewingStockCapacity = 500;
            var (fp, op) = Workshop(w, ModuleTypes.HewingStationId, 60, 60);
            Logs(w, 400);
            int logsBefore = w.Inventory.TotalOf(Items.Log);
            for (int i = 0; i < 2500; i++)
            {
                w.Step(1);
                Assert.IsNull(w.Inventory.Audit(), "tick " + i);
            }
            Assert.IsTrue(Fetches(w).Any(t => t.State == TaskState.Cancelled), "sunset cancelled a fetch under way");
            Assert.IsTrue(w.Inventory.Locations.Any(l => l.Kind == LocationKind.GroundPile && l.OwnerOperationId == op.Id && l.CountOf(Items.Log.Id) > 0),
                "its load stays as a workshop-owned pile");
            Assert.AreEqual(logsBefore, w.Inventory.TotalOf(Items.Log) + w.Inventory.FindWorkshopStock(fp).Total,
                "no log is lost or made, wherever the fetches end up");
        }

        [Test]
        public void SaveMidFetch_RoundTripsByteIdentical_AndFinishes()
        {
            var (w, map) = Make();
            var (fp, _) = Workshop(w, ModuleTypes.HewingStationId, 60, 60);
            Logs(w, 4);
            for (int i = 0; i < 400 && !Fetches(w).Any(t => t.State == TaskState.Active && t.CurrentStep >= 1); i++) w.Step(1);
            Assert.IsTrue(Fetches(w).Any(t => t.State == TaskState.Active), "a fetch is under way");
            byte[] first = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(first, map);
            Assert.AreEqual(first, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash));
            Assert.IsNull(back.Inventory.Audit());
            LedgerLocation stock = back.Inventory.FindWorkshopStock(fp);
            for (int i = 0; i < 800 && stock.CountOf(Items.Log.Id) == 0; i++) back.Step(1);
            Assert.Greater(stock.CountOf(Items.Log.Id), 0);
        }
    }
}
