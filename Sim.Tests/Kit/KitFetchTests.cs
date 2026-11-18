using System.Linq;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Kit;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Operations.Workshops;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Kit
{
    /// <summary>T-564, decision 0052, ST-21..24: kit fetch by the building's own family, and the drop on removal.</summary>
    public class KitFetchTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld World()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8, BushCount = 0, StartingPlanks = 0 });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 80);
            for (int fam = 2; fam <= 3; fam++)
            {
                w.People.AddFamily(fam, "F" + fam, 40, 40);
                for (int i = 0; i < 2; i++) w.People.AddCitizen(fam * 10 + i, "C" + fam + i, Sex.Male, AdultBirth, fam, 40, 40);
            }
            w.Footprints.Add(1, FootprintTypeDef.Camp, M(40, 40), 0);
            w.Camp = new LordsLegacy.Sim.Start.CampInfo(1, M(40, 40), M(42, 42), M(44, 44));
            w.Camp.GeneralStoreLocation = w.Inventory.AddGoodsStore(M(40, 40));
            Ox ox = w.People.AddOx(50, "O", 40, 44);
            ox.State = OxState.Resting(40000, 44000);
            return w;
        }

        // Real pipeline: place, build through ticks, assign family 2.
        private static int BuildGranary(SimWorld w, double x, double z)
        {
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(x, z), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int fp = (int)r.Created[0];
            for (int i = 0; i < 6000 && !w.IsBuilt(fp); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(fp));
            return fp;
        }

        private static Operation Assign(SimWorld w, int fp, int family)
        {
            Operation op = w.Operations.FindByModule(fp);
            Assert.IsTrue(w.Execute(new AssignFamily(op.Id, family)).IsAccepted);
            return op;
        }

        private static int OpenHauls(SimWorld w, int opId) =>
            w.Tasks.ByOwner(opId).Count(t => t.Def.Id == HandHaulTaskDefs.HaulId && !t.IsFinished);

        private static Footprint Shortcut(SimWorld w, string type, PointMm at)
        {
            Footprint fp = w.Footprints.Add(500 + w.Footprints.Count, ModuleTypes.Find(type), at, 0);
            if (type == ModuleTypes.StorageYardId)
            {
                Operation yop = w.Operations.Create(OperationTypes.StorageYardOpId, 0);
                w.Operations.AddModule(yop.Id, fp.Id);
                w.Inventory.AddGoodsStore(100, fp.Id);
            }
            else
            {
                Operation op = w.Operations.Create(OperationTypes.WorkshopOperationFor(type), 0);
                w.Operations.AddModule(op.Id, fp.Id);
                WorkshopBuildings.OnBuilt(w, fp);
            }
            return fp;
        }

        [Test]
        public void CampStore_FillsGranaryKit_ThroughRealCommandsAndTicks_ThenClaimCloses()
        {
            SimWorld w = World();
            int fp = BuildGranary(w, 60, 60);
            Operation op = Assign(w, fp, 2);
            w.Inventory.Produce(w.Camp.GeneralStoreLocation, Items.Basket, 5);
            LedgerLocation kit = KitStores.Find(w, fp);
            for (int i = 0; i < 4000 && kit.CountOf(Items.Basket.Id) < 2; i++) w.Step(1);
            Assert.AreEqual(2, kit.CountOf(Items.Basket.Id), "granary has 2 slots");
            Assert.AreEqual(3, w.Camp.GeneralStoreLocation.CountOf(Items.Basket.Id));
            w.Step(2);
            Assert.IsNull(w.KitClaims.Get(fp), "filled slots close the claim");
            Assert.AreEqual(0, OpenHauls(w, op.Id), "a full building makes no new trip");
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void Claim_OpensWhenBuiltAndStaysWhileSlotsFree()
        {
            SimWorld w = World();
            int fp = BuildGranary(w, 60, 60);
            w.Step(2);
            Assert.IsNotNull(w.KitClaims.Get(fp));
        }

        [Test]
        public void NoBasketAnywhere_OpensNoTask_AndKeepsClaim()
        {
            SimWorld w = World();
            int fp = BuildGranary(w, 60, 60);
            Operation op = Assign(w, fp, 2);
            w.Step(5);
            Assert.AreEqual(0, OpenHauls(w, op.Id) + w.Tasks.All.Count(t => t.Def.Id == HandHaulTaskDefs.HaulId));
            Assert.IsNotNull(w.KitClaims.Get(fp));
        }

        [Test]
        public void TripBelongsToBuildingsOwnFamily_AndReservesSourceAndSlots()
        {
            SimWorld w = World();
            int fp = BuildGranary(w, 60, 60);
            Operation op = Assign(w, fp, 2);
            w.Inventory.Produce(w.Camp.GeneralStoreLocation, Items.Basket, 5);
            new KitFetchSystem().Step(w);
            TaskRecord t = w.Tasks.All.Single(x => x.Def.Id == HandHaulTaskDefs.HaulId);
            Assert.AreEqual(op.Id, t.OwnerOperationId);
            Assert.AreEqual(EligibilityPolicy.AssignedFamilyOnly, t.Def.Eligibility);
            Assert.AreEqual(3, w.Camp.GeneralStoreLocation.AvailableOf(Items.Basket.Id), "2 reserved at the source");
            Assert.AreEqual(0, KitStores.Find(w, fp).FreeSpace, "slots reserved");
            Assert.IsNull(w.Inventory.Audit());
        }

        [TestCase(ModuleTypes.StorageYardId)]
        [TestCase(ModuleTypes.WeaverId)]
        public void FetchesFromYardOrWeaverStore_WhenNearestAndCampEmpty(string type)
        {
            SimWorld w = World();
            int fp = BuildGranary(w, 60, 60);
            Assign(w, fp, 2);
            Footprint src = Shortcut(w, type, M(62, 70));
            LedgerLocation store = w.Inventory.FindGoodsStore(src.Id);
            w.Inventory.Produce(store, Items.Basket, 4);
            new KitFetchSystem().Step(w);
            TaskRecord t = w.Tasks.All.Single(x => x.Def.Id == HandHaulTaskDefs.HaulId);
            Assert.AreEqual(2, store.CountOf(Items.Basket.Id) - store.AvailableOf(Items.Basket.Id), "2 baskets reserved at that store");
            Assert.AreEqual(HandHaulTaskDefs.HaulId, t.Def.Id);
        }

        [Test]
        public void NearestSourceWins_OverFartherCampStore()
        {
            SimWorld w = World();
            int fp = BuildGranary(w, 60, 60);
            Footprint yard = Shortcut(w, ModuleTypes.StorageYardId, M(60, 70));
            LedgerLocation ystore = w.Inventory.FindGoodsStore(yard.Id);
            w.Inventory.Produce(ystore, Items.Basket, 3);
            w.Inventory.Produce(w.Camp.GeneralStoreLocation, Items.Basket, 3);
            LedgerLocation found = KitFetchSystem.NearestSource(w, w.Footprints.Get(fp).AccessPoint);
            double dYard = yard.AccessPoint.DistanceTo(w.Footprints.Get(fp).AccessPoint);
            double dCamp = w.Camp.GeneralStoreLocation.Position.DistanceTo(w.Footprints.Get(fp).AccessPoint);
            Assert.AreEqual(dYard < dCamp ? ystore.Id : w.Camp.GeneralStoreLocation.Id, found.Id);
        }

        [Test]
        public void OldestClaimServedFirst_WhenOnlyOneTripOfBasketsExists()
        {
            SimWorld w = World();
            int older = BuildGranary(w, 60, 60);
            Assign(w, older, 2);
            w.Step(1);
            int newer = BuildGranary(w, 30, 60);
            Assign(w, newer, 3);
            Assert.Less(w.KitClaims.Get(older).FirstClaimTick, w.KitClaims.Get(newer).FirstClaimTick);
            // Remove the old claim's priority by tick only: both free, 2 baskets only.
            w.Inventory.Produce(w.Camp.GeneralStoreLocation, Items.Basket, 2);
            new KitFetchSystem().Step(w);
            int olderOp = w.Operations.FindByModule(older).Id;
            int newerOp = w.Operations.FindByModule(newer).Id;
            Assert.AreEqual(1, OpenHauls(w, olderOp));
            Assert.AreEqual(0, OpenHauls(w, newerOp));
        }

        [Test]
        public void Deconstruct_DropsKitAsUnownedPileAtAccessPoint_AndRemovesStoreAndClaim()
        {
            SimWorld w = World();
            int fp = BuildGranary(w, 60, 60);
            LedgerLocation kit = KitStores.Find(w, fp);
            w.Inventory.Produce(kit, Items.Basket, 2);
            PointMm access = w.Footprints.Get(fp).AccessPoint;
            w.KitClaims.Claim(fp, 1);
            Operation op = w.Operations.FindByModule(fp);
            w.Deconstruction.Add(new DeconstructionEntry(fp));
            DeconstructionWork.Finish(w, fp);
            Assert.IsNull(w.KitClaims.Get(fp));
            Assert.IsNull(w.Inventory.FindGoodsStore(fp), "kit store gone");
            LedgerLocation pile = w.Inventory.Locations.Single(l => l.Kind == LocationKind.GroundPile && l.CountOf(Items.Basket.Id) > 0);
            Assert.IsNull(pile.OwnerOperationId, "unowned");
            Assert.AreEqual(2, pile.CountOf(Items.Basket.Id));
            Assert.AreEqual(0.0, pile.Position.DistanceTo(access), 1.0);
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void DropOnRemoval_NoKitStore_IsNoOp()
        {
            SimWorld w = World();
            Footprint yard = Shortcut(w, ModuleTypes.StorageYardId, M(60, 70));
            Assert.DoesNotThrow(() => KitStores.DropOnRemoval(w, yard.Id));
            Assert.IsNotNull(w.Inventory.FindGoodsStore(yard.Id));
        }
    }
}
