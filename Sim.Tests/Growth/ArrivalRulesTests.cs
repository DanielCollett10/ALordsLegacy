using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Arrivals;
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
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Growth
{
    /// <summary>Dawn arrival check and house reservation (T-504, GR-20..GR-22, GR-24).</summary>
    public class ArrivalRulesTests
    {
        private const long Adult = -4_000_000;
        private const int Day = 1440;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld Make()
        {
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(80, 50) }, new[] { M(20, 50), M(50, 50), M(80, 50) });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(60, 40)), Items.Log, 40);
            w.EntryPoint = M(20, 50);
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
            w.Construction.Remove(house); // built
            return house;
        }

        private static Family Settle(SimWorld w, int id, int house)
        {
            Family f = w.People.AddFamily(id, "F" + id, 10, 10);
            w.People.AddCitizen(id * 10, "A" + id, Sex.Male, Adult, id, 10, 10);
            w.People.AddCitizen(id * 10 + 1, "B" + id, Sex.Female, Adult, id, 10, 10);
            Assert.IsTrue(w.Homes.MoveIn(w.Homes.Get(house), f));
            return f;
        }

        private static FamilyPlan Plan(params int[] ages)
        {
            var members = ages.Select(a => new FamilyMemberPlan("N", Sex.Male, a)).ToList();
            return new FamilyPlan("Plan", FamilyMakeUp.CoupleNoChildren, members);
        }

        // One housed family of two adults (need 2) plus a free house; plan of two adults (need 2): 30 x 4 = 120 rations = 40 meat.
        private static SimWorld Ready(out int freeHouse, int meat = 40)
        {
            SimWorld w = Make();
            int used = Place(w, 25);
            freeHouse = Place(w, 50);
            Settle(w, 1, used);
            if (meat > 0) w.Inventory.Produce(w.Inventory.AddStockpile(M(60, 45)), Items.Meat, meat);
            w.Nav.Sync(w);
            return w;
        }

        [Test]
        public void Check_AllConditionsMet_IsAllowedWithTheFreeHouse()
        {
            SimWorld w = Ready(out int free);
            ArrivalCheck c = ArrivalRules.Check(w, Plan(30, 30));
            Assert.IsTrue(c.Allowed, c.Reason);
            Assert.AreEqual(free, c.HouseId);
            Assert.IsNull(c.Reason);
        }

        [Test]
        public void Check_IsPure_ChangesNothing()
        {
            SimWorld w = Ready(out int free);
            int version = w.Homes.Version;
            ArrivalRules.Check(w, Plan(30, 30));
            Assert.AreEqual(version, w.Homes.Version);
            Assert.AreEqual(0, w.Homes.Get(free).ReservedForFamilyId);
        }

        [Test]
        public void Check_NoHouseAtAll_NoEmptyHouse()
        {
            SimWorld w = Make();
            Assert.AreEqual("No empty house", ArrivalRules.Check(w, Plan(30, 30)).Reason);
        }

        [Test]
        public void Check_OnlyHouseIsOccupied_NoEmptyHouse()
        {
            SimWorld w = Make();
            Settle(w, 1, Place(w, 25));
            Assert.AreEqual("No empty house", ArrivalRules.Check(w, Plan(30, 30)).Reason);
        }

        [Test]
        public void Check_HouseStillUnderConstruction_NoEmptyHouse()
        {
            SimWorld w = Make();
            int road = w.Roads.Segments.First().Id;
            var shape = new AreaShape(new[] { M(25, 51.25), M(45, 51.25), M(45, 61.25), M(25, 61.25) },
                new[] { AreaEdge.FollowsRoad(road, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            Assert.IsTrue(w.Execute(new DesignatePlot(shape)).IsAccepted);
            Assert.AreEqual("No empty house", ArrivalRules.Check(w, Plan(30, 30)).Reason);
        }

        [Test]
        public void Check_ReservedHouseIsNotFree_NoEmptyHouse()
        {
            SimWorld w = Ready(out int free);
            Assert.IsTrue(w.Homes.Reserve(w.Homes.Get(free), 99));
            Assert.AreEqual("No empty house", ArrivalRules.Check(w, Plan(30, 30)).Reason);
        }

        [Test]
        public void Check_HomelessFamilyWithMembersWaiting_NoEmptyHouse()
        {
            SimWorld w = Ready(out _, meat: 60); // the extra citizen eats too
            Family f = w.People.AddFamily(2, "Camp", 10, 10);
            w.People.AddCitizen(21, "C", Sex.Male, Adult, 2, 10, 10);
            Assert.AreEqual("No empty house", ArrivalRules.Check(w, Plan(30, 30)).Reason);
            f.Passage = FamilyPassage.Leaving; // in passage: does not count
            Assert.IsTrue(ArrivalRules.Check(w, Plan(30, 30)).Allowed);
        }

        [Test]
        public void Check_HomelessFamilyWithNoMembers_DoesNotBlock()
        {
            SimWorld w = Ready(out _);
            w.People.AddFamily(2, "Ghost", 10, 10);
            Assert.IsTrue(ArrivalRules.Check(w, Plan(30, 30)).Allowed);
        }

        [Test]
        public void Check_NoEntryPoint_NoHouseCanBeReached()
        {
            SimWorld w = Ready(out _);
            w.EntryPoint = null;
            Assert.AreEqual("No house can be reached", ArrivalRules.Check(w, Plan(30, 30)).Reason);
        }

        [Test]
        public void Check_EntryPointOutsideTheMap_NoHouseCanBeReached()
        {
            SimWorld w = Ready(out _);
            w.EntryPoint = M(-500, -500);
            Assert.AreEqual("No house can be reached", ArrivalRules.Check(w, Plan(30, 30)).Reason);
        }

        [Test]
        public void Check_TwoFreeHouses_NearestByPathWins_NotLowestId()
        {
            SimWorld w = Make();
            int far = Place(w, 55);
            int near = Place(w, 25);
            Assert.Less(far, near, "the far house has the lower id");
            w.Inventory.Produce(w.Inventory.AddStockpile(M(60, 45)), Items.Meat, 100);
            w.Nav.Sync(w);
            ArrivalCheck c = ArrivalRules.Check(w, Plan(30, 30));
            Assert.IsTrue(c.Allowed, c.Reason);
            Assert.AreEqual(near, c.HouseId);
        }

        [Test]
        public void Check_NotEnoughFood_ReasonNotEnoughFood_AndExactBoundaryPasses()
        {
            SimWorld w = Ready(out _, meat: 39);
            Assert.AreEqual("Not enough food", ArrivalRules.Check(w, Plan(30, 30)).Reason);
            w.Inventory.Produce(w.Inventory.AddStockpile(M(60, 46)), Items.Meat, 1); // 40 meat = 120 rations = 30 x 4
            Assert.IsTrue(ArrivalRules.Check(w, Plan(30, 30)).Allowed);
        }

        [Test]
        public void Check_PlanNeedCountsChildrenAtChildRations()
        {
            SimWorld w = Ready(out _, meat: 40);
            // two adults + one child: need 2 + 2 + 0.5 = 4.5; 30 x 4.5 = 135 > 120
            Assert.AreEqual("Not enough food", ArrivalRules.Check(w, Plan(30, 30, 5)).Reason);
        }

        [Test]
        public void Check_HungryWithinTheWindow_SomeoneWasHungry_AndOlderIsFine()
        {
            SimWorld w = Ready(out _);
            w.LastHungryTick = w.Tick;
            Assert.AreEqual("Someone was hungry this week", ArrivalRules.Check(w, Plan(30, 30)).Reason);
            w.LastHungryTick = w.Tick - w.Tuning.ArrivalNoHungerDays * Day;
            Assert.AreEqual("Someone was hungry this week", ArrivalRules.Check(w, Plan(30, 30)).Reason, "exactly the window is not older than it");
            w.LastHungryTick = w.Tick - w.Tuning.ArrivalNoHungerDays * Day - 1;
            Assert.IsTrue(ArrivalRules.Check(w, Plan(30, 30)).Allowed);
        }

        [Test]
        public void Check_ArrivalWithinTheGap_TooSoon_AndOlderIsFine()
        {
            SimWorld w = Ready(out _);
            w.LastArrivalTick = w.Tick;
            Assert.AreEqual("Too soon since the last family", ArrivalRules.Check(w, Plan(30, 30)).Reason);
            w.LastArrivalTick = w.Tick - w.Tuning.ArrivalGapDays * Day - 1;
            Assert.IsTrue(ArrivalRules.Check(w, Plan(30, 30)).Allowed);
        }

        [Test]
        public void Check_ReasonsAreReportedInOrder_HouseThenFoodThenHungerThenGap()
        {
            SimWorld w = Ready(out _, meat: 0);
            w.LastHungryTick = w.Tick;
            w.LastArrivalTick = w.Tick;
            Assert.AreEqual("Not enough food", ArrivalRules.Check(w, Plan(30, 30)).Reason);
            w.Inventory.Produce(w.Inventory.AddStockpile(M(60, 46)), Items.Meat, 40);
            Assert.AreEqual("Someone was hungry this week", ArrivalRules.Check(w, Plan(30, 30)).Reason);
            w.LastHungryTick = null;
            Assert.AreEqual("Too soon since the last family", ArrivalRules.Check(w, Plan(30, 30)).Reason);
        }

        // Pipeline: a real midnight of eating with an empty larder sets LastHungryTick; Check then refuses.
        [Test]
        public void Pipeline_RealMidnightHunger_BlocksArrivalUntilTheWindowPasses()
        {
            SimWorld w = Ready(out _);
            Assert.IsNull(w.LastHungryTick);
            w.LastArrivalTick = w.Tick; // keep ArrivalSystem quiet: this test is about the hunger window only (T-505)
            do w.Step(); while (!(w.Now.Hour == 0 && w.Now.Minute == 0));
            Assert.IsNotNull(w.LastHungryTick, "the housed family's larder is empty, so it went hungry");
            Assert.AreEqual("Someone was hungry this week", ArrivalRules.Check(w, Plan(30, 30)).Reason);
        }

        [Test]
        public void MoveIn_ReservedHouseIsNeverFilledByTheSystem_ButItsFamilyCanMoveIn()
        {
            SimWorld w = Ready(out int free);
            HomeRecord home = w.Homes.Get(free);
            Assert.IsTrue(w.Homes.Reserve(home, 7));
            Family camp = w.People.AddFamily(2, "Camp", 10, 10);
            w.People.AddCitizen(21, "C", Sex.Male, Adult, 2, 10, 10);
            new MoveInSystem().Step(w);
            Assert.AreEqual(0, home.FamilyId, "reserved house stays Empty");
            Assert.IsFalse(w.Homes.MoveIn(home, camp), "another family cannot be linked either");
            Family arriving = w.People.AddFamily(7, "Arrive", 0, 0);
            Assert.IsTrue(w.Homes.MoveIn(home, arriving));
            Assert.AreEqual(0, home.ReservedForFamilyId, "reservation ends at move-in");
            Assert.AreEqual(7, home.FamilyId);
        }

        [Test]
        public void Reserve_OccupiedOrAlreadyReserved_ReturnsFalse_AndReleaseClears()
        {
            SimWorld w = Ready(out int free);
            HomeRecord used = w.Homes.All.First(h => h.FamilyId != 0);
            Assert.IsFalse(w.Homes.Reserve(used, 5));
            HomeRecord home = w.Homes.Get(free);
            Assert.IsTrue(w.Homes.Reserve(home, 5));
            Assert.IsFalse(w.Homes.Reserve(home, 6));
            w.Homes.ReleaseReservation(home);
            Assert.AreEqual(0, home.ReservedForFamilyId);
        }

        [Test]
        public void SaveRoundTrip_ReservationAndLastArrivalTick_Survive()
        {
            MapData map = SyntheticMap.CreateRamp();
            SimWorld w = Ready(out int free);
            w.Homes.Reserve(w.Homes.Get(free), 42);
            w.LastArrivalTick = 1234;
            byte[] first = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(first, map);
            Assert.AreEqual(42, back.Homes.Get(free).ReservedForFamilyId);
            Assert.AreEqual(1234L, back.LastArrivalTick);
            Assert.IsNull(SaveDiff.FirstDifference(first, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash)));
        }

        [Test]
        public void SaveRoundTrip_NoLastArrival_StaysNull_AndVersionIsAtLeast24()
        {
            MapData map = SyntheticMap.CreateRamp();
            SimWorld w = Ready(out _);
            SimWorld back = SimSave.Read(SimSave.Write(w, "s", 1, map.MapId, map.ContentHash), map);
            Assert.IsNull(back.LastArrivalTick);
            Assert.GreaterOrEqual(SaveWriter.CurrentVersion, 24);
        }
    }
}
