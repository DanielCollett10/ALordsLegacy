using LordsLegacy.EditorTools.Scenes;
using LordsLegacy.Game;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LordsLegacy.Tests.EditMode
{
    /// <summary>SimConfig defaults equal the code defaults (T-169, decision 0019).</summary>
    public class SimConfigTests
    {
        private const double Eps = 1e-9;

        [Test]
        public void AssetExistsAndIsIdempotent()
        {
            var a = SimConfigGenerator.EnsureAsset();
            var b = SimConfigGenerator.EnsureAsset();
            Assert.IsNotNull(a);
            Assert.AreSame(a, b);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SimConfig>(SimConfigGenerator.AssetPath));
        }

        [Test]
        public void AssetTuningEqualsCodeDefaults()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<SimConfig>(SimConfigGenerator.AssetPath);
            Assert.IsNotNull(cfg, "Run LordsLegacy/Config/Create default SimConfig");
            AssertTuningEquals(cfg.BuildTuning(), new SimTuning());
        }

        [Test]
        public void DefaultSimConfigBuildsCodeDefaultTuning()
        {
            var cfg = ScriptableObject.CreateInstance<SimConfig>();
            try
            {
                AssertTuningEquals(cfg.BuildTuning(), new SimTuning());
                Assert.AreEqual(30, cfg.BuildTuning().RealMinutesPerGameDayAt1x, Eps);
                Assert.AreEqual(1.74, cfg.BuildTuning().WalkSpeedMPerGameMinute, Eps);
                Assert.AreEqual(1.04, cfg.BuildTuning().OxSpeedMPerGameMinute, Eps);
                Assert.IsFalse(cfg.BuildTuning().RestDayEnabled);
                Assert.AreEqual(120, cfg.BuildTuning().SundayMealMinutes);
                Assert.AreEqual(6, cfg.BuildTuning().MinSleepHours, Eps);
                Assert.AreEqual(1.25, cfg.BuildRoadCatalog().All[0].SpeedMultiplier, Eps);
            }
            finally { Object.DestroyImmediate(cfg); }
        }

        private static void AssertTuningEquals(SimTuning t, SimTuning d)
        {
            foreach (var p in typeof(SimTuning).GetProperties())
            {
                if (!p.CanWrite) continue;
                object x = p.GetValue(t), y = p.GetValue(d);
                if (x is int[] xa) CollectionAssert.AreEqual((int[])y, xa, p.Name);
                else if (x is double xd) Assert.AreEqual((double)y, xd, Eps, p.Name);
                else Assert.AreEqual(y, x, p.Name);
            }
        }

        [Test]
        public void AssetRoadTypesEqualCodeDefaults()
        {
            var cat = AssetDatabase.LoadAssetAtPath<SimConfig>(SimConfigGenerator.AssetPath).BuildRoadCatalog();
            Assert.AreEqual(RoadTypes.All.Count, cat.All.Count);
            for (int i = 0; i < cat.All.Count; i++)
            {
                var a = cat.All[i]; var d = RoadTypes.All[i];
                Assert.AreEqual(d.Id, a.Id);
                Assert.AreEqual(d.WidthM, a.WidthM, Eps);
                Assert.AreEqual(d.MaxGrade, a.MaxGrade, Eps);
                Assert.AreEqual(d.SpeedMultiplier, a.SpeedMultiplier, Eps);
                Assert.AreEqual(d.VisualId, a.VisualId);
            }
        }

        [Test]
        public void AssetAreaTypesEqualCodeDefaults()
        {
            var cat = AssetDatabase.LoadAssetAtPath<SimConfig>(SimConfigGenerator.AssetPath).BuildAreaCatalog();
            Assert.AreEqual(AreaTypes.All.Count, cat.All.Count);
            for (int i = 0; i < cat.All.Count; i++)
            {
                var a = cat.All[i]; var d = AreaTypes.All[i];
                Assert.AreEqual(d.Id, a.Id);
                Assert.AreEqual(d.NamePrefix, a.NamePrefix);
                Assert.AreEqual(d.Kind, a.Kind);
                Assert.AreEqual(d.MinPoints, a.MinPoints);
                Assert.AreEqual(d.MaxPoints, a.MaxPoints);
                Assert.AreEqual(d.MinAreaM2, a.MinAreaM2, Eps);
                Assert.AreEqual(d.MaxAreaM2, a.MaxAreaM2, Eps);
                Assert.AreEqual(d.MaxSlopeDegrees.HasValue, a.MaxSlopeDegrees.HasValue);
                if (d.MaxSlopeDegrees.HasValue) Assert.AreEqual(d.MaxSlopeDegrees.Value, a.MaxSlopeDegrees.Value, Eps);
                Assert.AreEqual(d.ClearsTrees, a.ClearsTrees);
                Assert.AreEqual(d.BlocksRoads, a.BlocksRoads);
                Assert.AreEqual(d.NearRoadReachM, a.NearRoadReachM, Eps);
            }
        }
    }
}
