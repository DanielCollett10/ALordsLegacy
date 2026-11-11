using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class SendCitizenToTests
    {
        // 200 m of flat ground; citizens spawn at (100, 100) with no family home set elsewhere.
        private static SimWorld NewWorld()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            return SimWorld.FromMapData(map, new SimTuning { WorldSeed = 3, StartHour = 12 });
        }

        private static Citizen AddCitizen(SimWorld w, int id, double x, double z)
        {
            if (w.People.FindFamily(1) == null) w.People.AddFamily(1, "Test", x, z);
            return w.People.AddCitizen(id, "C" + id, Sex.Female, 0, 1, x, z);
        }

        private static SendCitizenTo Send(int citizenId, double x, double z) =>
            new SendCitizenTo(citizenId, new PathPoint(x, z));

        [Test]
        public void UnknownCitizen_IsNotFound_AndNothingChanges()
        {
            SimWorld w = NewWorld();
            Citizen c = AddCitizen(w, 1, 100, 100);
            CommandResult r = w.Execute(Send(999, 120, 100));

            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual(CommandRejectCode.NotFound, r.Reason);
            Assert.IsNull(c.DebugSend);
            Assert.IsNull(c.Move);
        }

        [Test]
        public void NoRoute_IsNotAllowed_WithNoRouteText_AndNothingChanges()
        {
            SimWorld w = NewWorld();
            Citizen c = AddCitizen(w, 1, 100, 100);
            var routineOrder = new MoveOrder(110, 100);
            c.Move = routineOrder;
            Activity before = c.Activity;

            CommandResult r = w.Execute(Send(1, -500, -500)); // far off the map: nothing walkable in snap range

            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual(CommandRejectCode.NotAllowed, r.Reason);
            Assert.AreEqual("No route", r.Text);
            Assert.IsNull(c.DebugSend);
            Assert.AreSame(routineOrder, c.Move, "a rejected send must not replace the routine's order");
            Assert.AreEqual(before, c.Activity);
            Assert.AreEqual(100, c.X, 0.0);
            Assert.AreEqual(100, c.Z, 0.0);
        }

        [Test]
        public void NonFinitePoint_IsInvalidArgument_AndNothingChanges()
        {
            SimWorld w = NewWorld();
            Citizen c = AddCitizen(w, 1, 100, 100);
            CommandResult r = w.Execute(Send(1, double.NaN, 100));

            Assert.AreEqual(CommandRejectCode.InvalidArgument, r.Reason);
            Assert.IsNull(c.DebugSend);
            Assert.IsNull(c.Move);
        }

        [Test]
        public void Validate_DoesNotMutate()
        {
            SimWorld w = NewWorld();
            Citizen c = AddCitizen(w, 1, 100, 100);
            Assert.IsTrue(Send(1, 120, 100).Validate(w).IsAccepted);
            Assert.IsNull(c.DebugSend);
            Assert.IsNull(c.Move);
        }

        [Test]
        public void Accepted_SetsWalkForThatCitizenOnly()
        {
            SimWorld w = NewWorld();
            Citizen c = AddCitizen(w, 1, 100, 100);
            Citizen other = AddCitizen(w, 2, 60, 60);

            CommandResult r = w.Execute(Send(1, 120, 100));

            Assert.IsTrue(r.IsAccepted);
            Assert.IsNotNull(c.DebugSend);
            Assert.AreSame(c.DebugSend.Order, c.Move);
            Assert.AreEqual(120, c.Move.TargetX, 1e-9);
            Assert.AreEqual(100, c.Move.TargetZ, 1e-9);
            Assert.AreEqual(Activity.Walking, c.Activity);
            Assert.IsNull(other.DebugSend);
            Assert.IsNull(other.Move);
        }

        [Test]
        public void Accepted_WorksWhilePaused_AndTheWalkStartsOnTheNextTick()
        {
            SimWorld w = NewWorld();
            Citizen c = AddCitizen(w, 1, 100, 100);
            w.Pause();
            long tick = w.Tick;

            CommandResult r = w.Execute(Send(1, 120, 100));

            Assert.IsTrue(r.IsAccepted);
            Assert.AreEqual(tick, w.Tick, "executing a command must not advance time");
            Assert.AreEqual(100, c.X, 0.0, "nothing moves while paused");
            Assert.AreEqual(MoveStatus.Pending, c.Move.Status);

            w.Resume();
            w.Step();
            Assert.AreEqual(tick + 1, w.Tick);
            Assert.AreNotEqual(MoveStatus.Pending, c.Move.Status, "the order is planned on the next tick");
            Assert.Greater(c.X, 100.0);
        }

        [Test]
        public void Accepted_IsLoggedInCommandLog_WithTick()
        {
            SimWorld w = NewWorld();
            AddCitizen(w, 1, 100, 100);
            w.Step(5);
            var cmd = Send(1, 120, 100);

            w.Execute(cmd);

            Assert.AreEqual(1, w.CommandLog.Count);
            CommandLogEntry e = w.CommandLog.Entries[0];
            Assert.AreEqual(5, e.Tick);
            Assert.AreSame(cmd, e.Command);
        }

        [Test]
        public void Rejected_IsAlsoLoggedInCommandLog()
        {
            SimWorld w = NewWorld();
            w.Execute(Send(42, 120, 100));
            Assert.AreEqual(1, w.CommandLog.Count);
        }

        [Test]
        public void SecondSend_ReplacesTheFirst()
        {
            SimWorld w = NewWorld();
            Citizen c = AddCitizen(w, 1, 100, 100);
            w.Execute(Send(1, 120, 100));
            DebugSendState first = c.DebugSend;

            w.Execute(Send(1, 100, 120));

            Assert.AreNotSame(first, c.DebugSend);
            Assert.AreEqual(100, c.Move.TargetX, 1e-9);
            Assert.AreEqual(120, c.Move.TargetZ, 1e-9);
        }
    }
}
