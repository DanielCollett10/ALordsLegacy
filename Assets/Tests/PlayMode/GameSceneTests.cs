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

        private static void AssertNoNullRefs(Component c)
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            foreach (var f in c.GetType().GetFields(F))
            {
                bool serialized = f.IsPublic || f.GetCustomAttributes(typeof(SerializeField), true).Length > 0;
                if (!serialized || !typeof(Object).IsAssignableFrom(f.FieldType)) continue;
                var v = (Object)f.GetValue(c);
                Assert.IsTrue(v != null, c.GetType().Name + "." + f.Name + " is a null serialized reference.");
            }
        }

        [UnityTest]
        public IEnumerator GameSceneHasEveryM1PieceWired()
        {
            var load = SceneManager.LoadSceneAsync("Game");
            Assert.IsNotNull(load, "Game scene is not in Build Settings.");
            yield return load;
            yield return null;

            var cam = Camera.main;
            Assert.IsNotNull(cam, "No main camera.");
            Assert.IsNotNull(cam.GetComponent<CameraRig>(), "CameraRig missing on main camera.");

            var trees = Object.FindObjectOfType<LordsLegacy.Game.Visuals.TreeRenderer>();
            Assert.IsNotNull(trees, "TreeRenderer missing.");
            AssertNoNullRefs(trees);

            var sun = Object.FindObjectOfType<SunLightController>();
            Assert.IsNotNull(sun, "SunLightController missing.");
            AssertNoNullRefs(sun);
            Assert.AreEqual(LightType.Directional, sun.GetComponent<Light>().type);

            var picker = Object.FindObjectOfType<GroundPicker>();
            Assert.IsNotNull(picker, "GroundPicker missing.");
            AssertNoNullRefs(picker);

            var topBar = Object.FindObjectOfType<TopBarController>();
            Assert.IsNotNull(topBar, "TopBarController missing.");
            AssertNoNullRefs(topBar);
            var readout = Object.FindObjectOfType<GroundReadoutView>();
            Assert.IsNotNull(readout, "GroundReadoutView missing.");
            AssertNoNullRefs(readout);

            var overlay = Object.FindObjectOfType<DebugOverlayView>();
            Assert.IsNotNull(overlay, "DebugOverlayView missing.");
            AssertNoNullRefs(overlay);

            var docs = Object.FindObjectsOfType<UnityEngine.UIElements.UIDocument>();
            var names = new System.Collections.Generic.List<string>();
            foreach (var doc in docs)
            {
                Assert.IsNotNull(doc.panelSettings, doc.name + " has no PanelSettings.");
                Assert.IsNotNull(doc.visualTreeAsset, doc.name + " has no UXML.");
                names.Add(doc.name);
            }
            names.Sort(System.StringComparer.Ordinal);
            CollectionAssert.AreEqual(new[] { "DebugOverlay", "GroundReadout", "TopBar" }, names);
        }

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
