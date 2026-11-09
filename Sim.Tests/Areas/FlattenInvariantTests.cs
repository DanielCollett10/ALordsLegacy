using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Areas
{
    /// <summary>AR-30 / AR-33: the flattened outline passes through every vertex in order; reverse NearRoad/NearArea edges stay straight.</summary>
    public class FlattenInvariantTests
    {
        private const string T = AreaTypes.TestId;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld World() => SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());

        // Road along +X from (20,50) to (80,50): Left side z = 51.25.
        private static RoadSegment Road(SimWorld w) =>
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(80, 50) },
                new[] { M(20, 50), M(35, 50), M(50, 50), M(65, 50), M(80, 50) });

        // Neighbour square (40,40)-(80,80), all straight.
        private static AreaRecord Neighbour(SimWorld w)
        {
            var verts = new[] { M(40, 40), M(80, 40), M(80, 80), M(40, 80) };
            Assert.IsTrue(w.Execute(new DesignateArea(AreaShape.Straight(verts), T)).IsAccepted);
            return w.Areas.All.First();
        }

        // Neighbour whose top edge bulges up to z = 90 at x = 60; returns it plus a hug shape from x~70 to x~50, b 6 m outside.
        private static AreaShape BulgedHug(SimWorld w, out AreaRecord nb)
        {
            var verts = new[] { M(40, 40), M(80, 40), M(80, 80), M(40, 80) };
            var edges = new[] { AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Bent(M(60, 90)), AreaEdge.Straight() };
            Assert.IsTrue(w.Execute(new DesignateArea(new AreaShape(verts, edges), T)).IsAccepted);
            AreaRecord rec = nb = w.Areas.All.First();
            PointMm Near(double x) => rec.Polygon.Where(p => p.ZMetres > 80.5).OrderBy(p => System.Math.Abs(p.XMetres - x)).First();
            PointMm a = Near(70), b0 = Near(50), b = new PointMm(b0.X, b0.Z + 6000);
            return Quad(AreaEdge.NearArea(nb.Id), a, b, new PointMm(b.X, b.Z + 14000), new PointMm(a.X, a.Z + 14000));
        }

        private static AreaShape Quad(AreaEdge first, PointMm a, PointMm b, PointMm c, PointMm d) =>
            new AreaShape(new[] { a, b, c, d }, new[] { first, AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });

        /// <summary>Every vertex is in the polygon, in shape order (either direction round), with each edge's run between its two vertices.</summary>
        private static void AssertPassesThroughEveryVertex(AreaShape shape, PointMm[] poly)
        {
            var idx = new int[shape.Vertices.Count];
            for (int i = 0; i < idx.Length; i++)
            {
                idx[i] = System.Array.IndexOf(poly, shape.Vertices[i]);
                Assert.GreaterOrEqual(idx[i], 0, "vertex " + i + " missing from the flattened outline");
            }
            int n = poly.Length, up = 0, down = 0;
            for (int i = 0; i < idx.Length; i++)
            {
                int next = idx[(i + 1) % idx.Length];
                // An edge's run is the stretch from its vertex to the next: no other vertex may sit inside it.
                int fwd = ((next - idx[i]) % n + n) % n, back = ((idx[i] - next) % n + n) % n;
                if (fwd > 0) up += fwd;
                if (back > 0) down += back;
            }
            Assert.IsTrue(up == n || down == n, "vertices are not visited once round the outline in shape order");
        }

        private static IEnumerable<TestCaseData> EdgeKinds()
        {
            yield return new TestCaseData("Straight");
            yield return new TestCaseData("Bent");
            yield return new TestCaseData("FollowsRoad");
            yield return new TestCaseData("NearRoad");
            yield return new TestCaseData("NearArea");
        }

        [TestCaseSource(nameof(EdgeKinds))]
        public void EveryVertexIsInTheFlattenedOutline_InShapeOrder(string kind)
        {
            SimWorld w = World();
            // The road (z = 50) and the neighbour (z 40..80) overlap, so only build the one the case needs.
            RoadSegment seg = kind == "FollowsRoad" || kind == "NearRoad" ? Road(w) : null;
            AreaShape shape;
            switch (kind)
            {
                case "NearArea":
                    shape = BulgedHug(w, out _);
                    break;
                case "Bent":
                    shape = new AreaShape(new[] { M(30, 20), M(70, 20), M(70, 30), M(30, 30) },
                        new[] { AreaEdge.Bent(M(50, 15)), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
                    break;
                case "FollowsRoad":
                    shape = Quad(AreaEdge.FollowsRoad(seg.Id, RoadSide.Left), M(30, 51.25), M(70, 51.25), M(70, 80), M(30, 80));
                    break;
                case "NearRoad":
                    shape = Quad(AreaEdge.NearRoad(seg.Id, RoadSide.Left), M(30, 52.25), M(70, 56.25), M(70, 80), M(30, 80));
                    break;
                default:
                    shape = AreaShape.Straight(new[] { M(30, 60), M(70, 60), M(70, 80), M(30, 80) });
                    break;
            }
            PointMm[] poly = AreaFlattener.Flatten(shape, w.Roads, w.Areas, 1.0, w.RoadTypes);
            AssertPassesThroughEveryVertex(shape, poly);
            if (kind != "Straight") Assert.Greater(poly.Length, 4, kind + " edge should add interior samples");
        }

        [Test]
        public void ShapeReorderedByUntangler_StillPassesThroughEveryVertex()
        {
            var clicks = new[] { M(30, 30), M(70, 70), M(70, 30), M(30, 70) }; // hourglass order
            Assert.IsTrue(AreaUntangler.TryUntangle(clicks, out int[] order));
            var verts = order.Select(i => clicks[i]).ToArray();
            AreaShape shape = AreaShape.Straight(verts);
            PointMm[] poly = AreaFlattener.Flatten(shape, null);
            Assert.IsTrue(Polygons.IsSimple(poly));
            AssertPassesThroughEveryVertex(shape, poly);
            Assert.AreEqual(4, poly.Length);
        }

        [Test]
        public void ShapeReorderedByUntangler_WithBentEdge_StillPassesThroughEveryVertex()
        {
            var clicks = new[] { M(30, 30), M(70, 70), M(70, 30), M(30, 70) };
            Assert.IsTrue(AreaUntangler.TryUntangle(clicks, out int[] order));
            var verts = order.Select(i => clicks[i]).ToArray();
            var edges = new[] { AreaEdge.Bent(M(50, 20)), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() };
            AreaShape shape = new AreaShape(verts, edges);
            AssertPassesThroughEveryVertex(shape, AreaFlattener.Flatten(shape, null));
        }

        [Test]
        public void NearRoad_ForwardEdge_StillTapers()
        {
            SimWorld w = World();
            RoadSegment seg = Road(w);
            AreaShape shape = Quad(AreaEdge.NearRoad(seg.Id, RoadSide.Left), M(30, 52.25), M(70, 56.25), M(70, 80), M(30, 80));
            PointMm[] poly = AreaFlattener.Flatten(shape, w.Roads);
            Assert.That(poly, Does.Contain(M(50, 51.25 + 3.0)));
        }

        [Test]
        public void NearRoad_FarVertexNearestPointBehindSnappedVertex_FlattensToExactStraightSegment()
        {
            SimWorld w = World();
            RoadSegment seg = Road(w);
            // A is 1 m off the Left side at x = 50; B's nearest point P = (49, 51.25) lies behind A relative to B.
            PointMm a = M(50, 52.25), b = M(49, 56.25);
            AreaShape shape = Quad(AreaEdge.NearRoad(seg.Id, RoadSide.Left), a, b, M(49, 80), M(50, 80));
            PointMm[] poly = AreaFlattener.Flatten(shape, w.Roads);
            Assert.AreEqual(4, poly.Length, "no samples between A and B: the edge is the plain segment");
            AssertPassesThroughEveryVertex(shape, poly);
        }

        [Test]
        public void NearRoad_ReverseCase_SnappedVertexIsTheSecondOne_StaysStraight()
        {
            SimWorld w = World();
            RoadSegment seg = Road(w);
            PointMm a = M(49, 56.25), b = M(50, 52.25); // snapped vertex is B
            AreaShape shape = Quad(AreaEdge.NearRoad(seg.Id, RoadSide.Left), a, b, M(50, 80), M(49, 80));
            Assert.AreEqual(4, AreaFlattener.Flatten(shape, w.Roads).Length);
        }

        [Test]
        public void NearArea_ForwardEdge_StillTapers()
        {
            SimWorld w = World();
            AreaShape shape = BulgedHug(w, out _);
            PointMm[] poly = AreaFlattener.Flatten(shape, w.Roads, w.Areas, 1.0, w.RoadTypes);
            Assert.Greater(poly.Length, 4);
        }

        [Test]
        public void NearArea_FarVertexNearestPointBehindSnappedVertex_FlattensToExactStraightSegment()
        {
            SimWorld w = World();
            AreaRecord nb = Neighbour(w);
            // A is 1 m above the top edge at x = 60; B's nearest outline point (59, 80) is behind A relative to B.
            PointMm a = M(60, 81), b = M(59, 85);
            AreaShape shape = Quad(AreaEdge.NearArea(nb.Id), a, b, M(59, 100), M(60, 100));
            PointMm[] poly = AreaFlattener.Flatten(shape, w.Roads, w.Areas, 1.0, w.RoadTypes);
            Assert.AreEqual(4, poly.Length, "no samples between A and B: the edge is the plain segment");
            AssertPassesThroughEveryVertex(shape, poly);
        }
    }
}
