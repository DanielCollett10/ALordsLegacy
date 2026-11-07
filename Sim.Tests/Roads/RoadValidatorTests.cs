using System;
using System.Collections.Generic;
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
    public class RoadValidatorTests
    {
        private const string Type = RoadTypes.DirtPathId;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld RampWorld() => SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());

        // Height 0.1 per metre up to x=100, then 0.4 per metre (too steep for 15%).
        private static SimWorld CliffWorld()
        {
            const int nodes = 101;
            var h = new float[nodes * nodes];
            for (int iz = 0; iz < nodes; iz++)
                for (int ix = 0; ix < nodes; ix++)
                {
                    float x = ix * 2f;
                    h[iz * nodes + ix] = x <= 100 ? 0.1f * x : 10f + 0.4f * (x - 100);
                }
            var map = new MapData("cliff", new HeightGrid(0f, 0f, 2f, nodes, nodes, h), new MapTree[0]);
            return SimWorld.FromMapData(map, new SimTuning());
        }

        private static RoadSegment AddExisting(SimWorld w, double x0, double z0, double x1, double z1)
        {
            var cps = new[] { M(x0, z0), M(x1, z1) };
            var samples = new CatmullRomSampler(w.Tuning).Sample(cps);
            return w.Roads.AddSegment(Type, CurveKind.CatmullRomCentripetal, cps, samples);
        }

        private static RoadPreview V(SimWorld w, int ignore, params PointMm[] cps) =>
            RoadValidator.Validate(w, cps, Type, ignore);

        private static RoadPreview V(SimWorld w, params PointMm[] cps) => RoadValidator.Validate(w, cps, Type);

        [Test]
        public void Valid_StraightRoadOnRamp_HasLengthGradeAndNoJunctions()
        {
            RoadPreview p = V(RampWorld(), M(50, 50), M(100, 50));
            Assert.That(p.Reason, Is.EqualTo(RoadRefusal.Valid));
            Assert.That(p.IsValid, Is.True);
            Assert.That(p.ReasonText, Is.Empty);
            Assert.That(p.LengthM, Is.EqualTo(50).Within(0.01));
            Assert.That(p.MaxGrade, Is.EqualTo(0.1).Within(0.005));
            Assert.That(p.Junctions, Is.Empty);
            Assert.That(p.InvalidRanges, Is.Empty);
            Assert.That(p.Samples.Count, Is.EqualTo(51));
        }

        [Test]
        public void OutsideMap_EndPastEdge_IsRefusedOutsideTheMap()
        {
            RoadPreview p = V(RampWorld(), M(190, 50), M(210, 50));
            Assert.That(p.Reason, Is.EqualTo(RoadRefusal.OutsideMap));
            Assert.That(p.ReasonText, Is.EqualTo("outside the map"));
        }

        [Test]
        public void OutsideMap_BeatsTooSteep()
        {
            // starts outside AND climbs the cliff: outside is reported first
            RoadPreview p = V(CliffWorld(), M(-10, 50), M(150, 50));
            Assert.That(p.Reason, Is.EqualTo(RoadRefusal.OutsideMap));
        }

        [Test]
        public void TooSteep_ReportsOneRangeCoveringTheCliff()
        {
            RoadPreview p = V(CliffWorld(), M(50, 50), M(150, 50));
            Assert.That(p.Reason, Is.EqualTo(RoadRefusal.TooSteep));
            Assert.That(p.ReasonText, Is.EqualTo("too steep"));
            Assert.That(p.InvalidRanges.Count, Is.EqualTo(1));
            Assert.That(p.InvalidRanges[0].StartIndex, Is.InRange(47, 52));
            Assert.That(p.InvalidRanges[0].EndIndex, Is.EqualTo(p.Samples.Count - 1));
            Assert.That(p.MaxGrade, Is.GreaterThan(0.15));
        }

        [Test]
        public void TooSteep_TwoSeparateStretches_GiveTwoRanges()
        {
            // Down the cliff and back would be one range each way only if flat between; use two cliffs: x 100.. and a flat gap is
            // not available, so check a steep-flat-steep path by going up the cliff, along z at x=150, then back down.
            RoadPreview p = V(CliffWorld(), M(60, 50), M(150, 50), M(150, 80), M(60, 80));
            Assert.That(p.Reason, Is.EqualTo(RoadRefusal.TooSteep));
            Assert.That(p.InvalidRanges.Count, Is.EqualTo(2));
            Assert.That(p.InvalidRanges[0].EndIndex, Is.LessThan(p.InvalidRanges[1].StartIndex));
        }

        [Test]
        public void CrossesItself_FigureCross_IsRefused()
        {
            RoadPreview p = V(RampWorld(), M(50, 50), M(100, 100), M(100, 50), M(50, 100));
            Assert.That(p.Reason, Is.EqualTo(RoadRefusal.CrossesItself));
            Assert.That(p.ReasonText, Is.EqualTo("crosses itself"));
        }

        [Test]
        public void CrossesItself_EndTouchingStart_IsRefused()
        {
            RoadPreview p = V(RampWorld(), M(50, 50), M(80, 50), M(80, 80), M(50, 80), M(50, 50));
            Assert.That(p.Reason, Is.EqualTo(RoadRefusal.CrossesItself));
        }

        [Test]
        public void Overlap_ParallelOneAndHalfMetresForTwentyMetres_IsRefused()
        {
            SimWorld w = RampWorld();
            AddExisting(w, 40, 100, 140, 100);
            RoadPreview p = V(w, M(60, 101.5), M(80, 101.5));
            Assert.That(p.Reason, Is.EqualTo(RoadRefusal.OverlapsRoad));
            Assert.That(p.ReasonText, Is.EqualTo("overlaps road"));
            Assert.That(p.Junctions, Is.Empty);
        }

        [Test]
        public void Overlap_ParallelJustOverOneWidthAway_IsValid()
        {
            SimWorld w = RampWorld();
            AddExisting(w, 40, 100, 140, 100);
            Assert.That(V(w, M(60, 102.6), M(80, 102.6)).Reason, Is.EqualTo(RoadRefusal.Valid));
        }

        [Test]
        public void Overlap_CollinearOnTopOfExistingRoad_IsRefused()
        {
            SimWorld w = RampWorld();
            AddExisting(w, 40, 100, 140, 100);
            Assert.That(V(w, M(60, 100), M(90, 100)).Reason, Is.EqualTo(RoadRefusal.OverlapsRoad));
        }

        [Test]
        public void Tjunction_EndingOnAnotherRoadAt20Degrees_IsValidWithOneT()
        {
            SimWorld w = RampWorld();
            RoadSegment existing = AddExisting(w, 40, 100, 140, 100);
            double a = 20 * Math.PI / 180;
            RoadPreview p = V(w, M(100 - 40 * Math.Cos(a), 100 - 40 * Math.Sin(a)), M(100, 100));
            Assert.That(p.Reason, Is.EqualTo(RoadRefusal.Valid));
            Assert.That(p.Junctions.Count, Is.EqualTo(1));
            PlannedJunction j = p.Junctions[0];
            Assert.That(j.Kind, Is.EqualTo(JunctionKind.T));
            Assert.That(j.SegmentId, Is.EqualTo(existing.Id));
            Assert.That(j.NodeId, Is.EqualTo(-1));
            Assert.That(j.Point, Is.EqualTo(M(100, 100)));
            Assert.That(j.ExistingArcM, Is.EqualTo(60).Within(0.01));
            Assert.That(j.NewArcM, Is.EqualTo(p.LengthM).Within(0.001));
        }

        [Test]
        public void Tjunction_StartOnAnotherRoad_ReportsNewArcZero()
        {
            SimWorld w = RampWorld();
            RoadSegment existing = AddExisting(w, 40, 100, 140, 100);
            RoadPreview p = V(w, M(100, 100), M(100, 150));
            Assert.That(p.Reason, Is.EqualTo(RoadRefusal.Valid));
            Assert.That(p.Junctions.Count, Is.EqualTo(1));
            Assert.That(p.Junctions[0].Kind, Is.EqualTo(JunctionKind.T));
            Assert.That(p.Junctions[0].SegmentId, Is.EqualTo(existing.Id));
            Assert.That(p.Junctions[0].NewArcM, Is.EqualTo(0));
        }

        [Test]
        public void Tjunction_EndWithinHalfWidthButNotOnTheLine_SnapsToNearestPoint()
        {
            SimWorld w = RampWorld();
            AddExisting(w, 40, 100, 140, 100);
            RoadPreview p = V(w, M(100, 140), M(100, 101));
            Assert.That(p.Reason, Is.EqualTo(RoadRefusal.Valid));
            Assert.That(p.Junctions.Count, Is.EqualTo(1));
            Assert.That(p.Junctions[0].Kind, Is.EqualTo(JunctionKind.T));
            Assert.That(p.Junctions[0].Point, Is.EqualTo(M(100, 100)));
        }

        [Test]
        public void Tjunction_EndJustOutsideHalfWidth_MakesNoJunction()
        {
            SimWorld w = RampWorld();
            AddExisting(w, 40, 100, 140, 100);
            RoadPreview p = V(w, M(100, 140), M(100, 101.3));
            Assert.That(p.Junctions, Is.Empty);
        }

        [Test]
        public void Xjunction_CrossingAnotherRoadAt30Degrees_IsValidWithOneX()
        {
            SimWorld w = RampWorld();
            RoadSegment existing = AddExisting(w, 40, 100, 140, 100);
            double a = 30 * Math.PI / 180;
            RoadPreview p = V(w, M(100 - 30 * Math.Cos(a), 100 - 30 * Math.Sin(a)), M(100 + 30 * Math.Cos(a), 100 + 30 * Math.Sin(a)));
            Assert.That(p.Reason, Is.EqualTo(RoadRefusal.Valid));
            Assert.That(p.Junctions.Count, Is.EqualTo(1));
            PlannedJunction j = p.Junctions[0];
            Assert.That(j.Kind, Is.EqualTo(JunctionKind.X));
            Assert.That(j.SegmentId, Is.EqualTo(existing.Id));
            Assert.That(j.Point.DistanceTo(M(100, 100)), Is.LessThan(5));
            Assert.That(j.ExistingArcM, Is.EqualTo(60).Within(0.01));
            Assert.That(j.NewArcM, Is.EqualTo(30).Within(0.01));
        }

        [Test]
        public void Xjunction_CrossingTwoRoads_GivesTwoJunctionsInOrderAlongTheNewRoad()
        {
            SimWorld w = RampWorld();
            RoadSegment a = AddExisting(w, 60, 40, 60, 160);
            RoadSegment b = AddExisting(w, 90, 40, 90, 160);
            RoadPreview p = V(w, M(100, 100), M(40, 100));
            Assert.That(p.Reason, Is.EqualTo(RoadRefusal.Valid));
            Assert.That(p.Junctions.Count, Is.EqualTo(2));
            Assert.That(p.Junctions[0].SegmentId, Is.EqualTo(b.Id));
            Assert.That(p.Junctions[1].SegmentId, Is.EqualTo(a.Id));
        }

        [Test]
        public void JoinNode_EndExactlyOnExistingNode_JoinsWithoutSplit()
        {
            SimWorld w = RampWorld();
            RoadSegment existing = AddExisting(w, 40, 100, 140, 100);
            RoadPreview p = V(w, M(140, 100), M(140, 150));
            Assert.That(p.Reason, Is.EqualTo(RoadRefusal.Valid));
            Assert.That(p.Junctions.Count, Is.EqualTo(1));
            PlannedJunction j = p.Junctions[0];
            Assert.That(j.Kind, Is.EqualTo(JunctionKind.JoinNode));
            Assert.That(j.NodeId, Is.EqualTo(existing.NodeB));
            Assert.That(j.SegmentId, Is.EqualTo(existing.Id));
            Assert.That(j.ExistingArcM, Is.EqualTo(existing.LengthM).Within(0.001));
            Assert.That(j.NewArcM, Is.EqualTo(0));
        }

        [Test]
        public void JoinNode_NodeAtStartOfExistingRoad_UsesNodeAAndArcZero()
        {
            SimWorld w = RampWorld();
            RoadSegment existing = AddExisting(w, 40, 100, 140, 100);
            RoadPreview p = V(w, M(40, 150), M(40, 100));
            Assert.That(p.Junctions.Count, Is.EqualTo(1));
            Assert.That(p.Junctions[0].Kind, Is.EqualTo(JunctionKind.JoinNode));
            Assert.That(p.Junctions[0].NodeId, Is.EqualTo(existing.NodeA));
            Assert.That(p.Junctions[0].ExistingArcM, Is.EqualTo(0));
        }

        [Test]
        public void IgnoreSegment_RoadOverExistingSegment_IsValidWhenThatSegmentIsIgnored()
        {
            SimWorld w = RampWorld();
            RoadSegment existing = AddExisting(w, 40, 100, 140, 100);
            Assert.That(V(w, M(60, 101), M(100, 101)).Reason, Is.EqualTo(RoadRefusal.OverlapsRoad));
            RoadPreview p = V(w, existing.Id, M(60, 101), M(100, 101));
            Assert.That(p.Reason, Is.EqualTo(RoadRefusal.Valid));
            Assert.That(p.Junctions, Is.Empty);
        }

        [Test]
        public void IgnoreSegment_OtherSegmentsStillCount()
        {
            SimWorld w = RampWorld();
            RoadSegment a = AddExisting(w, 40, 100, 140, 100);
            AddExisting(w, 60, 40, 60, 160);
            RoadPreview p = V(w, a.Id, M(30, 100), M(90, 100));
            Assert.That(p.Junctions.Count, Is.EqualTo(1));
            Assert.That(p.Junctions[0].Kind, Is.EqualTo(JunctionKind.X));
        }

        [Test]
        public void Validate_DoesNotChangeTheWorld()
        {
            SimWorld w = RampWorld();
            AddExisting(w, 40, 100, 140, 100);
            int version = w.Roads.Version, segs = w.Roads.SegmentCount, nodes = w.Roads.NodeCount;
            long tick = w.Tick;
            V(w, M(100, 60), M(100, 140));
            V(w, M(100, 100), M(100, 140));
            Assert.That(w.Roads.Version, Is.EqualTo(version));
            Assert.That(w.Roads.SegmentCount, Is.EqualTo(segs));
            Assert.That(w.Roads.NodeCount, Is.EqualTo(nodes));
            Assert.That(w.Tick, Is.EqualTo(tick));
            Assert.That(w.CommandLog.Count, Is.EqualTo(0));
        }

        [Test]
        public void Degenerate_NullEmptySingleAndRepeatedPoints_AreNotEnoughPoints()
        {
            SimWorld w = RampWorld();
            Assert.That(RoadValidator.Validate(w, null, Type).Reason, Is.EqualTo(RoadRefusal.NotEnoughPoints));
            Assert.That(V(w).Reason, Is.EqualTo(RoadRefusal.NotEnoughPoints));
            Assert.That(V(w, M(50, 50)).Reason, Is.EqualTo(RoadRefusal.NotEnoughPoints));
            RoadPreview same = V(w, M(50, 50), M(50, 50));
            Assert.That(same.Reason, Is.EqualTo(RoadRefusal.NotEnoughPoints));
            Assert.That(same.LengthM, Is.EqualTo(0));
        }

        [Test]
        public void Degenerate_UnknownOrNullType_IsUnknownType()
        {
            SimWorld w = RampWorld();
            Assert.That(RoadValidator.Validate(w, new[] { M(1, 1), M(9, 9) }, "road.nope").Reason, Is.EqualTo(RoadRefusal.UnknownType));
            Assert.That(RoadValidator.Validate(w, new[] { M(1, 1), M(9, 9) }, null).Reason, Is.EqualTo(RoadRefusal.UnknownType));
        }

        [Test]
        public void Degenerate_NullWorld_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => RoadValidator.Validate(null, new[] { M(1, 1), M(9, 9) }, Type));
        }

        [Test]
        public void Degenerate_NoMapWorld_FlatFieldContainsEverything_IsValid()
        {
            var w = new SimWorld(new SimTuning());
            RoadPreview p = V(w, M(10, 10), M(30, 10));
            Assert.That(p.Reason, Is.EqualTo(RoadRefusal.Valid));
            Assert.That(p.MaxGrade, Is.EqualTo(0));
        }

        [Test]
        public void Degenerate_ShortRoadUnderOneMetre_StillValidates()
        {
            RoadPreview p = V(RampWorld(), M(50, 50), M(50, 50.9));
            // RD-02: the last point replaces nothing when it is the only other point, so a 0.9 m road is allowed.
            Assert.That(p.Samples.Count, Is.EqualTo(2));
            Assert.That(p.Reason, Is.EqualTo(RoadRefusal.Valid));
        }
    }
}
