using System.Linq;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class MapExitsTests
    {
        private static PointMm P(long x, long z) => new PointMm(x, z);
        private static SimWorld Ramp() => SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());

        private static StartLayout Layout(params PointMm[] road) => new StartLayout(
            P(100000, 100000), 0, P(0, 0), P(6000, 0), P(-6000, 0), road,
            new[] { new StartFamily("Ash", P(-8000, -4000), new[] { new StartCitizen("A", Sex.Male, -100) }) }, 0);

        private static void AddRoad(SimWorld w, PointMm a, PointMm b) =>
            w.Roads.AddSegment("road.dirt", CurveKind.CatmullRomCentripetal, new[] { a, b }, new[] { a, b });

        [Test]
        public void IsOnEdge_ExactlyAtTolerance_True()
        {
            var w = Ramp();
            Assert.IsTrue(MapExits.IsOnEdge(w, P(2000, 100000)));
            Assert.IsTrue(MapExits.IsOnEdge(w, P(198000, 100000)));
            Assert.IsTrue(MapExits.IsOnEdge(w, P(100000, 2000)));
            Assert.IsTrue(MapExits.IsOnEdge(w, P(100000, 198000)));
        }

        [Test]
        public void IsOnEdge_OneMmFurtherIn_False()
        {
            var w = Ramp();
            Assert.IsFalse(MapExits.IsOnEdge(w, P(2001, 100000)));
            Assert.IsFalse(MapExits.IsOnEdge(w, P(197999, 100000)));
            Assert.IsFalse(MapExits.IsOnEdge(w, P(100000, 2001)));
            Assert.IsFalse(MapExits.IsOnEdge(w, P(100000, 197999)));
        }

        [Test]
        public void All_ReturnsEndAndJunctionNodesOnEdge_NotInteriorOnes()
        {
            var w = Ramp();
            AddRoad(w, P(0, 100000), P(100000, 100000));   // end on edge, junction later
            AddRoad(w, P(100000, 100000), P(100000, 200000)); // other end on top edge
            AddRoad(w, P(100000, 100000), P(150000, 150000));  // interior end
            var ids = MapExits.All(w).Select(n => n.Position).ToList();
            CollectionAssert.AreEquivalent(new[] { P(0, 100000), P(100000, 200000) }, ids);
        }

        [Test]
        public void All_JunctionNodeOnEdge_IsIncluded()
        {
            var w = Ramp();
            AddRoad(w, P(0, 50000), P(0, 100000));
            AddRoad(w, P(0, 100000), P(0, 150000));
            AddRoad(w, P(0, 100000), P(50000, 100000));
            var junction = MapExits.All(w).Single(n => n.Position == P(0, 100000));
            Assert.AreEqual(3, junction.SegmentIds.Count);
            Assert.AreEqual(3, MapExits.All(w).Count);
        }

        [Test]
        public void Nearest_PicksShortestPathCost()
        {
            var w = Ramp();
            AddRoad(w, P(0, 100000), P(50000, 100000));
            AddRoad(w, P(150000, 100000), P(200000, 100000));
            w.Nav.Sync(w);
            Assert.AreEqual(P(200000, 100000), MapExits.Nearest(w, P(160000, 110000)));
            Assert.AreEqual(P(0, 100000), MapExits.Nearest(w, P(30000, 90000)));
        }

        [Test]
        public void Nearest_NoRoadExit_ReturnsNearestWalkableEdgePoint()
        {
            var w = Ramp();
            w.Nav.Sync(w);
            PointMm? p = MapExits.Nearest(w, P(100000, 20000));
            Assert.IsTrue(p.HasValue);
            Assert.IsTrue(MapExits.IsOnEdge(w, p.Value));
            Assert.AreEqual(0, p.Value.Z);
            Assert.That(p.Value.X, Is.InRange(98000L, 102000L));
        }

        [Test]
        public void ComputeEntry_TrunkEndOnEdge_ReturnsThatEnd()
        {
            var w = Ramp();
            Assert.AreEqual(P(200000, 130000), MapExits.ComputeEntry(w, new[] { P(100000, 130000), P(200000, 130000) }));
            Assert.AreEqual(P(0, 130000), MapExits.ComputeEntry(w, new[] { P(0, 130000), P(100000, 130000) }));
        }

        [Test]
        public void ComputeEntry_TrunkStopsShort_ReturnsWalkableEdgePointNearestCloserEnd()
        {
            var w = Ramp();
            PointMm? e = MapExits.ComputeEntry(w, new[] { P(60000, 130000), P(140000, 130000) });
            Assert.IsTrue(e.HasValue);
            Assert.IsTrue(MapExits.IsOnEdge(w, e.Value));
            Assert.AreEqual(0, e.Value.X);
            Assert.That(e.Value.Z, Is.InRange(128000L, 132000L));
        }

        [Test]
        public void ComputeEntry_EmptyTrunk_IsNull()
        {
            Assert.IsNull(MapExits.ComputeEntry(Ramp(), new PointMm[0]));
        }

        [Test]
        public void Apply_SetsEntryPoint()
        {
            var w = Ramp();
            StartLayoutApplier.Apply(w, Layout(P(100000, 130000), P(200000, 130000)));
            Assert.AreEqual(P(200000, 130000), w.EntryPoint);
        }

        [Test]
        public void EntryPoint_SaveRoundTrip_IdenticalAndSurvivesTrunkDeletion()
        {
            var map = SyntheticMap.CreateRamp();
            var w = SimWorld.FromMapData(map, new SimTuning());
            StartLayoutApplier.Apply(w, Layout(P(60000, 130000), P(140000, 130000)));
            PointMm? entry = w.EntryPoint;
            Assert.IsTrue(entry.HasValue);

            byte[] bytes = SimSave.Write(w, "n", 0, map.MapId, 0);
            SimWorld back = SimSave.Read(bytes, map);
            Assert.AreEqual(entry, back.EntryPoint);

            foreach (int id in back.Roads.Segments.Select(s => s.Id).ToList()) back.Roads.RemoveSegment(id);
            Assert.AreEqual(0, back.Roads.SegmentCount);
            Assert.AreEqual(entry, back.EntryPoint);
        }

        [Test]
        public void EntryPoint_NullRoundTrips()
        {
            var map = SyntheticMap.CreateRamp();
            var w = SimWorld.FromMapData(map, new SimTuning());
            SimWorld back = SimSave.Read(SimSave.Write(w, "n", 0, map.MapId, 0), map);
            Assert.IsNull(back.EntryPoint);
        }
    }
}
