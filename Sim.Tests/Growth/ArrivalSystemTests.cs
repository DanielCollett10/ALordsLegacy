using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Growth;
using LordsLegacy.Sim.Homes;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Growth
{
    /// <summary>ArrivalSystem: a family walks in at dawn with a month of food and moves in (T-505, GR-22b, GR-24, GR-26).</summary>
    public class ArrivalSystemTests
    {
        private const long Adult = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld Make(int meat, out int freeHouse)
        {
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(150, 50) }, new[] { M(20, 50), M(85, 50), M(150, 50) });
            Support.HouseStock.Add(w, M(60, 40), 400);
            w.EntryPoint = M(20, 50);
            int used = Place(w, 25);
            freeHouse = Place(w, 50);
            Family f = w.People.AddFamily(1, "Home", 10, 10);
            w.People.AddCitizen(10, "A", Sex.Male, Adult, 1, 10, 10);
            w.People.AddCitizen(11, "B", Sex.Female, Adult, 1, 10, 10);
            Assert.IsTrue(w.Homes.MoveIn(w.Homes.Get(used), f));
            w.Inventory.Produce(w.Inventory.AddStockpile(M(60, 45)), Items.Meat, meat);
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

        private static Family StepUntilArrival(SimWorld w, int maxTicks = 3 * 1440)
        {
            for (int i = 0; i < maxTicks; i++)
            {
                w.Step();
                Family n = w.People.Families.FirstOrDefault(f => f.Passage == FamilyPassage.Arriving);
                if (n != null) return n;
            }
            return null;
        }

        [Test]
        public void AllowedDawn_FamilyArrives_ReservesHouse_AndLogsIt()
        {
            SimWorld w = Make(200, out int free);
            Family n = StepUntilArrival(w);
            Assert.IsNotNull(n, "an arrival within three days");
            Assert.AreEqual(FamilyPassage.Arriving, n.Passage);
            Assert.AreEqual(n.Id, w.Homes.Get(free).ReservedForFamilyId);
            Assert.IsTrue(n.MemberIds.Count >= 2);
            Footprints.Footprint house = w.Footprints.Get(free);
            for (int i = 0; i < n.MemberIds.Count; i++)
            {
                Citizen c = w.People.FindCitizen(n.MemberIds[i]);
                Assert.AreEqual(house.AccessPoint.XMetres, c.Move.TargetX, 1e-6);
                Assert.AreEqual(Activity.Walking, c.Activity);
                for (int j = 0; j < i; j++)
                {
                    Citizen o = w.People.FindCitizen(n.MemberIds[j]);
                    double d = System.Math.Sqrt((c.X - o.X) * (c.X - o.X) + (c.Z - o.Z) * (c.Z - o.Z));
                    Assert.AreEqual(i - j, d, 0.5, "members spread 1 m apart");
                }
            }
            Assert.IsTrue(w.SettlementLog.Entries.Any(e => e.Text == "The " + n.Surname + " family has arrived"));
            Assert.AreEqual(w.Tick, w.LastArrivalTick);
        }

        [Test]
        public void ArrivalFood_AllThreeKinds_CeilOfMonthShare_AuditPasses()
        {
            SimWorld w = Make(200, out int free);
            Family n = StepUntilArrival(w);
            double need = n.MemberIds.Sum(id =>
                w.People.FindCitizen(id).AgeYears(w.Tick, w.Tuning.AgeingRate) >= 14 ? w.Tuning.AdultRations : w.Tuning.ChildRations);
            var larder = w.Inventory.Get(n.LarderLocationId);
            foreach (ItemDef item in new[] { Items.Meat, Items.Berries, Items.Mushrooms })
            {
                int expect = (int)System.Math.Ceiling(w.Tuning.ArrivalFoodDays * need / 3.0 / item.RationsPerItem);
                Assert.AreEqual(expect, larder.CountOf(item.Id), item.Id);
                Assert.Greater(expect, 0);
            }
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void FullPipeline_WalksInAndMovesIn_FoodGoesToHouseLarder_IdsNotReused()
        {
            SimWorld w = Make(200, out int free);
            Family n = StepUntilArrival(w);
            int famId = n.Id, citizens = n.MemberIds.Count;
            for (int i = 0; i < 1500 && n.Passage == FamilyPassage.Arriving; i++) w.Step();
            Assert.AreEqual(FamilyPassage.None, n.Passage);
            Assert.AreEqual(free, n.HomeHouseId);
            HomeRecord home = w.Homes.Get(free);
            Assert.AreEqual(famId, home.FamilyId);
            Assert.AreEqual(0, home.ReservedForFamilyId);
            Assert.Greater(w.Inventory.Get(home.LarderLocationId).CountOf(Items.Meat.Id), 0);
            Assert.AreEqual(0, w.Inventory.Get(n.LarderLocationId).Total, "camp larder emptied into the house");
            Assert.IsNull(w.Inventory.Audit());
            Assert.AreEqual(famId + 1, w.People.NextFamilyId);
            Assert.AreEqual(2 + citizens, w.People.CitizenCount);
        }

        [Test]
        public void HouseRemovedOnTheWay_WithCamp_FamilyHomelessAtCamp_FoodInLarder()
        {
            SimWorld w = Make(200, out int free);
            w.Camp = new CampInfo(0, M(30, 30), M(30, 30), M(32, 32));
            Family n = StepUntilArrival(w);
            HouseRemoval.RemoveWithPlot(w, free);
            w.Step();
            Assert.AreEqual(FamilyPassage.None, n.Passage);
            Assert.IsTrue(n.IsHomeless);
            Assert.AreEqual(30, n.HomeX);
            Assert.IsTrue(w.Homes.All.All(h => h.ReservedForFamilyId == 0));
            Assert.Greater(w.Inventory.Get(n.LarderLocationId).CountOf(Items.Meat.Id), 0);
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void HouseRemovedOnTheWay_NoCamp_FamilySleepsRough_NoExceptions()
        {
            SimWorld w = Make(200, out int free);
            w.RoughSleepSpot = M(40, 40);
            Family n = StepUntilArrival(w);
            HouseRemoval.RemoveWithPlot(w, free);
            for (int i = 0; i < 300; i++) w.Step();
            Assert.AreEqual(FamilyPassage.None, n.Passage);
            Assert.IsTrue(n.IsHomeless);
            Assert.IsTrue(RoughSleep.IsSleepingRough(w, n));
            Assert.AreEqual(RoughSleep.Spot(w).Value.XMetres, n.HomeX, 1e-6);
            Assert.Greater(w.Inventory.Get(n.LarderLocationId).CountOf(Items.Meat.Id), 0);
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void SaveMidWalk_RoundTrips_AndTheFamilyStillMovesIn()
        {
            SimWorld w = Make(200, out int free);
            Family n = StepUntilArrival(w);
            for (int i = 0; i < 5; i++) w.Step();
            MapData map = SyntheticMap.CreateRamp();
            SimWorld back = SimSave.Read(SimSave.Write(w, "n", 0, map.MapId, map.ContentHash), map);
            Family b = back.People.FindFamily(n.Id);
            Assert.AreEqual(FamilyPassage.Arriving, b.Passage);
            Assert.AreEqual(n.Id, back.Homes.Get(free).ReservedForFamilyId);
            Assert.AreEqual(n.MemberIds.Count, b.MemberIds.Count);
            Assert.AreEqual(back.People.NextFamilyId, w.People.NextFamilyId);
            for (int i = 0; i < 1500 && b.Passage == FamilyPassage.Arriving; i++) back.Step();
            Assert.AreEqual(free, b.HomeHouseId);
            Assert.IsNull(back.Inventory.Audit());
        }

        [Test]
        public void Over21Days_AtMostThreeArrivals()
        {
            SimWorld w = Make(2000, out int free);
            for (double x = 75; x <= 125; x += 25) Place(w, x);
            int start = w.People.FamilyCount;
            for (int i = 0; i < 21 * 1440; i++) w.Step();
            Assert.LessOrEqual(w.People.FamilyCount - start, 3);
            Assert.GreaterOrEqual(w.People.FamilyCount - start, 1);
        }
    }
}
