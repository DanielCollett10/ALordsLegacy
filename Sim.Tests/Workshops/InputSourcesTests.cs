using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.Operations.Workshops;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Workshops
{
    /// <summary>Ox input sources per item and ox steps that carry any item (T-522).</summary>
    public class InputSourcesTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld NewWorld() => new SimWorld(new SimTuning());

        private static LedgerLocation Pile(SimWorld w, double x, int logs)
        {
            LedgerLocation p = w.Inventory.GetOrAddGroundPile(M(x, 100), null);
            w.Inventory.Produce(p, Items.Log, logs);
            return p;
        }

        private static LedgerLocation Storage(SimWorld w, int id, double x, int logs)
        {
            w.Footprints.Add(id, ModuleTypes.LogStorage, M(x, 100), 0);
            w.Operations.AddModule(w.Operations.Create(LordsLegacy.Sim.Operations.OperationTypes.WoodcuttingId, id).Id, id);
            LedgerLocation l = w.Inventory.GetOrAddStorage(id, ModuleTypes.LogStorage);
            w.Inventory.Produce(l, Items.Log, logs);
            return l;
        }

        private static LedgerLocation HewingStore(SimWorld w, int id, double x, int beams)
        {
            w.Footprints.Add(id, ModuleTypes.HewingStation, M(x, 100), 0);
            LedgerLocation l = w.Inventory.AddGoodsStore(20, id);
            w.Inventory.AddWorkshopStock(id, Items.Log, 6);
            if (beams > 0) w.Inventory.Produce(l, Items.Beam, beams);
            return l;
        }

        [Test]
        public void Logs_NearerGroundPileWins_ThenNearerStorageWins()
        {
            SimWorld w = NewWorld();
            LedgerLocation pile = Pile(w, 10, 2);
            LedgerLocation store = Storage(w, 50, 60, 3);
            Assert.AreSame(pile, InputSources.Nearest(w, Items.Log, M(0, 100)));
            Assert.AreSame(store, InputSources.Nearest(w, Items.Log, M(100, 100)));
        }

        [Test]
        public void Beams_NearestHewingStationStore()
        {
            SimWorld w = NewWorld();
            HewingStore(w, 60, 10, 2);
            LedgerLocation far = HewingStore(w, 61, 90, 2);
            Assert.AreEqual(far.Id, InputSources.Nearest(w, Items.Beam, M(100, 100)).Id);
            Assert.AreEqual(w.Inventory.FindGoodsStore(60).Id, InputSources.Nearest(w, Items.Beam, M(0, 100)).Id);
        }

        [Test]
        public void ReservedStock_IsNotReturned()
        {
            SimWorld w = NewWorld();
            LedgerLocation near = Pile(w, 10, 2);
            LedgerLocation far = Pile(w, 80, 2);
            w.Inventory.ReserveStock(near, Items.Log, 2, 99);
            Assert.AreSame(far, InputSources.Nearest(w, Items.Log, M(0, 100)));
            w.Inventory.ReserveStock(far, Items.Log, 2, 99);
            Assert.IsNull(InputSources.Nearest(w, Items.Log, M(0, 100)));
        }

        [Test]
        public void WorkshopStock_IsNeverReturned()
        {
            SimWorld w = NewWorld();
            LedgerLocation store = HewingStore(w, 60, 10, 0);
            LedgerLocation stock = w.Inventory.FindWorkshopStock(60);
            w.Inventory.Produce(stock, Items.Log, 3);
            Assert.IsNull(InputSources.Nearest(w, Items.Log, M(10, 100)), "logs in a workshop stock are not a source");
            Assert.IsNull(InputSources.Nearest(w, Items.Beam, M(10, 100)), "an empty store is not a source");
            w.Inventory.Produce(store, Items.Beam, 1);
            Assert.AreSame(store, InputSources.Nearest(w, Items.Beam, M(10, 100)));
        }

        [Test]
        public void RegisteringAFinder_AddsASource_WithoutChangingTheSearch()
        {
            SimWorld w = NewWorld();
            LedgerLocation fake = w.Inventory.AddGoodsStore(20, 0);
            w.Inventory.Produce(fake, Items.Plank, 2);
            var sources = InputSources.CreateDefault();
            Assert.IsNull(sources.Find(w, Items.Plank, M(0, 0)));
            sources.Register(Items.Plank, (world, item) => new[] { new InputSource(fake, M(5, 5)) });
            Assert.AreSame(fake, sources.Find(w, Items.Plank, M(0, 0)));
        }

        [Test]
        public void OxSteps_DefaultToLogs_AndCarryAnyItem()
        {
            Assert.AreEqual(Items.Log.Id, TaskStep.LoadOx(1, 1, 5).ItemId);
            Assert.AreEqual(Items.Log.Id, TaskStep.UnloadOx(1, 1, 5).ItemId);
            Assert.AreEqual(Items.Beam.Id, TaskStep.LoadOx(1, 1, 5, false, Items.Beam.Id).ItemId);
        }

        [Test]
        public void SaveRoundTrip_KeepsOxStepItemId()
        {
            MapData map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            SimWorld w = SimWorld.FromMapData(map, new SimTuning());
            var def = new TaskDef("task.haul_logs", new[]
            {
                TaskStep.FetchOx(),
                TaskStep.LoadOx(3, 1, 5, false, Items.Beam.Id),
                TaskStep.UnloadOx(4, 1, 5, false, Items.Beam.Id),
                TaskStep.ReturnOx(),
            }, EligibilityPolicy.AssignedFamilyOnly, RequiredResource.Ox);
            TaskRecord t = w.Tasks.Create(def, 1, null, TaskPriority.High);

            byte[] bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(bytes, map);
            var steps =back.Tasks.Get(t.Id).Def.Steps;
            Assert.AreEqual(Items.Beam.Id, steps[1].ItemId);
            Assert.AreEqual(Items.Beam.Id, steps[2].ItemId);
            Assert.AreEqual(bytes, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash));
        }
    }
}
