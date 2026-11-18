using LordsLegacy.Sim.Commands;
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
    /// <summary>Yard job source 3 (ST-42, ST-42b, PL-30d): collect any free workshop output item, fullest first.</summary>
    public class YardWorkshopSourceTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld Make()
        {
            var w = SimWorld.FromMapData(new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]),
                new SimTuning { StartHour = 8, StartingPlanks = 0 });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 8);
            for (int fam = 2; fam <= 4; fam++)
            {
                w.People.AddFamily(fam, "F" + fam, 40, 40);
                for (int i = 0; i < 2; i++) w.People.AddCitizen(fam * 10 + i, "C" + fam + i, Sex.Male, -4_000_000, fam, 40, 40);
            }
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
            return w.Operations.FindByModule(id);
        }

        private static Footprint Workshop(SimWorld w, string type, PointMm at)
        {
            Footprint fp = w.Footprints.Add(100 + w.Operations.Count, ModuleTypes.Find(type), at, 0);
            Operation op = w.Operations.Create(OperationTypes.WorkshopOperationFor(type), 0);
            w.Operations.AddModule(op.Id, fp.Id);
            WorkshopBuildings.OnBuilt(w, fp);
            return fp;
        }

        private static LedgerLocation Sawpit(SimWorld w, PointMm at, int planks, int capacity = 10)
        {
            Footprint fp = Workshop(w, ModuleTypes.SawpitId, at);
            LedgerLocation store = w.Inventory.FindGoodsStore(fp.Id);
            store.Capacity = capacity;
            if (planks > 0) w.Inventory.Produce(store, Items.Plank, planks);
            return store;
        }

        [Test]
        public void OnePlankInSawpit_CollectedAtOnce_OneItemTrip()
        {
            SimWorld w = Make();
            Operation yard = BuiltYard(w, M(100, 100));
            LedgerLocation store = Sawpit(w, M(60, 100), 1);

            YardTrip t = new YardWorkshopSource().NextTrip(w, yard);

            Assert.IsNotNull(t, "any free output item is collected, no threshold");
            Assert.AreSame(store, t.Source);
            Assert.AreEqual(Items.Plank.Id, t.Item.Id);
            Assert.AreEqual(1, t.Count);
        }

        [Test]
        public void OnlyReservedPlank_NoTrip()
        {
            SimWorld w = Make();
            Operation yard = BuiltYard(w, M(100, 100));
            LedgerLocation store = Sawpit(w, M(60, 100), 1);
            Assert.AreNotEqual(0, w.Inventory.ReserveStock(store, Items.Plank, 1, 999), "the one plank is reserved");

            Assert.IsNull(new YardWorkshopSource().NextTrip(w, yard), "a reserved item is not free");
        }

        [Test]
        public void FullestStoreFirst_NotNearest()
        {
            SimWorld w = Make();
            Operation yard = BuiltYard(w, M(100, 100));
            Sawpit(w, M(90, 100), 6);
            LedgerLocation fuller = Sawpit(w, M(30, 100), 9);

            YardTrip t = new YardWorkshopSource().NextTrip(w, yard);

            Assert.AreSame(fuller, t.Source);
        }

        [Test]
        public void HewingStoreOfBeams_SkippedAsNotHandCarriable()
        {
            SimWorld w = Make();
            Operation yard = BuiltYard(w, M(100, 100));
            Footprint fp = Workshop(w, ModuleTypes.HewingStationId, M(60, 100));
            LedgerLocation store = w.Inventory.FindGoodsStore(fp.Id);
            w.Inventory.Produce(store, Items.Beam, store.Capacity);

            Assert.IsNull(new YardWorkshopSource().NextTrip(w, yard), "beams go by ox, not this source");
        }

        [Test]
        public void GatheringHutStore_Ignored()
        {
            SimWorld w = Make();
            Operation yard = BuiltYard(w, M(100, 100));
            Footprint hut = w.Footprints.Add(300, ModuleTypes.Find(ModuleTypes.ForagerHutId), M(60, 100), 0);
            Operation op = w.Operations.Create(OperationTypes.ForagingId, 0);
            w.Operations.AddModule(op.Id, hut.Id);
            LedgerLocation store = w.Inventory.AddGoodsStore(10, hut.Id);
            w.Inventory.Produce(store, Items.Plank, 10);

            Assert.IsNull(new YardWorkshopSource().NextTrip(w, yard));
        }

        [Test]
        public void IsWorkshop_NullOrUnmappedDef_False_RealDefsTrue()
        {
            var t = new SimTuning();
            Assert.IsFalse(WorkshopBuildings.IsWorkshop((WorkshopDef)null));
            Assert.IsFalse(WorkshopBuildings.IsWorkshop(OperationTypes.WorkshopOf(OperationTypes.ForagingId, t)), "no-input def (gathering) excluded");
            Assert.IsTrue(WorkshopBuildings.IsWorkshop(WorkshopDef.Hewing(t)));
            Assert.IsTrue(WorkshopBuildings.IsWorkshop(WorkshopDef.Sawing(t)));
        }

        [Test]
        public void WorkshopInputStock_NeverCollected()
        {
            SimWorld w = Make();
            Operation yard = BuiltYard(w, M(100, 100));
            Footprint fp = Workshop(w, ModuleTypes.SawpitId, M(60, 100));
            LedgerLocation stock = w.Inventory.FindWorkshopStock(fp.Id);
            w.Inventory.Produce(stock, Items.Beam, stock.Capacity);

            Assert.IsNull(new YardWorkshopSource().NextTrip(w, yard), "only the output store is read");
        }

        [Test]
        public void FullYard_NoTrip()
        {
            SimWorld w = Make();
            Operation yard = BuiltYard(w, M(100, 100));
            Sawpit(w, M(60, 100), 8);
            LedgerLocation ys = w.Inventory.FindGoodsStore(yard.ModuleIds[0]);
            ys.Capacity = 1;
            w.Inventory.Produce(ys, Items.Plank, 1);

            Assert.IsNull(new YardWorkshopSource().NextTrip(w, yard), "no room in the yard");
        }
    }
}
