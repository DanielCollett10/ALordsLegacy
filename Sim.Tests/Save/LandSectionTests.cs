using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Save
{
    public class LandSectionTests
    {
        private static PointMm P(double xm, double zm) => new PointMm((long)(xm * 1000), (long)(zm * 1000));

        private static (SimWorld world, MapData map) Make()
        {
            var map = SyntheticMap.CreateRamp();
            return (SimWorld.FromMapData(map, new SimTuning()), map);
        }

        private static SimWorld RoundTrip(SimWorld w, MapData map)
        {
            var bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            var back = SimSave.Read(bytes, map);
            Assert.AreEqual(bytes, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash), "second write is identical");
            return back;
        }

        private static List<TreeRecord> Everything(TreeRegistry t) =>
            t.QueryRect(new Bounds2(-1e6f, -1e6f, 1e6f, 1e6f));

        private static void AssertSameTrees(TreeRegistry a, TreeRegistry b)
        {
            Assert.AreEqual(a.Count, b.Count);
            Assert.AreEqual(a.All.Select(t => t.Id.Value), b.All.Select(t => t.Id.Value));
            Assert.AreEqual(a.Felled.Select(t => t.Id.Value), b.Felled.Select(t => t.Id.Value));
            Assert.AreEqual(a.Stumps.Select(t => t.Id.Value), b.Stumps.Select(t => t.Id.Value));
            Assert.AreEqual(a.All.Select(t => t.ReservedBy), b.All.Select(t => t.ReservedBy));
        }

        [Test]
        public void Trees_RemovedFelledStumpAndReserved_AllRestored()
        {
            var (w, map) = Make();
            var ids = w.Trees.All.Select(t => t.Id).Take(5).ToList();
            Assert.GreaterOrEqual(ids.Count, 5);
            w.Trees.Remove(ids[0]);
            w.Trees.SetState(ids[1], TreeState.Felled);
            w.Trees.SetState(ids[2], TreeState.Felled);
            w.Trees.SetState(ids[2], TreeState.Stump);
            w.Trees.Reserve(ids[3], 77);
            w.Trees.SetState(ids[4], TreeState.Felled);
            w.Trees.Remove(ids[4]);

            var back = RoundTrip(w, map);
            AssertSameTrees(w.Trees, back.Trees);
            Assert.IsFalse(back.Trees.TryGet(ids[0], out _));
            Assert.AreEqual(TreeState.Felled, back.Trees.Get(ids[1]).State);
            Assert.AreEqual(TreeState.Stump, back.Trees.Get(ids[2]).State);
            Assert.AreEqual(77, back.Trees.Get(ids[3]).ReservedBy);
            Assert.IsFalse(back.Trees.TryGet(ids[4], out _));
            Assert.Throws<System.ArgumentException>(() => back.Trees.Add(ids[0], 0, 0, 1), "removed ids stay removed");
        }

        [Test]
        public void Trees_QueriesGiveSameResults()
        {
            var (w, map) = Make();
            var all = w.Trees.All.ToList();
            for (int i = 0; i < all.Count; i += 3) w.Trees.Remove(all[i].Id);
            for (int i = 1; i < all.Count; i += 7) w.Trees.SetState(all[i].Id, TreeState.Felled);
            for (int i = 2; i < all.Count; i += 11) w.Trees.Reserve(all[i].Id, i);
            var back = RoundTrip(w, map);

            var b = new Bounds2(-30, -30, 60, 60);
            Assert.AreEqual(w.Trees.QueryRect(b).Select(t => t.Id.Value), back.Trees.QueryRect(b).Select(t => t.Id.Value));
            Assert.AreEqual(w.Trees.QueryRadius(10, 10, 40).Select(t => t.Id.Value), back.Trees.QueryRadius(10, 10, 40).Select(t => t.Id.Value));
            Assert.AreEqual(Everything(w.Trees).Count, Everything(back.Trees).Count);
            var a1 = w.Trees.NearestUnreservedStanding(5, 5);
            var a2 = back.Trees.NearestUnreservedStanding(5, 5);
            Assert.AreEqual(a1?.Id.Value, a2?.Id.Value);
            var circle = TreeRegion.Circle(10, 10, 40);
            Assert.AreEqual(w.Trees.CountStandingIn(circle), back.Trees.CountStandingIn(circle));
        }

        [Test]
        public void Trees_UntouchedWorld_WritesNoChanges()
        {
            var (w, map) = Make();
            var back = RoundTrip(w, map);
            Assert.AreEqual(w.Trees.Count, back.Trees.Count);
            Assert.AreEqual(0, back.Trees.RemovedIdsSorted().Count);
        }

        [Test]
        public void Roads_RestoredWithSameIdsGeometryTangentsAndCounters()
        {
            var (w, map) = Make();
            var t1 = new RoadTangent(1, 2);
            var t2 = new RoadTangent(-3, 0.7);
            var s1 = w.Roads.AddSegment("road.dirt_path", CurveKind.CatmullRomCentripetal,
                new[] { P(0, 0), P(5, 3), P(10, 0) }, new[] { P(0, 0), P(2, 2), P(5, 3), P(8, 2), P(10, 0) }, t1, t2);
            var s2 = w.Roads.AddSegment("road.dirt_path", CurveKind.CatmullRomCentripetal,
                new[] { P(10, 0), P(20, 5) }, new[] { P(10, 0), P(20, 5) });
            var split = w.Roads.SplitSegmentAt(s2.Id, P(15, 2.5));
            var gone = w.Roads.AddSegment("road.dirt_path", CurveKind.CatmullRomCentripetal,
                new[] { P(50, 50), P(60, 50) }, new[] { P(50, 50), P(60, 50) });
            w.Roads.RemoveSegment(gone.Id);

            var back = RoundTrip(w, map);
            var a = w.Roads; var b = back.Roads;
            Assert.AreEqual(a.NodeCount, b.NodeCount);
            Assert.AreEqual(a.SegmentCount, b.SegmentCount);
            Assert.AreEqual(a.Nodes.Select(n => n.Id), b.Nodes.Select(n => n.Id));
            foreach (var n in a.Nodes)
            {
                var m = b.GetNode(n.Id);
                Assert.AreEqual(n.Position, m.Position);
                Assert.AreEqual(n.SegmentIds, m.SegmentIds);
            }
            foreach (var s in a.Segments)
            {
                var m = b.GetSegment(s.Id);
                Assert.AreEqual(s.NodeA, m.NodeA);
                Assert.AreEqual(s.NodeB, m.NodeB);
                Assert.AreEqual(s.TypeId, m.TypeId);
                Assert.AreEqual(s.Curve, m.Curve);
                Assert.AreEqual(s.ControlPoints, m.ControlPoints);
                Assert.AreEqual(s.Samples, m.Samples);
                Assert.AreEqual(s.LengthM, m.LengthM);
                Assert.AreEqual(s.StartTangent, m.StartTangent);
                Assert.AreEqual(s.EndTangent, m.EndTangent);
            }
            Assert.AreEqual(t1, b.GetSegment(s1.Id).StartTangent);
            Assert.AreEqual(t2, b.GetSegment(s1.Id).EndTangent);
            Assert.IsTrue(split.Split);
            Assert.AreEqual(a.GetSegment(split.FirstSegmentId).ControlPoints, b.GetSegment(split.FirstSegmentId).ControlPoints);
            Assert.AreEqual(a.ComponentCount, b.ComponentCount);
            Assert.AreEqual(a.AreConnected(1, 3), b.AreConnected(1, 3));

            var h1 = a.FindNearest(P(15, 2.6), 5000);
            var h2 = b.FindNearest(P(15, 2.6), 5000);
            Assert.AreEqual(h1.Kind, h2.Kind);
            Assert.AreEqual(h1.SegmentId, h2.SegmentId);
            Assert.AreEqual(h1.Point, h2.Point);

            // Counters carry on: the next ids match what the original issues.
            var n1 = a.AddSegment("road.dirt_path", CurveKind.CatmullRomCentripetal, new[] { P(70, 0), P(80, 0) }, new[] { P(70, 0), P(80, 0) });
            var n2 = b.AddSegment("road.dirt_path", CurveKind.CatmullRomCentripetal, new[] { P(70, 0), P(80, 0) }, new[] { P(70, 0), P(80, 0) });
            Assert.AreEqual(n1.Id, n2.Id);
            Assert.AreEqual(n1.NodeA, n2.NodeA);
            Assert.AreEqual(n1.NodeB, n2.NodeB);
        }

        [Test]
        public void Roads_EmptyNetwork_RoundTrips()
        {
            var (w, map) = Make();
            var back = RoundTrip(w, map);
            Assert.AreEqual(0, back.Roads.SegmentCount);
            var s = back.Roads.AddSegment("road.dirt_path", CurveKind.CatmullRomCentripetal, new[] { P(0, 0), P(1, 0) }, new[] { P(0, 0), P(1, 0) });
            Assert.AreEqual(1, s.Id);
        }

        private static AreaShape Square(double x, double z, double s) =>
            AreaShape.Straight(new[] { P(x, z), P(x + s, z), P(x + s, z + s), P(x, z + s) });

        [Test]
        public void Areas_ShapesNamesCountersAndIds_RoundTrip()
        {
            var (w, map) = Make();
            Assert.IsTrue(w.Execute(new DesignateArea(Square(50, 50, 20), AreaTypes.TestId)).IsAccepted);
            var bent = new AreaShape(new[] { P(100, 50), P(120, 50), P(120, 70), P(100, 70) },
                new[] { AreaEdge.Bent(P(110, 44)), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            var rb = w.Execute(new DesignateArea(bent, AreaTypes.TestId));
            Assert.IsTrue(rb.IsAccepted, rb.Text);
            Assert.IsTrue(w.Execute(new DesignateArea(Square(150, 50, 20), AreaTypes.TestWorkId)).IsAccepted);
            Assert.IsTrue(w.Areas.Remove(2)); // gap in ids; counters must not reuse it

            var back = RoundTrip(w, map);
            Assert.AreEqual(w.Areas.Count, back.Areas.Count);
            foreach (var a in w.Areas.All)
            {
                var b = back.Areas.Get(a.Id);
                Assert.IsNotNull(b);
                Assert.AreEqual(a.TypeId, b.TypeId);
                Assert.AreEqual(a.Name, b.Name);
                Assert.AreEqual(a.Polygon, b.Polygon);
                Assert.AreEqual(a.Bounds, b.Bounds);
                Assert.AreEqual(a.Shape.Vertices, b.Shape.Vertices);
                for (int i = 0; i < a.Shape.Edges.Count; i++)
                {
                    Assert.AreEqual(a.Shape.Edges[i].Kind, b.Shape.Edges[i].Kind);
                    Assert.AreEqual(a.Shape.Edges[i].Handle, b.Shape.Edges[i].Handle);
                    Assert.AreEqual(a.Shape.Edges[i].SegmentId, b.Shape.Edges[i].SegmentId);
                    Assert.AreEqual(a.Shape.Edges[i].Side, b.Shape.Edges[i].Side);
                }
            }

            // Counters carry on identically.
            var c1 = w.Execute(new DesignateArea(Square(50, 100, 20), AreaTypes.TestId));
            var c2 = back.Execute(new DesignateArea(Square(50, 100, 20), AreaTypes.TestId));
            Assert.IsTrue(c1.IsAccepted, c1.Text);
            Assert.IsTrue(c2.IsAccepted, c2.Text);
            Assert.AreEqual(w.Areas.All.Last().Id, back.Areas.All.Last().Id);
            Assert.AreEqual(w.Areas.All.Last().Name, back.Areas.All.Last().Name);
            StringAssert.EndsWith("3", back.Areas.All.Last().Name); // 3rd test-type area; removed names not reused
        }

        [Test]
        public void Areas_Queries_GiveSameResults()
        {
            var (w, map) = Make();
            w.Execute(new DesignateArea(Square(50, 50, 20), AreaTypes.TestId));
            w.Execute(new DesignateArea(Square(100, 50, 20), AreaTypes.TestId));
            w.Execute(new DesignateArea(Square(50, 100, 20), AreaTypes.TestWorkId));
            var back = RoundTrip(w, map);

            foreach (var kind in new[] { AreaKind.Building, AreaKind.Work })
                foreach (var pt in new[] { P(60, 60), P(110, 60), P(60, 110), P(5, 5), P(70, 70) })
                    Assert.AreEqual(w.Areas.FindAt(pt, kind)?.Id, back.Areas.FindAt(pt, kind)?.Id);

            var bounds = new BoundsMm(P(40, 40), P(130, 80));
            Assert.AreEqual(w.Areas.QueryOverlapCandidates(bounds).Select(a => a.Id),
                            back.Areas.QueryOverlapCandidates(bounds).Select(a => a.Id));
            Assert.IsNotEmpty(back.Areas.QueryOverlapCandidates(bounds));

            foreach (var pt in new[] { P(49, 49), P(60, 49.5), P(95, 60), P(300, 300) })
            {
                var h1 = w.Areas.FindNearestSnap(pt, 5000);
                var h2 = back.Areas.FindNearestSnap(pt, 5000);
                Assert.AreEqual(h1.HasValue, h2.HasValue);
                if (!h1.HasValue) continue;
                Assert.AreEqual(h1.Value.Kind, h2.Value.Kind);
                Assert.AreEqual(h1.Value.AreaId, h2.Value.AreaId);
                Assert.AreEqual(h1.Value.Point, h2.Value.Point);
            }
        }

        [Test]
        public void Areas_Empty_RoundTrips()
        {
            var (w, map) = Make();
            var back = RoundTrip(w, map);
            Assert.AreEqual(0, back.Areas.Count);
            Assert.IsTrue(back.Execute(new DesignateArea(Square(50, 50, 20), AreaTypes.TestId)).IsAccepted);
            Assert.AreEqual(1, back.Areas.All.First().Id);
        }

        [Test]
        public void Land_TruncatedSection_ThrowsSaveFormatException()
        {
            var (w, map) = Make();
            w.Roads.AddSegment("road.dirt_path", CurveKind.CatmullRomCentripetal, new[] { P(0, 0), P(1, 0) }, new[] { P(0, 0), P(1, 0) });
            var bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            var cut = bytes.Take(bytes.Length - 10).ToArray();
            Assert.Throws<SaveFormatException>(() => SimSave.Read(cut, map));
        }
    }
}
