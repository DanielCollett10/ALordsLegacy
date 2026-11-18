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

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>
    /// Storage yards as sources (T-541, ST-06, ST-07, PL-31): construction planks, sawpit beam fetches, the beam reserve
    /// and the village totals all count yard stock by distance.
    /// </summary>
    public class YardSourcesTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        // Geometry (metres): yard at (150,50); sawpit store at (30,150); mid sawpit store at (100,100).
        // Site near the yard (140,60) is ~14 m from the yard, ~56 m from mid and ~134 m from the far store.
        private static readonly PointMm YardSite = M(140, 60);
        private static readonly PointMm SawSite = M(30, 140);

        private static SimWorld Make()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8, WorkshopDefaultReserve = 5 });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 8);
            for (int fam = 2; fam <= 4; fam++)
            {
                w.People.AddFamily(fam, "F" + fam, 40, 40);
                for (int i = 0; i < 2; i++) w.People.AddCitizen(fam * 10 + i, "C" + fam + i, Sex.Male, AdultBirth, fam, 40, 40);
            }
            Ox ox = w.People.AddOx(50, "O", 40, 44);
            ox.State = OxState.Resting(40000, 44000);
            return w;
        }

        /// <summary>A storage yard placed, built by workers through Sim ticks only (ST-03), returned with its goods store.</summary>
        private static LedgerLocation BuiltYard(SimWorld w, PointMm at)
        {
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.StorageYardId, at, 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int id = (int)r.Created[0];
            for (int i = 0; i < 6000 && !w.IsBuilt(id); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(id), "yard built through Sim ticks");
            LedgerLocation store = w.Inventory.FindGoodsStore(id);
            Assert.IsNotNull(store);
            return store;
        }

        /// <summary>A built workshop (hewing station or sawpit) with its output goods store; shortcut for fixtures.</summary>
        private static LedgerLocation WorkshopStore(SimWorld w, string type, PointMm at)
        {
            Footprint fp = w.Footprints.Add(100 + w.Operations.Count, ModuleTypes.Find(type), at, 0);
            Operation op = w.Operations.Create(OperationTypes.WorkshopOperationFor(type), 0);
            w.Operations.AddModule(op.Id, fp.Id);
            WorkshopBuildings.OnBuilt(w, fp);
            return w.Inventory.FindGoodsStore(fp.Id);
        }

        private static LedgerLocation Sawpit(SimWorld w, PointMm at) => WorkshopStore(w, ModuleTypes.SawpitId, at);
        private static LedgerLocation Hewing(SimWorld w, PointMm at) => WorkshopStore(w, ModuleTypes.HewingStationId, at);

        // ---- Planks for construction (ST-06): nearest store with stock, by distance to the site ----

        [Test]
        public void Planks_ReservedFromYard_WhenYardIsNearerThanSawpitStore()
        {
            SimWorld w = Make();
            LedgerLocation sawpit = Sawpit(w, M(30, 150));
            LedgerLocation yard = BuiltYard(w, M(150, 50));
            w.Inventory.Produce(sawpit, Items.Plank, 10);
            w.Inventory.Produce(yard, Items.Plank, 10);

            var ids = ConstructionSources.TryReserve(w, Items.Plank, 4, YardSite, 1);

            Assert.IsNotNull(ids);
            Assert.AreEqual(6, yard.AvailableOf(Items.Plank.Id), "yard is the nearest store with stock");
            Assert.AreEqual(10, sawpit.AvailableOf(Items.Plank.Id));
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void Planks_ReservedFromSawpitStore_WhenSawpitStoreIsNearerThanYard()
        {
            SimWorld w = Make();
            LedgerLocation sawpit = Sawpit(w, M(30, 150));
            LedgerLocation yard = BuiltYard(w, M(150, 50));
            w.Inventory.Produce(sawpit, Items.Plank, 10);
            w.Inventory.Produce(yard, Items.Plank, 10);

            var ids = ConstructionSources.TryReserve(w, Items.Plank, 4, SawSite, 1);

            Assert.IsNotNull(ids);
            Assert.AreEqual(6, sawpit.AvailableOf(Items.Plank.Id));
            Assert.AreEqual(10, yard.AvailableOf(Items.Plank.Id), "farther yard untouched");
        }

        [Test]
        public void Planks_YardCountsAsItsFootprintCentre_NotTheOrigin_WhenSawpitCreatedFirst()
        {
            // Regression: stores bound to a footprint have no position of their own; both would sit at (0,0) and the
            // earlier-created sawpit store would win the tie on id even though the yard is nearer the site.
            SimWorld w = Make();
            LedgerLocation sawpit = Sawpit(w, M(30, 150));
            LedgerLocation yard = BuiltYard(w, M(150, 50));
            w.Inventory.Produce(sawpit, Items.Plank, 10);
            w.Inventory.Produce(yard, Items.Plank, 10);

            ConstructionSources.TryReserve(w, Items.Plank, 3, YardSite, 1);

            Assert.AreEqual(7, yard.AvailableOf(Items.Plank.Id));
            Assert.AreEqual(10, sawpit.AvailableOf(Items.Plank.Id));
        }

        [Test]
        public void Planks_SplitAcrossYardThenNearestNext_WhenOneStoreRunsOut_ThreeStores()
        {
            // Three shapes of store at increasing distance from the site: yard (6), mid sawpit (4), far sawpit (10).
            SimWorld w = Make();
            LedgerLocation far = Sawpit(w, M(30, 150));
            LedgerLocation mid = Sawpit(w, M(100, 100));
            LedgerLocation yard = BuiltYard(w, M(150, 50));
            w.Inventory.Produce(yard, Items.Plank, 6);
            w.Inventory.Produce(mid, Items.Plank, 4);
            w.Inventory.Produce(far, Items.Plank, 10);

            var ids = ConstructionSources.TryReserve(w, Items.Plank, 12, YardSite, 1);

            Assert.IsNotNull(ids);
            Assert.AreEqual(0, yard.AvailableOf(Items.Plank.Id));
            Assert.AreEqual(0, mid.AvailableOf(Items.Plank.Id));
            Assert.AreEqual(8, far.AvailableOf(Items.Plank.Id), "2 taken from the far store");
            Assert.AreEqual(3, ids.Count);
        }

        [Test]
        public void Planks_Refused_WhenYardHasNoStockAndNoOtherStoreDoes()
        {
            SimWorld w = Make();
            BuiltYard(w, M(150, 50));

            Assert.IsNull(ConstructionSources.TryReserve(w, Items.Plank, 1, YardSite, 1));
            Assert.AreEqual(0, ConstructionSources.Available(w, Items.Plank));
        }

        // ---- Beams for sawpits (ST-07): nearer of a hewing store or a yard ----

        [Test]
        public void BeamFetch_FromYard_WhenYardIsNearerThanHewingStore()
        {
            SimWorld w = Make();
            LedgerLocation hewing = Hewing(w, M(30, 150));
            LedgerLocation yard = BuiltYard(w, M(150, 50));
            w.Inventory.Produce(hewing, Items.Beam, 5);
            w.Inventory.Produce(yard, Items.Beam, 5);

            LedgerLocation pick = InputSources.Nearest(w, Items.Beam, YardSite);

            Assert.AreSame(yard, pick);
        }

        [Test]
        public void BeamFetch_FromHewingStore_WhenHewingStoreIsNearerThanYard()
        {
            SimWorld w = Make();
            LedgerLocation hewing = Hewing(w, M(30, 150));
            LedgerLocation yard = BuiltYard(w, M(150, 50));
            w.Inventory.Produce(hewing, Items.Beam, 5);
            w.Inventory.Produce(yard, Items.Beam, 5);

            LedgerLocation pick = InputSources.Nearest(w, Items.Beam, SawSite);

            Assert.AreSame(hewing, pick);
        }

        [Test]
        public void BeamFetch_SkipsYard_WhenYardHasNoBeamsLeft()
        {
            SimWorld w = Make();
            LedgerLocation hewing = Hewing(w, M(30, 150));
            LedgerLocation yard = BuiltYard(w, M(150, 50));
            w.Inventory.Produce(hewing, Items.Beam, 5);

            Assert.AreSame(hewing, InputSources.Nearest(w, Items.Beam, YardSite), "empty yard is never picked");
        }

        // ---- Beam reserve (PL-31): yard beams count in storage ----

        [Test]
        public void InputReserve_CountsYardBeams_InStorageAndInTheFreeAmount()
        {
            SimWorld w = Make();
            LedgerLocation hewing = Hewing(w, M(30, 150));
            LedgerLocation yard = BuiltYard(w, M(150, 50));
            w.Inventory.Produce(hewing, Items.Beam, 3);
            w.Inventory.Produce(yard, Items.Beam, 7);
            Operation sawpit = w.Operations.Create(OperationTypes.WorkshopOperationFor(ModuleTypes.SawpitId), 0);

            Assert.AreEqual(10, InputReserve.InStorage(w, Items.Beam), "3 hewing + 7 yard");
            Assert.AreEqual(10, InputReserve.Free(w, Items.Beam));
            Assert.IsTrue(InputReserve.Allows(w, sawpit, Items.Beam, 5), "10 - 5 keeps the reserve of 5");
            Assert.IsFalse(InputReserve.Allows(w, sawpit, Items.Beam, 6), "10 - 6 falls below the reserve of 5");
        }

        [Test]
        public void InputReserve_YardBeamsOnly_DecideTheFetch()
        {
            SimWorld w = Make();
            LedgerLocation yard = BuiltYard(w, M(150, 50));
            w.Inventory.Produce(yard, Items.Beam, 6);
            Operation sawpit = w.Operations.Create(OperationTypes.WorkshopOperationFor(ModuleTypes.SawpitId), 0);

            Assert.IsTrue(InputReserve.Allows(w, sawpit, Items.Beam, 1), "6 - 1 = 5 meets the reserve");
            Assert.IsFalse(InputReserve.Allows(w, sawpit, Items.Beam, 2), "6 - 2 = 4 is below the reserve");
        }

        // ---- Village totals (T-525 read model): yard stock included, nothing duplicated ----

        [Test]
        public void GoodsStats_BeamsAndPlanksIncludeYard_ReservedOnesToo()
        {
            SimWorld w = Make();
            LedgerLocation hewing = Hewing(w, M(30, 150));
            LedgerLocation yard = BuiltYard(w, M(150, 50));
            w.Inventory.Produce(hewing, Items.Beam, 3);
            w.Inventory.Produce(yard, Items.Beam, 7);
            w.Inventory.Produce(yard, Items.Plank, 5);
            int logsBefore = GoodsStats.Logs(w);

            ConstructionSources.TryReserve(w, Items.Beam, 4, YardSite, 1);

            Assert.AreEqual(10, GoodsStats.Beams(w), "reserved beams still count");
            Assert.AreEqual(5, GoodsStats.Planks(w));
            Assert.AreEqual(logsBefore, GoodsStats.Logs(w), "yard stock is never logs");
            Assert.IsNull(w.Inventory.Audit());
        }
    }
}
