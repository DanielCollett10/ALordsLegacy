using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Areas
{
    /// <summary>AR-26: FollowsRoadPartly hugs a road side from the snapped vertex, then runs straight.</summary>
    public class PartialRoadEdgeTests
    {
        private const string T = AreaTypes.TestId;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        // Straight road along +X from (20,50) to (80,50): Left side z = 51.25.
        private static RoadSegment StraightRoad(SimWorld w) =>
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(80, 50) },
                new[] { M(20, 50), M(35, 50), M(50, 50), M(65, 50), M(80, 50) });

        private static SimWorld World() => SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());

        private static AreaShape Shape(int segId, PointMm a, PointMm b, PointMm c, PointMm d) =>
            new AreaShape(new[] { a, b, c, d },
                new[] { AreaEdge.FollowsRoadPartly(segId, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });

        [Test]
        public void FarVertexBeyondReach_HugsTheSideWhileNearTheChord_ThenGoesStraight()
        {
            SimWorld w = World();
            RoadSegment seg = StraightRoad(w);
            PointMm a = M(30, 51.25), b = M(70, 66.25); // b is 15 m off the side
            PointMm[] poly = AreaFlattener.Flatten(Shape(seg.Id, a, b, M(70, 90), M(30, 90)), w.Roads);
            Assert.That(poly, Does.Contain(a));
            Assert.That(poly, Does.Contain(b));
            Assert.That(poly, Does.Contain(M(35, 51.25)));
            Assert.That(poly, Does.Contain(M(50, 51.25)));
            Assert.That(poly, Does.Not.Contain(M(65, 51.25)), "stops once the road strays more than 10 m from the chord");
        }

        [Test]
        public void SnappedVertexCanBeTheSecondOne()
        {
            SimWorld w = World();
            RoadSegment seg = StraightRoad(w);
            PointMm a = M(70, 66.25), b = M(30, 51.25);
            PointMm[] poly = AreaFlattener.Flatten(Shape(seg.Id, a, b, M(30, 90), M(70, 90)), w.Roads);
            Assert.That(poly, Does.Contain(M(35, 51.25)));
            Assert.That(poly, Does.Contain(M(50, 51.25)));
            Assert.That(poly, Does.Not.Contain(M(65, 51.25)));
        }

        [Test]
        public void RoadLeavingTheChordAtOnce_StaysStraight()
        {
            SimWorld w = World();
            RoadSegment seg = StraightRoad(w);
            PointMm a = M(30, 51.25), b = M(31, 80); // straight up away from the road
            PointMm[] poly = AreaFlattener.Flatten(Shape(seg.Id, a, b, M(10, 80), M(10, 60)), w.Roads);
            Assert.AreEqual(4, poly.Length);
        }

        [Test]
        public void Validator_NeedsOneVertexOnTheSideLine()
        {
            SimWorld w = World();
            RoadSegment seg = StraightRoad(w);
            Assert.AreEqual(AreaRefusal.Valid, AreaValidator.Validate(w, Shape(seg.Id, M(30, 51.25), M(70, 66.25), M(70, 90), M(30, 90)), T).Reason);
            Assert.AreEqual("not on road", AreaValidator.Validate(w, Shape(seg.Id, M(30, 56), M(70, 66.25), M(70, 90), M(30, 90)), T).ReasonText);
            Assert.AreEqual("not on road", AreaValidator.Validate(w, Shape(999, M(30, 51.25), M(70, 66.25), M(70, 90), M(30, 90)), T).ReasonText);
        }

        [Test]
        public void MissingSegment_FlattensStraight()
        {
            SimWorld w = World();
            PointMm[] v = { M(30, 51.25), M(70, 66.25), M(70, 90), M(30, 90) };
            Assert.AreEqual(v, AreaFlattener.Flatten(Shape(999, v[0], v[1], v[2], v[3]), w.Roads));
        }
    }
}
