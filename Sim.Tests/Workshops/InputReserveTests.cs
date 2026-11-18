using System.Linq;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Operations.Workshops;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Workshops
{
    /// <summary>The workshop input reserve (T-537, PL-30..PL-34).</summary>
    public class InputReserveTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld Make(int oxen, int reserve = 5)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8, WorkshopDefaultReserve = reserve });
            for (int fam = 2; fam <= 4; fam++)
            {
                w.People.AddFamily(fam, "F" + fam, 40, 40);
                for (int i = 0; i < 2; i++) w.People.AddCitizen(fam * 10 + i, "C" + fam + i, Sex.Male, AdultBirth, fam, 40, 40);
            }
            for (int i = 0; i < oxen; i++) w.People.AddOx(50 + i, "O" + i, 40, 44 + i).State = OxState.Resting(40000, 44000 + 1000 * i);
            return w;
        }

        private static int Workshop(SimWorld w, string type, double x, double z, int family)
        {
            int id = w.Footprints.Add(100 + w.Operations.Count, ModuleTypes.Find(type), M(x, z), 0).Id;
            Operation op = w.Operations.Create(OperationTypes.WorkshopOperationFor(type), 0);
            w.Operations.AddModule(op.Id, id);
            WorkshopBuildings.OnBuilt(w, w.Footprints.Get(id));
            Assert.IsTrue(w.Execute(new AssignFamily(op.Id, family)).IsAccepted);
            return id;
        }

        private static int Fetches(SimWorld w) =>
            w.Tasks.All.Count(t => t.Def.Id == WorkshopSystem.FetchTaskId && !t.IsFinished);

        private static LedgerLocation Logs(SimWorld w, int n)
        {
            LedgerLocation p = w.Inventory.AddStockpile(M(20, 20));
            if (n > 0) w.Inventory.Produce(p, Items.Log, n);
            return p;
        }

        [TestCase(0, 0)]
        [TestCase(5, 0)]
        [TestCase(6, 1)]
        [TestCase(7, 2)]
        [TestCase(30, 3)]
        public void Logs_FetchesAllowedAboveReserve_CountingFetchesOnTheWay(int logs, int expected)
        {
            SimWorld w = Make(oxen: 3);
            Workshop(w, ModuleTypes.HewingStationId, 60, 60, 3);
            Logs(w, logs);
            w.Step(1);
            Assert.AreEqual(expected, Fetches(w), "reserve 5, " + logs + " logs, 3 oxen");
            w.Step(3);
            Assert.AreEqual(expected, Fetches(w), "later ticks open no more while the fetches are on the way");
        }

        [TestCase(66, 6)]
        [TestCase(60, 0)]
        [TestCase(59, 0)]
        public void Logs_ReserveSixty_AtMostTheSurplusIsFetched(int logs, int expected)
        {
            SimWorld w = Make(oxen: 10, reserve: 60);
            Workshop(w, ModuleTypes.HewingStationId, 60, 60, 3);
            Logs(w, logs);
            w.Step(1);
            Assert.AreEqual(expected, Fetches(w));
        }

        [Test]
        public void Logs_AtReserve_NoFetchAtAll_EvenWithLogsOnTheGround()
        {
            SimWorld w = Make(oxen: 2);
            Workshop(w, ModuleTypes.HewingStationId, 60, 60, 3);
            Logs(w, 5);
            LedgerLocation ground = w.Inventory.AddGroundPile(M(30, 30), null);
            w.Inventory.Produce(ground, Items.Log, 40);
            w.Step(5);
            Assert.AreEqual(0, Fetches(w));
        }

        [Test]
        public void Logs_InputStockAndLogsOnTheGround_AreNeverCounted()
        {
            SimWorld w = Make(oxen: 2);
            int hew = Workshop(w, ModuleTypes.HewingStationId, 60, 60, 3);
            w.Inventory.Produce(w.Inventory.FindWorkshopStock(hew), Items.Log, 4);
            LedgerLocation ground = w.Inventory.AddGroundPile(M(30, 30), null);
            w.Inventory.Produce(ground, Items.Log, 40);
            Logs(w, 3);
            Assert.AreEqual(3, InputReserve.InStorage(w, Items.Log));
        }

        [Test]
        public void Logs_ReservedLogsInStorage_StillCount()
        {
            SimWorld w = Make(oxen: 2);
            Workshop(w, ModuleTypes.HewingStationId, 60, 60, 3);
            LedgerLocation pile = Logs(w, 6);
            w.Inventory.ReserveStock(pile, Items.Log, 5, 999);
            Assert.AreEqual(6, InputReserve.InStorage(w, Items.Log));
        }

        [TestCase(5, 0)]
        [TestCase(6, 1)]
        [TestCase(7, 2)]
        public void Beams_FetchesAllowedAboveReserve_CountingFetchesOnTheWay(int beams, int expected)
        {
            SimWorld w = Make(oxen: 3);
            int hew = Workshop(w, ModuleTypes.HewingStationId, 60, 60, 3);
            Workshop(w, ModuleTypes.SawpitId, 70, 70, 4);
            w.Inventory.Produce(w.Inventory.FindGoodsStore(hew), Items.Beam, beams);
            w.Step(1);
            Assert.AreEqual(expected, Fetches(w), "reserve 5, " + beams + " beams");
        }

        [Test]
        public void ReserveZero_MeansNone()
        {
            SimWorld w = Make(oxen: 1, reserve: 0);
            Workshop(w, ModuleTypes.HewingStationId, 60, 60, 3);
            Logs(w, 1);
            w.Step(1);
            Assert.AreEqual(1, Fetches(w));
        }

        [Test]
        public void Reserve_StopsOnlyNewFetches_FetchedWorkIsProcessed()
        {
            SimWorld w = Make(oxen: 1);
            int hew = Workshop(w, ModuleTypes.HewingStationId, 60, 60, 3);
            w.Inventory.Produce(w.Inventory.FindWorkshopStock(hew), Items.Log, 2);
            Logs(w, 0);
            w.Step(Day);
            Assert.Greater(w.Inventory.FindGoodsStore(hew).CountOf(Items.Beam.Id), 0, "the 2 logs in the stock are hewn although storage is empty");
        }

        private const int Day = 1440;

        [Test]
        public void OneHewingStationAndOneSawpit_NoYard_DefaultReserve_PlanksKeepBeingMadeOverTenDays()
        {
            SimWorld w = Make(oxen: 2);
            int hew = Workshop(w, ModuleTypes.HewingStationId, 60, 60, 3);
            int saw = Workshop(w, ModuleTypes.SawpitId, 70, 70, 4);
            Logs(w, 80);
            LedgerLocation planks = w.Inventory.FindGoodsStore(saw);
            int made = 0, secondHalf = 0;
            for (int d = 0; d < 10; d++)
            {
                w.Step(Day);
                int n = planks.CountOf(Items.Plank.Id);
                made += n;
                if (d >= 5) secondHalf += n;
                if (n > 0) w.Inventory.Consume(planks, Items.Plank, n); // the village uses them
                Assert.IsNull(w.Inventory.Audit(), "day " + d);
            }
            Assert.Greater(made, 0);
            Assert.Greater(secondHalf, 0, "planks are still being made in days 6-10, not stuck on the reserve");
            Assert.IsNotNull(w.Inventory.FindGoodsStore(hew));
        }
    }
}
