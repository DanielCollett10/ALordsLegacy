using System;
using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Roads
{
    public class RoadNetworkTests
    {
        private const string Type = "road.dirt_path";
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static RoadSegment Add(RoadNetwork n, params PointMm[] cps)
        {
            var samples = new CatmullRomSampler(new SimTuning()).Sample(cps);
            return n.AddSegment(Type, CurveKind.CatmullRomCentripetal, cps, samples);
        }

        private static RoadSegment Straight(RoadNetwork n, double x0, double z0, double x1, double z1) =>
            Add(n, M(x0, z0), M(x1, z1));

        [Test]
        public void SimWorld_Roads_IsNeverNullAndStartsEmpty()
        {
            var w = new SimWorld(new SimTuning());
            Assert.That(w.Roads, Is.Not.Null);
            Assert.That(w.Roads.SegmentCount, Is.EqualTo(0));
            Assert.That(w.Roads.ComponentCount, Is.EqualTo(0));
        }

        [Test]
        public void AddSegment_CreatesNodesAtControlPointEnds()
        {
            var n = new RoadNetwork();
            RoadSegment s = Add(n, M(0, 0), M(5, 5), M(10, 0));
            Assert.That(n.GetNode(s.NodeA).Position, Is.EqualTo(s.ControlPoints[0]));
            Assert.That(n.GetNode(s.NodeB).Position, Is.EqualTo(s.ControlPoints.Last()));
            Assert.That(s.Samples[0], Is.EqualTo(s.ControlPoints[0]));
            Assert.That(s.TypeId, Is.EqualTo(Type));
            Assert.That(s.LengthM, Is.GreaterThan(10));
            Assert.That(n.NodeCount, Is.EqualTo(2));
        }

        [Test]
        public void AddSegment_ReusesNodeAtSamePosition()
        {
            var n = new RoadNetwork();
            RoadSegment a = Straight(n, 0, 0, 10, 0);
            RoadSegment b = Straight(n, 10, 0, 20, 0);
            Assert.That(b.NodeA, Is.EqualTo(a.NodeB));
            Assert.That(n.NodeCount, Is.EqualTo(3));
            Assert.That(n.GetNode(a.NodeB).SegmentIds, Is.EquivalentTo(new[] { a.Id, b.Id }));
        }

        [Test]
        public void AddSegment_RejectsFewerThanTwoPointsAndMismatchedEnds()
        {
            var n = new RoadNetwork();
            var one = new[] { M(0, 0) };
            var two = new[] { M(0, 0), M(5, 0) };
            Assert.Throws<ArgumentException>(() => n.AddSegment(Type, CurveKind.CatmullRomCentripetal, one, two));
            Assert.Throws<ArgumentException>(() => n.AddSegment(Type, CurveKind.CatmullRomCentripetal, two, one));
            Assert.Throws<ArgumentException>(() =>
                n.AddSegment(Type, CurveKind.CatmullRomCentripetal, two, new[] { M(0, 0), M(6, 0) }));
            Assert.That(n.Version, Is.EqualTo(0));
        }

        [Test]
        public void Split_MidSpan_KeepsSamplesExactlyAndInsertsCutControlPoint()
        {
            var n = new RoadNetwork();
            RoadSegment s = Add(n, M(0, 0), M(10, 5), M(20, 0), M(30, 5));
            PointMm[] original = s.Samples.ToArray();
            PointMm[] cps = s.ControlPoints.ToArray();

            SplitResult r = n.SplitSegmentAt(s.Id, M(14, 3));
            Assert.That(r.Split, Is.True);
            RoadSegment f = n.GetSegment(r.FirstSegmentId), g = n.GetSegment(r.SecondSegmentId);
            Assert.That(n.GetSegment(s.Id), Is.Null);
            PointMm cut = n.GetNode(r.NodeId).Position;

            // Samples: first half + second half = original with cut point (maybe inserted once).
            Assert.That(f.Samples.Last(), Is.EqualTo(cut));
            Assert.That(g.Samples[0], Is.EqualTo(cut));
            var joined = f.Samples.Concat(g.Samples.Skip(1)).ToList();
            Assert.That(joined.Where(p => original.Contains(p)).ToList(), Is.EqualTo(original.ToList()));
            Assert.That(joined.Count - original.Length, Is.LessThanOrEqualTo(1));

            // Control points.
            Assert.That(f.ControlPoints.First(), Is.EqualTo(cps[0]));
            Assert.That(f.ControlPoints.Last(), Is.EqualTo(cut));
            Assert.That(g.ControlPoints.First(), Is.EqualTo(cut));
            Assert.That(g.ControlPoints.Last(), Is.EqualTo(cps.Last()));
            Assert.That(f.ControlPoints, Is.EqualTo(new[] { cps[0], cps[1], cut }));
            Assert.That(g.ControlPoints, Is.EqualTo(new[] { cut, cps[2], cps[3] }));
            Assert.That(f.NodeB, Is.EqualTo(r.NodeId));
            Assert.That(g.NodeA, Is.EqualTo(r.NodeId));
            Assert.That(f.LengthM + g.LengthM, Is.EqualTo(s.LengthM).Within(1e-6));
        }

        [Test]
        public void Split_OnSampleVertex_DoesNotDuplicateTheSample()
        {
            var n = new RoadNetwork();
            RoadSegment s = Straight(n, 0, 0, 10, 0); // samples every 1 m
            SplitResult r = n.SplitSegmentAt(s.Id, M(4, 0));
            RoadSegment f = n.GetSegment(r.FirstSegmentId), g = n.GetSegment(r.SecondSegmentId);
            Assert.That(f.Samples.Count + g.Samples.Count, Is.EqualTo(s.Samples.Count + 1));
            Assert.That(f.Samples.Last(), Is.EqualTo(M(4, 0)));
            Assert.That(g.Samples[0], Is.EqualTo(M(4, 0)));
            Assert.That(f.Samples.Zip(f.Samples.Skip(1), (a, b) => a == b).Any(x => x), Is.False);
            Assert.That(g.Samples.Zip(g.Samples.Skip(1), (a, b) => a == b).Any(x => x), Is.False);
        }

        [Test]
        public void Split_OnInteriorControlPoint_DoesNotDuplicateControlPoint()
        {
            var n = new RoadNetwork();
            RoadSegment s = Add(n, M(0, 0), M(10, 5), M(20, 0));
            SplitResult r = n.SplitSegmentAt(s.Id, M(10, 5));
            Assert.That(n.GetSegment(r.FirstSegmentId).ControlPoints, Is.EqualTo(new[] { M(0, 0), M(10, 5) }));
            Assert.That(n.GetSegment(r.SecondSegmentId).ControlPoints, Is.EqualTo(new[] { M(10, 5), M(20, 0) }));
        }

        [Test]
        public void Split_AtEnds_ChangesNothingAndReturnsExistingNode()
        {
            var n = new RoadNetwork();
            RoadSegment s = Straight(n, 0, 0, 10, 0);
            int v = n.Version;
            SplitResult a = n.SplitSegmentAt(s.Id, M(0, 0));
            SplitResult b = n.SplitSegmentAt(s.Id, M(10, 0));
            SplitResult beyond = n.SplitSegmentAt(s.Id, M(50, 0));
            Assert.That(a.Split, Is.False);
            Assert.That(a.NodeId, Is.EqualTo(s.NodeA));
            Assert.That(b.NodeId, Is.EqualTo(s.NodeB));
            Assert.That(beyond.NodeId, Is.EqualTo(s.NodeB));
            Assert.That(n.Version, Is.EqualTo(v));
            Assert.That(n.GetSegment(s.Id), Is.SameAs(s));
        }

        [Test]
        public void Split_UnknownSegment_Throws()
        {
            Assert.Throws<ArgumentException>(() => new RoadNetwork().SplitSegmentAt(1, M(0, 0)));
        }

        [Test]
        public void Split_ThenSplitAgain_KeepsEveryJoinConnected()
        {
            var n = new RoadNetwork();
            RoadSegment s = Straight(n, 0, 0, 30, 0);
            SplitResult r1 = n.SplitSegmentAt(s.Id, M(10, 0));
            SplitResult r2 = n.SplitSegmentAt(r1.SecondSegmentId, M(20, 0));
            Assert.That(n.SegmentCount, Is.EqualTo(3));
            Assert.That(n.NodeCount, Is.EqualTo(4));
            Assert.That(n.ComponentCount, Is.EqualTo(1));
            Assert.That(n.GetNode(r2.NodeId).SegmentIds.Count, Is.EqualTo(2));
        }

        [Test]
        public void Remove_DropsEndNodesWithNoSegmentsButKeepsSharedNodes()
        {
            var n = new RoadNetwork();
            RoadSegment a = Straight(n, 0, 0, 10, 0);
            RoadSegment b = Straight(n, 10, 0, 20, 0);
            Assert.That(n.RemoveSegment(a.Id), Is.True);
            Assert.That(n.GetNode(a.NodeA), Is.Null);
            Assert.That(n.GetNode(a.NodeB), Is.Not.Null);
            Assert.That(n.NodeCount, Is.EqualTo(2));
            Assert.That(n.RemoveSegment(b.Id), Is.True);
            Assert.That(n.NodeCount, Is.EqualTo(0));
            Assert.That(n.RemoveSegment(b.Id), Is.False);
        }

        [Test]
        public void Remove_ThenAddAtSameSpot_MakesFreshNode()
        {
            var n = new RoadNetwork();
            RoadSegment a = Straight(n, 0, 0, 10, 0);
            n.RemoveSegment(a.Id);
            RoadSegment b = Straight(n, 0, 0, 10, 0);
            Assert.That(b.Id, Is.Not.EqualTo(a.Id));
            Assert.That(n.NodeCount, Is.EqualTo(2));
        }

        [Test]
        public void Version_IncreasesOnEveryChange()
        {
            var n = new RoadNetwork();
            Assert.That(n.Version, Is.EqualTo(0));
            RoadSegment s = Straight(n, 0, 0, 10, 0);
            int v1 = n.Version;
            SplitResult r = n.SplitSegmentAt(s.Id, M(5, 0));
            int v2 = n.Version;
            n.RemoveSegment(r.FirstSegmentId);
            int v3 = n.Version;
            Assert.That(v1, Is.GreaterThan(0));
            Assert.That(v2, Is.GreaterThan(v1));
            Assert.That(v3, Is.GreaterThan(v2));
        }

        [Test]
        public void AreConnected_FollowsChainsAndHandlesUnknownAndSameNode()
        {
            var n = new RoadNetwork();
            RoadSegment a = Straight(n, 0, 0, 10, 0);
            RoadSegment b = Straight(n, 10, 0, 20, 0);
            RoadSegment c = Straight(n, 100, 0, 110, 0);
            Assert.That(n.AreConnected(a.NodeA, b.NodeB), Is.True);
            Assert.That(n.AreConnected(a.NodeA, c.NodeA), Is.False);
            Assert.That(n.AreConnected(a.NodeA, a.NodeA), Is.True);
            Assert.That(n.AreConnected(a.NodeA, 999), Is.False);
            Assert.That(n.AreConnected(999, 999), Is.False);
        }

        [Test]
        public void Connectivity_BreaksWhenMiddleSegmentRemoved()
        {
            var n = new RoadNetwork();
            RoadSegment a = Straight(n, 0, 0, 10, 0);
            RoadSegment b = Straight(n, 10, 0, 20, 0);
            RoadSegment c = Straight(n, 20, 0, 30, 0);
            n.RemoveSegment(b.Id);
            Assert.That(n.AreConnected(a.NodeA, c.NodeB), Is.False);
            Assert.That(n.ComponentCount, Is.EqualTo(2));
        }

        [Test]
        public void ComponentCount_CountsSeparateNetworks()
        {
            var n = new RoadNetwork();
            Assert.That(n.ComponentCount, Is.EqualTo(0));
            Straight(n, 0, 0, 10, 0);
            Straight(n, 10, 0, 10, 10);
            Assert.That(n.ComponentCount, Is.EqualTo(1));
            Straight(n, 100, 0, 110, 0);
            Assert.That(n.ComponentCount, Is.EqualTo(2));
        }

        [Test]
        public void ComponentCount_ClosedLoopSegment_IsOneNetwork()
        {
            var n = new RoadNetwork();
            RoadSegment loop = Add(n, M(0, 0), M(10, 0), M(10, 10), M(0, 0));
            Assert.That(loop.NodeA, Is.EqualTo(loop.NodeB));
            Assert.That(n.ComponentCount, Is.EqualTo(1));
            Assert.That(n.GetNode(loop.NodeA).SegmentIds.Count, Is.EqualTo(1));
            Assert.That(n.RemoveSegment(loop.Id), Is.True);
            Assert.That(n.NodeCount, Is.EqualTo(0));
        }

        [Test]
        public void ReachableSegmentCount_CountsSameNetworkOnly()
        {
            var n = new RoadNetwork();
            RoadSegment a = Straight(n, 0, 0, 10, 0);
            Straight(n, 10, 0, 20, 0);
            Straight(n, 10, 0, 10, 10); // T junction
            RoadSegment far = Straight(n, 100, 0, 110, 0);
            Assert.That(n.ReachableSegmentCount(a.Id), Is.EqualTo(3));
            Assert.That(n.ReachableSegmentCount(far.Id), Is.EqualTo(1));
            Assert.That(n.ReachableSegmentCount(999), Is.EqualTo(0));
        }

        [Test]
        public void FindNearest_EmptyNetwork_ReturnsNone()
        {
            RoadHit h = new RoadNetwork().FindNearest(M(0, 0), 1000000);
            Assert.That(h.Kind, Is.EqualTo(RoadHitKind.None));
            Assert.That(h.NodeId, Is.EqualTo(-1));
            Assert.That(h.SegmentId, Is.EqualTo(-1));
        }

        [Test]
        public void FindNearest_OutOfRange_ReturnsNone()
        {
            var n = new RoadNetwork();
            Straight(n, 0, 0, 10, 0);
            Assert.That(n.FindNearest(M(5, 3), 2999).Kind, Is.EqualTo(RoadHitKind.None));
        }

        [Test]
        public void FindNearest_NearEnd_ReturnsEndHit()
        {
            var n = new RoadNetwork();
            RoadSegment s = Straight(n, 0, 0, 10, 0);
            RoadHit h = n.FindNearest(M(10.5, 0.5), 2000);
            Assert.That(h.Kind, Is.EqualTo(RoadHitKind.End));
            Assert.That(h.NodeId, Is.EqualTo(s.NodeB));
            Assert.That(h.SegmentId, Is.EqualTo(s.Id));
            Assert.That(h.Point, Is.EqualTo(M(10, 0)));
            Assert.That(h.DistanceMm, Is.EqualTo(Math.Sqrt(500000)).Within(1e-6));
            Assert.That(h.ArcPositionM, Is.EqualTo(s.LengthM).Within(1e-9));
            Assert.That(n.FindNearest(M(-0.5, 0), 2000).ArcPositionM, Is.EqualTo(0));
        }

        [Test]
        public void FindNearest_OnLine_ReturnsLineHitWithArcPosition()
        {
            var n = new RoadNetwork();
            RoadSegment s = Straight(n, 0, 0, 10, 0);
            RoadHit h = n.FindNearest(M(4, 1), 2000);
            Assert.That(h.Kind, Is.EqualTo(RoadHitKind.Line));
            Assert.That(h.NodeId, Is.EqualTo(-1));
            Assert.That(h.SegmentId, Is.EqualTo(s.Id));
            Assert.That(h.Point, Is.EqualTo(M(4, 0)));
            Assert.That(h.DistanceMm, Is.EqualTo(1000).Within(1e-9));
            Assert.That(h.ArcPositionM, Is.EqualTo(4).Within(1e-9));
        }

        [Test]
        public void FindNearest_EndInRangePreferredOverNearerLinePoint()
        {
            var n = new RoadNetwork();
            Straight(n, 0, 0, 10, 0);
            Straight(n, 5, 1.5, 5, 10); // end at (5,1.5)
            // Query is 0.2 m from the first road's line but 1.3 m from the second road's end.
            RoadHit h = n.FindNearest(M(5, 0.2), 1500);
            Assert.That(h.Kind, Is.EqualTo(RoadHitKind.End));
            Assert.That(h.Point, Is.EqualTo(M(5, 1.5)));
            // With a smaller range the end is out of range and the line wins.
            Assert.That(n.FindNearest(M(5, 0.2), 1000).Kind, Is.EqualTo(RoadHitKind.Line));
        }

        [Test]
        public void FindNearest_AtJunction_ReportsNodeAfterSplit()
        {
            var n = new RoadNetwork();
            RoadSegment s = Straight(n, 0, 0, 10, 0);
            SplitResult r = n.SplitSegmentAt(s.Id, M(5, 0));
            RoadHit h = n.FindNearest(M(5, 0.1), 500);
            Assert.That(h.Kind, Is.EqualTo(RoadHitKind.End));
            Assert.That(h.NodeId, Is.EqualTo(r.NodeId));
        }

        [Test]
        public void FindNearest_PicksClosestOfTwoLines()
        {
            var n = new RoadNetwork();
            Straight(n, 0, 0, 10, 0);
            RoadSegment b = Straight(n, 0, 3, 10, 3);
            RoadHit h = n.FindNearest(M(5, 2), 5000);
            Assert.That(h.Kind, Is.EqualTo(RoadHitKind.Line));
            Assert.That(h.SegmentId, Is.EqualTo(b.Id));
        }
    }
}
