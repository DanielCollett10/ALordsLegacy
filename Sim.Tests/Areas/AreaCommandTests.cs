using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Areas
{
    /// <summary>DesignateArea, DeleteArea and RD-13 (roads refuse building areas).</summary>
    public class AreaCommandTests
    {
        private const string T = AreaTypes.TestId;
        private const string W = AreaTypes.TestWorkId;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);
        private static SimWorld Ramp() => SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());

        private static AreaShape Square(double x, double z, double s) =>
            AreaShape.Straight(new[] { M(x, z), M(x + s, z), M(x + s, z + s), M(x, z + s) });

        private static SimWorld WithTrees()
        {
            SimWorld w = Ramp();
            w.Trees.Add(new TreeId(9001), 60f, 60f, 5);   // inside the square 50..70
            w.Trees.Add(new TreeId(9002), 65f, 55f, 5);   // inside
            w.Trees.Add(new TreeId(9003), 90f, 90f, 5);   // outside
            return w;
        }

        private static CommandResult Road(SimWorld w, double x0, double z0, double x1, double z1) =>
            w.Execute(new DrawRoad(new[] { M(x0, z0), M(x1, z1) }, RoadTypes.DirtPathId));

        [Test]
        public void Designate_Valid_AddsAreaAndReportsId()
        {
            SimWorld w = Ramp();
            CommandResult r = w.Execute(new DesignateArea(Square(50, 50, 20), T));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Assert.AreEqual(1, w.Areas.Count);
            Assert.AreEqual(new long[] { 1 }, r.Created);
            Assert.IsNotNull(w.Areas.Get((int)r.Created[0]));
        }

        [Test]
        public void Designate_BuildingArea_RemovesTreesInsideOutline()
        {
            SimWorld w = WithTrees();
            CommandResult r = w.Execute(new DesignateArea(Square(50, 50, 20), T));
            Assert.AreEqual(new long[] { 9001, 9002 }, r.RemovedTreeIds);
            Assert.IsNull(FindTree(w, 9001));
            Assert.IsNotNull(FindTree(w, 9003));
        }

        [Test]
        public void Designate_WorkArea_DoesNotRemoveTrees()
        {
            SimWorld w = WithTrees();
            CommandResult r = w.Execute(new DesignateArea(Square(50, 50, 20), W));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Assert.IsEmpty(r.RemovedTreeIds);
            Assert.IsNotNull(FindTree(w, 9001));
        }

        [Test]
        public void Designate_Invalid_RejectedAndChangesNothing()
        {
            SimWorld w = WithTrees();
            var tiny = new CommandResult[] { w.Execute(new DesignateArea(Square(50, 50, 2), T)) };
            Assert.IsTrue(tiny[0].IsRejected);
            Assert.AreEqual("too small", tiny[0].Text);
            Assert.AreEqual(0, w.Areas.Count);
            Assert.IsNotNull(FindTree(w, 9001));
        }

        [Test]
        public void Designate_UnknownType_IsInvalidArgument()
        {
            CommandResult r = Ramp().Execute(new DesignateArea(Square(50, 50, 20), "area.nope"));
            Assert.AreEqual(CommandRejectCode.InvalidArgument, r.Reason);
        }

        [Test]
        public void Designate_OverlappingArea_IsConflict()
        {
            SimWorld w = Ramp();
            w.Execute(new DesignateArea(Square(50, 50, 20), T));
            CommandResult r = w.Execute(new DesignateArea(Square(60, 60, 20), T));
            Assert.AreEqual(CommandRejectCode.Conflict, r.Reason);
            Assert.AreEqual("overlaps Test area 1", r.Text);
        }

        [Test]
        public void Designate_WorksWhilePaused()
        {
            SimWorld w = Ramp();
            w.Pause();
            Assert.IsTrue(w.Execute(new DesignateArea(Square(50, 50, 20), T)).IsAccepted);
            Assert.IsTrue(w.Execute(new DeleteArea(1)).IsAccepted);
        }

        [Test]
        public void Delete_RemovesArea_AndTreesDoNotReturn()
        {
            SimWorld w = WithTrees();
            CommandResult d = w.Execute(new DesignateArea(Square(50, 50, 20), T));
            int id = (int)d.Created[0];
            CommandResult r = w.Execute(new DeleteArea(id));
            Assert.IsTrue(r.IsAccepted);
            Assert.AreEqual(new long[] { id }, r.Removed);
            Assert.IsNull(w.Areas.Get(id));
            Assert.IsNull(FindTree(w, 9001));
            Assert.IsNull(FindTree(w, 9002));
        }

        [Test]
        public void Delete_UnknownId_IsRejectedNotFound()
        {
            SimWorld w = Ramp();
            CommandResult r = w.Execute(new DeleteArea(42));
            Assert.AreEqual(CommandRejectCode.NotFound, r.Reason);
            r = w.Execute(new DeleteArea(-1));
            Assert.AreEqual(CommandRejectCode.NotFound, r.Reason);
        }

        [Test]
        public void Delete_TwiceSecondIsRejected()
        {
            SimWorld w = Ramp();
            w.Execute(new DesignateArea(Square(50, 50, 20), T));
            Assert.IsTrue(w.Execute(new DeleteArea(1)).IsAccepted);
            Assert.IsTrue(w.Execute(new DeleteArea(1)).IsRejected);
        }

        [Test]
        public void Road_CrossingBuildingArea_RefusedWithName()
        {
            SimWorld w = Ramp();
            w.Execute(new DesignateArea(Square(50, 50, 20), T));
            CommandResult r = Road(w, 30, 60, 90, 60);
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual("crosses Test area 1", r.Text);
            Assert.AreEqual(CommandRejectCode.Conflict, r.Reason);
            Assert.IsFalse(w.Roads.Segments.GetEnumerator().MoveNext());
        }

        [Test]
        public void Road_CrossingWorkArea_IsAllowed()
        {
            SimWorld w = Ramp();
            w.Execute(new DesignateArea(Square(50, 50, 20), W));
            Assert.IsTrue(Road(w, 30, 60, 90, 60).IsAccepted);
        }

        [Test]
        public void Road_ClearOfBuildingArea_IsAllowed()
        {
            SimWorld w = Ramp();
            w.Execute(new DesignateArea(Square(50, 50, 20), T));
            Assert.IsTrue(Road(w, 30, 90, 90, 90).IsAccepted);
        }

        [Test]
        public void Road_TouchingBuildingAreaEdgeOnly_IsAllowed()
        {
            SimWorld w = Ramp();
            w.Execute(new DesignateArea(Square(50, 50, 20), T));
            // Corridor spans z 71..73.5 (dirt path width 2.5 m); the area ends at z = 70.
            Assert.IsTrue(Road(w, 30, 72.25, 90, 72.25).IsAccepted);
        }

        [Test]
        public void Road_OverlapsRoadIsReportedBeforeCrossesArea()
        {
            SimWorld w = Ramp();
            Road(w, 20, 60, 100, 60);
            // The area sits clear of the existing road (z 66..86).
            Assert.IsTrue(w.Execute(new DesignateArea(Square(50, 66, 20), T)).IsAccepted);
            // The new road runs close beside the existing one (overlaps road) and also clips the area.
            CommandResult r = Road(w, 30, 62, 90, 72);
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual("overlaps road", r.Text);
        }

        [Test]
        public void Road_PreviewExposesCrossedAreaName()
        {
            SimWorld w = Ramp();
            w.Execute(new DesignateArea(Square(50, 50, 20), T));
            RoadPreview p = RoadValidator.Validate(w, new[] { M(30, 60), M(90, 60) }, RoadTypes.DirtPathId);
            Assert.AreEqual(RoadRefusal.CrossesArea, p.Reason);
            Assert.AreEqual("Test area 1", p.CrossedAreaName);
        }

        [Test]
        public void Road_NoAreas_ReasonUnchangedAndNameEmpty()
        {
            SimWorld w = Ramp();
            RoadPreview p = RoadValidator.Validate(w, new[] { M(30, 60), M(90, 60) }, RoadTypes.DirtPathId);
            Assert.IsTrue(p.IsValid);
            Assert.AreEqual(string.Empty, p.CrossedAreaName);
        }

        [Test]
        public void Road_AfterAreaDeleted_IsAllowedAgain()
        {
            SimWorld w = Ramp();
            w.Execute(new DesignateArea(Square(50, 50, 20), T));
            w.Execute(new DeleteArea(1));
            Assert.IsTrue(Road(w, 30, 60, 90, 60).IsAccepted);
        }

        private static TreeRecord FindTree(SimWorld w, int id)
        {
            foreach (TreeRecord t in w.Trees.All) if (t.Id.Value == id) return t;
            return null;
        }
    }
}
