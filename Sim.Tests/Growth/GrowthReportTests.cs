using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Growth;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Growth
{
    /// <summary>GrowthReport, system order, the arrival-to-empty loop and hunger not slowing work (T-506).</summary>
    public class GrowthReportTests
    {
        private const long Adult = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld Village(int meat, out int freeHouse)
        {
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(150, 50) }, new[] { M(20, 50), M(85, 50), M(150, 50) });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(60, 40)), Items.Log, 400);
            w.EntryPoint = M(20, 50);
            int used = Place(w, 25);
            freeHouse = Place(w, 50);
            Family f = w.People.AddFamily(1, "Home", 10, 10);
            w.People.AddCitizen(10, "A", Sex.Male, Adult, 1, 10, 10);
            w.People.AddCitizen(11, "B", Sex.Female, Adult, 1, 10, 10);
            Assert.IsTrue(w.Homes.MoveIn(w.Homes.Get(used), f));
            if (meat > 0) w.Inventory.Produce(w.Inventory.AddStockpile(M(60, 45)), Items.Meat, meat);
            w.Nav.Sync(w);
            return w;
        }

        private static int Place(SimWorld w, double x)
        {
            int road = w.Roads.Segments.First().Id;
            var shape = new AreaShape(
                new[] { M(x, 51.25), M(x + 20, 51.25), M(x + 20, 61.25), M(x, 61.25) },
                new[] { AreaEdge.FollowsRoad(road, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            CommandResult r = w.Execute(new DesignatePlot(shape));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int house = (int)r.Created[1];
            w.Construction.Remove(house);
            return house;
        }

        [Test]
        public void DefaultSystems_RunEatingThenDepartureThenArrival()
        {
            var order = SimSystems.CreateDefault().Select(s => s.GetType()).ToList();
            int eat = order.IndexOf(typeof(EatingSystem));
            int dep = order.IndexOf(typeof(DepartureSystem));
            int arr = order.IndexOf(typeof(ArrivalSystem));
            Assert.GreaterOrEqual(eat, 0);
            Assert.Less(eat, dep);
            Assert.Less(dep, arr);
        }

        [Test]
        public void StatusOf_HungryArrivingLeavingAndFed()
        {
            SimWorld w = Village(0, out _);
            Family f = w.People.FindFamily(1);
            Assert.IsNull(GrowthReport.StatusOf(w, 1), "fed: nothing to say");
            f.HungryDays = 1;
            Assert.AreEqual("Hungry (1 / 30 days)", GrowthReport.StatusOf(w, 1));
            f.HungryDays = 12;
            Assert.AreEqual("Hungry (12 / 30 days)", GrowthReport.StatusOf(w, 1));
            f.Passage = FamilyPassage.Arriving;
            Assert.AreEqual("Arriving", GrowthReport.StatusOf(w, 1));
            f.Passage = FamilyPassage.Leaving;
            Assert.AreEqual("Leaving (hungry)", GrowthReport.StatusOf(w, 1));
            Assert.IsNull(GrowthReport.StatusOf(w, 99), "unknown family");
        }

        [Test]
        public void Of_CountsAndReason_AndDoesNotTouchRandomOrWorld()
        {
            SimWorld w = Village(0, out _);
            w.People.FindFamily(1).Passage = FamilyPassage.Leaving;
            long version = w.People.Version;
            int locations = w.Inventory.Locations.Count();
            GrowthReport r = GrowthReport.Of(w);
            Assert.AreEqual(1, r.Families);
            Assert.AreEqual(2, r.Citizens);
            Assert.AreEqual(0, r.Arriving);
            Assert.AreEqual(1, r.Leaving);
            Assert.AreEqual(ArrivalRules.NotEnoughFood, r.ArrivalReason);
            Assert.AreEqual(version, w.People.Version);
            Assert.AreEqual(locations, w.Inventory.Locations.Count());
        }

        [Test]
        public void Of_NoHouse_ReasonIsNoEmptyHouse()
        {
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());
            Assert.AreEqual(ArrivalRules.NoEmptyHouse, GrowthReport.Of(w).ArrivalReason);
        }

        [Test]
        public void EndToEnd_FamilyMovesIn_ThenNoFood_EveryoneLeaves_PopulationZero()
        {
            SimWorld w = Village(2000, out int free);
            int start = w.People.FamilyCount;
            Family arrived = null;
            for (int i = 0; i < 7 * 1440 && arrived == null; i++)
            {
                w.Step();
                arrived = w.People.Families.FirstOrDefault(f => f.Id != 1);
            }
            Assert.IsNotNull(arrived, "a family within 7 days");
            for (int i = 0; i < 1500 && arrived.Passage == FamilyPassage.Arriving; i++) w.Step();
            Assert.AreEqual(free, arrived.HomeHouseId, "moved in");
            Assert.IsNull(w.Inventory.Audit());

            foreach (LedgerLocation loc in w.Inventory.Locations.ToList())
                foreach (ItemDef item in new[] { Items.Meat, Items.Berries, Items.Mushrooms })
                    if (loc.CountOf(item.Id) > 0) Assert.IsTrue(w.Inventory.Consume(loc, item, loc.CountOf(item.Id)));
            Assert.AreEqual(0, FoodStats.TotalRations(w));

            for (int day = 0; day < 40 && w.People.CitizenCount > 0; day++)
            {
                w.Step(1440);
                Assert.IsNull(w.Inventory.Audit(), "audit day " + day);
            }
            Assert.AreEqual(0, w.People.CitizenCount);
            Assert.AreEqual(0, w.People.FamilyCount);
        }

        // 200 m flat map, 8:00 start, one 4-log tree. A hungry family fells at the same rate as a fed one (GR-01).
        private static (int stumps, int logs) Fell(bool hungry)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]),
                new[] { new MapTree(new TreeId(1), 100f, 85f, 16), new MapTree(new TreeId(2), 80f, 97f, 16) });
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
            CommandResult r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) }), AreaTypes.WoodcuttingId));
            Operation op = w.Operations.FindByArea((int)r.Created[0]);
            w.Footprints.Add(1, ModuleTypes.WoodcuttingHut, M(100, 100), 0);
            w.Operations.AddModule(op.Id, 1);
            w.Footprints.Add(2, ModuleTypes.LogStorage, M(110, 100), 0);
            w.Operations.AddModule(op.Id, 2);
            Family f = w.People.AddFamily(1, "F", 100, 100);
            w.People.AddCitizen(10, "C", Sex.Male, Adult, 1, 100, 100);
            Assert.IsTrue(w.Execute(new AssignFamily(op.Id, 1)).IsAccepted);
            if (hungry) { f.IsHungry = true; f.HungryDays = 10; }
            var progress = new List<int>();
            for (int i = 0; i < 300; i++)
            {
                w.Step();
                if (hungry) { f.IsHungry = true; f.HungryDays = 10; }
                progress.Add(w.Trees.All.Count(t => t.State == TreeState.Stump) * 1000 + w.Inventory.TotalOf(Items.Log));
            }
            return (progress.Last() / 1000, progress.Last() % 1000);
        }

        [Test]
        public void HungryWoodcutter_FellsAtTheSameRateAsAFedOne()
        {
            var fed = Fell(false);
            var hungry = Fell(true);
            Assert.Greater(fed.stumps + fed.logs, 0, "the fed one did some work");
            Assert.AreEqual(fed, hungry);
        }
    }
}
