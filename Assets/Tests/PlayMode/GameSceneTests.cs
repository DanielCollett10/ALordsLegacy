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

            var trees = Object.FindFirstObjectByType<LordsLegacy.Game.Visuals.TreeRenderer>();
            Assert.IsNotNull(trees, "TreeRenderer missing.");
            AssertNoNullRefs(trees);

            var sun = Object.FindFirstObjectByType<SunLightController>();
            Assert.IsNotNull(sun, "SunLightController missing.");
            AssertNoNullRefs(sun);
            Assert.AreEqual(LightType.Directional, sun.GetComponent<Light>().type);

            var picker = Object.FindFirstObjectByType<GroundPicker>();
            Assert.IsNotNull(picker, "GroundPicker missing.");
            AssertNoNullRefs(picker);

            var topBar = Object.FindFirstObjectByType<TopBarController>();
            Assert.IsNotNull(topBar, "TopBarController missing.");
            AssertNoNullRefs(topBar);
            var readout = Object.FindFirstObjectByType<GroundReadoutView>();
            Assert.IsNotNull(readout, "GroundReadoutView missing.");
            AssertNoNullRefs(readout);

            var overlay = Object.FindFirstObjectByType<DebugOverlayView>();
            Assert.IsNotNull(overlay, "DebugOverlayView missing.");
            AssertNoNullRefs(overlay);

            var docs = Object.FindObjectsOfType<UnityEngine.UIElements.UIDocument>();
            var names = new System.Collections.Generic.List<string>();
            foreach (var doc in docs)
            {
                Assert.IsNotNull(doc.panelSettings, doc.name + " has no PanelSettings.");
                // GroundReadoutView clones its UXML from its own _visualTree field, so its UIDocument has none.
                // CitizenPanelView builds its panel in code (T-263).
                // OperationPanelView builds its panel in code too (T-281).
                if (doc.name != "GroundReadout" && doc.name != "CitizenPanel" && doc.name != "OperationPanel") Assert.IsNotNull(doc.visualTreeAsset, doc.name + " has no UXML.");
                names.Add(doc.name);
            }
            names.Sort(System.StringComparer.Ordinal);
            CollectionAssert.AreEqual(new[] { "BuildBar", "CitizenPanel", "DebugOverlay", "GroundReadout", "OperationPanel", "ToolPanel", "TopBar" }, names);
        }

        [UnityTest]
        public IEnumerator GameSceneHasM3RoadToolWired()
        {
            var load = SceneManager.LoadSceneAsync("Game");
            Assert.IsNotNull(load, "Game scene is not in Build Settings.");
            yield return load;
            yield return null;

            var bar = Object.FindFirstObjectByType<BuildBarView>();
            Assert.IsNotNull(bar, "BuildBarView missing.");
            AssertNoNullRefs(bar);

            var panel = Object.FindFirstObjectByType<ToolPanelView>();
            Assert.IsNotNull(panel, "ToolPanelView missing.");
            AssertNoNullRefs(panel);

            var controller = Object.FindFirstObjectByType<LordsLegacy.Game.Tools.ToolController>();
            Assert.IsNotNull(controller, "ToolController missing.");
            AssertNoNullRefs(controller);

            var roads = Object.FindFirstObjectByType<LordsLegacy.Game.Visuals.RoadRenderer>();
            Assert.IsNotNull(roads, "RoadRenderer missing.");
            AssertNoNullRefs(roads);

            var tool = Object.FindFirstObjectByType<LordsLegacy.Game.Tools.RoadTool>();
            Assert.IsNotNull(tool, "RoadTool missing.");
            AssertNoNullRefs(tool);

            var areaRenderer = Object.FindFirstObjectByType<LordsLegacy.Game.Visuals.AreaRenderer>();
            Assert.IsNotNull(areaRenderer, "AreaRenderer missing.");
            AssertNoNullRefs(areaRenderer);
            var areaTools = Object.FindObjectsByType<LordsLegacy.Game.Tools.AreaTool>(FindObjectsSortMode.None);
            Assert.AreEqual(4, areaTools.Length, "Test area, Test work area, Woodcutter work area and Woodcutter tools expected.");
            foreach (var t in areaTools) AssertNoNullRefs(t);
        }

        [UnityTest]
        public IEnumerator GameSceneHasM4AreaToolsAndOverlayWired()
        {
            var load = SceneManager.LoadSceneAsync("Game");
            Assert.IsNotNull(load, "Game scene is not in Build Settings.");
            yield return load;
            yield return null;

            var renderer = Object.FindFirstObjectByType<LordsLegacy.Game.Visuals.AreaRenderer>();
            Assert.IsNotNull(renderer, "AreaRenderer missing.");
            AssertNoNullRefs(renderer);

            var ids = new System.Collections.Generic.List<string>();
            foreach (var t in Object.FindObjectsByType<LordsLegacy.Game.Tools.AreaTool>(FindObjectsSortMode.None))
                ids.Add(t.Id);
            ids.Sort(System.StringComparer.Ordinal);
            CollectionAssert.AreEqual(new[] { LordsLegacy.Game.Tools.AreaTool.TestToolId, LordsLegacy.Game.Tools.AreaTool.TestWorkToolId, "woodcutter", "woodcutter-work" }, ids);
            var moduleTool = Object.FindFirstObjectByType<LordsLegacy.Game.Tools.ModuleTool>();
            Assert.IsNotNull(moduleTool, "ModuleTool missing.");
            var moduleRenderer = Object.FindFirstObjectByType<LordsLegacy.Game.Visuals.ModuleRenderer>();
            Assert.IsNotNull(moduleRenderer, "ModuleRenderer missing.");
            AssertNoNullRefs(moduleRenderer);
            var logPileRenderer = Object.FindFirstObjectByType<LordsLegacy.Game.Visuals.LogPileRenderer>();
            Assert.IsNotNull(logPileRenderer, "LogPileRenderer missing.");
            AssertNoNullRefs(logPileRenderer);
            AssertNoNullRefs(moduleTool);

            // The overlay's area labels are in the UXML the generator wires (T-116).
            var overlay = Object.FindFirstObjectByType<DebugOverlayView>();
            Assert.IsNotNull(overlay, "DebugOverlayView missing.");
            Assert.AreEqual("Areas --", overlay.TextOf(DebugOverlayView.AreaCountName), "Area count label missing from the overlay.");
            Assert.AreEqual("Area --", overlay.TextOf(DebugOverlayView.AreaNameName), "Area name label missing from the overlay.");
        }

        [UnityTest]
        public IEnumerator GameSceneHasM7PeopleWired()
        {
            var load = SceneManager.LoadSceneAsync("Game");
            Assert.IsNotNull(load, "Game scene is not in Build Settings.");
            yield return load;
            yield return null;

            var host = Object.FindFirstObjectByType<SimHost>();
            Assert.IsNotNull(host, "SimHost missing.");
            Assert.IsNotNull(host.StartLayout, "SimHost has no StartLayout wired.");

            var people = Object.FindFirstObjectByType<LordsLegacy.Game.Visuals.PeopleRenderer>();
            Assert.IsNotNull(people, "PeopleRenderer missing.");
            AssertNoNullRefs(people);
            var camp = Object.FindFirstObjectByType<LordsLegacy.Game.Visuals.CampRenderer>();
            Assert.IsNotNull(camp, "CampRenderer missing.");
            AssertNoNullRefs(camp);

            var citizenPanel = Object.FindFirstObjectByType<CitizenPanelView>();
            Assert.IsNotNull(citizenPanel, "CitizenPanelView missing.");
            AssertNoNullRefs(citizenPanel);
            Assert.IsNotNull(citizenPanel.GetComponent<UnityEngine.UIElements.UIDocument>(), "CitizenPanel has no UIDocument.");

            var opPanel = Object.FindFirstObjectByType<OperationPanelView>();
            Assert.IsNotNull(opPanel, "OperationPanelView missing.");
            AssertNoNullRefs(opPanel);
            Assert.IsNotNull(opPanel.GetComponent<UnityEngine.UIElements.UIDocument>(), "OperationPanel has no UIDocument.");

            var agents = Object.FindFirstObjectByType<AgentPicker>();
            Assert.IsNotNull(agents, "AgentPicker missing.");
            AssertNoNullRefs(agents);

            var send = Object.FindFirstObjectByType<DebugSendController>();
            Assert.IsNotNull(send, "DebugSendController missing.");
            AssertNoNullRefs(send);

            Assert.Greater(host.World.People.CitizenCount, 0, "Start layout produced no people.");
        }

        [UnityTest]
        public IEnumerator GameSceneInitialisesSimFromBakedMap()
        {
            var load = SceneManager.LoadSceneAsync("Game");
            Assert.IsNotNull(load, "Game scene is not in Build Settings; run LordsLegacy/Scenes/Generate Game scene.");
            yield return load;
            yield return null; // let SimHost.Start run

            var host = Object.FindFirstObjectByType<SimHost>();
            Assert.IsNotNull(host, "No SimHost in Game scene.");
            Assert.IsTrue(host.IsInitialized, "SimHost did not initialise from testmap.lsmap.");
            Assert.Greater(host.World.Trees.Count, 0);
            Assert.IsNotNull(host.Config, "SimHost has no SimConfig wired (T-169).");
            Assert.AreEqual("SimConfig", host.Config.name);

            var terrain = Terrain.activeTerrain;
            Assert.IsNotNull(terrain, "No terrain in Game scene.");
            Assert.IsNotNull(terrain.GetComponent<TerrainCollider>(), "Terrain has no TerrainCollider.");
            Assert.AreEqual(Vector3.zero, terrain.transform.position);
            Assert.AreEqual(1f, terrain.heightmapPixelError, 0.001f, "Terrain heightmapPixelError must be 1 (T-196).");

            foreach (var p in Points)
            {
                float sim = host.World.Terrain.HeightAt(p.x, p.y);
                float unity = terrain.SampleHeight(new Vector3(p.x, 0f, p.y));
                Assert.AreEqual(unity, sim, 0.25f, "Height mismatch at " + p);
            }
        }
    }
}
