using System;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class DebugSendSystemTests
    {
        private static readonly FootprintTypeDef Blocker = new FootprintTypeDef("footprint.test_block", 8000, 8000, true, false);

        private static PointMm M(double xm, double zm) => new PointMm((long)(xm * 1000), (long)(zm * 1000));

        // 200 m of flat ground. StartHour 12 is daytime (the routine would mill about).
        private static SimWorld NewWorld(int startHour = 12)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            return SimWorld.FromMapData(map, new SimTuning { WorldSeed = 11, StartHour = startHour });
        }

        private static Citizen AddCitizen(SimWorld w, double x, double z)
        {
            if (w.People.FindFamily(1) == null) w.People.AddFamily(1, "Test", x, z);
            return w.People.AddCitizen(w.People.CitizenCount + 1, "C", Sex.Male, 0, 1, x, z);
        }

        private static bool Arrived(Citizen c) => c.DebugSend != null && c.DebugSend.LingerUntilTick >= 0;

        private static void StepUntilArrived(SimWorld w, Citizen c, int max = 5000)
        {
            int n = 0;
            while (!Arrived(c) && n++ < max) w.Step();
            Assert.IsTrue(Arrived(c), "citizen did not arrive");
        }

        private static double Dist(double ax, double az, double bx, double bz) =>
            Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

        [Test]
        public void Arrives_AtTheTargetPoint()
        {
            SimWorld w = NewWorld();
            Citizen c = AddCitizen(w, 100, 100);
            w.Execute(new SendCitizenTo(c.Id, new PathPoint(120, 100)));

            StepUntilArrived(w, c);

            // Within one nav cell: the pathfinder may snap the goal to a cell centre.
            Assert.Less(Dist(c.X, c.Z, 120, 100), 2.0);
            Assert.AreEqual(Activity.MillingAbout, c.Activity);
            Assert.IsNull(c.Move, "the walk order is cleared on arrival");
        }

        [Test]
        public void SendToOwnPosition_ArrivesOnTheFirstTick_AndLingers()
        {
            SimWorld w = NewWorld();
            Citizen c = AddCitizen(w, 100, 100);
            w.Execute(new SendCitizenTo(c.Id, new PathPoint(100, 100)));

            w.Step();

            Assert.IsTrue(Arrived(c), "a zero-length walk is done on the first tick");
            Assert.AreEqual(100, c.X, 1e-9);
            Assert.AreEqual(100, c.Z, 1e-9);
        }

        [Test]
        public void Lingers_ForDebugLingerMinutes_StandingStill_WithRoutineHeldOff()
        {
            SimWorld w = NewWorld();
            Citizen c = AddCitizen(w, 100, 100);
            w.Execute(new SendCitizenTo(c.Id, new PathPoint(120, 100)));
            StepUntilArrived(w, c);

            Assert.AreEqual(30, w.Tuning.DebugLingerMinutes);
            long until = c.DebugSend.LingerUntilTick;
            Assert.AreEqual(w.Tick + w.Tuning.DebugLingerMinutes, until);
            double px = c.X, pz = c.Z;

            while (w.Tick < until - 1)
            {
                w.Step();
                Assert.IsNotNull(c.DebugSend, "still lingering at tick " + w.Tick);
                Assert.IsNull(c.Move, "the routine must not send it anywhere at tick " + w.Tick);
                Assert.AreEqual(px, c.X, 0.0);
                Assert.AreEqual(pz, c.Z, 0.0);
            }
        }

        [Test]
        public void Resumes_ByDayMillingAboutAgain_AfterTheLinger()
        {
            SimWorld w = NewWorld();
            Citizen c = AddCitizen(w, 100, 100);
            w.Execute(new SendCitizenTo(c.Id, new PathPoint(120, 100)));
            StepUntilArrived(w, c);
            long until = c.DebugSend.LingerUntilTick;
            MoveOrder debugOrder = c.DebugSend.Order;

            while (w.Tick < until) w.Step();
            Assert.IsNull(c.DebugSend, "the send ends when the linger is over");

            for (int i = 0; i < 3 && c.Move == null; i++) w.Step();
            Assert.IsNotNull(c.Move, "the routine gives a new order");
            Assert.AreNotSame(debugOrder, c.Move);
            Assert.AreEqual(Activity.MillingAbout, c.Activity);
        }

        [Test]
        public void Resumes_AtNight_WalkingHomeToSleep_AfterTheLinger()
        {
            SimWorld w = NewWorld(startHour: 23);
            Citizen c = AddCitizen(w, 100, 100);
            Family home = w.People.FindFamily(c.FamilyId);
            w.Execute(new SendCitizenTo(c.Id, new PathPoint(120, 100)));
            StepUntilArrived(w, c);
            long until = c.DebugSend.LingerUntilTick;

            while (w.Tick < until) w.Step();
            for (int i = 0; i < 3 && c.Move == null; i++) w.Step();

            Assert.IsNotNull(c.Move);
            Assert.AreEqual(home.HomeX, c.Move.TargetX, 1e-9, "at night the routine heads home");
            Assert.AreEqual(home.HomeZ, c.Move.TargetZ, 1e-9);
        }

        [Test]
        public void RoutineOrder_IsReplacedBySend_AndKeptAwayUntilArrival()
        {
            SimWorld w = NewWorld();
            Citizen c = AddCitizen(w, 100, 100);
            w.Step(); // routine starts milling: it has issued its own order
            Assert.IsNotNull(c.Move);

            w.Execute(new SendCitizenTo(c.Id, new PathPoint(120, 100)));
            MoveOrder sent = c.Move;
            Assert.AreSame(c.DebugSend.Order, sent);

            int n = 0;
            while (!Arrived(c) && n++ < 5000)
            {
                w.Step();
                // The send's order is cleared on arrival; until then no other order may replace it.
                Assert.IsTrue(c.Move == null || ReferenceEquals(sent, c.Move),
                    "the routine must not replace the send at tick " + w.Tick);
            }
            Assert.IsTrue(Arrived(c));
        }

        [Test]
        public void WalkThatLosesItsRoute_EndsTheSend_AndTheRoutineResumes()
        {
            SimWorld w = NewWorld();
            Citizen c = AddCitizen(w, 21, 101);
            w.Execute(new SendCitizenTo(c.Id, new PathPoint(181, 101)));
            w.Step(20);
            Assert.IsNotNull(c.DebugSend);
            Assert.IsFalse(c.DebugSend.Order.IsFinished, "still walking before the wall");

            // A wall of 8 m blockers across x = 100 m, edge to edge: no route any more.
            for (int z = 4; z < 200; z += 8) w.Footprints.Add(z, Blocker, M(100, z), 0);

            int n = 0;
            while (c.DebugSend != null && n++ < 10) w.Step();
            Assert.IsNull(c.DebugSend, "a failed walk ends the send");
            Assert.IsNull(c.Move);
        }

        [Test]
        public void TwoCitizens_AreHandledIndependently()
        {
            SimWorld w = NewWorld();
            Citizen a = AddCitizen(w, 100, 100);
            Citizen b = AddCitizen(w, 60, 60);
            w.Execute(new SendCitizenTo(a.Id, new PathPoint(120, 100)));

            StepUntilArrived(w, a);

            Assert.IsNull(b.DebugSend, "sending a does not send b");
            Assert.AreNotEqual(Activity.Walking, b.Activity, "b is not on a debug walk");
        }
    }
}
