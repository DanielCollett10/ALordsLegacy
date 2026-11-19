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
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Workshops
{
    /// <summary>The daub pit: first zero-input workshop (T-583, HL-01..02, ST-42b, decision 0051).</summary>
    public class DaubPitTests
    {
        private const long AdultBirth = -4_000_000;
        private const int Day = 1440;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld Make(int logs)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8, StartingPlanks = 0 });
            if (logs > 0) w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, logs);
            for (int fam = 3; fam <= 4; fam++)
            {
                w.People.AddFamily(fam, "F" + fam, 40, 40);
                for (int i = 0; i < 2; i++) w.People.AddCitizen(fam * 10 + i, "C" + fam + i, Sex.Male, AdultBirth, fam, 40, 40);
            }
            w.People.AddOx(50, "O", 40, 44).State = OxState.Resting(40000, 44000);
            return w;
        }

        private static int PlaceAndBuild(SimWorld w, PointMm at)
        {
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.DaubPitId, at, 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int id = (int)r.Created[0];
            for (int i = 0; i < 6000 && !w.IsBuilt(id); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(id), "built by workers through Sim ticks only");
            return id;
        }

        private static (SimWorld w, int id, Operation op) Working()
        {
            SimWorld w = Make(4);
            int id = PlaceAndBuild(w, M(50, 50));
            Operation op = w.Operations.FindByModule(id);
            Assert.IsTrue(w.Execute(new AssignFamily(op.Id, 3)).IsAccepted);
            return (w, id, op);
        }

        [Test]
        public void Daub_IsGeneral_HandLoadTwo_InCatalogue()
        {
            Assert.AreEqual(ItemCategory.General, Items.Daub.Category);
            Assert.AreEqual(2, HandLoads.For(Items.Daub, new SimTuning()));
            Assert.AreSame(Items.Daub, Items.Find("item.daub"));
        }

        [Test]
        public void Def_NoInputs_TenPerDay_StoreTwenty_LightWork()
        {
            WorkshopDef d = WorkshopDef.Daubing(new SimTuning());
            Assert.AreEqual(0, d.Inputs.Count);
            Assert.AreEqual(Items.Daub.Id, d.Output.Id);
            Assert.AreEqual(10, d.BatchesPerFamilyDay);
            Assert.AreEqual(20, d.OutputCapacity);
            Assert.IsTrue(d.IsLightWork);
            Assert.AreEqual(new SimTuning().ForagerMinAge, d.MinWorkAgeYears);
            Assert.IsFalse(WorkshopBuildings.IsWorkshop(d), "no input, so no yard collection (ST-42b)");
        }

        [Test]
        public void Type_FourByFour_FourLogs240Min_OneFamilySlot()
        {
            FootprintTypeDef t = ModuleTypes.Find(ModuleTypes.DaubPitId);
            Assert.AreEqual(4000, t.WidthMm);
            Assert.AreEqual(4000, t.DepthMm);
            Assert.AreEqual(4, t.ConstructionRecipe.Logs);
            Assert.AreEqual(240, t.ConstructionRecipe.BuildWorkerMinutes);
            Assert.AreEqual(OperationTypes.DaubingId, OperationTypes.WorkshopOperationFor(ModuleTypes.DaubPitId));
            Assert.AreEqual(1, OperationTypes.Find(OperationTypes.DaubingId).FamilySlots);
        }

        [Test]
        public void Place_RefusedWithThreeLogs()
        {
            SimWorld w = Make(3);
            Assert.IsTrue(w.Execute(new PlaceStructure(ModuleTypes.DaubPitId, M(50, 50), 0)).IsRejected);
        }

        [Test]
        public void Built_HasStoreOnly_NoInputStock()
        {
            var (w, id, _) = Working();
            Assert.AreEqual(20, w.Inventory.FindGoodsStore(id).Capacity);
            Assert.IsFalse(w.Inventory.Locations.Any(l => l.Kind == LocationKind.WorkshopStock && l.ModuleId == id));
        }

        [Test]
        public void WorkingDay_MakesUpToTenDaub_NoFetchTasks()
        {
            var (w, id, op) = Working();
            w.Step(2 * Day);
            int daub = w.Inventory.FindGoodsStore(id).CountOf(Items.Daub.Id);
            Assert.That(daub, Is.InRange(8, 20), "up to 10 a day, less the commute; capped by the 20 store");
            Assert.IsFalse(w.Tasks.ByOwner(op.Id).Any(t => t.Def.Id == WorkshopSystem.FetchTaskId || t.Def.Id == "task.haul_by_hand"),
                "a zero-input workshop never fetches");
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void NoFamily_StatusNoWorkers_NeverWaitingForInput()
        {
            SimWorld w = Make(4);
            int id = PlaceAndBuild(w, M(50, 50));
            Operation op = w.Operations.FindByModule(id);
            Assert.AreEqual(OperationState.NoWorkers, OperationStatus.Reported(w, op));
        }

        [Test]
        public void StoreFull_StopsWork_StatusStoreFull_AndRestartsWhenASiteTakesDaub()
        {
            var (w, id, op) = Working();
            LedgerLocation store = w.Inventory.FindGoodsStore(id);
            for (int i = 0; i < 6 * Day && store.CountOf(Items.Daub.Id) < 20; i++) w.Step(1);
            Assert.AreEqual(20, store.CountOf(Items.Daub.Id), "filled to the store limit");
            w.Step(Day / 4); // let any open work finish
            Assert.AreEqual(OperationState.WorkshopStoreFull, OperationStatus.Reported(w, op));
            Assert.AreNotEqual(OperationState.WaitingForInput, OperationStatus.Reported(w, op));
            int before = store.CountOf(Items.Daub.Id);
            w.Step(Day / 4);
            Assert.AreEqual(before, store.CountOf(Items.Daub.Id), "stopped while full");

            // The pit store is a construction source: a site reserves and takes 10 daub.
            Assert.AreEqual(20, ConstructionSources.Available(w, Items.Daub));
            var ids = ConstructionSources.TryReserve(w, Items.Daub, 10, M(60, 60), 9999);
            Assert.IsNotNull(ids, "a site may reserve daub from the pit store");
            foreach (int rid in ids) w.Inventory.Release(rid);
            Assert.IsTrue(w.Inventory.Consume(store, Items.Daub, 10));

            for (int i = 0; i < 2 * Day && store.CountOf(Items.Daub.Id) <= 10; i++) w.Step(1);
            Assert.Greater(store.CountOf(Items.Daub.Id), 10, "work restarted once there was room");
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void YardFamily_IgnoresFullDaubPitStore()
        {
            SimWorld w = Make(12);
            int pit = PlaceAndBuild(w, M(60, 100));
            int yardId = PlaceYard(w, M(100, 100));
            Operation yard = w.Operations.FindByModule(yardId);
            w.Inventory.Produce(w.Inventory.FindGoodsStore(pit), Items.Daub, 20);

            Assert.IsNull(new YardWorkshopSource().NextTrip(w, yard), "ST-42b: yards only collect from workshops with an input");
            w.Execute(new AssignFamily(yard.Id, 4));
            w.Step(Day);
            Assert.AreEqual(20, w.Inventory.FindGoodsStore(pit).CountOf(Items.Daub.Id), "nothing was carried away");
        }

        private static int PlaceYard(SimWorld w, PointMm at)
        {
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.StorageYardId, at, 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int id = (int)r.Created[0];
            for (int i = 0; i < 6000 && !w.IsBuilt(id); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(id));
            return id;
        }
    }
}
