using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Areas
{
    /// <summary>Placement checks 6-8 of the area validator (AR-14..16).</summary>
    public class AreaPlacementTests
    {
        private const string T = AreaTypes.TestId;
        private const string W = AreaTypes.TestWorkId;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);
        private static SimWorld Ramp() => SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());

        // Height rises 0.3 m per metre of x: about 16.7 degrees.
        private static SimWorld Steep()
        {
            const int nodes = 101;
            var h = new float[nodes * nodes];
            for (int iz = 0; iz < nodes; iz++)
                for (int ix = 0; ix < nodes; ix++) h[iz * nodes + ix] = 0.3f * (ix * 2f);
            var map = new MapData("steep", new HeightGrid(0f, 0f, 2f, nodes, nodes, h), new MapTree[0]);
            return SimWorld.FromMapData(map, new SimTuning());
        }

        private static AreaShape Square(double x, double z, double s) =>
            AreaShape.Straight(new[] { M(x, z), M(x + s, z), M(x + s, z + s), M(x, z + s) });

        // Road along +X at z = 50; corridor spans z 48.75..51.25.
        private static RoadSegment Road(SimWorld w) =>
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(80, 50) }, new[] { M(20, 50), M(50, 50), M(80, 50) });

        private static AreaRecord AddArea(SimWorld w, AreaTypeDef type, AreaShape shape)
        {
            var poly = AreaFlattener.Flatten(shape, w.Roads, w.Tuning);
            var rec = new AreaRecord(w.Areas.NextId(), type.Id, w.Areas.NextName(type), shape, poly);
            w.Areas.Add(rec);
            return rec;
        }

        [Test]
        public void TooSteep_BuildingArea()
        {
            AreaPreview p = AreaValidator.Validate(Steep(), Square(50, 50, 20), T);
            Assert.AreEqual(AreaRefusal.TooSteep, p.Reason);
            Assert.AreEqual("too steep", p.ReasonText);
        }

        [Test]
        public void GentleSlope_Passes()
        {
            Assert.IsTrue(AreaValidator.Validate(Ramp(), Square(50, 50, 20), T).IsValid);
        }

        [Test]
        public void Overlap_NamesTheOtherArea()
        {
            SimWorld w = Ramp();
            AreaRecord a = AddArea(w, AreaTypes.Test, Square(50, 50, 20));
            AreaPreview p = AreaValidator.Validate(w, Square(60, 55, 20), T);
            Assert.AreEqual(AreaRefusal.OverlapsArea, p.Reason);
            Assert.AreEqual("overlaps " + a.Name, p.ReasonText);
            Assert.AreEqual("overlaps Test area 1", p.ReasonText);
        }

        [Test]
        public void Overlap_BelowTolerance_CountsAsTouching()
        {
            SimWorld w = Ramp();
            AddArea(w, AreaTypes.Test, Square(50, 50, 20));
            // 1 cm x 20 m sliver = 0.2 m² would fail; 4 mm x 20 m = 0.08 m² passes.
            AreaShape s = AreaShape.Straight(new[] { M(69.996, 50), M(90, 50), M(90, 70), M(69.996, 70) });
            Assert.IsTrue(AreaValidator.Validate(w, s, T).IsValid);
        }

        [Test]
        public void TouchingAnotherArea_Passes()
        {
            SimWorld w = Ramp();
            AddArea(w, AreaTypes.Test, Square(50, 50, 20));
            Assert.IsTrue(AreaValidator.Validate(w, Square(70, 50, 20), T).IsValid);
            Assert.IsTrue(AreaValidator.Validate(w, Square(50, 70, 20), T).IsValid);
        }

        [Test]
        public void WorkAreaExisting_DoesNotBlockBuildingArea()
        {
            SimWorld w = Ramp();
            AddArea(w, AreaTypes.TestWork, Square(50, 50, 20));
            Assert.IsTrue(AreaValidator.Validate(w, Square(55, 55, 20), T).IsValid);
        }

        [Test]
        public void CrossesRoad_BuildingArea()
        {
            SimWorld w = Ramp();
            Road(w);
            AreaPreview p = AreaValidator.Validate(w, Square(50, 45, 20), T);
            Assert.AreEqual(AreaRefusal.CrossesRoad, p.Reason);
            Assert.AreEqual("crosses road", p.ReasonText);
        }

        [Test]
        public void EdgeFlushAlongRoadSide_Passes()
        {
            SimWorld w = Ramp();
            Road(w);
            Assert.IsTrue(AreaValidator.Validate(w, Square(40, 51.25, 20), T).IsValid);
            Assert.IsTrue(AreaValidator.Validate(w, Square(40, 48.75 - 20, 20), T).IsValid);
        }

        [Test]
        public void AreaFarFromRoad_Passes()
        {
            SimWorld w = Ramp();
            Road(w);
            Assert.IsTrue(AreaValidator.Validate(w, Square(120, 120, 20), T).IsValid);
        }

        [Test]
        public void WorkArea_OverlappingEverything_Passes()
        {
            SimWorld w = Steep();
            Road(w);
            AddArea(w, AreaTypes.Test, Square(50, 40, 20));
            Assert.IsTrue(AreaValidator.Validate(w, Square(55, 45, 20), W).IsValid);
        }

        [Test]
        public void Order_SteepBeforeOverlapBeforeRoad()
        {
            SimWorld w = Steep();
            Road(w);
            AddArea(w, AreaTypes.Test, Square(50, 40, 20));
            AreaShape s = Square(55, 45, 20); // steep, overlaps the area and crosses the road
            Assert.AreEqual(AreaRefusal.TooSteep, AreaValidator.Validate(w, s, T).Reason);

            w = Ramp();
            Road(w);
            AddArea(w, AreaTypes.Test, Square(50, 40, 20));
            Assert.AreEqual(AreaRefusal.OverlapsArea, AreaValidator.Validate(w, s, T).Reason);

            w = Ramp();
            Road(w);
            Assert.AreEqual(AreaRefusal.CrossesRoad, AreaValidator.Validate(w, s, T).Reason);
        }

        [Test]
        public void ShapeChecksComeBeforePlacementChecks()
        {
            SimWorld w = Steep();
            Assert.AreEqual(AreaRefusal.TooSmall, AreaValidator.Validate(w, Square(50, 50, 9), T).Reason);
        }
    }
}
