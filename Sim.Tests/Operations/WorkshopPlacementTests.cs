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
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>Place, build and deconstruct the hewing station and sawpit (T-521, HW-01, PL-01, PL-02).</summary>
    public class WorkshopPlacementTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static (SimWorld w, MapData map) Make(int logs = 6)
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

        private static int Place(SimWorld w, string type)
        {
            CommandResult r = w.Execute(new PlaceStructure(type, M(50, 50), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return (int)r.Created[0];
        }

        private static void BuildThroughTicks(SimWorld w, int id)
        {
            for (int i = 0; i < 6000 && !w.IsBuilt(id); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(id), "built by workers through Sim ticks only");
        }

        [TestCase(ModuleTypes.HewingStationId, OperationTypes.HewingId)]
        [TestCase(ModuleTypes.SawpitId, OperationTypes.SawingId)]
        public void Place_ReservesSixLogs_CreatesSiteAndOperation(string type, string opType)
        {
            var (w, _) = Make();
            int id = Place(w, type);
            Assert.AreEqual(6, ModuleTypes.Find(type).ConstructionRecipe.Logs);
            Assert.AreEqual(480, ModuleTypes.Find(type).ConstructionRecipe.BuildWorkerMinutes);
            Assert.IsNotNull(w.Construction.Get(id));
            Assert.AreEqual(6, w.Construction.Get(id).ReservationIds.Count == 0 ? 0 : w.Construction.Get(id).ReservationIds.Sum(r => w.Inventory.GetReservation(r).Count));
            Operation op = w.Operations.All.Single();
            Assert.AreEqual(opType, op.TypeId);
            Assert.AreEqual(0, op.AreaId);
            Assert.AreEqual(new[] { id }, op.ModuleIds.ToArray());
            Assert.IsNull(w.Inventory.FindWorkshopStock(id), "no stock until built");
            Assert.AreEqual(1, OperationTypes.Find(opType).FamilySlots);
            Assert.IsNotNull(OperationTypes.WorkshopOf(opType, w.Tuning));
        }

        [TestCase(ModuleTypes.HewingStationId)]
        [TestCase(ModuleTypes.SawpitId)]
        public void Place_RefusedWhenShortOfLogs_ChangesNothing(string type)
        {
            var (w, _) = Make(5);
            CommandResult r = w.Execute(new PlaceStructure(type, M(50, 50), 0));
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual(0, w.Operations.Count);
            Assert.AreEqual(0, w.Footprints.All.Count(f => f.Type.Id == type));
        }

        [TestCase(ModuleTypes.HewingStationId)]
        [TestCase(ModuleTypes.SawpitId)]
        public void Assign_RefusedUntilBuilt_ThenOneSlot_AndStockAndStoreExistWithDefCapacities(string type)
        {
            var (w, _) = Make();
            int id = Place(w, type);
            int opId = w.Operations.FindByModule(id).Id;
            CommandResult early = w.Execute(new AssignFamily(opId, 3));
            Assert.AreEqual(CommandRejectCode.NotAllowed, early.Reason);
            Assert.AreEqual("Hut not built yet", early.Text);

            BuildThroughTicks(w, id);
            Assert.IsTrue(w.Execute(new AssignFamily(opId, 3)).IsAccepted);
            CommandResult second = w.Execute(new AssignFamily(opId, 4));
            Assert.AreEqual(CommandRejectCode.NotAllowed, second.Reason);
            StringAssert.Contains("no free family slot", second.Text);

            WorkshopDef def = OperationTypes.WorkshopOf(OperationTypes.WorkshopOperationFor(type), w.Tuning);
            LedgerLocation stock = w.Inventory.FindWorkshopStock(id);
            LedgerLocation store = w.Inventory.FindGoodsStore(id);
            Assert.IsNotNull(stock);
            Assert.IsNotNull(store);
            Assert.AreEqual(def.InputStockCapacity, stock.Capacity);
            Assert.AreEqual(def.Input.Id, stock.InputItemId);
            Assert.AreEqual(def.OutputCapacity, store.Capacity);
            Assert.IsNull(w.Inventory.Audit());
        }

        [TestCase(ModuleTypes.HewingStationId)]
        [TestCase(ModuleTypes.SawpitId)]
        public void Deconstruct_DropsStockAndStoreAsUnownedPiles_RemovesOperationAndLocations(string type)
        {
            var (w, _) = Make();
            int id = Place(w, type);
            BuildThroughTicks(w, id);
            int opId = w.Operations.FindByModule(id).Id;
            w.Execute(new AssignFamily(opId, 3));
            WorkshopDef def = OperationTypes.WorkshopOf(OperationTypes.WorkshopOperationFor(type), w.Tuning);
            w.Inventory.Produce(w.Inventory.FindWorkshopStock(id), def.Input, 4);
            w.Inventory.Produce(w.Inventory.FindGoodsStore(id), def.Output, 7);
            int outputBefore = w.Inventory.TotalOf(def.Output.Id);

            w.Tuning.DeconstructTimeShare = 0;
            Assert.IsTrue(w.Execute(new MarkForDeconstruction(id)).IsAccepted);

            Assert.IsNull(w.Footprints.Get(id));
            Assert.IsNull(w.Operations.Get(opId));
            Assert.IsNull(w.People.FindFamily(3).WorkplaceId);
            Assert.IsNull(w.Inventory.FindWorkshopStock(id));
            Assert.IsNull(w.Inventory.FindGoodsStore(id));
            int onPiles = 0, outOnPiles = 0;
            foreach (LedgerLocation l in w.Inventory.Locations)
                if (l.Kind == LocationKind.GroundPile)
                {
                    if (l.CountOf(def.Input.Id) + l.CountOf(def.Output.Id) > 0 && l.OwnerOperationId != null) Assert.Fail("pile with dropped goods is owned");
                    onPiles += l.CountOf(def.Input.Id);
                    outOnPiles += l.CountOf(def.Output.Id);
                }
            Assert.GreaterOrEqual(onPiles, 4);
            Assert.AreEqual(7, outOnPiles);
            Assert.AreEqual(outputBefore, w.Inventory.TotalOf(def.Output.Id));
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void CancelUnbuiltWorkshop_RemovesOperation()
        {
            var (w, _) = Make();
            int id = Place(w, ModuleTypes.SawpitId);
            Assert.IsTrue(w.Execute(new MarkForDeconstruction(id)).IsAccepted);
            Assert.AreEqual(0, w.Operations.Count);
            Assert.IsNull(w.Footprints.Get(id));
        }

        [Test]
        public void SaveRoundTrip_BothWorkshopsWithStock_IsByteIdentical()
        {
            var (w, map) = Make(12);
            int a = (int)w.Execute(new PlaceStructure(ModuleTypes.HewingStationId, M(50, 50), 0)).Created[0];
            int b = (int)w.Execute(new PlaceStructure(ModuleTypes.SawpitId, M(90, 90), 0)).Created[0];
            for (int i = 0; i < 12000 && !(w.IsBuilt(a) && w.IsBuilt(b)); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(a) && w.IsBuilt(b));
            w.Inventory.Produce(w.Inventory.FindWorkshopStock(a), Items.Log, 3);
            w.Inventory.Produce(w.Inventory.FindGoodsStore(a), Items.Beam, 2);
            w.Inventory.Produce(w.Inventory.FindWorkshopStock(b), Items.Beam, 3);
            w.Inventory.Produce(w.Inventory.FindGoodsStore(b), Items.Plank, 5);
            byte[] first = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(first, map);
            Assert.AreEqual(first, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash));
            Assert.AreEqual(3, back.Inventory.FindWorkshopStock(a).CountOf(Items.Log.Id));
            Assert.AreEqual(5, back.Inventory.FindGoodsStore(b).CountOf(Items.Plank.Id));
            Assert.IsNull(back.Inventory.Audit());
        }
    }
}
