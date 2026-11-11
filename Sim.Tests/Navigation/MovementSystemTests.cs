using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class MovementSystemTests
    {
        private static readonly FootprintTypeDef Blocker = new FootprintTypeDef("footprint.test_block", 8000, 8000, true, false);

        private static PointMm M(double xm, double zm) => new PointMm((long)(xm * 1000), (long)(zm * 1000));

        // 200 m x 200 m of flat ground, default systems (nav sync first, movement after).
        private static SimWorld NewWorld()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            return SimWorld.FromMapData(map, new SimTuning());
        }

        private static Citizen AddCitizen(SimWorld w, double x, double z)
        {
            if (w.People.FindFamily(1) == null) w.People.AddFamily(1, "Test", x, z);
            return w.People.AddCitizen(w.People.CitizenCount + 1, "C", Sex.Male, 0, 1, x, z);
        }

        private static void AddRoad(SimWorld w, double x0, double z0, double x1, double z1) =>
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(x0, z0), M(x1, z1) }, new[] { M(x0, z0), M(x1, z1) });

        private static int TicksToFinish(SimWorld w, IMovable a, int max = 5000)
        {
            int n = 0;
            while (!a.Move.IsFinished && n < max) { w.Step(); n++; }
            return n;
        }

        [Test]
        public void Default_SystemList_HasMovementAfterNavSync()
        {
            IReadOnlyList<ISimSystem> list = SimSystems.CreateDefault();
            int sync = -1, move = -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is NavSyncSystem) sync = i;
                if (list[i] is MovementSystem) move = i;
            }
            Assert.That(sync, Is.GreaterThanOrEqualTo(0));
            Assert.That(move, Is.GreaterThan(sync));
        }

        [Test]
        public void Citizen_Walks100mOnFlatGround_InCeilOfDistanceOverWalkSpeedTicks()
        {
            SimWorld w = NewWorld();
            Citizen c = AddCitizen(w, 21, 101);
            c.Move = new MoveOrder(121, 101);
            int ticks = TicksToFinish(w, c);
            Assert.AreEqual(MoveStatus.Done, c.Move.Status);
            Assert.AreEqual((int)Math.Ceiling(100.0 / w.Tuning.WalkSpeedMPerGameMinute), ticks);
            Assert.AreEqual(121, c.X, 1e-9);
            Assert.AreEqual(101, c.Z, 1e-9);
        }

        [Test]
        public void Ox_Walks100mOnFlatGround_InCeilOfDistanceOverOxSpeedTicks()
        {
            SimWorld w = NewWorld();
            Ox o = w.People.AddOx(1, "Ox", 21, 101);
            o.Move = new MoveOrder(121, 101);
            int ticks = TicksToFinish(w, o);
            Assert.AreEqual(MoveStatus.Done, o.Move.Status);
            Assert.AreEqual((int)Math.Ceiling(100.0 / w.Tuning.OxSpeedMPerGameMinute), ticks);
        }

        [Test]
        public void RoadStretch_IsWalked125TimesFaster()
        {
            SimWorld flat = NewWorld();
            Citizen a = AddCitizen(flat, 15, 101);
            a.Move = new MoveOrder(105, 101);
            TicksToFinish(flat, a);

            SimWorld road = NewWorld();
            AddRoad(road, 9, 101, 111, 101);
            Citizen b = AddCitizen(road, 15, 101);
            b.Move = new MoveOrder(105, 101);
            int ticks = TicksToFinish(road, b);

            double walk = road.Tuning.WalkSpeedMPerGameMinute;
            Assert.AreEqual((int)Math.Ceiling(90.0 / (walk * 1.25)), ticks);
            Assert.AreEqual(1.25, b.CurrentMultiplier, 1e-6);
            Assert.AreEqual(1.0, a.CurrentMultiplier, 1e-6);
        }

        [Test]
        public void Facing_FollowsTheDirectionOfTravel()
        {
            SimWorld w = NewWorld();
            Citizen c = AddCitizen(w, 21, 101);
            c.Move = new MoveOrder(61, 101);
            w.Step();
            Assert.AreEqual(Math.Atan2(1, 0), c.FacingRad, 1e-9);
            c.Move = new MoveOrder(c.X, 141);
            w.Step();
            Assert.AreEqual(0.0, c.FacingRad, 1e-9);
        }

        [Test]
        public void PreviousPosition_IsPositionAtStartOfTick_AndMultiplierIsKept()
        {
            SimWorld w = NewWorld();
            Citizen c = AddCitizen(w, 21, 101);
            c.Move = new MoveOrder(121, 101);
            w.Step();
            double x1 = c.X;
            Assert.AreEqual(21, c.PreviousX, 1e-9);
            Assert.AreEqual(w.Tuning.WalkSpeedMPerGameMinute, x1 - 21, 1e-9);
            w.Step();
            Assert.AreEqual(x1, c.PreviousX, 1e-9);
            Assert.AreEqual(1.0, c.CurrentMultiplier, 1e-9);
        }

        [Test]
        public void AgentWithNoOrder_DoesNotMove_AndPreviousEqualsPosition()
        {
            SimWorld w = NewWorld();
            Citizen c = AddCitizen(w, 21, 101);
            w.Step(3);
            Assert.AreEqual(21, c.X);
            Assert.AreEqual(21, c.PreviousX);
        }

        [Test]
        public void StartEqualsTarget_FinishesInOneTickWithoutMoving()
        {
            SimWorld w = NewWorld();
            Citizen c = AddCitizen(w, 21, 101);
            c.Move = new MoveOrder(21, 101);
            w.Step();
            Assert.AreEqual(MoveStatus.Done, c.Move.Status);
            Assert.AreEqual(21, c.X, 1e-9);
        }

        [Test]
        public void TargetFarOutsideTheMap_FailsWithNoRouteReason()
        {
            SimWorld w = NewWorld();
            Citizen c = AddCitizen(w, 21, 101);
            c.Move = new MoveOrder(5000, 5000);
            w.Step();
            Assert.AreEqual(MoveStatus.Failed, c.Move.Status);
            Assert.AreEqual("No route", c.Move.FailReason);
            Assert.AreEqual(21, c.X, 1e-9);
        }

        [Test]
        public void DeleteRoadSegment_ReplansWalkerNextTick_AndSpeedDropsToWalkSpeed()
        {
            SimWorld w = NewWorld();
            AddRoad(w, 9, 101, 111, 101);
            w.Step();
            Citizen c = AddCitizen(w, 15, 101);
            c.Move = new MoveOrder(105, 101);
            w.Step(10);
            Assert.AreEqual(1.25, c.CurrentMultiplier, 1e-6);
            int plannedVersion = c.Move.PlannedNavVersion;
            int segId = 0;
            foreach (RoadSegment s in w.Roads.Segments) segId = s.Id;
            Assert.IsTrue(w.Execute(new DeleteRoadSegment(segId)).IsAccepted);
            Assert.AreNotEqual(plannedVersion, w.Nav.Version);

            double before = c.X;
            w.Step();
            Assert.AreEqual(w.Nav.Version, c.Move.PlannedNavVersion);
            Assert.AreEqual(1.0, c.CurrentMultiplier, 1e-9);
            Assert.AreEqual(w.Tuning.WalkSpeedMPerGameMinute, c.X - before, 1e-9);
            Assert.AreEqual(w.Tuning.WalkSpeedMPerGameMinute, c.Move.ProgressM, 1e-9);
            TicksToFinish(w, c);
            Assert.AreEqual(MoveStatus.Done, c.Move.Status);
        }

        [Test]
        public void FootprintPlacedOnWalker_PushesToNearestWalkablePoint()
        {
            SimWorld w = NewWorld();
            w.Step();
            Citizen c = AddCitizen(w, 101, 101);
            w.Footprints.Add(1, Blocker, M(101, 101), 0);
            w.Step();
            Assert.IsFalse(w.Nav.IsBlocked(w.Nav.CellX(c.X), w.Nav.CellZ(c.Z)));
            double d = Math.Sqrt((c.X - 101) * (c.X - 101) + (c.Z - 101) * (c.Z - 101));
            Assert.That(d, Is.GreaterThan(3.9).And.LessThan(8.0));
            Assert.AreEqual(c.X, c.PreviousX);
        }

        [Test]
        public void FootprintPlacedOnRoute_ReplansAroundIt_AndNeverEntersIt()
        {
            SimWorld w = NewWorld();
            w.Step();
            Citizen c = AddCitizen(w, 21, 101);
            c.Move = new MoveOrder(181, 101);
            w.Step(20);
            w.Footprints.Add(1, Blocker, M(c.X + 40, 101), 0);
            int n = 0;
            while (!c.Move.IsFinished && n++ < 500)
            {
                w.Step();
                Assert.IsFalse(w.Nav.IsBlocked(w.Nav.CellX(c.X), w.Nav.CellZ(c.Z)), "tick " + n);
            }
            Assert.AreEqual(MoveStatus.Done, c.Move.Status);
        }

        private static List<double> Run()
        {
            SimWorld w = NewWorld();
            AddRoad(w, 9, 101, 111, 101);
            Citizen c = AddCitizen(w, 15, 101);
            Ox o = w.People.AddOx(1, "Ox", 15, 60);
            c.Move = new MoveOrder(150, 140);
            o.Move = new MoveOrder(100, 101);
            var log = new List<double>();
            for (int i = 0; i < 150; i++)
            {
                if (i == 30) w.Footprints.Add(1, Blocker, M(60, 101), 0);
                w.Step();
                log.Add(c.X); log.Add(c.Z); log.Add(o.X); log.Add(o.Z); log.Add(c.FacingRad);
            }
            return log;
        }

        [Test]
        public void TwoRunsOfTheSameScenario_GiveIdenticalPositions()
        {
            CollectionAssert.AreEqual(Run(), Run());
        }
    }
}
