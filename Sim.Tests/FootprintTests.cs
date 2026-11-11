using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class FootprintTests
    {
        private static PointMm P(long x, long z) => new PointMm(x, z);
        private static SimWorld NewWorld() => new SimWorld(new SimTuning());

        [Test]
        public void CampType_HasSpecValues()
        {
            var t = FootprintTypeDef.Camp;
            Assert.AreEqual("footprint.camp", t.Id);
            Assert.AreEqual(24000, t.WidthMm);
            Assert.AreEqual(16000, t.DepthMm);
            Assert.IsFalse(t.BlocksMovement);
            Assert.IsTrue(t.ClearsTrees);
        }

        [Test]
        public void Polygon_At0Degrees_IsAxisAlignedCcw()
        {
            var f = new Footprint(1, FootprintTypeDef.Camp, P(100000, 50000), 0);
            Assert.AreEqual(new[] { P(88000, 42000), P(112000, 42000), P(112000, 58000), P(88000, 58000) }, f.Polygon);
            Assert.IsTrue(Polygons.IsCounterClockwise(f.Polygon));
        }

        [Test]
        public void Polygon_At30Degrees_IsRotatedCcw()
        {
            var f = new Footprint(1, FootprintTypeDef.Camp, P(0, 0), 30);
            Assert.AreEqual(P(-6392, -12928), f.Polygon[0]);
            Assert.AreEqual(P(14392, -928), f.Polygon[1]);
            Assert.AreEqual(P(6392, 12928), f.Polygon[2]);
            Assert.AreEqual(P(-14392, 928), f.Polygon[3]);
            Assert.IsTrue(Polygons.IsCounterClockwise(f.Polygon));
            Assert.IsTrue(Polygons.IsSimple(f.Polygon));
        }

        [Test]
        public void AccessPoint_At0Degrees_IsOneMetreOutsideFrontEdgeMidpoint()
        {
            var f = new Footprint(1, FootprintTypeDef.Camp, P(0, 0), 0);
            Assert.AreEqual(P(0, -9000), f.AccessPoint);
        }

        [Test]
        public void AccessPoint_At30Degrees_IsOneMetreOutFromFrontMidpoint()
        {
            var f = new Footprint(1, FootprintTypeDef.Camp, P(0, 0), 30);
            double mx = (f.Polygon[0].X + f.Polygon[1].X) / 2.0, mz = (f.Polygon[0].Z + f.Polygon[1].Z) / 2.0;
            double dx = f.AccessPoint.X - mx, dz = f.AccessPoint.Z - mz;
            Assert.AreEqual(1000, System.Math.Sqrt(dx * dx + dz * dz), 2);
            Assert.IsFalse(Polygons.Contains(f.Polygon, f.AccessPoint));
        }

        [Test]
        public void FindAt_InsideEdgeAndOutside()
        {
            var w = NewWorld();
            w.Footprints.Add(1, FootprintTypeDef.Camp, P(0, 0), 0);
            Assert.AreEqual(1, w.Footprints.FindAt(P(0, 0)).Id);
            Assert.AreEqual(1, w.Footprints.FindAt(P(12000, 8000)).Id);
            Assert.IsNull(w.Footprints.FindAt(P(12001, 0)));
        }

        [Test]
        public void FindAt_RotatedCorner_MissesWhereBoundsHit()
        {
            var w = NewWorld();
            w.Footprints.Add(1, FootprintTypeDef.Camp, P(0, 0), 30);
            Assert.IsNull(w.Footprints.FindAt(P(-14000, -13000)));
        }

        [Test]
        public void Version_BumpsOnAddAndSuccessfulRemoveOnly()
        {
            var r = NewWorld().Footprints;
            Assert.AreEqual(0, r.Version);
            r.Add(1, FootprintTypeDef.Camp, P(0, 0), 0);
            Assert.AreEqual(1, r.Version);
            Assert.IsFalse(r.Remove(9));
            Assert.AreEqual(1, r.Version);
            Assert.IsTrue(r.Remove(1));
            Assert.AreEqual(2, r.Version);
            Assert.AreEqual(0, r.Count);
        }

        [Test]
        public void Add_DuplicateId_Throws()
        {
            var r = NewWorld().Footprints;
            r.Add(1, FootprintTypeDef.Camp, P(0, 0), 0);
            Assert.Throws<System.ArgumentException>(() => r.Add(1, FootprintTypeDef.Camp, P(0, 0), 0));
        }

        [Test]
        public void QueryRect_ReturnsOverlappingInIdOrder_AndIdsIterate()
        {
            var r = NewWorld().Footprints;
            r.Add(5, FootprintTypeDef.Camp, P(100000, 0), 0);
            r.Add(2, FootprintTypeDef.Camp, P(0, 0), 0);
            var hits = r.QueryRect(new BoundsMm(P(-1000, -1000), P(1000, 1000)));
            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(2, hits[0].Id);
            CollectionAssert.AreEqual(new[] { 2, 5 }, r.Ids);
            Assert.AreEqual(0, r.QueryRect(new BoundsMm(P(50000, 50000), P(60000, 60000))).Count);
        }
    }
}
