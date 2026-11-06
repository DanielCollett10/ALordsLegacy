using System.Collections;
using LordsLegacy.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>Loads the generated Game scene and checks SimHost, the baked map and the terrain agree.</summary>
    public class GameSceneTests
    {
        private static readonly Vector2[] Points =
        {
            new Vector2(100f, 100f), new Vector2(513.5f, 777f), new Vector2(1024f, 1024f),
            new Vector2(1500.25f, 1400f), new Vector2(1900f, 300f),
        };

        [UnityTest]
        public IEnumerator GameSceneInitialisesSimFromBakedMap()
        {
            var load = SceneManager.LoadSceneAsync("Game");
            Assert.IsNotNull(load, "Game scene is not in Build Settings; run LordsLegacy/Scenes/Generate Game scene.");
            yield return load;
            yield return null; // let SimHost.Start run

            var host = Object.FindObjectOfType<SimHost>();
            Assert.IsNotNull(host, "No SimHost in Game scene.");
            Assert.IsTrue(host.IsInitialized, "SimHost did not initialise from testmap.lsmap.");
            Assert.Greater(host.World.Trees.Count, 0);

            var terrain = Terrain.activeTerrain;
            Assert.IsNotNull(terrain, "No terrain in Game scene.");
            Assert.IsNotNull(terrain.GetComponent<TerrainCollider>(), "Terrain has no TerrainCollider.");
            Assert.AreEqual(Vector3.zero, terrain.transform.position);

            foreach (var p in Points)
            {
                float sim = host.World.Terrain.HeightAt(p.x, p.y);
                float unity = terrain.SampleHeight(new Vector3(p.x, 0f, p.y));
                Assert.AreEqual(unity, sim, 0.25f, "Height mismatch at " + p);
            }
        }
    }
}
