using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Areas
{
    /// <summary>NearArea edges (AR-25): an edge hugging another area's curved outline.</summary>
    public class NearAreaEdgeTests
    {
        private const string T = AreaTypes.TestId;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        // Neighbour: square (40,40)-(80,80) whose top edge bulges up to z = 90 at x = 60.
        private static SimWorld WorldWithNeighbour(out AreaRecord neighbour)
        {
            SimWorld w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());
            var verts = new[] { M(40, 40), M(80, 40), M(80, 80), M(40, 80) };
            var edges = new[] { AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Bent(M(60, 90)), AreaEdge.Straight() };
            Assert.IsTrue(w.Execute(new DesignateArea(new AreaShape(verts, edges), T)).IsAccepted);
            neighbour = w.Areas.All.First();
            return w;
        }

        private static PointMm OutlinePointNear(AreaRecord r, double x)
        {
            PointMm best = default; double bd = double.MaxValue;
            foreach (var p in r.Polygon)
            {
                if (p.ZMetres <= 80.5) continue; // the curved part only
                double d = System.Math.Abs(p.XMetres - x);
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }

        private static AreaShape Hug(int areaId, PointMm a, PointMm b, double up) =>
            new AreaShape(new[] { a, b, new PointMm(b.X, b.Z + (long)(up * 1000)), new PointMm(a.X, a.Z + (long)(up * 1000)) },
                new[] { AreaEdge.NearArea(areaId), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });

        [Test]
        public void EdgeFollowsNeighbourCurve_BothVerticesStay()
        {
            SimWorld w = WorldWithNeighbour(out AreaRecord n);
            PointMm a = OutlinePointNear(n, 70);
            PointMm b0 = OutlinePointNear(n, 50);
            PointMm b = new PointMm(b0.X, b0.Z + 6000); // 6 m outside
            AreaShape shape = Hug(n.Id, a, b, 20);
            AreaPreview p = AreaValidator.Validate(w, shape, T);
            Assert.IsTrue(p.IsValid, p.ReasonText);
            Assert.That(p.Polygon, Does.Contain(a));
            Assert.That(p.Polygon, Does.Contain(b));
            // Several neighbour outline samples lie between the vertices, pushed outside (z above the outline).
            int between = p.Polygon.Count(q => q.XMetres > 50.5 && q.XMetres < 69.5 && q.ZMetres > 83 && q.ZMetres < 100 && q.ZMetres < 94);
            Assert.Greater(between, 5);
            Assert.IsTrue(w.Execute(new DesignateArea(shape, T)).IsAccepted);
        }

        [Test]
        public void SameAsStraight_WhenBothOnOutline_FollowsItsCurve()
        {
            SimWorld w = WorldWithNeighbour(out AreaRecord n);
            PointMm a = OutlinePointNear(n, 70), b = OutlinePointNear(n, 50);
            PointMm[] poly = AreaFlattener.Flatten(Hug(n.Id, a, b, 20), w.Roads, w.Areas, 1.0);
            foreach (PointMm q in poly.Where(q => q.XMetres > 51 && q.XMetres < 69 && q.ZMetres < 95 && q.ZMetres > 80.5))
                Assert.LessOrEqual(Polylines.Nearest(n.Polygon.Concat(new[] { n.Polygon[0] }).ToArray(), q).DistanceMm, 50.0);
        }

        [Test]
        public void VertexBeyondReach_IsRefused()
        {
            SimWorld w = WorldWithNeighbour(out AreaRecord n);
            PointMm a = OutlinePointNear(n, 70), b0 = OutlinePointNear(n, 50);
            AreaPreview p = AreaValidator.Validate(w, Hug(n.Id, a, new PointMm(b0.X, b0.Z + 12000), 20), T);
            Assert.AreEqual(AreaRefusal.NotOnRoad, p.Reason);
        }

        [Test]
        public void VertexInsideNeighbour_IsRefused()
        {
            SimWorld w = WorldWithNeighbour(out AreaRecord n);
            PointMm a = OutlinePointNear(n, 70), b0 = OutlinePointNear(n, 50);
            AreaPreview p = AreaValidator.Validate(w, Hug(n.Id, a, new PointMm(b0.X, b0.Z - 3000), 20), T);
            Assert.AreEqual(AreaRefusal.NotOnRoad, p.Reason);
        }

        [Test]
        public void MissingNeighbour_IsRefused_AndFlattensStraight()
        {
            SimWorld w = WorldWithNeighbour(out AreaRecord n);
            PointMm a = OutlinePointNear(n, 70), b = new PointMm(OutlinePointNear(n, 50).X, 86000);
            AreaShape shape = Hug(999, a, b, 20);
            Assert.AreEqual(AreaRefusal.NotOnRoad, AreaValidator.Validate(w, shape, T).Reason);
            Assert.AreEqual(4, AreaFlattener.Flatten(shape, w.Roads, w.Areas, 1.0).Length);
        }
    }
}
