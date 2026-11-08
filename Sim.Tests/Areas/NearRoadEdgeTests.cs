using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Areas
{
    public class NearRoadEdgeTests
    {
        private const string T = AreaTypes.TestId;
        private const string W = AreaTypes.TestWorkId;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);
        private static SimWorld Ramp() => SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());

        // Straight road along +X from (20,50) to (80,50): Left side z = 51.25, Right side z = 48.75.
        private static RoadSegment StraightRoad(SimWorld w) =>
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(80, 50) },
                new[] { M(20, 50), M(35, 50), M(50, 50), M(65, 50), M(80, 50) });

        // Wavy road: z = 50 + 8 sin(pi (x-20) / 60), a sample every 5 m.
        private static RoadSegment CurvedRoad(SimWorld w)
        {
            var s = new PointMm[13];
            for (int i = 0; i < s.Length; i++)
            {
                double x = 20 + 5 * i;
                s[i] = M(x, 50 + 8 * System.Math.Sin(System.Math.PI * (x - 20) / 60));
            }
            return w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { s[0], s[12] }, s);
        }

        private static AreaShape NearShape(int segId, RoadSide side, PointMm a, PointMm b, PointMm c, PointMm d) =>
            new AreaShape(new[] { a, b, c, d },
                new[] { AreaEdge.NearRoad(segId, side), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });

        [Test]
        public void AreaTypes_HaveNearRoadReachOfTenMetres()
        {
            Assert.AreEqual(10.0, AreaTypes.Test.NearRoadReachM);
            Assert.AreEqual(10.0, AreaTypes.TestWork.NearRoadReachM);
        }

        [Test]
        public void StraightRoad_TapersLinearly_AndKeepsBothVertices()
        {
            SimWorld w = Ramp();
            RoadSegment seg = StraightRoad(w);
            PointMm a = M(30, 52.25), b = M(70, 56.25); // 1 m and 5 m outside the Left side
            AreaShape shape = NearShape(seg.Id, RoadSide.Left, a, b, M(70, 80), M(30, 80));
            PointMm[] poly = AreaFlattener.Flatten(shape, w.Roads);
            Assert.That(poly, Does.Contain(a));
            Assert.That(poly, Does.Contain(b));
            // Side samples at x = 35, 50, 65 sit 1 + 4*(x-30)/40 m outside z = 51.25.
            Assert.That(poly, Does.Contain(M(35, 51.25 + 1.5)));
            Assert.That(poly, Does.Contain(M(50, 51.25 + 3.0)));
            Assert.That(poly, Does.Contain(M(65, 51.25 + 4.5)));
            Assert.IsTrue(AreaValidator.Validate(w, shape, T).IsValid);
        }

        [Test]
        public void FlattenedEdge_StartsAndEndsOnTheVertices_InEdgeOrder()
        {
            SimWorld w = Ramp();
            RoadSegment seg = StraightRoad(w);
            PointMm a = M(30, 52.25), b = M(70, 56.25);
            AreaShape shape = NearShape(seg.Id, RoadSide.Left, a, b, M(70, 80), M(30, 80));
            PointMm[] poly = AreaFlattener.Flatten(shape, w.Roads);
            int ia = System.Array.IndexOf(poly, a), ib = System.Array.IndexOf(poly, b);
            Assert.That(ia, Is.GreaterThanOrEqualTo(0));
            Assert.That(ib, Is.GreaterThanOrEqualTo(0));
            // Edge points lie between them in x, strictly monotone.
            int count = poly.Length;
            int step = (ib - ia + count) % count == 4 ? 1 : -1; // CCW normalisation may reverse the order
            var xs = new System.Collections.Generic.List<long>();
            for (int k = 0, i = ia; k < 5; k++, i = (i + step + count) % count) xs.Add(poly[i].X);
            Assert.That(xs, Is.Ordered.Ascending);
        }

        [Test]
        public void VerticesOnTheSideLine_FlattenLikeFollowsRoad()
        {
            SimWorld w = Ramp();
            RoadSegment seg = CurvedRoad(w);
            PointMm[] side = seg.SideLine(RoadSide.Left);
            PointMm a = side[2], b = side[9];
            PointMm c = M(b.XMetres, 90), d = M(a.XMetres, 90);
            var follows = new AreaShape(new[] { a, b, c, d },
                new[] { AreaEdge.FollowsRoad(seg.Id, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            AreaShape near = NearShape(seg.Id, RoadSide.Left, a, b, c, d);
            PointMm[] expected = AreaFlattener.Flatten(follows, w.Roads);
            Assert.That(expected.Length, Is.GreaterThan(4));
            Assert.AreEqual(expected, AreaFlattener.Flatten(near, w.Roads));
            Assert.IsTrue(AreaValidator.Validate(w, near, T).IsValid);
        }

        [Test]
        public void CurvedRoad_EdgeStaysWithinTenMetres_AndRoughlyParallel()
        {
            SimWorld w = Ramp();
            RoadSegment seg = CurvedRoad(w);
            PointMm[] side = seg.SideLine(RoadSide.Left);
            PointMm a = M(side[1].XMetres, side[1].ZMetres + 2), b = M(side[11].XMetres, side[11].ZMetres + 6);
            AreaShape shape = NearShape(seg.Id, RoadSide.Left, a, b, M(b.XMetres, 95), M(a.XMetres, 95));
            AreaPreview p = AreaValidator.Validate(w, shape, W);
            Assert.AreEqual(AreaRefusal.Valid, p.Reason);
            Assert.That(p.Polygon, Does.Contain(a));
            Assert.That(p.Polygon, Does.Contain(b));
            PointMm[] edge = p.Polygon.Where(q => q.XMetres >= a.XMetres - 0.01 && q.XMetres <= b.XMetres + 0.01
                                                  && q.ZMetres < 80 && q.ZMetres > 40).ToArray();
            Assert.That(edge.Length, Is.GreaterThan(5));
            foreach (PointMm q in edge)
            {
                double dist = Polylines.Nearest(side, q).DistanceMm / 1000.0;
                Assert.That(dist, Is.LessThanOrEqualTo(10.0));
                Assert.That(dist, Is.InRange(1.5, 6.5), "tapers between about 2 m and 6 m from the side");
            }
        }

        [Test]
        public void ReverseVertexOrder_StillFollowsTheRoad()
        {
            SimWorld w = Ramp();
            RoadSegment seg = StraightRoad(w);
            AreaShape shape = NearShape(seg.Id, RoadSide.Left, M(70, 56.25), M(30, 52.25), M(30, 80), M(70, 80));
            PointMm[] poly = AreaFlattener.Flatten(shape, w.Roads);
            Assert.That(poly, Does.Contain(M(50, 54.25)));
            Assert.IsTrue(Polygons.IsSimple(poly));
        }

        [Test]
        public void RightSide_OffsetsAwayFromTheRoad()
        {
            SimWorld w = Ramp();
            RoadSegment seg = StraightRoad(w);
            AreaShape shape = NearShape(seg.Id, RoadSide.Right, M(30, 46.75), M(70, 46.75), M(70, 20), M(30, 20));
            PointMm[] poly = AreaFlattener.Flatten(shape, w.Roads);
            Assert.That(poly, Does.Contain(M(50, 46.75)));
            Assert.IsTrue(AreaValidator.Validate(w, shape, T).IsValid);
        }

        [Test]
        public void ReachExceeded_IsNotOnRoad_BothTypes()
        {
            SimWorld w = Ramp();
            RoadSegment seg = StraightRoad(w);
            // Vertex b is 11.75 m outside the Left side (z = 51.25).
            AreaShape shape = NearShape(seg.Id, RoadSide.Left, M(30, 53), M(70, 63), M(70, 90), M(30, 90));
            Assert.AreEqual("not on road", AreaValidator.Validate(w, shape, T).ReasonText);
            Assert.AreEqual("not on road", AreaValidator.Validate(w, shape, W).ReasonText);
        }

        [Test]
        public void JustInsideReach_IsAccepted()
        {
            SimWorld w = Ramp();
            RoadSegment seg = StraightRoad(w);
            AreaShape shape = NearShape(seg.Id, RoadSide.Left, M(30, 53), M(70, 61), M(70, 90), M(30, 90));
            Assert.AreEqual(AreaRefusal.Valid, AreaValidator.Validate(w, shape, W).Reason);
        }

        [Test]
        public void WrongSide_VertexOnTheRoadSide_IsNotOnRoad()
        {
            SimWorld w = Ramp();
            RoadSegment seg = StraightRoad(w);
            // Right-side line is z = 48.75; the Left-of-road vertices are on the road side of it.
            AreaShape onRoadSide = NearShape(seg.Id, RoadSide.Right, M(30, 50), M(70, 46), M(70, 20), M(30, 20));
            Assert.AreEqual("not on road", AreaValidator.Validate(w, onRoadSide, T).ReasonText);
            // Left side asked for but vertices are on the far (Right) side of the road.
            AreaShape wrong = NearShape(seg.Id, RoadSide.Left, M(30, 45), M(70, 45), M(70, 20), M(30, 20));
            Assert.AreEqual("not on road", AreaValidator.Validate(w, wrong, W).ReasonText);
        }

        [Test]
        public void MissingSegment_IsNotOnRoad()
        {
            SimWorld w = Ramp();
            AreaShape shape = NearShape(999, RoadSide.Left, M(30, 53), M(70, 55), M(70, 90), M(30, 90));
            Assert.AreEqual("not on road", AreaValidator.Validate(w, shape, T).ReasonText);
            Assert.AreEqual("not on road", AreaValidator.Validate(w, shape, W).ReasonText);
        }

        [Test]
        public void Flatten_MissingSegmentOrNullNetwork_FallsBackToStraight()
        {
            SimWorld w = Ramp();
            PointMm[] v = { M(30, 53), M(70, 55), M(70, 90), M(30, 90) };
            AreaShape shape = NearShape(999, RoadSide.Left, v[0], v[1], v[2], v[3]);
            Assert.AreEqual(v, AreaFlattener.Flatten(shape, w.Roads));
            Assert.AreEqual(v, AreaFlattener.Flatten(shape, null));
        }

        [Test]
        public void OtherChecksRunOnTheFlattenedOutline_CrossingEdgesAreRefused()
        {
            SimWorld w = Ramp();
            RoadSegment seg = StraightRoad(w);
            // Bow-tie: the NearRoad edge and the opposite straight edge cross.
            AreaShape shape = NearShape(seg.Id, RoadSide.Left, M(30, 53), M(70, 55), M(30, 90), M(70, 90));
            Assert.AreEqual("edges cross", AreaValidator.Validate(w, shape, T).ReasonText);
        }

        [Test]
        public void DesignateArea_StoresFlattenedOutline_AndRoadDeletionLeavesItUnchanged()
        {
            SimWorld w = Ramp();
            RoadSegment seg = CurvedRoad(w);
            PointMm[] side = seg.SideLine(RoadSide.Left);
            PointMm a = M(side[1].XMetres, side[1].ZMetres + 2), b = M(side[11].XMetres, side[11].ZMetres + 6);
            AreaShape shape = NearShape(seg.Id, RoadSide.Left, a, b, M(b.XMetres, 95), M(a.XMetres, 95));
            CommandResult r = w.Execute(new DesignateArea(shape, W));
            Assert.That(r.IsAccepted, Is.True);
            AreaRecord rec = w.Areas.Get((int)r.Created[0]);
            PointMm[] before = rec.Polygon.ToArray();
            Assert.AreEqual(AreaFlattener.Flatten(shape, w.Roads), before);

            Assert.That(w.Execute(new DeleteRoadSegment(seg.Id)).IsAccepted, Is.True);
            Assert.IsNull(w.Roads.GetSegment(seg.Id));
            Assert.AreEqual(before, w.Areas.Get(rec.Id).Polygon.ToArray());
        }
    }
}
