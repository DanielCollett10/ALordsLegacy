using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Markets;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Growth
{
    /// <summary>Food, home and market readers skip families in passage (T-500, decision 0045, GR-13).</summary>
    public class PassageFoodHomeMarketReadersTests
    {
        private const long Adult = -4_000_000;
        private static PointMm P(long x, long z) => new PointMm(x, z);
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        // ---- food (camp world, Sunday start) ----

        private static SimWorld CampWorld()
        {
            var layout = new StartLayout(P(100000, 100000), 0, P(0, 0), P(6000, 0), P(-6000, 0), new PointMm[0], new StartFamily[0], 2);
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning { StartWeekday = Weekday.Sunday, StartingMeat = 100 });
            StartLayoutApplier.Apply(w, layout);
            return w;
        }

        private static Family AddCampFamily(SimWorld w, int id, double dx, FamilyPassage passage = FamilyPassage.None)
        {
            double x = w.Camp.Fire.XMetres + dx, z = w.Camp.Fire.ZMetres;
            Family f = w.People.AddFamily(id, "F" + id, x, z);
            w.People.AddCitizen(id, "C" + id, Sex.Male, Adult, id, x, z);
            f.Passage = passage;
            return f;
        }

        private static void Allot(SimWorld w) => AllotmentSystem.Run(w, new IFoodMarket[] { new CampFoodMarket(w) });

        [Test]
        public void Allotment_LeavingFamily_ReservesNothing()
        {
            SimWorld w = CampWorld();
            Family leaving = AddCampFamily(w, 1, 10, FamilyPassage.Leaving);
            Family settled = AddCampFamily(w, 2, 12);
            Allot(w);
            Assert.AreEqual(0, leaving.AllotmentReservationIds.Count);
            Assert.Greater(w.Camp.FoodStoreLocation.ReservedOf(Items.Meat.Id), 0, "only the settled family's food is reserved");
            Assert.Greater(settled.AllotmentReservationIds.Count, 0, "a settled family still gets its allotment");
        }

        [Test]
        public void Allotment_ArrivingFamily_ReservesNothing()
        {
            SimWorld w = CampWorld();
            Family arriving = AddCampFamily(w, 1, 10, FamilyPassage.Arriving);
            Allot(w);
            Assert.AreEqual(0, arriving.AllotmentReservationIds.Count);
            Assert.AreEqual(0, w.Camp.FoodStoreLocation.ReservedOf(Items.Meat.Id));
        }

        private static TaskRecord TripOf(SimWorld w, Family f) =>
            w.Tasks.ByOwner(AllotmentSystem.OwnerFor(f.Id)).FirstOrDefault(t => t.Def.Id == FoodTripSystem.FetchFoodTaskId);

        // Runs to Sunday dawn so the real allotment has run, re-allots both families, then returns.
        private static (SimWorld w, Family a, Family b) TripSetup(bool bLeaves)
        {
            SimWorld w = CampWorld();
            Family a = AddCampFamily(w, 1, 10);
            Family b = AddCampFamily(w, 2, 12);
            for (int i = 0; i < 60 * 24 && a.AllotmentReservationIds.Count == 0; i++) w.Step(1);
            Assert.Greater(a.AllotmentReservationIds.Count, 0, "dawn allotment ran");
            Allot(w); // both families reserved again (the dawn run may have been before b existed in the list order)
            Assert.Greater(b.AllotmentReservationIds.Count, 0);
            if (bLeaves) b.Passage = FamilyPassage.Leaving;
            return (w, a, b);
        }

        [Test]
        public void FoodTrip_LeavingFamilyWithReservation_OpensNoTrip()
        {
            var (w, _, b) = TripSetup(true);
            for (int i = 0; i < FoodTripSystem.MorningMinutes; i++) w.Step(1);
            Assert.IsNull(TripOf(w, b));
        }

        [Test]
        public void FoodTrip_SettledFamilyWithReservation_StillOpensItsTrip_Control()
        {
            var (w, _, b) = TripSetup(false);
            bool opened = false;
            for (int i = 0; i < FoodTripSystem.MorningMinutes && !opened; i++) { w.Step(1); opened = TripOf(w, b) != null; }
            Assert.IsTrue(opened);
        }

        // ---- homes ----

        [Test]
        public void MoveIn_LeavingHomelessFamily_IsNeverChosen_ArrivingNeither_SettledOneIs()
        {
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(80, 50) }, new[] { M(20, 50), M(50, 50), M(80, 50) });
            Support.HouseStock.Add(w, M(60, 40), 20);
            Family leaving = w.People.AddFamily(1, "L", 10, 10);
            w.People.AddCitizen(10, "L0", Sex.Male, Adult, 1, 10, 10);
            leaving.Passage = FamilyPassage.Leaving;
            Family arriving = w.People.AddFamily(2, "A", 10, 10);
            w.People.AddCitizen(20, "A0", Sex.Male, Adult, 2, 10, 10);
            arriving.Passage = FamilyPassage.Arriving;
            int road = w.Roads.Segments.First().Id;
            var shape = new AreaShape(
                new[] { M(30, 51.25), M(50, 51.25), M(50, 61.25), M(30, 61.25) },
                new[] { AreaEdge.FollowsRoad(road, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            CommandResult r = w.Execute(new DesignatePlot(shape));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int house = (int)r.Created[1];
            w.Construction.Remove(house);
            w.Step(5);
            Assert.AreEqual(0, w.Homes.Get(house).FamilyId, "nobody in passage takes the house");
            Assert.IsTrue(leaving.IsHomeless);
            Assert.IsTrue(arriving.IsHomeless);
            Assert.IsNull(LordsLegacy.Sim.Homes.MoveInSystem.Choose(w, w.Footprints.Get(house)));
            Family settled = w.People.AddFamily(3, "S", 10, 10);
            w.People.AddCitizen(30, "S0", Sex.Male, Adult, 3, 10, 10);
            w.Step(1);
            Assert.AreEqual(3, w.Homes.Get(house).FamilyId);
        }

        // ---- market ----

        [Test]
        public void Stall_LeavingStoreFamily_GetsNoStall_AndStoreFamilyWorkCreatesNoTrips()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8, BushCount = 0 });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 40);
            w.Inventory.Produce(w.Inventory.AddGoodsStore(M(20, 22)), Items.Plank, 10); // stalls cost 2 planks (PL-10)
            Family f = w.People.AddFamily(2, "F2", 40, 40);
            for (int i = 0; i < 2; i++) w.People.AddCitizen(20 + i, "C" + i, Sex.Male, Adult, 2, 40, 40);
            w.People.AddFamily(3, "F3", 40, 40);
            for (int i = 0; i < 2; i++) w.People.AddCitizen(30 + i, "D" + i, Sex.Male, Adult, 3, 40, 40);
            Ox ox = w.People.AddOx(50, "O", 40, 44);
            ox.State = OxState.Resting(40000, 44000);
            var area = AreaShape.Straight(new[] { M(10, 10), M(30, 10), M(30, 30), M(10, 30) });
            Assert.IsTrue(w.Execute(new DesignateArea(area, AreaTypes.MarketId)).IsAccepted);
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(60, 60), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int fp = (int)r.Created[0];
            for (int i = 0; i < 6000 && !w.IsBuilt(fp); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(fp));
            int op = w.Operations.FindByModule(fp).Id;
            Assert.IsTrue(w.Execute(new SetGranarySupplies(op, true)).IsAccepted);
            Assert.IsTrue(w.Execute(new AssignFamily(op, 2)).IsAccepted);
            f.Passage = FamilyPassage.Leaving;
            w.Step(5);
            Assert.AreEqual(0, w.Markets.Current.StallFootprintIds.Count, "no stall for a family in passage");
            Assert.AreEqual(0, w.Tasks.All.Count(t => !t.IsFinished && t.Def.Id == StoreFamilySystem.StockStallTaskId));
            f.Passage = FamilyPassage.None;
            w.Step(2);
            Assert.AreEqual(1, w.Markets.Current.StallFootprintIds.Count, "a settled family gets its stall as before");
        }
    }
}
