using System.IO;
using LordsLegacy.EditorTools;
using LordsLegacy.Game.Authoring;
using LordsLegacy.Sim.Map;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LordsLegacy.Tests.EditMode
{
    public class MapBakeTests
    {
        GameObject _go;
        Terrain _terrain;
        TerrainData _td;
        TreeAuthoringData _trees;

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _td = AssetDatabase.LoadAssetAtPath<TerrainData>(MapBaker.TerrainAssetPath);
            Assert.IsNotNull(_td, "TerrainData missing at " + MapBaker.TerrainAssetPath);
            _trees = AssetDatabase.LoadAssetAtPath<TreeAuthoringData>(MapBaker.TreesAssetPath);
            Assert.IsNotNull(_trees);
            _go = Terrain.CreateTerrainGameObject(_td);
            _go.transform.position = Vector3.zero;
            _terrain = _go.GetComponent<Terrain>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void GeneratedTerrainChecks()
        {
            Assert.AreEqual(2048f, _td.size.x, 0.001f);
            Assert.AreEqual(2048f, _td.size.z, 0.001f);
            Assert.AreEqual(1025, _td.heightmapResolution);
            float min = float.MaxValue, max = float.MinValue;
            var h = _td.GetHeights(0, 0, 1025, 1025);
            foreach (var v in h) { if (v < min) min = v; if (v > max) max = v; }
            Assert.That((max - min) * _td.size.y, Is.GreaterThanOrEqualTo(20f));
            Assert.IsTrue(File.Exists(ForestScatter.RegionPath));
            var r = ForestScatter.ReadRegion(ForestScatter.RegionPath);
            Assert.That(r.minX, Is.GreaterThanOrEqualTo(0f));
            Assert.That(r.minZ, Is.GreaterThanOrEqualTo(0f));
            Assert.That(r.maxX, Is.LessThanOrEqualTo(_td.size.x));
            Assert.That(r.maxZ, Is.LessThanOrEqualTo(_td.size.z));
        }

        [Test]
        public void ValidateRejectsMovedTerrain()
        {
            _go.transform.position = new Vector3(5f, 0f, 0f);
            Assert.Throws<System.InvalidOperationException>(() => MapBaker.Validate(_terrain));
        }

        [Test]
        public void BakedFileMatchesTerrainAndTrees()
        {
            var map = MapDataIO.Read(File.ReadAllBytes(MapBaker.OutputPath));
            Assert.AreEqual(1025, map.Heights.Width);
            Assert.AreEqual(_trees.trees.Count, map.Trees.Count);
            for (int i = 0; i < _trees.trees.Count; i++)
                Assert.AreEqual(_trees.trees[i].id, map.Trees[i].Id.Value);

            var rng = new System.Random(42);
            for (int i = 0; i < 100; i++)
            {
                float x = rng.Next(0, 1025) * 2f, z = rng.Next(0, 1025) * 2f;
                Assert.AreEqual(_terrain.SampleHeight(new Vector3(x, 0, z)), map.Heights.HeightAt(x, z), 0.01f, "node " + x + "," + z);
            }
            for (int i = 0; i < 100; i++)
            {
                float x = (float)(rng.NextDouble() * 2048.0), z = (float)(rng.NextDouble() * 2048.0);
                Assert.AreEqual(_terrain.SampleHeight(new Vector3(x, 0, z)), map.Heights.HeightAt(x, z), 0.25f, "off-node " + x + "," + z);
            }
        }
    }
}
