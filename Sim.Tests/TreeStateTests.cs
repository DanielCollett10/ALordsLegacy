using System;
using System.Linq;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class TreeStateTests
    {
        private static TreeId Id(int n) => new TreeId(n);

        private static TreeRegistry Reg(params (int id, float x, float z)[] ts)
        {
            var r = new TreeRegistry();
            foreach (var t in ts) r.Add(Id(t.id), t.x, t.z, 10);
            return r;
        }

        [Test]
        public void SetState_FellThenStump_MovesBetweenQueriesAndBumpsVersion()
        {
            var r = Reg((1, 0, 0), (2, 5, 0));
            int v = r.Version;
            Assert.IsTrue(r.SetState(Id(1), TreeState.Felled));
            Assert.AreEqual(v + 1, r.Version);
            Assert.AreEqual(1, r.Count);
            CollectionAssert.AreEqual(new[] { 2 }, r.All.Select(t => t.Id.Value));
            CollectionAssert.AreEqual(new[] { 1 }, r.Felled.Select(t => t.Id.Value));
            Assert.IsEmpty(r.Stumps);
            Assert.IsTrue(r.SetState(Id(1), TreeState.Stump));
            Assert.AreEqual(v + 2, r.Version);
            Assert.IsEmpty(r.Felled);
            CollectionAssert.AreEqual(new[] { 1 }, r.Stumps.Select(t => t.Id.Value));
        }

        [Test]
        public void SetState_SameStateOrUnknownId_ReturnsFalseAndKeepsVersion()
        {
            var r = Reg((1, 0, 0));
            int v = r.Version;
            Assert.IsFalse(r.SetState(Id(1), TreeState.Standing));
            Assert.IsFalse(r.SetState(Id(9), TreeState.Felled));
            Assert.AreEqual(v, r.Version);
        }

        [Test]
        public void SetState_Backwards_Throws()
        {
            var r = Reg((1, 0, 0));
            r.SetState(Id(1), TreeState.Stump);
            Assert.Throws<InvalidOperationException>(() => r.SetState(Id(1), TreeState.Felled));
            Assert.Throws<InvalidOperationException>(() => r.SetState(Id(1), TreeState.Standing));
        }

        [Test]
        public void SetState_NonStandingTreeIsHiddenFromRectAndRadiusQueries()
        {
            var r = Reg((1, 0, 0));
            r.SetState(Id(1), TreeState.Felled);
            Assert.IsEmpty(r.QueryRadius(0, 0, 5));
            Assert.IsEmpty(r.QueryRect(new LordsLegacy.Sim.Terrain.Bounds2(-1, -1, 1, 1)));
        }

        [Test]
        public void Reserve_SecondOwnerRefused_SameOwnerOk_ReleaseFrees()
        {
            var r = Reg((1, 0, 0));
            Assert.IsTrue(r.Reserve(Id(1), 7));
            Assert.IsTrue(r.Reserve(Id(1), 7));
            Assert.IsFalse(r.Reserve(Id(1), 8));
            Assert.AreEqual(7, r.Get(Id(1)).ReservedBy);
            Assert.IsTrue(r.Release(Id(1)));
            Assert.IsFalse(r.Release(Id(1)));
            Assert.IsTrue(r.Reserve(Id(1), 8));
        }

        [Test]
        public void Reserve_UnknownOrNonStanding_ReturnsFalse()
        {
            var r = Reg((1, 0, 0));
            Assert.IsFalse(r.Reserve(Id(9), 1));
            r.SetState(Id(1), TreeState.Felled);
            Assert.IsFalse(r.Reserve(Id(1), 1));
        }

        [Test]
        public void SetState_LeavingStandingCancelsReservation()
        {
            var r = Reg((1, 0, 0));
            r.Reserve(Id(1), 3);
            r.SetState(Id(1), TreeState.Felled);
            Assert.IsNull(r.Get(Id(1)).ReservedBy);
        }

        [Test]
        public void Remove_CancelsReservation()
        {
            var r = Reg((1, 0, 0));
            r.Reserve(Id(1), 3);
            var rec = r.Get(Id(1));
            Assert.IsTrue(r.Remove(Id(1)));
            Assert.IsNull(rec.ReservedBy);
            Assert.IsFalse(r.Release(Id(1)));
            Assert.IsFalse(r.Reserve(Id(1), 4));
        }

        [Test]
        public void Clearing_RemovesReservedTree_AndFreesItsReservation()
        {
            var r = Reg((1, 0, 0), (2, 50, 0));
            r.Reserve(Id(1), 3);
            var rec = r.Get(Id(1));
            TreeClearing.RemoveNearPolyline(r, new[] { PointMm.FromMetres(0, 0) }, 1);
            Assert.IsNull(rec.ReservedBy);
            Assert.AreEqual(1, r.Count);
        }

        [Test]
        public void Remove_FelledTree_DoesNotChangeStandingCount()
        {
            var r = Reg((1, 0, 0), (2, 5, 0));
            r.SetState(Id(1), TreeState.Felled);
            r.Remove(Id(1));
            Assert.AreEqual(1, r.Count);
            r.Remove(Id(2));
            Assert.AreEqual(0, r.Count);
        }

        [Test]
        public void Clearing_LeavesFelledAndStumpsAlone()
        {
            var r = Reg((1, 0, 0), (2, 1, 0), (3, 2, 0));
            r.SetState(Id(1), TreeState.Felled);
            r.SetState(Id(2), TreeState.Stump);
            var removed = TreeClearing.RemoveNearPolyline(r, new[] { PointMm.FromMetres(1, 0) }, 5);
            CollectionAssert.AreEqual(new[] { Id(3) }, removed);
            Assert.AreEqual(1, r.Felled.Count());
            Assert.AreEqual(1, r.Stumps.Count());
        }

        [Test]
        public void CircleRegion_CountsStandingOnlyEdgeInclusive()
        {
            var r = Reg((1, 0, 0), (2, 3, 0), (3, 4, 0), (4, 1, 1));
            r.SetState(Id(4), TreeState.Felled);
            var c = TreeRegion.Circle(0, 0, 3);
            Assert.AreEqual(2, r.CountStandingIn(c));
            Assert.IsTrue(r.AnyStandingIn(c));
        }

        [Test]
        public void CircleRegion_ZeroRadiusOnlyExactPoint()
        {
            var r = Reg((1, 2, 2));
            Assert.IsTrue(r.AnyStandingIn(TreeRegion.Circle(2, 2, 0)));
            Assert.IsFalse(r.AnyStandingIn(TreeRegion.Circle(2.1f, 2, 0)));
        }

        [Test]
        public void PolygonRegion_CountsInsideAndOnBoundary()
        {
            var r = Reg((1, 1, 1), (2, 0, 0), (3, 5, 5), (4, 4, 4));
            var sq = TreeRegion.Polygon(new[]
            {
                PointMm.FromMetres(0, 0), PointMm.FromMetres(4, 0), PointMm.FromMetres(4, 4), PointMm.FromMetres(0, 4)
            });
            Assert.AreEqual(3, r.CountStandingIn(sq));
        }

        [Test]
        public void Region_EmptyRegistryOrNoTrees_IsFalseAndZero()
        {
            var r = new TreeRegistry();
            var c = TreeRegion.Circle(0, 0, 10);
            Assert.IsFalse(r.AnyStandingIn(c));
            Assert.AreEqual(0, r.CountStandingIn(c));
        }

        [Test]
        public void Region_BadArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TreeRegion.Circle(0, 0, -1));
            Assert.Throws<ArgumentException>(() => TreeRegion.Polygon(new[] { PointMm.FromMetres(0, 0), PointMm.FromMetres(1, 0) }));
            Assert.Throws<ArgumentNullException>(() => new TreeRegistry().CountStandingIn(null));
        }

        [Test]
        public void Nearest_SkipsReservedFelledAndStumps()
        {
            var r = Reg((1, 1, 0), (2, 2, 0), (3, 3, 0), (4, 4, 0));
            r.Reserve(Id(1), 1);
            r.SetState(Id(2), TreeState.Felled);
            Assert.AreEqual(3, r.NearestUnreservedStanding(0, 0).Id.Value);
            r.Release(Id(1));
            Assert.AreEqual(1, r.NearestUnreservedStanding(0, 0).Id.Value);
        }

        [Test]
        public void Nearest_RespectsRegion()
        {
            var r = Reg((1, 1, 0), (2, 10, 0));
            var far = TreeRegion.Circle(10, 0, 2);
            Assert.AreEqual(2, r.NearestUnreservedStanding(0, 0, far).Id.Value);
            Assert.IsNull(r.NearestUnreservedStanding(0, 0, TreeRegion.Circle(50, 50, 1)));
        }

        [Test]
        public void Nearest_NoneAvailable_ReturnsNull()
        {
            var r = Reg((1, 1, 0));
            r.Reserve(Id(1), 1);
            Assert.IsNull(r.NearestUnreservedStanding(0, 0));
            Assert.IsNull(new TreeRegistry().NearestUnreservedStanding(0, 0));
        }

        [Test]
        public void Nearest_Tie_GoesToLowestId()
        {
            var r = Reg((5, 1, 0), (2, -1, 0));
            Assert.AreEqual(2, r.NearestUnreservedStanding(0, 0).Id.Value);
        }
    }
}
