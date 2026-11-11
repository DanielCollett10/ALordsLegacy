using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class NavGridTests
    {
        private static readonly FootprintTypeDef Blocker = new FootprintTypeDef("footprint.test_block", 8000, 8000, true, false);

        private static PointMm M(double xm, double zm) => new PointMm((long)(xm * 1000), (long)(zm * 1000));

        // 40 m x 40 m. Flat for x < 20 m; from x = 20 m the ground rises 2 m per metre (about 63 degrees).
        private static SimWorld NewWorld()
        {
            const int n = 21;
            var h = new float[n * n];
            for (int iz = 0; iz < n; iz++)
                for (int ix = 0; ix < n; ix++)
                {
                    float x = ix * 2f;
                    h[iz * n + ix] = x > 20f ? (x - 20f) * 2f : 0f;
                }
            var map = new MapData("nav", new HeightGrid(0f, 0f, 2f, n, n, h), new MapTree[0]);
            return SimWorld.FromMapData(map, new SimTuning());
        }

        private static RoadSegment Road(SimWorld w, double x0, double z0, double x1, double z1) =>
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(x0, z0), M(x1, z1) }, new[] { M(x0, z0), M(x1, z1) });

        private static void AssertSameCells(NavGrid a, NavGrid b)
        {
            Assert.AreEqual(a.Width, b.Width);
            Assert.AreEqual(a.Depth, b.Depth);
            for (int iz = 0; iz < a.Depth; iz++)
                for (int ix = 0; ix < a.Width; ix++)
                {
                    Assert.AreEqual(a.IsBlocked(ix, iz), b.IsBlocked(ix, iz), "blocked " + ix + "," + iz);
                    Assert.AreEqual(a.SpeedMultiplier(ix, iz), b.SpeedMultiplier(ix, iz), 1e-9, "mult " + ix + "," + iz);
                }
        }

        [Test]
        public void Nav_IsNeverNull_AndCoversTerrainWith2mCells()
        {
            SimWorld w = NewWorld();
            Assert.IsNotNull(w.Nav);
            Assert.AreEqual(20, w.Nav.Width);
            Assert.AreEqual(20, w.Nav.Depth);
            Assert.AreEqual(2.0, w.Nav.CellSizeM);
        }

        [Test]
        public void WorldWithoutMap_HasGridWithNoCells_AndSyncDoesNotThrow()
        {
            var w = new SimWorld(new SimTuning());
            w.Step();
            Assert.AreEqual(0, w.Nav.Width);
            Assert.IsTrue(w.Nav.IsBlocked(0, 0));
        }

        [Test]
        public void SteepCell_IsBlocked_AndFlatCellIsNotWithMultiplierOne()
        {
            SimWorld w = NewWorld();
            w.Step();
            Assert.IsTrue(w.Nav.IsBlocked(15, 5));
            Assert.IsFalse(w.Nav.IsBlocked(2, 5));
            Assert.AreEqual(1.0, w.Nav.SpeedMultiplier(2, 5));
        }

        [Test]
        public void SlopeNeverChangesMultiplier_OnSteepRoadCellStillBlocked()
        {
            SimWorld w = NewWorld();
            Road(w, 24, 10, 36, 10);
            w.Step();
            Assert.IsTrue(w.Nav.IsBlocked(15, 4));
        }

        [Test]
        public void RoadCell_HasRoadMultiplier_AndCellOffRoadHasOne()
        {
            SimWorld w = NewWorld();
            Road(w, 4, 10, 16, 10);
            w.Step();
            Assert.AreEqual(1.25, w.Nav.SpeedMultiplier(5, 4), 1e-6);
            Assert.AreEqual(1.25, w.Nav.SpeedMultiplier(5, 5), 1e-6);
            Assert.AreEqual(1.0, w.Nav.SpeedMultiplier(5, 8), 1e-6);
            Assert.AreEqual(1.0, w.Nav.SpeedMultiplier(0, 4), 1e-6);
        }

        [Test]
        public void HighestMultiplierWins_WhereTwoRoadTypesOverlap()
        {
            var fast = new RoadTypeDef("road.fast", 2.5, 0.15, 2.0, "v");
            var catalog = new RoadTypeCatalog(new[] { RoadTypes.DirtPath, fast });
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 21, 21, new float[21 * 21]), new MapTree[0]);
            SimWorld w = SimWorld.FromMapData(map, new SimTuning(), catalog);
            Road(w, 4, 10, 16, 10);
            w.Roads.AddSegment("road.fast", CurveKind.CatmullRomCentripetal,
                new[] { M(8, 10), M(12, 10) }, new[] { M(8, 10), M(12, 10) });
            w.Step();
            Assert.AreEqual(2.0, w.Nav.SpeedMultiplier(5, 4), 1e-6);
            Assert.AreEqual(1.25, w.Nav.SpeedMultiplier(2, 4), 1e-6);
        }

        [Test]
        public void DeleteRoadSegment_ResetsItsCells()
        {
            SimWorld w = NewWorld();
            RoadSegment s = Road(w, 4, 10, 16, 10);
            w.Step();
            Assert.AreEqual(1.25, w.Nav.SpeedMultiplier(5, 4), 1e-6);
            int before = w.Nav.Version;
            Assert.IsTrue(w.Execute(new DeleteRoadSegment(s.Id)).IsAccepted);
            Assert.AreEqual(1.0, w.Nav.SpeedMultiplier(5, 4), 1e-6);
            Assert.Greater(w.Nav.Version, before);
        }

        [Test]
        public void MoveRoadNode_UpdatesOldAndNewCells()
        {
            SimWorld w = NewWorld();
            RoadSegment s = Road(w, 4, 10, 16, 10);
            w.Step();
            Assert.AreEqual(1.25, w.Nav.SpeedMultiplier(5, 4), 1e-6);
            Assert.AreEqual(1.0, w.Nav.SpeedMultiplier(5, 12), 1e-6);
            // Move the whole road 12 m south-to-north is not possible with one node, so move the far node.
            CommandResult r = w.Execute(new MoveRoadNode(s.NodeB, M(16, 30)));
            Assert.IsTrue(r.IsAccepted, r.ToString());
            // The old corridor near the far end is clear; the new far end is on the road.
            Assert.AreEqual(1.0, w.Nav.SpeedMultiplier(7, 4), 1e-6);
            Assert.AreEqual(1.25, w.Nav.SpeedMultiplier(7, 14), 1e-6);
            var fresh = NewWorld();
            foreach (RoadSegment seg in w.Roads.Segments)
                fresh.Roads.AddSegment(seg.TypeId, seg.Curve, seg.ControlPoints, seg.Samples);
            fresh.Step();
            AssertSameCells(fresh.Nav, w.Nav);
        }

        [Test]
        public void BlockingFootprint_BlocksCentres_AndRemovingItUnblocks()
        {
            SimWorld w = NewWorld();
            w.Footprints.Add(1, Blocker, M(10, 10), 0);
            w.Step();
            Assert.IsTrue(w.Nav.IsBlocked(5, 5));
            Assert.IsFalse(w.Nav.IsBlocked(9, 5));
            Assert.IsTrue(w.Footprints.Remove(1));
            w.Step();
            Assert.IsFalse(w.Nav.IsBlocked(5, 5));
        }

        [Test]
        public void WalkableFootprint_DoesNotBlock()
        {
            SimWorld w = NewWorld();
            w.Footprints.Add(1, FootprintTypeDef.Camp, M(10, 10), 0);
            w.Step();
            Assert.IsFalse(w.Nav.IsBlocked(5, 5));
        }

        [Test]
        public void ReplacedFootprintWithSameId_IsPickedUp()
        {
            SimWorld w = NewWorld();
            w.Footprints.Add(1, Blocker, M(10, 10), 0);
            w.Step();
            w.Footprints.Remove(1);
            w.Footprints.Add(1, Blocker, M(30, 30), 0);
            w.Step();
            Assert.IsFalse(w.Nav.IsBlocked(5, 5));
            Assert.IsTrue(w.Nav.IsBlocked(15, 15) || w.Nav.IsBlocked(14, 14));
        }

        [Test]
        public void Version_BumpsOnChange_AndNotWhenNothingChanged()
        {
            SimWorld w = NewWorld();
            w.Step();
            int v = w.Nav.Version;
            w.Step(3);
            Assert.AreEqual(v, w.Nav.Version);
            Road(w, 4, 10, 16, 10);
            w.Step();
            Assert.AreEqual(v + 1, w.Nav.Version);
        }

        [Test]
        public void Execute_LeavesNavCurrent_WithoutStepping()
        {
            SimWorld w = NewWorld();
            w.Step();
            var cmd = new DrawRoad(new[] { M(4, 10), M(16, 10) }, RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal);
            CommandResult r = w.Execute(cmd);
            Assert.IsTrue(r.IsAccepted, r.ToString());
            Assert.AreEqual(1.25, w.Nav.SpeedMultiplier(5, 4), 1e-6);
        }

        [Test]
        public void DirtyRebuild_EqualsFullRebuild_AfterManyEdits()
        {
            SimWorld w = NewWorld();
            w.Step();
            RoadSegment a = Road(w, 4, 10, 16, 10);
            w.Footprints.Add(1, Blocker, M(10, 24), 30);
            w.Step();
            RoadSegment b = Road(w, 10, 4, 10, 36);
            w.Step();
            w.Roads.RemoveSegment(a.Id);
            w.Footprints.Add(2, Blocker, M(30, 8), 0);
            w.Step();
            w.Footprints.Remove(1);
            w.Step();

            SimWorld fresh = NewWorld();
            foreach (RoadSegment seg in w.Roads.Segments)
                fresh.Roads.AddSegment(seg.TypeId, seg.Curve, seg.ControlPoints, seg.Samples);
            foreach (Footprint f in w.Footprints.All)
                fresh.Footprints.Add(f.Id, f.Type, f.Centre, f.RotationDegrees);
            fresh.Step();
            AssertSameCells(fresh.Nav, w.Nav);
            Assert.IsNotNull(b);
        }

        [Test]
        public void NavSyncSystem_IsFirstInDefaultSystems()
        {
            Assert.IsInstanceOf<NavSyncSystem>(SimSystems.CreateDefault()[0]);
        }
    }
}
