using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Core
{
    /// <summary>World-owned road and area type catalogs (decision 0019).</summary>
    public class TypeCatalogTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        // Height rises 0.2 m per metre of x (a 20% grade).
        private static MapData SteepMap()
        {
            const int nodes = 101;
            var h = new float[nodes * nodes];
            for (int iz = 0; iz < nodes; iz++)
                for (int ix = 0; ix < nodes; ix++) h[iz * nodes + ix] = 0.2f * ix * 2f;
            return new MapData("steep", new HeightGrid(0f, 0f, 2f, nodes, nodes, h), new MapTree[0]);
        }

        private static RoadTypeCatalog Roads(double width, double maxGrade) =>
            new RoadTypeCatalog(new[] { new RoadTypeDef(RoadTypes.DirtPathId, width, maxGrade, 1.5, "visual.road.dirt_path") });

        private static AreaTypeCatalog Areas(double minM2, double reachM) =>
            new AreaTypeCatalog(new[]
            {
                new AreaTypeDef(AreaTypes.TestId, "Test area", AreaKind.Building, 4, 8, minM2, 5000, 10, true, true, reachM),
                AreaTypes.TestWork
            });

        [Test]
        public void DefaultWorld_HasNonNullCatalogs_WithTodaysDefinitions()
        {
            var w = new SimWorld(new SimTuning());
            Assert.IsNotNull(w.RoadTypes);
            Assert.IsNotNull(w.AreaTypes);
            Assert.AreSame(RoadTypes.DirtPath, w.RoadTypes.Find(RoadTypes.DirtPathId));
            Assert.AreSame(AreaTypes.Test, w.AreaTypes.Find(AreaTypes.TestId));
            Assert.AreSame(AreaTypes.TestWork, w.AreaTypes.Find(AreaTypes.TestWorkId));
            Assert.IsNull(w.RoadTypes.Find("nope"));
            Assert.IsNull(w.AreaTypes.Find(null));
        }

        [Test]
        public void NullCatalogArguments_FallBackToDefaults_InBothConstructors()
        {
            var a = new SimWorld(new SimTuning(), null, null);
            var b = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning(), null, null);
            Assert.AreEqual(1, a.RoadTypes.All.Count);
            Assert.AreEqual(2, b.AreaTypes.All.Count);
        }

        [Test]
        public void MaxGrade30_AcceptsA20PercentRoad_ThatTheDefaultWorldRefuses()
        {
            var cps = new[] { M(20, 50), M(60, 50) };
            var def = SimWorld.FromMapData(SteepMap(), new SimTuning());
            var custom = SimWorld.FromMapData(SteepMap(), new SimTuning(), Roads(2.5, 0.30));
            Assert.AreEqual(RoadRefusal.TooSteep, RoadValidator.Validate(def, cps, RoadTypes.DirtPathId).Reason);
            Assert.AreEqual(RoadRefusal.Valid, RoadValidator.Validate(custom, cps, RoadTypes.DirtPathId).Reason);
            Assert.IsTrue(custom.Execute(new LordsLegacy.Sim.Commands.DrawRoad(cps, RoadTypes.DirtPathId)).IsAccepted);
            Assert.IsTrue(def.Execute(new LordsLegacy.Sim.Commands.DrawRoad(cps, RoadTypes.DirtPathId)).IsRejected);
        }

        [Test]
        public void CustomRoadWidth_ChangesSideLineAndCorridor()
        {
            var cps = new[] { M(20, 50), M(60, 50) };
            var seg = new SimWorld(new SimTuning()).Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                cps, cps);
            var wide = Roads(10, 0.15);
            Assert.AreEqual(55.0, seg.SideLine(RoadSide.Left, wide)[0].ZMetres, 1e-6);
            Assert.AreEqual(51.25, seg.SideLine(RoadSide.Left, RoadTypes.DefaultCatalog)[0].ZMetres, 1e-6);
            Assert.AreEqual(0, seg.SideLine(RoadSide.Left, new RoadTypeCatalog(null))[0].ZMetres - 50, 1e-6);
            Assert.AreEqual(4, seg.CorridorPolygon(wide).Length);
        }

        [Test]
        public void CustomRoadType_Unknown_InWorldCatalog_IsRefusedAsUnknown()
        {
            var w = new SimWorld(new SimTuning(), new RoadTypeCatalog(null));
            var cps = new[] { M(20, 50), M(60, 50) };
            Assert.AreEqual(RoadRefusal.UnknownType, RoadValidator.Validate(w, cps, RoadTypes.DirtPathId).Reason);
        }

        private static AreaShape Square(double x, double z, double s) =>
            AreaShape.Straight(new[] { M(x, z), M(x + s, z), M(x + s, z + s), M(x, z + s) });

        [Test]
        public void CustomAreaMinM2_ChangesTooSmall()
        {
            var def = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());
            var custom = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning(), null, Areas(150, 10));
            AreaShape tenBy10 = Square(30, 30, 10); // 100 m2
            Assert.AreEqual(AreaRefusal.Valid, AreaValidator.Validate(def, tenBy10, AreaTypes.TestId).Reason);
            Assert.AreEqual(AreaRefusal.TooSmall, AreaValidator.Validate(custom, tenBy10, AreaTypes.TestId).Reason);
            Assert.IsTrue(custom.Execute(new LordsLegacy.Sim.Commands.DesignateArea(tenBy10, AreaTypes.TestId)).IsRejected);
        }

        [Test]
        public void CustomNearRoadReach_ChangesTheNearRoadCheck()
        {
            // Road along +X at z=50: Left side z=51.25. Vertices a, b sit 12 m outside it.
            PointMm[] cps = { M(20, 50), M(80, 50) };
            PointMm[] samples = { M(20, 50), M(50, 50), M(80, 50) };
            var def = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());
            var custom = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning(), null, Areas(100, 15));
            int idDef = def.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal, cps, samples).Id;
            int idCustom = custom.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal, cps, samples).Id;
            AreaShape Shape(int id) => new AreaShape(
                new[] { M(30, 63.25), M(70, 63.25), M(70, 90), M(30, 90) },
                new[] { AreaEdge.NearRoad(id, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            Assert.AreEqual(AreaRefusal.NotOnRoad, AreaValidator.Validate(def, Shape(idDef), AreaTypes.TestId).Reason);
            Assert.AreEqual(AreaRefusal.Valid, AreaValidator.Validate(custom, Shape(idCustom), AreaTypes.TestId).Reason);
        }

        [Test]
        public void AreaRegistry_UsesTheWorldCatalog_ForKindAndUnknownTypes()
        {
            var types = new AreaTypeCatalog(new[] { AreaTypes.Test });
            var w = new SimWorld(new SimTuning(), null, types);
            Assert.AreEqual(AreaRefusal.UnknownType, AreaValidator.Validate(w, Square(30, 30, 12), AreaTypes.TestWorkId).Reason);
            Assert.AreEqual(AreaRefusal.Valid, AreaValidator.Validate(w, Square(30, 30, 12), AreaTypes.TestId).Reason);
        }
    }
}
