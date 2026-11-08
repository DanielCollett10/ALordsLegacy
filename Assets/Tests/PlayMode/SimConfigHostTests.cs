using LordsLegacy.Game;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>SimHost builds the world from a SimConfig (T-169, decision 0019).</summary>
    public class SimConfigHostTests
    {
        private GameObject _go;
        private SimConfig _config;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        // Height rises 0.2 m per metre of x: a road along x is a 20% grade.
        private static MapData SteepMap()
        {
            const int n = 101;
            var h = new float[n * n];
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++) h[z * n + x] = 0.2f * (x * 2f);
            return new MapData("steep", new HeightGrid(0f, 0f, 2f, n, n, h), new MapTree[0]);
        }

        private SimHost Build(float maxGradePercent, bool useConfig)
        {
            _go = new GameObject("SimHost");
            var host = _go.AddComponent<SimHost>();
            host.AutoUpdate = false;
            if (useConfig)
            {
                _config = ScriptableObject.CreateInstance<SimConfig>();
                _config.roadTypes[0].maxGradePercent = maxGradePercent;
                host.Config = _config;
            }
            var tuning = useConfig ? _config.BuildTuning() : new SimTuning();
            host.Initialize(tuning, SteepMap());
            return host;
        }

        private static CommandResult Draw(SimHost host) => host.Execute(new DrawRoad(
            new[] { PointMm.FromMetres(10, 50), PointMm.FromMetres(60, 50) }, RoadTypes.DirtPathId));

        [Test]
        public void ConfigMaxGrade30PercentLetsA20PercentRoadConfirm()
        {
            var host = Build(30f, true);
            Assert.AreEqual(0.30, host.World.RoadTypes.Find(RoadTypes.DirtPathId).MaxGrade, 1e-9);
            var r = Draw(host);
            Assert.IsTrue(r.IsAccepted,"20% road should confirm at 30% max grade");
        }

        [Test]
        public void DefaultsRejectA20PercentRoad()
        {
            var host = Build(0, false);
            Assert.IsFalse(Draw(host).IsAccepted,"20% road should be rejected at the 15% default");
        }
    }
}
