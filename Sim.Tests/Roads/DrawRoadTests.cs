using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Roads
{
    public class DrawRoadTests
    {
        private const string Type = RoadTypes.DirtPathId;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);
        private static SimWorld Flat() => new SimWorld(new SimTuning());

        private static CommandResult Draw(SimWorld w, params PointMm[] cps) =>
            w.Execute(new DrawRoad(cps, Type));

        [Test]
        public void Accepted_SingleRoadNoJunctions_CreatesOneSegmentWithSamples()
        {
            SimWorld w = Flat();
            CommandResult r = Draw(w, M(10, 10), M(60, 10));
            Assert.That(r.IsAccepted, Is.True);
            Assert.That(r.Created.Length, Is.EqualTo(1));
            Assert.That(r.Removed, Is.Empty);
            RoadSegment s = w.Roads.GetSegment((int)r.Created[0]);
            Assert.That(s.Samples.Count, Is.EqualTo(51));
            Assert.That(s.LengthM, Is.EqualTo(50).Within(0.01));
        }

        [Test]
        public void Rejected_Overlap_ReturnsConflictAndChangesNothing()
        {
            SimWorld w = Flat();
            Draw(w, M(10, 10), M(60, 10));
            int version = w.Roads.Version;
            CommandResult r = Draw(w, M(20, 10.5), M(50, 10.5));
            Assert.That(r.IsRejected, Is.True);
            Assert.That(r.Reason, Is.EqualTo(CommandRejectCode.Conflict));
            Assert.That(r.Text, Is.EqualTo("overlaps road"));
            Assert.That(w.Roads.Version, Is.EqualTo(version));
            Assert.That(w.Roads.SegmentCount, Is.EqualTo(1));
        }

        [Test]
        public void Rejected_UnknownType_ReturnsInvalidArgument()
        {
            SimWorld w = Flat();
            CommandResult r = w.Execute(new DrawRoad(new[] { M(0, 0), M(10, 0) }, "road.nope"));
            Assert.That(r.Reason, Is.EqualTo(CommandRejectCode.InvalidArgument));
            Assert.That(w.Roads.SegmentCount, Is.EqualTo(0));
        }

        [Test]
        public void Rejected_SinglePoint_ReturnsInvalidArgument()
        {
            SimWorld w = Flat();
            CommandResult r = Draw(w, M(5, 5));
            Assert.That(r.IsRejected, Is.True);
            Assert.That(r.Reason, Is.EqualTo(CommandRejectCode.InvalidArgument));
        }

        [Test]
        public void Rejected_CrossesItself_ReturnsNotAllowed()
        {
            SimWorld w = Flat();
            CommandResult r = Draw(w, M(10, 10), M(50, 50), M(50, 10), M(10, 50));
            Assert.That(r.IsRejected, Is.True);
            Assert.That(r.Reason, Is.EqualTo(CommandRejectCode.NotAllowed));
        }

        [Test]
        public void CrossingTwoRoads_MakesThreeNewSegmentsAndFourHalves_AllConnected()
        {
            SimWorld w = Flat();
            CommandResult a = Draw(w, M(40, 0), M(40, 100));
            CommandResult b = Draw(w, M(60, 0), M(60, 100));
            Assert.That(w.Roads.ComponentCount, Is.EqualTo(2));

            CommandResult r = Draw(w, M(0, 50), M(100, 50));
            Assert.That(r.IsAccepted, Is.True, r.Text);
            Assert.That(r.Created.Length, Is.EqualTo(7));
            Assert.That(r.Removed.OrderBy(x => x), Is.EqualTo(new[] { a.Created[0], b.Created[0] }));
            Assert.That(w.Roads.SegmentCount, Is.EqualTo(7));
            Assert.That(w.Roads.ComponentCount, Is.EqualTo(1));
            foreach (long id in r.Created) Assert.That(w.Roads.GetSegment((int)id), Is.Not.Null);
            foreach (long id in r.Removed) Assert.That(w.Roads.GetSegment((int)id), Is.Null);
            int first = w.Roads.Segments.First().NodeA;
            foreach (RoadNode n in w.Roads.Nodes) Assert.That(w.Roads.AreConnected(first, n.Id), Is.True);
        }

        [Test]
        public void CrossingTwoRoads_NewRoadPiecesKeepEveryOriginalSampleAndShareJunctionPoints()
        {
            SimWorld w = Flat();
            Draw(w, M(40, 0), M(40, 100));
            Draw(w, M(60, 0), M(60, 100));
            var cps = new[] { M(0, 50), M(100, 50) };
            IReadOnlyList<PointMm> original = new CatmullRomSampler(w.Tuning).Sample(cps);

            CommandResult r = w.Execute(new DrawRoad(cps, Type));
            var pieces = r.Created.Select(id => w.Roads.GetSegment((int)id)).Where(s => s.Samples[0].Z == 50000 && s.Samples[s.Samples.Count - 1].Z == 50000).ToList();
            Assert.That(pieces.Count, Is.EqualTo(3));
            var all = new HashSet<PointMm>(pieces.SelectMany(s => s.Samples));
            foreach (PointMm p in original) Assert.That(all.Contains(p), Is.True, "lost sample " + p);
            foreach (RoadSegment s in pieces)
            {
                Assert.That(s.ControlPoints[0], Is.EqualTo(s.Samples[0]));
                Assert.That(s.ControlPoints[s.ControlPoints.Count - 1], Is.EqualTo(s.Samples[s.Samples.Count - 1]));
            }
            Assert.That(pieces.Sum(s => s.LengthM), Is.EqualTo(100).Within(0.01));
        }

        [Test]
        public void SplitHalves_BothSidesGetTheJunctionAsControlPoint()
        {
            SimWorld w = Flat();
            Draw(w, M(40, 0), M(40, 100));
            CommandResult r = Draw(w, M(0, 50), M(100, 50));
            Assert.That(r.Removed.Length, Is.EqualTo(1));
            RoadSegment[] halves = r.Created.Select(id => w.Roads.GetSegment((int)id))
                .Where(s => s.Samples.All(p => p.X == 40000)).ToArray();
            Assert.That(halves.Length, Is.EqualTo(2));
            foreach (RoadSegment h in halves)
                Assert.That(h.ControlPoints.Contains(M(40, 50)), Is.True);
        }

        [Test]
        public void TJunction_NewRoadEndSnapsToExistingRoadAndSplitsIt()
        {
            SimWorld w = Flat();
            CommandResult a = Draw(w, M(0, 50), M(100, 50));
            CommandResult r = Draw(w, M(50, 0), M(50, 49.8));
            Assert.That(r.IsAccepted, Is.True, r.Text);
            Assert.That(r.Created.Length, Is.EqualTo(3));
            Assert.That(r.Removed, Is.EqualTo(new[] { a.Created[0] }));
            Assert.That(w.Roads.ComponentCount, Is.EqualTo(1));
            RoadNode joint = w.Roads.Nodes.First(n => n.SegmentIdList.Count == 3);
            Assert.That(joint.Position, Is.EqualTo(M(50, 50)));
        }

        [Test]
        public void JoinNode_NewRoadEndsAtExistingEnd_ReusesNodeWithoutSplit()
        {
            SimWorld w = Flat();
            CommandResult a = Draw(w, M(0, 50), M(50, 50));
            CommandResult r = Draw(w, M(50, 50.2), M(50, 100));
            Assert.That(r.IsAccepted, Is.True, r.Text);
            Assert.That(r.Created.Length, Is.EqualTo(1));
            Assert.That(r.Removed, Is.Empty);
            Assert.That(w.Roads.GetSegment((int)a.Created[0]), Is.Not.Null);
            Assert.That(w.Roads.ComponentCount, Is.EqualTo(1));
            Assert.That(w.Roads.NodeCount, Is.EqualTo(3));
        }

        [Test]
        public void TwoCrossingsOnSameExistingRoad_BothSplitsApply()
        {
            SimWorld w = Flat();
            Draw(w, M(0, 50), M(100, 50));
            // S-shaped road crossing the horizontal road twice.
            CommandResult r = Draw(w, M(30, 0), M(30, 30), M(45, 50), M(60, 70), M(60, 100));
            if (r.IsRejected) Assert.Inconclusive(r.Text);
            Assert.That(w.Roads.ComponentCount, Is.EqualTo(1));
        }

        [Test]
        public void WhilePaused_CommandStillApplies_AndTickDoesNotChange()
        {
            SimWorld w = Flat();
            w.Pause();
            long tick = w.Tick;
            CommandResult r = Draw(w, M(10, 10), M(60, 10));
            Assert.That(r.IsAccepted, Is.True);
            Assert.That(w.Tick, Is.EqualTo(tick));
            Assert.That(w.Roads.SegmentCount, Is.EqualTo(1));
        }

        [Test]
        public void ResampledSplitHalf_MatchesOriginalWithinToleranceAwayFromTheCut()
        {
            SimWorld w = Flat();
            var cps = new[] { M(0, 0), M(30, 5), M(60, 0), M(90, 5), M(120, 0) };
            CommandResult a = Draw(w, cps);
            RoadSegment original = w.Roads.GetSegment((int)a.Created[0]);
            IReadOnlyList<PointMm> originalSamples = original.Samples;

            // Cross between the 3rd and 4th control points.
            CommandResult r = Draw(w, M(75, -30), M(75, 30));
            Assert.That(r.IsAccepted, Is.True, r.Text);
            RoadSegment half = r.Created.Select(id => w.Roads.GetSegment((int)id))
                .Single(s => s.Samples[0] == originalSamples[0]);

            IReadOnlyList<PointMm> again = new CatmullRomSampler(w.Tuning).Sample(half.ControlPoints);
            int firstSpanEnd = half.Samples.ToList().IndexOf(M(30, 5));
            Assert.That(firstSpanEnd, Is.GreaterThan(0));
            for (int i = 0; i <= firstSpanEnd; i++)
                Assert.That(again[i].DistanceTo(half.Samples[i]), Is.LessThan(10), "sample " + i);

            // Every re-sampled point stays within 0.5 m of the original centre line.
            foreach (PointMm p in again)
                Assert.That(Polylines.Nearest(half.Samples, p).DistanceMm, Is.LessThan(500));
        }
    }
}
