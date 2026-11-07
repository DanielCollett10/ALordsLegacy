using System.Linq;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Roads
{
    /// <summary>RD-12: deleting one road segment; nodes left empty go, trees stay gone.</summary>
    public class DeleteRoadSegmentTests
    {
        private const string Type = RoadTypes.DirtPathId;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);
        private static SimWorld Flat() => new SimWorld(new SimTuning());

        private static CommandResult Draw(SimWorld w, params PointMm[] cps) =>
            w.Execute(new DrawRoad(cps, Type));

        // The segment whose centre line runs from (x0,0) to (x1,0) along the x axis.
        private static RoadSegment FindSegment(SimWorld w, double x0, double x1) =>
            w.Roads.Segments.Single(s => s.Samples[0] == M(x0, 0) && s.Samples[s.Samples.Count - 1] == M(x1, 0));

        [Test]
        public void Delete_OnlySegment_RemovesItAndItsNodes()
        {
            SimWorld w = Flat();
            CommandResult d = Draw(w, M(0, 0), M(100, 0));
            CommandResult r = w.Execute(new DeleteRoadSegment((int)d.Created[0]));
            Assert.That(r.IsAccepted, Is.True);
            Assert.That(r.Removed, Is.EqualTo(d.Created));
            Assert.That(r.Created, Is.Empty);
            Assert.That(w.Roads.SegmentCount, Is.EqualTo(0));
            Assert.That(w.Roads.NodeCount, Is.EqualTo(0));
            Assert.That(w.Roads.ComponentCount, Is.EqualTo(0));
        }

        [Test]
        public void Delete_StubOfAJunction_KeepsTheJunctionNodeAndDropsTheDeadEnd()
        {
            SimWorld w = Flat();
            Draw(w, M(0, 0), M(100, 0));
            CommandResult stub = Draw(w, M(50, -30), M(50, 0));
            Assert.That(stub.IsAccepted, Is.True);
            int nodesBefore = w.Roads.NodeCount;
            Assert.That(w.Execute(new DeleteRoadSegment((int)stub.Created[0])).IsAccepted, Is.True);
            // The stub's far end (50,-30) had only this segment, so it goes; the junction at (50,0) stays.
            Assert.That(w.Roads.NodeCount, Is.EqualTo(nodesBefore - 1));
            Assert.That(w.Roads.SegmentCount, Is.EqualTo(2));
            Assert.That(w.Roads.ComponentCount, Is.EqualTo(1));
        }

        [Test]
        public void Delete_MiddleSegment_BothSidesStayAndNetworkSplitsIntoTwoComponents()
        {
            // Main road 0..300 m split by two stubs at x=100 and x=200. Deleting the middle stretch (100..200)
            // leaves the west part (with its stub) and the east part (with its stub) as two networks.
            SimWorld w = Flat();
            Draw(w, M(0, 0), M(300, 0));
            Draw(w, M(100, -50), M(100, 0));
            Draw(w, M(200, -50), M(200, 0));
            RoadSegment middle = FindSegment(w, 100, 200);
            Assert.That(w.Roads.ComponentCount, Is.EqualTo(1));
            int segmentsBefore = w.Roads.SegmentCount;
            int nodesBefore = w.Roads.NodeCount;

            CommandResult r = w.Execute(new DeleteRoadSegment(middle.Id));

            Assert.That(r.IsAccepted, Is.True);
            Assert.That(w.Roads.SegmentCount, Is.EqualTo(segmentsBefore - 1));
            // Both junction nodes at x=100 and x=200 still have stubs, so no node is dropped.
            Assert.That(w.Roads.NodeCount, Is.EqualTo(nodesBefore));
            Assert.That(w.Roads.ComponentCount, Is.EqualTo(2));
            Assert.That(w.Roads.GetSegment(middle.Id), Is.Null);
            Assert.That(FindSegment(w, 0, 100), Is.Not.Null);
            Assert.That(FindSegment(w, 200, 300), Is.Not.Null);
        }

        [Test]
        public void Delete_UnknownId_IsRejectedNotFoundAndChangesNothing()
        {
            SimWorld w = Flat();
            Draw(w, M(0, 0), M(100, 0));
            int version = w.Roads.Version;
            int count = w.Roads.SegmentCount;
            CommandResult r = w.Execute(new DeleteRoadSegment(999));
            Assert.That(r.IsRejected, Is.True);
            Assert.That(r.Reason, Is.EqualTo(CommandRejectCode.NotFound));
            Assert.That(r.Removed, Is.Empty);
            Assert.That(w.Roads.Version, Is.EqualTo(version));
            Assert.That(w.Roads.SegmentCount, Is.EqualTo(count));
        }

        [Test]
        public void Delete_SameIdTwice_SecondIsRejected()
        {
            SimWorld w = Flat();
            CommandResult d = Draw(w, M(0, 0), M(100, 0));
            int id = (int)d.Created[0];
            Assert.That(w.Execute(new DeleteRoadSegment(id)).IsAccepted, Is.True);
            CommandResult again = w.Execute(new DeleteRoadSegment(id));
            Assert.That(again.IsRejected, Is.True);
            Assert.That(again.Reason, Is.EqualTo(CommandRejectCode.NotFound));
        }

        [Test]
        public void Delete_NegativeId_IsRejectedNotFound()
        {
            SimWorld w = Flat();
            CommandResult r = w.Execute(new DeleteRoadSegment(-1));
            Assert.That(r.IsRejected, Is.True);
            Assert.That(r.Reason, Is.EqualTo(CommandRejectCode.NotFound));
        }

        [Test]
        public void Delete_WhilePaused_AppliesAndTickDoesNotChange()
        {
            SimWorld w = Flat();
            CommandResult d = Draw(w, M(0, 0), M(100, 0));
            w.Step(5);
            w.Pause();
            long tick = w.Tick;
            CommandResult r = w.Execute(new DeleteRoadSegment((int)d.Created[0]));
            Assert.That(r.IsAccepted, Is.True);
            Assert.That(w.Roads.SegmentCount, Is.EqualTo(0));
            Assert.That(w.Tick, Is.EqualTo(tick));
        }

        [Test]
        public void Delete_IsRecordedInTheCommandLog_WithItsOutcome()
        {
            SimWorld w = Flat();
            CommandResult d = Draw(w, M(0, 0), M(100, 0));
            w.Execute(new DeleteRoadSegment(999));
            w.Execute(new DeleteRoadSegment((int)d.Created[0]));
            Assert.That(w.CommandLog.Count, Is.EqualTo(3));
            Assert.That(w.CommandLog.Entries[2].Command, Is.TypeOf<DeleteRoadSegment>());
        }
    }
}
