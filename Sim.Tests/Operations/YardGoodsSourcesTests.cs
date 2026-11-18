using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>Yard job sources: camp General store (ST-40) and loose General piles (ST-41), with ST-09 nearest yard.</summary>
    public class YardGoodsSourcesTests
    {
        private const long Adult = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);
        private static PointMm P(long x, long z) => new PointMm(x, z);

        // Camp centre (100,100) m; stockpile/camp store at (106,100). Near yard ~33 m from the camp store, far yard ~75 m.
        private static readonly PointMm NearYard = M(120, 130);
        private static readonly PointMm FarYard = M(170, 60);

        private static StartLayout Layout() => new StartLayout(
            P(100000, 100000), 0, P(0, 0), P(6000, 0), P(-6000, 0), new PointMm[0],
            new[]
            {
                new StartFamily("Ash", P(-8000, -4000), new[] { new StartCitizen("A", Sex.Male, Adult), new StartCitizen("B", Sex.Female, Adult) }),
                new StartFamily("Birch", P(-8000, 4000), new[] { new StartCitizen("C", Sex.Female, Adult) }),
            }, 2);

        // Real start layout (camp General store with no starting planks), flat map.
        private static SimWorld Started()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8, StartingPlanks = 0 });
            StartLayoutApplier.Apply(w, Layout());
            return w;
        }

        // A storage yard placed and built through Sim ticks only (ST-03).
        private static Operation BuiltYard(SimWorld w, PointMm at)
        {
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.StorageYardId, at, 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int id = (int)r.Created[0];
            for (int i = 0; i < 6000 && !w.IsBuilt(id); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(id), "yard built through Sim ticks");
            return w.Operations.FindByModule(id);
        }

        private static LedgerLocation StoreOf(SimWorld w, Operation yard) => w.Inventory.FindGoodsStore(yard.ModuleIds[0]);

        private static IEnumerable<TaskRecord> Hauls(SimWorld w) => w.Tasks.All.Where(t => t.Def.Id == HandHaulTaskDefs.HaulId);

        [Test]
        public void StandardSources_CampStoreAskedBeforeLoosePiles()
        {
            IReadOnlyList<IYardJobSource> s = YardJobs.Standard().Sources;
            Assert.AreEqual(2, s.Count);
            Assert.IsInstanceOf<YardCampStoreSource>(s[0], "ST-40 is asked first");
            Assert.IsInstanceOf<YardPileSource>(s[1], "ST-41 is asked second");
        }

        [Test]
        public void CampSource_FreeGeneralGoods_CarriedFromCampStoreToTheYard()
        {
            SimWorld w = Started();
            Operation yard = BuiltYard(w, NearYard);
            LedgerLocation camp = w.Camp.GeneralStoreLocation;
            w.Inventory.Produce(camp, Items.Plank, 5);

            YardTrip t = new YardCampStoreSource().NextTrip(w, yard);

            Assert.IsNotNull(t);
            Assert.AreSame(camp, t.Source);
            Assert.AreSame(StoreOf(w, yard), t.Destination);
            Assert.AreEqual(Items.Plank.Id, t.Item.Id);
            Assert.AreEqual(5, t.Count);
        }

        [Test]
        public void CampSource_ReservedStockNeverCollected_OnlyTheFreeRemainderIsOffered()
        {
            SimWorld w = Started();
            Operation yard = BuiltYard(w, NearYard);
            LedgerLocation camp = w.Camp.GeneralStoreLocation;
            w.Inventory.Produce(camp, Items.Plank, 5);
            w.Inventory.ReserveStock(camp, Items.Plank, 3, 555);
            Assert.AreEqual(2, camp.AvailableOf(Items.Plank.Id));

            YardTrip t = new YardCampStoreSource().NextTrip(w, yard);

            Assert.AreEqual(2, t.Count, "3 of 5 are promised to someone else");
        }

        [Test]
        public void CampSource_AllStockReserved_NoTrip()
        {
            SimWorld w = Started();
            Operation yard = BuiltYard(w, NearYard);
            LedgerLocation camp = w.Camp.GeneralStoreLocation;
            w.Inventory.Produce(camp, Items.Plank, 5);
            w.Inventory.ReserveStock(camp, Items.Plank, 5, 555);

            Assert.IsNull(new YardCampStoreSource().NextTrip(w, yard), "reserved stock is never collected");
        }

        [Test]
        public void CampSource_BeamsOnly_NoTripAndBeamsStayInCamp()
        {
            SimWorld w = Started();
            Operation yard = BuiltYard(w, NearYard);
            LedgerLocation camp = w.Camp.GeneralStoreLocation;
            w.Inventory.Produce(camp, Items.Beam, 4);

            Assert.IsNull(new YardCampStoreSource().NextTrip(w, yard), "beams are skipped (st-5)");
            Assert.AreEqual(4, camp.CountOf(Items.Beam.Id));
        }

        [Test]
        public void CampSource_BeamsAndPlanks_CarriesThePlanksNotTheBeams()
        {
            SimWorld w = Started();
            Operation yard = BuiltYard(w, NearYard);
            LedgerLocation camp = w.Camp.GeneralStoreLocation;
            w.Inventory.Produce(camp, Items.Beam, 4);
            w.Inventory.Produce(camp, Items.Plank, 3);

            YardTrip t = new YardCampStoreSource().NextTrip(w, yard);

            Assert.AreEqual(Items.Plank.Id, t.Item.Id, "the beam ahead in the list is passed over");
            Assert.AreEqual(3, t.Count);
        }

        [Test]
        public void CampSource_FullYard_NoTrip()
        {
            SimWorld w = Started();
            Operation yard = BuiltYard(w, NearYard);
            LedgerLocation store = StoreOf(w, yard);
            store.Capacity = 2;
            w.Inventory.Produce(store, Items.Plank, 2);
            w.Inventory.Produce(w.Camp.GeneralStoreLocation, Items.Plank, 5);

            Assert.IsNull(new YardCampStoreSource().NextTrip(w, yard), "no room, the goods wait in camp");
        }

        [Test]
        public void PileSource_UnownedGeneralPile_CarriedToTheYard()
        {
            SimWorld w = Started();
            Operation yard = BuiltYard(w, NearYard);
            LedgerLocation pile = w.Inventory.ProduceOnGround(M(150, 110), null, Items.Plank, 4);

            YardTrip t = new YardPileSource().NextTrip(w, yard);

            Assert.IsNotNull(t);
            Assert.AreSame(pile, t.Source);
            Assert.AreSame(StoreOf(w, yard), t.Destination);
            Assert.AreEqual(4, t.Count);
        }

        [Test]
        public void PileSource_OwnedPile_Ignored()
        {
            SimWorld w = Started();
            Operation yard = BuiltYard(w, NearYard);
            w.Inventory.ProduceOnGround(M(150, 110), 777, Items.Plank, 4);

            Assert.IsNull(new YardPileSource().NextTrip(w, yard), "a pile owned by an operation is not loose goods");
        }

        [Test]
        public void PileSource_LogsAndFood_Ignored_NotGeneralGoods()
        {
            SimWorld w = Started();
            Operation yard = BuiltYard(w, NearYard);
            w.Inventory.ProduceOnGround(M(150, 110), null, Items.Log, 3);
            w.Inventory.ProduceOnGround(M(150, 112), null, Items.Meat, 2);

            Assert.IsNull(new YardPileSource().NextTrip(w, yard));
        }

        [Test]
        public void PileSource_BeamPileOnly_NoTrip()
        {
            SimWorld w = Started();
            Operation yard = BuiltYard(w, NearYard);
            LedgerLocation pile = w.Inventory.ProduceOnGround(M(150, 110), null, Items.Beam, 2);

            Assert.IsNull(new YardPileSource().NextTrip(w, yard), "beams are skipped (st-5)");
            Assert.AreEqual(2, pile.CountOf(Items.Beam.Id));
        }

        [Test]
        public void PileSource_ReservedPile_NothingOffered()
        {
            SimWorld w = Started();
            Operation yard = BuiltYard(w, NearYard);
            LedgerLocation pile = w.Inventory.ProduceOnGround(M(150, 110), null, Items.Plank, 4);
            w.Inventory.ReserveStock(pile, Items.Plank, 4, 555);

            Assert.IsNull(new YardPileSource().NextTrip(w, yard), "reserved stock is never collected");
        }

        [Test]
        public void PileSource_NearestPileToTheYardFirst()
        {
            SimWorld w = Started();
            Operation yard = BuiltYard(w, NearYard);
            w.Inventory.ProduceOnGround(M(60, 60), null, Items.Plank, 2);
            LedgerLocation near = w.Inventory.ProduceOnGround(M(125, 120), null, Items.Plank, 2);

            YardTrip t = new YardPileSource().NextTrip(w, yard);

            Assert.AreSame(near, t.Source);
        }

        [Test]
        public void NearestYard_CampGoodsGoToTheNearerYardOnly_ThenTheFarOneWhenTheNearIsFull()
        {
            SimWorld w = Started();
            w.Inventory.Produce(w.Camp.GeneralStoreLocation, Items.Plank, 5); // before the yards: an empty camp store closes once a yard exists (ST-04)
            Operation near = BuiltYard(w, NearYard);
            Operation far = BuiltYard(w, FarYard);
            var camp = new YardCampStoreSource();

            Assert.IsNotNull(camp.NextTrip(w, near), "the nearer yard takes the trip");
            Assert.IsNull(camp.NextTrip(w, far), "the farther yard is not the nearest with room (ST-09)");

            LedgerLocation nearStore = StoreOf(w, near);
            nearStore.Capacity = 1;
            w.Inventory.Produce(nearStore, Items.Plank, 1);

            Assert.IsNull(camp.NextTrip(w, near), "the nearer yard is full");
            Assert.IsNotNull(camp.NextTrip(w, far), "so the farther yard with room takes it");
        }

        [Test]
        public void Pipeline_CampStoreEmptiedBeforeAnyPile_ByAStaffedYard()
        {
            SimWorld w = Started();
            Operation yard = BuiltYard(w, NearYard);
            Assert.IsTrue(w.Execute(new AssignFamily(yard.Id, 1)).IsAccepted);
            LedgerLocation camp = w.Camp.GeneralStoreLocation;
            w.Inventory.Produce(camp, Items.Plank, 3);
            LedgerLocation pile = w.Inventory.ProduceOnGround(M(150, 110), null, Items.Plank, 4);
            LedgerLocation store = StoreOf(w, yard);

            var sources = new List<int>();
            var seen = new HashSet<int>();
            for (int i = 0; i < 9000; i++)
            {
                w.Step(1);
                foreach (TaskRecord t in Hauls(w))
                    if (seen.Add(t.Id)) sources.Add(t.Def.Steps.First(s => s.Kind == StepKind.CollectItems).LocationId);
                if (camp.Total == 0 && pile.Total == 0 && !Hauls(w).Any(t => !t.IsFinished)) break;
            }

            Assert.AreEqual(7, store.CountOf(Items.Plank.Id), "all 7 planks reached the yard");
            Assert.AreEqual(0, camp.Total);
            Assert.AreEqual(0, pile.Total);
            int lastCamp = sources.LastIndexOf(camp.Id);
            int firstPile = sources.IndexOf(pile.Id);
            Assert.GreaterOrEqual(lastCamp, 0, "camp trips happened");
            Assert.GreaterOrEqual(firstPile, 0, "pile trips happened after the camp");
            Assert.Less(lastCamp, firstPile, "every camp trip is planned before the first pile trip (ST-40 before ST-41)");
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void Pipeline_BeamsOnlyEverywhere_NoHaulEverPlanned()
        {
            SimWorld w = Started();
            Operation yard = BuiltYard(w, NearYard);
            Assert.IsTrue(w.Execute(new AssignFamily(yard.Id, 1)).IsAccepted);
            LedgerLocation camp = w.Camp.GeneralStoreLocation;
            w.Inventory.Produce(camp, Items.Beam, 4);
            w.Inventory.ProduceOnGround(M(150, 110), null, Items.Beam, 2);

            w.Step(500);

            Assert.AreEqual(0, Hauls(w).Count());
            Assert.AreEqual(4, camp.CountOf(Items.Beam.Id));
        }

        [Test]
        public void Pipeline_FullYard_NoHaulPlanned_GoodsStayPut()
        {
            SimWorld w = Started();
            Operation yard = BuiltYard(w, NearYard);
            Assert.IsTrue(w.Execute(new AssignFamily(yard.Id, 1)).IsAccepted);
            LedgerLocation store = StoreOf(w, yard);
            store.Capacity = 2;
            w.Inventory.Produce(store, Items.Plank, 2);
            LedgerLocation camp = w.Camp.GeneralStoreLocation;
            w.Inventory.Produce(camp, Items.Plank, 5);

            w.Step(300);

            Assert.AreEqual(0, Hauls(w).Count());
            Assert.AreEqual(5, camp.CountOf(Items.Plank.Id));
        }
    }
}
