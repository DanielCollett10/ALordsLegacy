using System.Linq;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Roads
{
    /// <summary>RD-11 / BLD-10-B: a drawn road removes the trees in its corridor (half width + RoadTreeClearanceM).</summary>
    public class RoadTreeClearanceTests
    {
        private const string Type = RoadTypes.DirtPathId;
        // dirt path is 2.5 m wide, so the clearance distance is 1.25 + 0.5 = 1.75 m with the default tuning.
        private const double ClearM = 1.75;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);
        private static SimWorld Flat() => new SimWorld(new SimTuning());

        private static TreeId AddTree(SimWorld w, int id, double x, double z)
        {
            var t = new TreeId(id);
            w.Trees.Add(t, (float)x, (float)z, 10);
            return t;
        }

        [Test]
        public void TreeAtExactlyHalfWidthPlusClearance_IsRemoved()
        {
            SimWorld w = Flat();
            AddTree(w, 1, 30, 10 + ClearM);
            CommandResult r = w.Execute(new DrawRoad(new[] { M(10, 10), M(60, 10) }, Type));
            Assert.That(r.IsAccepted, Is.True);
            Assert.That(r.RemovedTreeIds, Is.EqualTo(new long[] { 1 }));
            Assert.That(w.Trees.TryGet(new TreeId(1), out _), Is.False);
        }

        [Test]
        public void TreeJustBeyondHalfWidthPlusClearance_IsKept()
        {
            SimWorld w = Flat();
            AddTree(w, 1, 30, 10 + ClearM + 0.01);
            CommandResult r = w.Execute(new DrawRoad(new[] { M(10, 10), M(60, 10) }, Type));
            Assert.That(r.IsAccepted, Is.True);
            Assert.That(r.RemovedTreeIds, Is.Empty);
            Assert.That(w.Trees.TryGet(new TreeId(1), out _), Is.True);
        }

        [Test]
        public void TreeOnOppositeSideAtBoundary_IsRemoved()
        {
            SimWorld w = Flat();
            AddTree(w, 1, 30, 10 - ClearM);
            CommandResult r = w.Execute(new DrawRoad(new[] { M(10, 10), M(60, 10) }, Type));
            Assert.That(r.RemovedTreeIds, Is.EqualTo(new long[] { 1 }));
        }

        [Test]
        public void TreeNearRoadEnd_UsesDistanceToEndCap_NotJustTheLine()
        {
            // Road ends at (10,10). A tree 1.5 m beyond the end along the line is inside clearance (1.5 < 1.75).
            // A tree 2 m beyond the end (outside the clearance) is kept.
            SimWorld w = Flat();
            AddTree(w, 1, 8.5, 10);
            AddTree(w, 2, 8, 10);
            CommandResult r = w.Execute(new DrawRoad(new[] { M(10, 10), M(60, 10) }, Type));
            Assert.That(r.RemovedTreeIds, Is.EqualTo(new long[] { 1 }));
            Assert.That(w.Trees.TryGet(new TreeId(2), out _), Is.True);
        }

        [Test]
        public void TreeAtZeroDistance_OnTheCentreLineAtAJunction_IsRemovedAndReportedOnce()
        {
            // The new road crosses the existing road at (30,10); the split puts a piece on each side. The tree sits
            // on both pieces' ends, so it must be reported once, not once per piece.
            SimWorld w = Flat();
            w.Execute(new DrawRoad(new[] { M(10, 10), M(60, 10) }, Type));
            AddTree(w, 7, 30, 10);
            CommandResult r = w.Execute(new DrawRoad(new[] { M(30, -20), M(30, 40) }, Type));
            Assert.That(r.IsAccepted, Is.True);
            Assert.That(r.RemovedTreeIds, Is.EqualTo(new long[] { 7 }));
            Assert.That(w.Trees.Count, Is.EqualTo(0));
        }

        [Test]
        public void RemovedTreeIds_AreReportedInAscendingOrder_AndSeparateFromSegmentIds()
        {
            SimWorld w = Flat();
            AddTree(w, 9, 20, 11);
            AddTree(w, 3, 40, 8.5);
            AddTree(w, 5, 50, 10.5);
            CommandResult r = w.Execute(new DrawRoad(new[] { M(10, 10), M(60, 10) }, Type));
            Assert.That(r.RemovedTreeIds, Is.EqualTo(new long[] { 3, 5, 9 }));
            Assert.That(r.Removed, Is.Empty);
            Assert.That(r.Created.Length, Is.EqualTo(1));
        }

        [Test]
        public void RejectedDraw_RemovesNoTrees()
        {
            SimWorld w = Flat();
            w.Execute(new DrawRoad(new[] { M(10, 10), M(60, 10) }, Type));
            AddTree(w, 4, 30, 10.5); // inside the corridor of the first road
            int version = w.Trees.Version;
            CommandResult r = w.Execute(new DrawRoad(new[] { M(20, 10.2), M(50, 10.2) }, Type));
            Assert.That(r.IsRejected, Is.True);
            Assert.That(r.RemovedTreeIds, Is.Empty);
            Assert.That(w.Trees.Version, Is.EqualTo(version));
            Assert.That(w.Trees.TryGet(new TreeId(4), out _), Is.True);
        }

        [Test]
        public void ClearingUsesTheTuningValue_NotAHardCodedDistance()
        {
            var tuning = new SimTuning { RoadTreeClearanceM = 3.0 };
            var w = new SimWorld(tuning);
            AddTree(w, 1, 30, 10 + 1.25 + 2.9); // within 1.25 + 3.0
            CommandResult r = w.Execute(new DrawRoad(new[] { M(10, 10), M(60, 10) }, Type));
            Assert.That(r.RemovedTreeIds, Is.EqualTo(new long[] { 1 }));
        }

        [Test]
        public void TreesNeverReturn_EvenAfterTheRoadIsDeleted()
        {
            SimWorld w = Flat();
            AddTree(w, 1, 30, 10);
            CommandResult draw = w.Execute(new DrawRoad(new[] { M(10, 10), M(60, 10) }, Type));
            w.Execute(new DeleteRoadSegment((int)draw.Created[0]));
            Assert.That(w.Trees.TryGet(new TreeId(1), out _), Is.False);
            Assert.That(w.Trees.Count, Is.EqualTo(0));
        }
    }
}
