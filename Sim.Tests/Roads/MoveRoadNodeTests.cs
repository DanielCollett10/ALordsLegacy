using System;
using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Roads
{
    /// <summary>RD-17: MoveRoadNode and RoadValidator.PreviewMove.</summary>
    public class MoveRoadNodeTests
    {
        private const string Type = RoadTypes.DirtPathId;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);
        private static SimWorld Flat() => new SimWorld(new SimTuning());

        // Flat up to x=100, then 0.4 per metre (too steep for 15%).
        private static SimWorld CliffWorld()
        {
            const int nodes = 101;
            var h = new float[nodes * nodes];
            for (int iz = 0; iz < nodes; iz++)
                for (int ix = 0; ix < nodes; ix++)
                {
                    float x = ix * 2f;
                    h[iz * nodes + ix] = x <= 100 ? 0f : 0.4f * (x - 100);
                }
            var map = new MapData("cliff", new HeightGrid(0f, 0f, 2f, nodes, nodes, h), new MapTree[0]);
            return SimWorld.FromMapData(map, new SimTuning());
        }

        private static RoadSegment Draw(SimWorld w, params PointMm[] cps)
        {
            CommandResult r = w.Execute(new DrawRoad(cps, Type));
            Assert.That(r.IsAccepted, Is.True, r.Text);
            return w.Roads.GetSegment((int)r.Created[0]);
        }

        private static int NodeAt(SimWorld w, PointMm p)
        {
            RoadNode n = w.Roads.Nodes.FirstOrDefault(x => x.Position == p);
            Assert.That(n, Is.Not.Null, "no node at " + p);
            return n.Id;
        }

        private static double AngleDeg(PointMm a0, PointMm a1, PointMm b0, PointMm b1)
        {
            double ax = a1.X - a0.X, az = a1.Z - a0.Z, bx = b1.X - b0.X, bz = b1.Z - b0.Z;
            double dot = (ax * bx + az * bz) / (Math.Sqrt(ax * ax + az * az) * Math.Sqrt(bx * bx + bz * bz));
            return Math.Acos(Math.Max(-1, Math.Min(1, dot))) * 180 / Math.PI;
        }

        [Test]
        public void EndNode_Moves_KeepsOtherControlPointsAndNodeId()
        {
            SimWorld w = Flat();
            RoadSegment old = Draw(w, M(20, 50), M(60, 50), M(100, 50));
            int node = NodeAt(w, M(100, 50));
            int version = w.Roads.Version;
            w.Trees.Add(new TreeId(1), 100f, 80.5f, 10);   // beside the new end
            w.Trees.Add(new TreeId(2), 150f, 150f, 10);    // far away

            CommandResult r = w.Execute(new MoveRoadNode(node, M(100, 80)));

            Assert.That(r.IsAccepted, Is.True, r.Text);
            Assert.That(r.Removed, Is.EqualTo(new long[] { old.Id }));
            Assert.That(r.Created.Length, Is.EqualTo(1));
            Assert.That(r.Created[0], Is.Not.EqualTo(old.Id));
            Assert.That(w.Roads.GetSegment(old.Id), Is.Null);
            RoadSegment now = w.Roads.GetSegment((int)r.Created[0]);
            Assert.That(now.ControlPoints, Is.EqualTo(new[] { M(20, 50), M(60, 50), M(100, 80) }));
            Assert.That(now.Samples[now.Samples.Count - 1], Is.EqualTo(M(100, 80)));
            Assert.That(now.NodeB, Is.EqualTo(node));
            Assert.That(w.Roads.GetNode(node).Position, Is.EqualTo(M(100, 80)));
            Assert.That(w.Roads.NodeCount, Is.EqualTo(2));
            Assert.That(w.Roads.Version, Is.GreaterThan(version));
            Assert.That(r.RemovedTreeIds, Is.EqualTo(new long[] { 1 }));
            Assert.That(w.Trees.TryGet(new TreeId(2), out _), Is.True);
        }

        [Test]
        public void StartNode_Moves_FirstControlPointReplaced()
        {
            SimWorld w = Flat();
            Draw(w, M(20, 50), M(60, 50), M(100, 50));
            int node = NodeAt(w, M(20, 50));
            CommandResult r = w.Execute(new MoveRoadNode(node, M(20, 20)));
            Assert.That(r.IsAccepted, Is.True, r.Text);
            RoadSegment now = w.Roads.GetSegment((int)r.Created[0]);
            Assert.That(now.ControlPoints, Is.EqualTo(new[] { M(20, 20), M(60, 50), M(100, 50) }));
        }

        [Test]
        public void Junction_ThreeSegments_AllReshapedToNewPosition()
        {
            SimWorld w = Flat();
            Draw(w, M(100, 100), M(150, 100));
            Draw(w, M(100, 100), M(100, 150));
            Draw(w, M(100, 100), M(50, 100));
            int node = NodeAt(w, M(100, 100));
            Assert.That(w.Roads.GetNode(node).SegmentIds.Count, Is.EqualTo(3));
            var oldIds = w.Roads.GetNode(node).SegmentIds.Select(i => (long)i).OrderBy(i => i).ToArray();

            CommandResult r = w.Execute(new MoveRoadNode(node, M(105, 108)));

            Assert.That(r.IsAccepted, Is.True, r.Text);
            Assert.That(r.Removed, Is.EqualTo(oldIds));
            Assert.That(r.Created.Length, Is.EqualTo(3));
            Assert.That(w.Roads.SegmentCount, Is.EqualTo(3));
            RoadNode n = w.Roads.GetNode(node);
            Assert.That(n.Position, Is.EqualTo(M(105, 108)));
            Assert.That(n.SegmentIds.Count, Is.EqualTo(3));
            foreach (int id in n.SegmentIds)
            {
                RoadSegment s = w.Roads.GetSegment(id);
                PointMm atNode = s.NodeA == node ? s.Samples[0] : s.Samples[s.Samples.Count - 1];
                Assert.That(atNode, Is.EqualTo(M(105, 108)));
            }
            Assert.That(w.Roads.NodeCount, Is.EqualTo(4));
        }

        [Test]
        public void Degree2SmoothJoint_BothCurvesLeaveAlongOneSharedDirection()
        {
            SimWorld w = Flat();
            Draw(w, M(20, 50), M(60, 50), M(100, 50));
            Draw(w, M(100, 50), M(140, 50), M(180, 50));
            int node = NodeAt(w, M(100, 50));
            Assert.That(w.Roads.GetNode(node).SegmentIds.Count, Is.EqualTo(2));

            CommandResult r = w.Execute(new MoveRoadNode(node, M(100, 75)));

            Assert.That(r.IsAccepted, Is.True, r.Text);
            RoadSegment a = w.Roads.GetNode(node).SegmentIds.Select(w.Roads.GetSegment).First(s => s.NodeB == node);
            RoadSegment b = w.Roads.GetNode(node).SegmentIds.Select(w.Roads.GetSegment).First(s => s.NodeA == node);
            int na = a.Samples.Count;
            // a arrives along its last step; b continues along its first step: the same direction.
            Assert.That(AngleDeg(a.Samples[na - 2], a.Samples[na - 1], b.Samples[0], b.Samples[1]), Is.LessThan(1));
            Assert.That(a.EndTangent.HasValue, Is.True);
            Assert.That(b.StartTangent.HasValue, Is.True);
        }

        [Test]
        public void Degree2_SegmentsStoreTangentsThatResampleIdentically()
        {
            SimWorld w = Flat();
            Draw(w, M(20, 50), M(60, 60), M(100, 50));
            Draw(w, M(100, 50), M(140, 40), M(180, 50));
            int node = NodeAt(w, M(100, 50));
            CommandResult r = w.Execute(new MoveRoadNode(node, M(110, 70)));
            Assert.That(r.IsAccepted, Is.True, r.Text);
            var sampler = new CatmullRomSampler(w.Tuning);
            foreach (long id in r.Created)
            {
                RoadSegment s = w.Roads.GetSegment((int)id);
                Assert.That(sampler.Sample(s.ControlPoints, s.StartTangent, s.EndTangent).SequenceEqual(s.Samples), Is.True);
            }
        }

        [Test]
        public void TooSteep_RejectsAndChangesNothing()
        {
            SimWorld w = CliffWorld();
            RoadSegment old = Draw(w, M(50, 50), M(90, 50));
            int node = NodeAt(w, M(90, 50));
            int version = w.Roads.Version;

            CommandResult r = w.Execute(new MoveRoadNode(node, M(150, 50)));

            Assert.That(r.IsRejected, Is.True);
            Assert.That(r.Reason, Is.EqualTo(CommandRejectCode.NotAllowed));
            Assert.That(r.Text, Is.EqualTo("too steep"));
            Assert.That(w.Roads.Version, Is.EqualTo(version));
            Assert.That(w.Roads.GetSegment(old.Id), Is.Not.Null);
            Assert.That(w.Roads.GetNode(node).Position, Is.EqualTo(M(90, 50)));
        }

        [Test]
        public void MoveAcrossAnotherRoad_IsRefusedWouldJoinARoad_AndChangesNothing()
        {
            SimWorld w = Flat();
            Draw(w, M(20, 100), M(180, 100));
            RoadSegment mine = Draw(w, M(100, 50), M(100, 70));
            int node = NodeAt(w, M(100, 70));
            int version = w.Roads.Version;

            CommandResult r = w.Execute(new MoveRoadNode(node, M(100, 150)));

            Assert.That(r.IsRejected, Is.True);
            Assert.That(r.Text, Is.EqualTo("would join a road"));
            Assert.That(r.Reason, Is.EqualTo(CommandRejectCode.Conflict));
            Assert.That(w.Roads.Version, Is.EqualTo(version));
            Assert.That(w.Roads.GetSegment(mine.Id), Is.Not.Null);
        }

        [Test]
        public void MoveOntoAnotherRoad_IsRefusedWouldJoinARoad()
        {
            SimWorld w = Flat();
            Draw(w, M(20, 100), M(180, 100));
            Draw(w, M(100, 50), M(100, 70));
            int node = NodeAt(w, M(100, 70));
            CommandResult r = w.Execute(new MoveRoadNode(node, M(100, 100)));
            Assert.That(r.IsRejected, Is.True);
            Assert.That(r.Text, Is.EqualTo("would join a road"));
        }

        [Test]
        public void MoveOntoAnotherRoadsEndNode_IsRefusedWouldJoinARoad()
        {
            SimWorld w = Flat();
            Draw(w, M(20, 100), M(180, 100));
            Draw(w, M(100, 50), M(100, 70));
            int node = NodeAt(w, M(100, 70));
            CommandResult r = w.Execute(new MoveRoadNode(node, M(180, 100)));
            Assert.That(r.IsRejected, Is.True);
            Assert.That(r.Text, Is.EqualTo("would join a road"));
        }

        [Test]
        public void JunctionAttachedRoadsDoNotCountAsOtherRoads_FarEndAtAJunctionIsAllowed()
        {
            // The moved segment's far end sits on a node shared with another road: not a new junction.
            SimWorld w = Flat();
            Draw(w, M(20, 50), M(60, 50));
            Draw(w, M(60, 50), M(100, 50));     // continues from the same node
            Draw(w, M(100, 50), M(100, 90));    // a branch at the far node
            int node = NodeAt(w, M(20, 50));
            CommandResult r = w.Execute(new MoveRoadNode(node, M(20, 30)));
            Assert.That(r.IsAccepted, Is.True, r.Text);
        }

        [Test]
        public void CrossesBuildingArea_IsRefused()
        {
            SimWorld w = Flat();
            var square = AreaShape.Straight(new[] { M(80, 60), M(100, 60), M(100, 80), M(80, 80) });
            Assert.That(w.Execute(new DesignateArea(square, AreaTypes.TestId)).IsAccepted, Is.True);
            RoadSegment old = Draw(w, M(20, 50), M(60, 50));
            int node = NodeAt(w, M(60, 50));
            int version = w.Roads.Version;

            CommandResult r = w.Execute(new MoveRoadNode(node, M(120, 70)));

            Assert.That(r.IsRejected, Is.True);
            Assert.That(r.Text, Does.StartWith("crosses "));
            Assert.That(r.Reason, Is.EqualTo(CommandRejectCode.Conflict));
            Assert.That(w.Roads.Version, Is.EqualTo(version));
            Assert.That(w.Roads.GetSegment(old.Id), Is.Not.Null);
        }

        [Test]
        public void FirstFailingSegmentRejectsWholeMove_NoSegmentChanges()
        {
            // Junction with two segments; the move makes the second one too steep.
            SimWorld w = CliffWorld();
            RoadSegment a = Draw(w, M(60, 50), M(40, 50));
            RoadSegment b = Draw(w, M(60, 50), M(60, 20));
            int node = NodeAt(w, M(60, 50));
            Assert.That(w.Execute(new MoveRoadNode(node, M(150, 50))).IsRejected, Is.True);
            Assert.That(w.Roads.GetSegment(a.Id), Is.Not.Null);
            Assert.That(w.Roads.GetSegment(b.Id), Is.Not.Null);
            Assert.That(w.Roads.GetNode(node).Position, Is.EqualTo(M(60, 50)));
        }

        [Test]
        public void UnknownNode_IsNotFound()
        {
            SimWorld w = Flat();
            CommandResult r = w.Execute(new MoveRoadNode(42, M(10, 10)));
            Assert.That(r.IsRejected, Is.True);
            Assert.That(r.Reason, Is.EqualTo(CommandRejectCode.NotFound));
        }

        [Test]
        public void WhilePaused_Works_DoesNotAdvanceTick_AndIsLogged()
        {
            SimWorld w = Flat();
            Draw(w, M(20, 50), M(60, 50));
            int node = NodeAt(w, M(60, 50));
            w.Pause();
            long tick = w.Tick;
            int logged = w.CommandLog.Count;

            CommandResult r = w.Execute(new MoveRoadNode(node, M(60, 70)));

            Assert.That(r.IsAccepted, Is.True, r.Text);
            Assert.That(w.Tick, Is.EqualTo(tick));
            Assert.That(w.CommandLog.Count, Is.EqualTo(logged + 1));
            Assert.That(w.CommandLog.Entries[w.CommandLog.Count - 1].Command, Is.InstanceOf<MoveRoadNode>());
        }

        [Test]
        public void NeighbourWithinOneMetre_IsDroppedByRd02()
        {
            SimWorld w = Flat();
            Draw(w, M(20, 50), M(60, 50), M(100, 50));
            int node = NodeAt(w, M(100, 50));
            CommandResult r = w.Execute(new MoveRoadNode(node, M(60.5, 50)));
            Assert.That(r.IsAccepted, Is.True, r.Text);
            RoadSegment now = w.Roads.GetSegment((int)r.Created[0]);
            Assert.That(now.ControlPoints, Is.EqualTo(new[] { M(20, 50), M(60.5, 50) }));
        }

        [Test]
        public void MoveOntoOtherEndOfSingleSegment_IsInvalidArgument()
        {
            SimWorld w = Flat();
            Draw(w, M(20, 50), M(60, 50));
            int node = NodeAt(w, M(60, 50));
            CommandResult r = w.Execute(new MoveRoadNode(node, M(20, 50)));
            Assert.That(r.IsRejected, Is.True);
            Assert.That(r.Reason, Is.EqualTo(CommandRejectCode.InvalidArgument));
        }

        [Test]
        public void MoveOutsideMap_IsRefused()
        {
            SimWorld w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());
            Draw(w, M(20, 150), M(60, 150));
            int node = NodeAt(w, M(60, 150));
            CommandResult r = w.Execute(new MoveRoadNode(node, M(260, 150)));
            Assert.That(r.IsRejected, Is.True);
            Assert.That(r.Text, Is.EqualTo("outside the map"));
        }

        [Test]
        public void PreviewMove_ReturnsPerSegmentPreviews_WithoutMutating()
        {
            SimWorld w = Flat();
            Draw(w, M(100, 100), M(150, 100));
            Draw(w, M(100, 100), M(100, 150));
            int node = NodeAt(w, M(100, 100));
            int version = w.Roads.Version;
            var oldIds = w.Roads.Segments.Select(s => s.Id).ToArray();

            NodeMovePreview p = RoadValidator.PreviewMove(w, node, M(110, 110));

            Assert.That(p.NodeFound, Is.True);
            Assert.That(p.IsValid, Is.True);
            Assert.That(p.Segments.Count, Is.EqualTo(2));
            foreach (SegmentMovePreview s in p.Segments)
                Assert.That(s.Preview.Samples.Any(x => x == M(110, 110)), Is.True);
            Assert.That(w.Roads.Version, Is.EqualTo(version));
            Assert.That(w.Roads.Segments.Select(s => s.Id).ToArray(), Is.EqualTo(oldIds));
            Assert.That(w.Roads.GetNode(node).Position, Is.EqualTo(M(100, 100)));
        }

        [Test]
        public void PreviewMove_UnknownNode_NotFound()
        {
            NodeMovePreview p = RoadValidator.PreviewMove(Flat(), 7, M(1, 1));
            Assert.That(p.NodeFound, Is.False);
            Assert.That(p.IsValid, Is.False);
            Assert.That(p.Segments, Is.Empty);
        }

        [Test]
        public void PreviewMove_WouldJoin_ReportsReasonOnTheSegment()
        {
            SimWorld w = Flat();
            Draw(w, M(20, 100), M(180, 100));
            Draw(w, M(100, 50), M(100, 70));
            int node = NodeAt(w, M(100, 70));
            NodeMovePreview p = RoadValidator.PreviewMove(w, node, M(100, 100));
            Assert.That(p.Reason, Is.EqualTo(RoadRefusal.WouldJoinRoad));
            Assert.That(p.Segments[0].Preview.Reason, Is.EqualTo(RoadRefusal.WouldJoinRoad));
            Assert.That(p.ReasonText, Is.EqualTo("would join a road"));
        }

        [Test]
        public void Validate_WithIgnoredSet_TreatsThoseSegmentsAsAbsent()
        {
            SimWorld w = Flat();
            RoadSegment a = Draw(w, M(20, 50), M(180, 50));
            RoadSegment b = Draw(w, M(20, 90), M(180, 90));
            var cps = new[] { M(20, 50), M(180, 50) };
            Assert.That(RoadValidator.Validate(w, cps, Type).IsValid, Is.False);
            RoadPreview p = RoadValidator.ValidateMove(w, cps, Type, new[] { a.Id }, new int[0], null, null);
            Assert.That(p.IsValid, Is.True, p.ReasonText);
            Assert.That(b.Id, Is.Not.EqualTo(a.Id));
        }
    }
}
