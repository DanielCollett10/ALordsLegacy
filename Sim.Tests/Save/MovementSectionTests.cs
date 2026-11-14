using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Save
{
    public class MovementSectionTests
    {
        private static MapData Map() =>
            new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);

        private static SimWorld Make(MapData map) => SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });

        private static byte[] Bytes(SimWorld w, MapData map) => SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);

        private static SimWorld Load(SimWorld w, MapData map)
        {
            byte[] bytes = Bytes(w, map);
            SimWorld back = SimSave.Read(bytes, map);
            Assert.AreEqual(bytes, Bytes(back, map), "second write is identical");
            return back;
        }

        private static (SimWorld, Citizen walker, Ox led, Citizen sender) Populated(MapData map)
        {
            SimWorld w = Make(map);
            w.People.AddFamily(1, "Ash", 20, 20);
            Citizen walker = w.People.AddCitizen(1, "Walker", Sex.Male, -40000, 1, 20, 20);
            Citizen sender = w.People.AddCitizen(2, "Sent", Sex.Female, -40000, 1, 22, 20);
            Ox led = w.People.AddOx(1, "Bess", 20, 20);
            Ox other = w.People.AddOx(2, "Dan", 30, 30);
            walker.Move = new MoveOrder(150, 160);
            walker.Activity = Activity.Walking;
            MovementSystem.Attach(led, walker);
            led.State = OxState.Led(walker.Id);
            w.Execute(new SendCitizenTo(sender.Id, new PathPoint(120, 60)));
            return (w, walker, led, sender);
        }

        private static void AssertSame(SimWorld a, SimWorld b)
        {
            foreach (Citizen c in a.People.Citizens)
            {
                Citizen d = b.People.FindCitizen(c.Id);
                Assert.AreEqual(c.X, d.X, "citizen x tick " + a.Tick);
                Assert.AreEqual(c.Z, d.Z);
                Assert.AreEqual(c.Activity, d.Activity);
            }
            foreach (Ox o in a.People.Oxen)
            {
                Ox p = b.People.FindOx(o.Id);
                Assert.AreEqual(o.X, p.X, "ox x tick " + a.Tick);
                Assert.AreEqual(o.Z, p.Z);
            }
        }

        [Test]
        public void MidWalkCitizenAndLedOx_ContinueTickForTickIdenticalAfterLoad()
        {
            MapData map = Map();
            var (w, walker, led, sender) = Populated(map);
            w.Step(40);
            Assert.AreEqual(MoveStatus.Moving, walker.Move.Status, "setup: still walking");
            Assert.Greater(walker.Move.SegmentIndex + walker.Move.ProgressM, 0);
            SimWorld back = Load(w, map);

            Citizen bw = back.People.FindCitizen(1);
            Assert.AreEqual(walker.Move.TargetX, bw.Move.TargetX);
            Assert.AreEqual(walker.Move.ProgressM, bw.Move.ProgressM);
            Assert.AreEqual(walker.Move.SegmentIndex, bw.Move.SegmentIndex);
            Assert.AreEqual(walker.Move.Path.Count, bw.Move.Path.Count);
            Assert.AreEqual(walker.Move.PlannedNavVersion, bw.Move.PlannedNavVersion);
            Assert.AreSame(bw, back.People.FindOx(1).Follow.Leader);
            Assert.AreEqual(led.Follow.OffsetM, back.People.FindOx(1).Follow.OffsetM);
            Assert.AreEqual(w.Nav.Version, back.Nav.Version);

            for (int i = 0; i < 400; i++)
            {
                w.Step();
                back.Step();
                AssertSame(w, back);
            }
            Assert.AreEqual(Bytes(w, map), Bytes(back, map), "identical bytes after a stretch of ticks");
        }

        [Test]
        public void DebugSend_RoundTripsOrderSharingAndLinger_AndFinishesTheSame()
        {
            MapData map = Map();
            var (w, walker, led, sender) = Populated(map);
            w.Step(10);
            SimWorld back = Load(w, map);
            Citizen b = back.People.FindCitizen(2);
            Assert.IsNotNull(b.DebugSend);
            Assert.AreSame(b.Move, b.DebugSend.Order, "send order is still the citizen's move order");
            Assert.AreEqual(-1L, b.DebugSend.LingerUntilTick);
            for (int i = 0; i < 300; i++) { w.Step(); back.Step(); AssertSame(w, back); }
            Assert.IsNull(back.People.FindCitizen(2).DebugSend, "debug send ended in both worlds");
            Assert.IsNull(sender.DebugSend);
            Assert.AreEqual(Bytes(w, map), Bytes(back, map));
        }

        [Test]
        public void DebugSendLingering_KeepsItsLingerTick()
        {
            MapData map = Map();
            var (w, _, _, sender) = Populated(map);
            for (int i = 0; i < 400 && (sender.DebugSend == null || sender.DebugSend.LingerUntilTick < 0); i++) w.Step();
            Assert.GreaterOrEqual(sender.DebugSend.LingerUntilTick, 0, "setup: lingering");
            SimWorld back = Load(w, map);
            Assert.AreEqual(sender.DebugSend.LingerUntilTick, back.People.FindCitizen(2).DebugSend.LingerUntilTick);
            Assert.IsNull(back.People.FindCitizen(2).Move);
        }

        [Test]
        public void RoutineAndWorkDay_ContinueIdenticallyAcrossSeveralDays()
        {
            MapData map = Map();
            SimWorld w = Make(map);
            w.People.AddFamily(1, "Ash", 20, 20);
            w.People.AddCitizen(1, "A", Sex.Male, -40000, 1, 20, 20, 0, Activity.Sleeping);
            w.People.AddCitizen(2, "B", Sex.Female, -40000, 1, 21, 20, 0, Activity.MillingAbout);
            w.People.AddOx(1, "Bess", 25, 25);
            w.Step(200);
            SimWorld back = Load(w, map);
            for (int i = 0; i < 3 * 1440; i++)
            {
                w.Step();
                back.Step();
                if (i % 7 == 0) AssertSame(w, back);
            }
            Assert.AreEqual(Bytes(w, map), Bytes(back, map));
        }

        [Test]
        public void Follow_ResolvesCitizenLeaderAndOxLeader()
        {
            MapData map = Map();
            SimWorld w = Make(map);
            w.People.AddFamily(1, "Ash", 20, 20);
            Citizen c = w.People.AddCitizen(1, "A", Sex.Male, -40000, 1, 20, 20);
            Ox o = w.People.AddOx(1, "Bess", 25, 25);
            MovementSystem.Attach(c, o, 2.5);
            SimWorld back = Load(w, map);
            Assert.AreSame(back.People.FindOx(1), back.People.FindCitizen(1).Follow.Leader);
            Assert.AreEqual(2.5, back.People.FindCitizen(1).Follow.OffsetM);
            Assert.IsNull(back.People.FindOx(1).Follow);
        }

        [Test]
        public void FailedAndDoneOrders_KeepStatusAndReason()
        {
            MapData map = Map();
            SimWorld w = Make(map);
            w.People.AddFamily(1, "Ash", 20, 20);
            Citizen c = w.People.AddCitizen(1, "A", Sex.Male, -40000, 1, 20, 20);
            Ox o = w.People.AddOx(1, "Bess", 25, 25);
            c.Move = new MoveOrder(5, 5) { Status = MoveStatus.Failed, FailReason = MoveOrder.NoRouteReason };
            o.Move = new MoveOrder(6, 6) { Status = MoveStatus.Done };
            SimWorld back = Load(w, map);
            Assert.AreEqual(MoveStatus.Failed, back.People.FindCitizen(1).Move.Status);
            Assert.AreEqual(MoveOrder.NoRouteReason, back.People.FindCitizen(1).Move.FailReason);
            Assert.AreEqual(MoveStatus.Done, back.People.FindOx(1).Move.Status);
            Assert.AreEqual(0, back.People.FindOx(1).Move.Path.Count);
        }
    }
}
