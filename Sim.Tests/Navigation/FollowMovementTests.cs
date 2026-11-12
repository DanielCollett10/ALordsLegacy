using System;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class FollowMovementTests
    {
        private static SimWorld NewWorld()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            return SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
        }

        private static double Dist(IMovable a, double x, double z) =>
            Math.Sqrt((a.X - x) * (a.X - x) + (a.Z - z) * (a.Z - z));

        private static (Citizen, Ox) Pair(SimWorld w, double x, double z)
        {
            w.People.AddFamily(1, "F", x, z);
            Citizen c = w.People.AddCitizen(1, "C", Sex.Male, -4_000_000, 1, x, z);
            Ox o = w.People.AddOx(1, "O", x, z);
            return (c, o);
        }

        [Test]
        public void Attached_PairMovesAtOxSpeed_FollowerIs1_5mBehind_FacingMatches()
        {
            SimWorld w = NewWorld();
            var (c, o) = Pair(w, 51, 101);
            MovementSystem.Attach(o, c);
            c.Move = new MoveOrder(121, 101);
            w.Step(1); // plans and moves
            double x0 = c.X, z0 = c.Z;
            w.Step(10);
            Assert.That(Dist(c, x0, z0), Is.EqualTo(10 * w.Tuning.OxSpeedMPerGameMinute).Within(1e-6));
            Assert.That(Dist(c, o.X, o.Z), Is.EqualTo(1.5).Within(1e-6));
            Assert.That(o.FacingRad, Is.EqualTo(c.FacingRad));
            Assert.That(c.FacingRad, Is.EqualTo(Math.PI / 2).Within(1e-9)); // facing +x
        }

        [Test]
        public void Attached_OxLeadingSlowerThanCitizen_OxStillIsOwnSpeed_And_CitizenLeaderUsesMin()
        {
            SimWorld w = NewWorld();
            var (c, o) = Pair(w, 51, 101);
            // Ox leads a citizen: pair still moves at the ox speed.
            MovementSystem.Attach(c, o);
            o.Move = new MoveOrder(121, 101);
            w.Step(1);
            double x0 = o.X, z0 = o.Z;
            w.Step(5);
            Assert.That(Dist(o, x0, z0), Is.EqualTo(5 * w.Tuning.OxSpeedMPerGameMinute).Within(1e-6));
            Assert.That(Dist(o, c.X, c.Z), Is.EqualTo(1.5).Within(1e-6));
        }

        [Test]
        public void Detach_LeaderReturnsToOwnSpeed_FollowerStaysPut()
        {
            SimWorld w = NewWorld();
            var (c, o) = Pair(w, 51, 101);
            MovementSystem.Attach(o, c);
            c.Move = new MoveOrder(121, 101);
            w.Step(5);
            MovementSystem.Detach(o);
            double ox = o.X, cx = c.X;
            w.Step(5);
            Assert.That(c.X - cx, Is.EqualTo(5 * w.Tuning.WalkSpeedMPerGameMinute).Within(1e-6));
            Assert.That(o.X, Is.EqualTo(ox));
            Assert.IsNull(o.Follow);
        }

        [Test]
        public void Attach_BehindPositionOffTheMap_IsClampedToAWalkableCell()
        {
            SimWorld w = NewWorld();
            var (c, o) = Pair(w, 1, 100);
            c.FacingRad = Math.PI / 2; // facing +x, so "behind" is x = -0.5: outside the grid
            MovementSystem.Attach(o, c);
            w.Step(1);
            NavGrid nav = w.Nav;
            Assert.IsFalse(nav.IsBlocked(nav.CellX(o.X), nav.CellZ(o.Z)));
            Assert.That(o.X, Is.GreaterThanOrEqualTo(0));
            Assert.That(o.X, Is.LessThan(c.X));
        }

        [Test]
        public void Attach_RejectsSelfAndNull()
        {
            SimWorld w = NewWorld();
            var (c, o) = Pair(w, 51, 101);
            Assert.Throws<ArgumentException>(() => MovementSystem.Attach(c, c));
            Assert.Throws<ArgumentNullException>(() => MovementSystem.Attach(null, c));
            Assert.Throws<ArgumentNullException>(() => MovementSystem.Attach(o, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => MovementSystem.Attach(o, c, -1));
        }

        [Test]
        public void Attach_ZeroOffset_PlacesFollowerOnTheLeader()
        {
            SimWorld w = NewWorld();
            var (c, o) = Pair(w, 51, 101);
            MovementSystem.Attach(o, c, 0);
            w.Step(1);
            Assert.AreEqual(c.X, o.X);
            Assert.AreEqual(c.Z, o.Z);
        }
    }
}
