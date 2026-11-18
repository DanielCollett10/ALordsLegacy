using System.Collections.Generic;
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
    /// <summary>T-560, decision 0051, MAT-23, PL-30: hand-carried inputs are fetched with task.haul_by_hand.</summary>
    public class WorkshopHandFetchTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static WorkshopDef RodWorkshop() => new WorkshopDef("workshop.rods",
            new[] { new RecipeInput(Items.WillowRod, 5, CarryClass.Hand, 40), new RecipeInput(Items.Log, 1, CarryClass.Ox, 10) },
            new[] { new RecipeOutput(Items.Basket, 1) }, 2, 40, 20, 1);

        [TearDown]
        public void TearDown() => OperationTypes.TestDefs = null;

        private static (SimWorld w, int fp, Operation op) Make(int reserve = 0)
        {
            OperationTypes.TestDefs = new Dictionary<string, WorkshopDef> { { OperationTypes.HewingId, RodWorkshop() } };
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8, WorkshopDefaultReserve = reserve });
            w.People.AddFamily(3, "F3", 60, 60);
            w.People.AddCitizen(30, "C", Sex.Male, AdultBirth, 3, 60, 60);
            w.People.AddOx(50, "O", 60, 64).State = OxState.Resting(60000, 64000);
            int id = w.Footprints.Add(100, ModuleTypes.Find(ModuleTypes.HewingStationId), M(60, 60), 0).Id;
            Operation op = w.Operations.Create(OperationTypes.HewingId, 0);
            w.Operations.AddModule(op.Id, id);
            WorkshopBuildings.OnBuilt(w, w.Footprints.Get(id));
            Assert.IsTrue(w.Execute(new AssignFamily(op.Id, 3)).IsAccepted);
            return (w, id, op);
        }

        private static LedgerLocation Building(SimWorld w, int id, string typeId, double x, double z, ItemDef item, int n)
        {
            int fid = w.Footprints.Add(id, ModuleTypes.Find(typeId), M(x, z), 0).Id;
            WorkshopBuildings.OnBuilt(w, w.Footprints.Get(fid));
            LedgerLocation s = w.Inventory.FindGoodsStore(fid);
            w.Inventory.Produce(s, item, n);
            return s;
        }

        private static List<TaskRecord> Hauls(SimWorld w)
        {
            var l = new List<TaskRecord>();
            foreach (TaskRecord t in w.Tasks.All) if (t.Def.Id == HandHaulTaskDefs.HaulId && !t.IsFinished) l.Add(t);
            return l;
        }

        private static int SourceOf(TaskRecord t)
        {
            foreach (TaskStep s in t.Def.Steps) if (s.Kind == StepKind.CollectItems) return s.LocationId;
            return -1;
        }

        [Test]
        public void Rods_FetchedByHand_UpToHandLoad_NoOxUsed()
        {
            var (w, fp, _) = Make();
            LedgerLocation hut = Building(w, 101, ModuleTypes.CoppiceHutId, 70, 60, Items.WillowRod, 30);
            w.Step(1);
            List<TaskRecord> hauls = Hauls(w);
            Assert.AreEqual(1, hauls.Count, "one working family: one trip");
            Assert.AreEqual(hut.Id, SourceOf(hauls[0]));
            Assert.AreEqual(w.Tuning.HandCarryRods, InputReserve.OnTheWay(w, Items.WillowRod));
            Assert.IsFalse(hauls[0].OxId.HasValue, "no ox on a hand trip");
            Assert.AreEqual(OxStateKind.Resting, w.People.FindOx(50).State.Kind);
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void Rods_ArriveInWorkshopStock_ThroughRealTicks()
        {
            var (w, fp, _) = Make();
            Building(w, 101, ModuleTypes.CoppiceHutId, 70, 60, Items.WillowRod, 30);
            LedgerLocation stock = w.Inventory.FindWorkshopStock(fp, Items.WillowRod.Id);
            for (int i = 0; i < 600 && stock.CountOf(Items.WillowRod.Id) == 0; i++) w.Step(1);
            Assert.AreEqual(w.Tuning.HandCarryRods, stock.CountOf(Items.WillowRod.Id).CompareTo(0) > 0 ? w.Tuning.HandCarryRods : -1);
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void Rods_NearestSourceChosen_AmongHutAndYard()
        {
            var (w, _, _) = Make();
            Building(w, 101, ModuleTypes.StorageYardId, 90, 60, Items.WillowRod, 30);
            LedgerLocation near = Building(w, 102, ModuleTypes.CoppiceHutId, 66, 60, Items.WillowRod, 30);
            w.Step(1);
            Assert.AreEqual(near.Id, SourceOf(Hauls(w)[0]));
        }

        [Test]
        public void Rods_YardIsASource_WhenNearest()
        {
            var (w, _, _) = Make();
            LedgerLocation yard = Building(w, 101, ModuleTypes.StorageYardId, 65, 60, Items.WillowRod, 30);
            Building(w, 102, ModuleTypes.CoppiceHutId, 95, 60, Items.WillowRod, 30);
            w.Step(1);
            Assert.AreEqual(yard.Id, SourceOf(Hauls(w)[0]));
        }

        [Test]
        public void Rods_ReservedStockAndWorkshopStockAreNeverSources()
        {
            var (w, fp, _) = Make();
            LedgerLocation hut = Building(w, 101, ModuleTypes.CoppiceHutId, 70, 60, Items.WillowRod, 30);
            Assert.AreNotEqual(0, w.Inventory.ReserveStock(hut, Items.WillowRod, 30, 9999));
            Assert.AreEqual(0, hut.AvailableOf(Items.WillowRod.Id));
            w.Inventory.Produce(w.Inventory.FindWorkshopStock(fp, Items.WillowRod.Id), Items.WillowRod, 5);
            Assert.IsNull(InputSources.Nearest(w, Items.WillowRod, M(60, 60)));
            w.Step(2);
            Assert.AreEqual(0, Hauls(w).Count);
        }

        [Test]
        public void Rods_ReserveRespected_TripCountsNAgainstIt()
        {
            // 15 rods in storage, reserve 6: one trip of 10 would leave 5 < 6, so no trip.
            var (w, _, op) = Make(6);
            Building(w, 101, ModuleTypes.CoppiceHutId, 70, 60, Items.WillowRod, 15);
            w.Step(2);
            Assert.AreEqual(0, Hauls(w).Count);
            // 16 rods leaves exactly 6: allowed, and counts as taken so a second trip is not opened.
            var (w2, _, _) = Make(6);
            Building(w2, 101, ModuleTypes.CoppiceHutId, 70, 60, Items.WillowRod, 16);
            w2.Step(1);
            Assert.AreEqual(1, Hauls(w2).Count);
            Assert.AreEqual(10, InputReserve.OnTheWay(w2, Items.WillowRod));
            Assert.AreEqual(6, InputReserve.Free(w2, Items.WillowRod));
        }

        [Test]
        public void Rods_InHutAndYardStoresCountAsStorage()
        {
            var (w, _, _) = Make();
            Building(w, 101, ModuleTypes.CoppiceHutId, 70, 60, Items.WillowRod, 7);
            Building(w, 102, ModuleTypes.StorageYardId, 80, 60, Items.WillowRod, 5);
            Assert.AreEqual(12, InputReserve.InStorage(w, Items.WillowRod));
            Assert.AreEqual(0, InputReserve.InStorage(w, Items.HazelRod));
        }

        [Test]
        public void Logs_StayOx_NoHandTripForOxInput()
        {
            var (w, _, _) = Make();
            w.Inventory.Produce(w.Inventory.AddStockpile(M(62, 62)), Items.Log, 20);
            w.Step(2);
            Assert.AreEqual(0, Hauls(w).Count);
            Assert.AreEqual(1, InputReserve.OnTheWay(w, Items.Log));
        }
    }
}
