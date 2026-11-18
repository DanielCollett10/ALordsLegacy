using System.Linq;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
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

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>Yard job source 4 (ST-43, HW-06): beams to the yard by pool ox.</summary>
    public class YardBeamSourceTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld Make()
        {
            var w = SimWorld.FromMapData(new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]),
                new SimTuning { StartHour = 8, StartingPlanks = 0 });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 8);
            w.People.AddFamily(2, "F2", 40, 40);
            for (int i = 0; i < 2; i++) w.People.AddCitizen(20 + i, "C" + i, Sex.Male, -4_000_000, 2, 40, 40);
            Ox ox = w.People.AddOx(50, "O", 40, 44);
            ox.State = OxState.Resting(40000, 44000);
            return w;
        }

        private static Operation BuiltYard(SimWorld w, PointMm at)
        {
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.StorageYardId, at, 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int id = (int)r.Created[0];
            for (int i = 0; i < 6000 && !w.IsBuilt(id); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(id));
            w.Step(2); // let the workshop sweep free the oxen of the finished build deliveries
            return w.Operations.FindByModule(id);
        }

        private static LedgerLocation Hewing(SimWorld w, PointMm at, int beams)
        {
            Footprint fp = w.Footprints.Add(100 + w.Operations.Count, ModuleTypes.Find(ModuleTypes.HewingStationId), at, 0);
            Operation op = w.Operations.Create(OperationTypes.WorkshopOperationFor(ModuleTypes.HewingStationId), 0);
            w.Operations.AddModule(op.Id, fp.Id);
            WorkshopBuildings.OnBuilt(w, fp);
            LedgerLocation store = w.Inventory.FindGoodsStore(fp.Id);
            if (beams > 0) w.Inventory.Produce(store, Items.Beam, beams);
            return store;
        }

        private static int OpenFetches(SimWorld w, Operation yard) =>
            w.Tasks.ByOwner(yard.Id).Count(t => t.Def.Id == WorkshopSystem.FetchTaskId && !t.IsFinished);

        [Test]
        public void Threshold_FourOfTen_NoTask_FiveOfTen_OneBeamTaskWithOx()
        {
            SimWorld w = Make();
            Operation yard = BuiltYard(w, M(100, 100));
            LedgerLocation store = Hewing(w, M(60, 100), 4);
            var src = new YardBeamSource();
            Assert.IsNull(src.NextTrip(w, yard));
            Assert.AreEqual(0, OpenFetches(w, yard), "4/10 is below the threshold");

            w.Inventory.Produce(store, Items.Beam, 1);
            Assert.IsNull(src.NextTrip(w, yard), "it makes its own task, never a hand trip");
            TaskRecord t = w.Tasks.ByOwner(yard.Id).Single(x => x.Def.Id == WorkshopSystem.FetchTaskId);
            Assert.AreEqual(50, t.OxId);
            Assert.AreEqual(OxStateKind.Reserved, w.People.FindOx(50).State.Kind);
            Assert.AreEqual(1, t.Def.Steps.First(s => s.Kind == StepKind.LoadOx).Count, "1 beam per trip");
        }

        [Test]
        public void LooseBeamPile_GetsATask()
        {
            SimWorld w = Make();
            Operation yard = BuiltYard(w, M(100, 100));
            LedgerLocation pile = w.Inventory.GetOrAddGroundPile(M(70, 100), null);
            w.Inventory.Produce(pile, Items.Beam, 2);

            new YardBeamSource().NextTrip(w, yard);

            Assert.AreEqual(1, OpenFetches(w, yard));
        }

        [Test]
        public void NoFreeOx_NoTask()
        {
            SimWorld w = Make();
            Operation yard = BuiltYard(w, M(100, 100));
            Hewing(w, M(60, 100), 8);
            w.People.FindOx(50).State = OxState.Reserved(999); // e.g. a construction delivery took it first

            new YardBeamSource().NextTrip(w, yard);

            Assert.AreEqual(0, OpenFetches(w, yard));
        }

        [Test]
        public void OxAssignedToAnotherOperation_NeverBorrowed()
        {
            SimWorld w = Make();
            Operation yard = BuiltYard(w, M(100, 100));
            Hewing(w, M(60, 100), 8);
            Operation other = w.Operations.Create(OperationTypes.WorkshopOperationFor(ModuleTypes.SawpitId), 0);
            w.Operations.AssignOx(other.Id, 50);

            new YardBeamSource().NextTrip(w, yard);

            Assert.AreEqual(0, OpenFetches(w, yard));
        }

        [Test]
        public void FullYard_NoTask()
        {
            SimWorld w = Make();
            Operation yard = BuiltYard(w, M(100, 100));
            Hewing(w, M(60, 100), 8);
            LedgerLocation ys = w.Inventory.FindGoodsStore(yard.ModuleIds[0]);
            ys.Capacity = 1;
            w.Inventory.Produce(ys, Items.Plank, 1);

            new YardBeamSource().NextTrip(w, yard);

            Assert.AreEqual(0, OpenFetches(w, yard));
        }

        [Test]
        public void OnlyOneWaitingTaskPerYard()
        {
            SimWorld w = Make();
            Operation yard = BuiltYard(w, M(100, 100));
            Hewing(w, M(60, 100), 9);
            Ox second = w.People.AddOx(51, "O2", 41, 44);
            second.State = OxState.Resting(41000, 44000);
            var src = new YardBeamSource();

            src.NextTrip(w, yard);
            src.NextTrip(w, yard);

            Assert.AreEqual(1, OpenFetches(w, yard));
        }

        [Test]
        public void ConstructionDelivery_TakesTheOnlyFreeOxFirst_ThenYardBeamTripRuns_ThroughSimTicks()
        {
            SimWorld w = Make();
            Operation yard = BuiltYard(w, M(100, 100));
            LedgerLocation store = Hewing(w, M(60, 100), 8);
            Assert.IsTrue(w.Execute(new AssignFamily(yard.Id, 2)).IsAccepted);
            w.People.AddFamily(3, "Helpers", 40, 40); // unassigned: the site's helpers
            for (int i = 0; i < 2; i++) w.People.AddCitizen(30 + i, "H" + i, Sex.Male, -4_000_000, 3, 40, 40);
            LedgerLocation ys = w.Inventory.FindGoodsStore(yard.ModuleIds[0]);
            w.Inventory.Produce(w.Inventory.AddStockpile(M(25, 25)), Items.Log, 20);
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.SawpitId, M(50, 50), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);

            w.Step(1);
            TaskRecord deliver = w.Tasks.All.First(t => t.Def.Id == ConstructionTaskDefs.DeliverId);
            Assert.IsTrue(deliver.OxId.HasValue, "the delivery holds the only ox in this tick");
            Assert.AreEqual(0, OpenFetches(w, yard), "no yard beam task got the ox in that tick");

            bool yardTask = false;
            for (int i = 0; i < 40000 && ys.CountOf(Items.Beam.Id) == 0; i++)
            {
                w.Step(1);
                if (OpenFetches(w, yard) > 0) yardTask = true;
            }
            Assert.IsTrue(yardTask, "the yard trip was made once the ox was free");
            Assert.GreaterOrEqual(ys.CountOf(Items.Beam.Id), 1);
            Assert.Less(store.CountOf(Items.Beam.Id), 8);
        }

        [Test]
        public void Standard_IncludesBeamSource()
        {
            Assert.IsTrue(YardJobs.Standard().Sources.Any(s => s is YardBeamSource));
        }

        [Test]
        public void FullPipeline_BeamsReachTheYard_ThroughSimTicks()
        {
            SimWorld w = Make();
            Operation yard = BuiltYard(w, M(100, 100));
            LedgerLocation store = Hewing(w, M(60, 100), 8);
            Assert.IsTrue(w.Execute(new AssignFamily(yard.Id, 2)).IsAccepted);
            LedgerLocation ys = w.Inventory.FindGoodsStore(yard.ModuleIds[0]);

            for (int i = 0; i < 3000 && ys.CountOf(Items.Beam.Id) == 0; i++) w.Step(1);

            Assert.GreaterOrEqual(ys.CountOf(Items.Beam.Id), 1, "the ox carried a beam to the yard");
            Assert.Less(store.CountOf(Items.Beam.Id), 8);
        }
    }
}
