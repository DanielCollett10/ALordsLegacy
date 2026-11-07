using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Areas
{
    public class AreaRegistryTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static AreaRecord Make(AreaRegistry reg, AreaTypeDef type, double x0, double z0, double size,
            bool clockwise = false)
        {
            var v = new[] { M(x0, z0), M(x0 + size, z0), M(x0 + size, z0 + size), M(x0, z0 + size) };
            if (clockwise) v = new[] { v[0], v[3], v[2], v[1] };
            var shape = AreaShape.Straight(v);
            var poly = AreaFlattener.Flatten(shape, null);
            return new AreaRecord(reg.NextId(), type.Id, reg.NextName(type), shape, poly);
        }

        private static AreaRegistry NewReg() => new AreaRegistry(32);

        [Test]
        public void SimWorld_Areas_IsNeverNullAndEmpty()
        {
            var w = new SimWorld(new SimTuning());
            Assert.IsNotNull(w.Areas);
            Assert.AreEqual(0, w.Areas.Count);
        }

        [Test]
        public void AddAndRemove_UpdateCountAndGet()
        {
            var reg = NewReg();
            var a = Make(reg, AreaTypes.Test, 0, 0, 10);
            reg.Add(a);
            Assert.AreEqual(1, reg.Count);
            Assert.AreSame(a, reg.Get(a.Id));
            Assert.IsTrue(reg.Remove(a.Id));
            Assert.AreEqual(0, reg.Count);
            Assert.IsNull(reg.Get(a.Id));
        }

        [Test]
        public void Remove_UnknownId_ReturnsFalseAndKeepsVersion()
        {
            var reg = NewReg();
            int v = reg.Version;
            Assert.IsFalse(reg.Remove(99));
            Assert.AreEqual(v, reg.Version);
        }

        [Test]
        public void Version_IncreasesOnEveryAddAndRemove()
        {
            var reg = NewReg();
            int v0 = reg.Version;
            var a = Make(reg, AreaTypes.Test, 0, 0, 10);
            reg.Add(a);
            int v1 = reg.Version;
            reg.Remove(a.Id);
            Assert.Greater(v1, v0);
            Assert.Greater(reg.Version, v1);
        }

        [Test]
        public void Add_DuplicateId_Throws()
        {
            var reg = NewReg();
            var a = Make(reg, AreaTypes.Test, 0, 0, 10);
            reg.Add(a);
            Assert.Throws<System.ArgumentException>(() => reg.Add(a));
        }

        [Test]
        public void Names_UsePrefixAndNumberPerType()
        {
            var reg = NewReg();
            Assert.AreEqual("Test area 1", reg.NextName(AreaTypes.Test));
            Assert.AreEqual("Test area 2", reg.NextName(AreaTypes.Test));
            Assert.AreEqual("Test work area 1", reg.NextName(AreaTypes.TestWork));
        }

        [Test]
        public void Names_AreNotReusedAfterRemove()
        {
            var reg = NewReg();
            var a = Make(reg, AreaTypes.Test, 0, 0, 10);
            reg.Add(a);
            Assert.AreEqual("Test area 1", a.Name);
            reg.Remove(a.Id);
            Assert.AreEqual("Test area 2", Make(reg, AreaTypes.Test, 0, 0, 10).Name);
        }

        [Test]
        public void Record_StoresBoundsAndPolygon()
        {
            var a = Make(NewReg(), AreaTypes.Test, 2, 3, 10);
            Assert.AreEqual(M(2, 3), a.Bounds.Min);
            Assert.AreEqual(M(12, 13), a.Bounds.Max);
            Assert.AreEqual(4, a.Polygon.Count);
        }

        [Test]
        public void Candidates_ReturnOnlyOverlappingBuildingAreas()
        {
            var reg = NewReg();
            var near = Make(reg, AreaTypes.Test, 0, 0, 10);
            var far = Make(reg, AreaTypes.Test, 500, 500, 10);
            reg.Add(near); reg.Add(far);
            var hits = reg.QueryOverlapCandidates(new BoundsMm(M(5, 5), M(20, 20)));
            Assert.AreEqual(new[] { near.Id }, hits.Select(h => h.Id).ToArray());
        }

        [Test]
        public void Candidates_ExcludeWorkAreas()
        {
            var reg = NewReg();
            reg.Add(Make(reg, AreaTypes.TestWork, 0, 0, 10));
            Assert.AreEqual(0, reg.QueryOverlapCandidates(new BoundsMm(M(0, 0), M(5, 5))).Count);
        }

        [Test]
        public void Candidates_DropRemovedArea()
        {
            var reg = NewReg();
            var a = Make(reg, AreaTypes.Test, 0, 0, 10);
            reg.Add(a);
            reg.Remove(a.Id);
            Assert.AreEqual(0, reg.QueryOverlapCandidates(new BoundsMm(M(0, 0), M(5, 5))).Count);
        }

        [Test]
        public void Candidates_LargeAreaSpanningCellsReturnedOnce()
        {
            var reg = NewReg();
            reg.Add(Make(reg, AreaTypes.Test, 0, 0, 100));
            Assert.AreEqual(1, reg.QueryOverlapCandidates(new BoundsMm(M(-10, -10), M(110, 110))).Count);
        }

        [Test]
        public void FindAt_InsideReturnsAreaOfThatKind()
        {
            var reg = NewReg();
            var b = Make(reg, AreaTypes.Test, 0, 0, 10);
            var w = Make(reg, AreaTypes.TestWork, 0, 0, 10);
            reg.Add(b); reg.Add(w);
            Assert.AreSame(b, reg.FindAt(M(5, 5), AreaKind.Building));
            Assert.AreSame(w, reg.FindAt(M(5, 5), AreaKind.Work));
        }

        [Test]
        public void FindAt_OutsideInsideBoundsButOutsidePolygon_ReturnsNull()
        {
            var reg = NewReg();
            var shape = AreaShape.Straight(new[] { M(0, 0), M(10, 0), M(0, 10) });
            reg.Add(new AreaRecord(1, AreaTypes.TestId, "t", shape, AreaFlattener.Flatten(shape, null)));
            Assert.IsNull(reg.FindAt(M(9, 9), AreaKind.Building));
            Assert.IsNotNull(reg.FindAt(M(2, 2), AreaKind.Building));
        }

        [Test]
        public void FindAt_EmptyRegistry_ReturnsNull()
        {
            Assert.IsNull(NewReg().FindAt(M(0, 0), AreaKind.Building));
        }

        [Test]
        public void Snap_EmptyRegistry_ReturnsNull()
        {
            Assert.IsNull(NewReg().FindNearestSnap(M(0, 0), 1000));
        }

        [Test]
        public void Snap_Vertex_WithinRange()
        {
            var reg = NewReg();
            var a = Make(reg, AreaTypes.Test, 0, 0, 10);
            reg.Add(a);
            var hit = reg.FindNearestSnap(M(10.3, 0.4), 1000).Value;
            Assert.AreEqual(AreaSnapKind.Vertex, hit.Kind);
            Assert.AreEqual(a.Id, hit.AreaId);
            Assert.AreEqual(1, hit.Index);
            Assert.AreEqual(M(10, 0), hit.Point);
            Assert.AreEqual(500, hit.DistanceMm, 1e-6);
        }

        [Test]
        public void Snap_Edge_WhenNoVertexInRange()
        {
            var reg = NewReg();
            var a = Make(reg, AreaTypes.Test, 0, 0, 10);
            reg.Add(a);
            var hit = reg.FindNearestSnap(M(5, -0.5), 1000).Value;
            Assert.AreEqual(AreaSnapKind.Edge, hit.Kind);
            Assert.AreEqual(0, hit.Index);
            Assert.AreEqual(M(5, 0), hit.Point);
            Assert.AreEqual(500, hit.DistanceMm, 1e-6);
        }

        [Test]
        public void Snap_VertexPreferredOverCloserEdge()
        {
            var reg = NewReg();
            reg.Add(Make(reg, AreaTypes.Test, 0, 0, 10));
            // 0.8 m from vertex (0,0) but only 0.1 m from edge 3 (x=0).
            var hit = reg.FindNearestSnap(M(-0.1, 0.79), 1000).Value;
            Assert.AreEqual(AreaSnapKind.Vertex, hit.Kind);
            Assert.AreEqual(0, hit.Index);
        }

        [Test]
        public void Snap_OutOfRange_ReturnsNull()
        {
            var reg = NewReg();
            reg.Add(Make(reg, AreaTypes.Test, 0, 0, 10));
            Assert.IsNull(reg.FindNearestSnap(M(5, -3), 1000));
        }

        [Test]
        public void Snap_ZeroRange_HitsExactVertexOnly()
        {
            var reg = NewReg();
            reg.Add(Make(reg, AreaTypes.Test, 0, 0, 10));
            Assert.AreEqual(AreaSnapKind.Vertex, reg.FindNearestSnap(M(10, 10), 0).Value.Kind);
            Assert.IsNull(reg.FindNearestSnap(M(10.001, 10), 0));
        }

        [Test]
        public void Snap_PicksNearestAcrossAreas()
        {
            var reg = NewReg();
            var a = Make(reg, AreaTypes.Test, 0, 0, 10);
            var b = Make(reg, AreaTypes.TestWork, 11, 0, 10);
            reg.Add(a); reg.Add(b);
            var hit = reg.FindNearestSnap(M(10.8, 0), 2000).Value;
            Assert.AreEqual(b.Id, hit.AreaId);
        }

        [Test]
        public void Snap_Edge_OnClockwiseShape_ReportsShapeEdgeIndex()
        {
            var reg = NewReg();
            // Clockwise: vertices (0,0),(0,10),(10,10),(10,0). Edge 0 runs (0,0)->(0,10), i.e. the x=0 side.
            var a = Make(reg, AreaTypes.Test, 0, 0, 10, clockwise: true);
            reg.Add(a);
            var hit = reg.FindNearestSnap(M(-0.5, 5), 1000).Value;
            Assert.AreEqual(AreaSnapKind.Edge, hit.Kind);
            Assert.AreEqual(0, hit.Index);
            Assert.AreEqual(M(0, 5), hit.Point);
        }

        [Test]
        public void Snap_Edge_OnBentEdge_FollowsCurveAndReportsEdgeIndex()
        {
            var reg = NewReg();
            var v = new[] { M(0, 0), M(10, 0), M(10, 10), M(0, 10) };
            var edges = new[] { AreaEdge.Bent(M(5, -3)), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() };
            var shape = new AreaShape(v, edges);
            reg.Add(new AreaRecord(1, AreaTypes.TestId, "t", shape, AreaFlattener.Flatten(shape, null)));
            var hit = reg.FindNearestSnap(M(5, -3.2), 1000).Value;
            Assert.AreEqual(AreaSnapKind.Edge, hit.Kind);
            Assert.AreEqual(0, hit.Index);
            Assert.AreEqual(-3000, hit.Point.Z, 100);
        }
    }
}
