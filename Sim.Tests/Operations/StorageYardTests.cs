using System.Linq;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>Place, build, assign and deconstruct the storage yard (T-540, ST-03, ST-05, ST-07, ST-08).</summary>
    public class StorageYardTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static (SimWorld w, MapData map) Make(int logs = 8)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
            if (logs > 0) w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, logs);
            for (int fam = 2; fam <= 4; fam++)
            {
                w.People.AddFamily(fam, "F" + fam, 40, 40);
                for (int i = 0; i < 2; i++) w.People.AddCitizen(fam * 10 + i, "C" + fam + i, Sex.Male, AdultBirth, fam, 40, 40);
            }
            Ox ox = w.People.AddOx(50, "O", 40, 44);
            ox.State = OxState.Resting(40000, 44000);
            return (w, map);
        }

        private static int Place(SimWorld w)
        {
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.StorageYardId, M(50, 50), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return (int)r.Created[0];
        }

        private static void BuildThroughTicks(SimWorld w, int id)
        {
            for (int i = 0; i < 6000 && !w.IsBuilt(id); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(id), "built by workers through Sim ticks only");
        }

        [Test]
        public void Place_Makes8x6Site_With8LogsAndAreaLessOperation_NoStoreYet()
        {
            var (w, _) = Make();
            int id = Place(w);
            Footprint fp = w.Footprints.Get(id);
            Assert.AreEqual(8000, fp.Type.WidthMm);
            Assert.AreEqual(6000, fp.Type.DepthMm);
            Assert.AreEqual(8, fp.Type.ConstructionRecipe.Logs);
            Assert.IsNotNull(w.Construction.Get(id));
            Operation op = w.Operations.Get(w.Operations.FindByModule(id).Id);
            Assert.AreEqual(OperationTypes.StorageYardOpId, op.TypeId);
            Assert.AreEqual(0, op.AreaId);
            Assert.AreEqual(2, OperationTypes.StorageYard.FamilySlots);
            Assert.IsNull(w.Inventory.FindGoodsStore(id), "no store until built");
        }

        [Test]
        public void Place_RefusedWhenShortOfLogs_ChangesNothing()
        {
            var (w, _) = Make(7);
            Assert.IsTrue(w.Execute(new PlaceStructure(ModuleTypes.StorageYardId, M(50, 50), 0)).IsRejected);
            Assert.AreEqual(0, w.Operations.Count);
            Assert.AreEqual(0, w.Footprints.All.Count(f => f.Type.Id == ModuleTypes.StorageYardId));
        }

        [Test]
        public void Place_RefusedWhenBlockedByAnotherYard()
        {
            var (w, _) = Make(16);
            Place(w);
            Assert.IsTrue(w.Execute(new PlaceStructure(ModuleTypes.StorageYardId, M(51, 50), 0)).IsRejected);
        }

        [Test]
        public void Pipeline_PlaceBuildAssign_AssignRefusedUntilBuilt_TwoSlots_StoreTakesBeamsNotLogsOrFood()
        {
            var (w, _) = Make();
            int id = Place(w);
            int opId = w.Operations.FindByModule(id).Id;
            CommandResult early = w.Execute(new AssignFamily(opId, 3));
            Assert.AreEqual(CommandRejectCode.NotAllowed, early.Reason);

            BuildThroughTicks(w, id);
            Assert.IsTrue(w.Execute(new AssignFamily(opId, 3)).IsAccepted);
            Assert.IsTrue(w.Execute(new AssignFamily(opId, 4)).IsAccepted);
            Assert.IsTrue(w.Execute(new AssignFamily(opId, 2)).IsRejected, "third family: no slot");

            LedgerLocation store = w.Inventory.FindGoodsStore(id);
            Assert.IsNotNull(store);
            Assert.AreEqual(w.Tuning.StorageYardCapacity, store.Capacity);
            Assert.AreEqual(300, store.Capacity);
            Assert.IsTrue(store.Accepts(Items.Beam.Category));
            Assert.IsFalse(store.Accepts(Items.Log.Category), "logs refused");
            Assert.IsFalse(store.Accepts(Items.Berries.Category), "food refused");
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void Deconstruct_DropsContentsAsOneUnownedPileAtAccessPoint()
        {
            var (w, _) = Make();
            int id = Place(w);
            BuildThroughTicks(w, id);
            Footprint fp = w.Footprints.Get(id);
            PointMm access = fp.AccessPoint;
            w.Inventory.Produce(w.Inventory.FindGoodsStore(id), Items.Beam, 5);
            w.Inventory.Produce(w.Inventory.FindGoodsStore(id), Items.Plank, 3);

            w.Tuning.DeconstructTimeShare = 0;
            Assert.IsTrue(w.Execute(new MarkForDeconstruction(id)).IsAccepted);

            Assert.IsNull(w.Footprints.Get(id));
            Assert.IsNull(w.Inventory.FindGoodsStore(id));
            Assert.AreEqual(0, w.Operations.Count);
            var piles = w.Inventory.Locations.Where(l => l.Kind == LocationKind.GroundPile && l.CountOf(Items.Beam.Id) > 0).ToList();
            Assert.AreEqual(1, piles.Count);
            Assert.IsNull(piles[0].OwnerOperationId);
            Assert.AreEqual(5, piles[0].CountOf(Items.Beam.Id));
            Assert.AreEqual(3, piles[0].CountOf(Items.Plank.Id), "one pile for all items");
            Assert.AreEqual(access, piles[0].Position);
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void SaveRoundTrip_YardWithStock_IsByteIdentical()
        {
            var (w, map) = Make();
            int id = Place(w);
            BuildThroughTicks(w, id);
            w.Inventory.Produce(w.Inventory.FindGoodsStore(id), Items.Beam, 12);
            byte[] first = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(first, map);
            Assert.AreEqual(first, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash));
            Assert.AreEqual(12, back.Inventory.FindGoodsStore(id).CountOf(Items.Beam.Id));
            Assert.AreEqual(OperationTypes.StorageYardOpId, back.Operations.FindByModule(id).TypeId);
            Assert.IsNull(back.Inventory.Audit());
        }
    }
}
