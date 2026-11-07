#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using LordsLegacy.Game;
using LordsLegacy.Game.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LordsLegacy.EditorTools.Scenes
{
    /// <summary>
    /// Builds Assets/Scenes/Game.unity from scratch (idempotent: same inputs, same scene) and makes it
    /// scene 0 in Build Settings. Each component is one method so later tasks can add steps to
    /// <see cref="Generate"/>. Scene content lives here; Sim rules do not (CLAUDE.md).
    /// </summary>
    public static class GameSceneGenerator
    {
        /// <summary>Path of the generated scene.</summary>
        public const string ScenePath = "Assets/Scenes/Game.unity";
        /// <summary>Terrain data asset from map-1 (decision 0003).</summary>
        public const string TerrainDataPath = "Assets/Maps/TestMap/TestMapTerrain.asset";
        /// <summary>Name of the terrain object in the scene.</summary>
        public const string TerrainName = "Terrain";

        /// <summary>Menu entry: regenerates Game.unity and updates Build Settings.</summary>
        [MenuItem("LordsLegacy/Scenes/Generate Game scene")]
        public static void GenerateMenu() => Generate();

        /// <summary>Creates a fresh scene, adds every component, saves it and registers it as scene 0.</summary>
        public static void Generate()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateTerrain();
            var host = CreateSimHost().GetComponent<SimHost>();
            var cam = CreateCamera().GetComponent<Camera>();
            var light = CreateLight().GetComponent<Light>();
            CreateTreeRenderer(host, cam);
            CreateGroundPicker(host, cam);
            var topBar = CreateTopBar(host);
            CreateBuildBar();
            CreateRoadTool(host);
            CreateGroundReadout();
            CreateDebugOverlay(host);
            CreateSunController(host, light, topBar);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            SceneYamlNormalizer.Normalize(ScenePath);
            AssetDatabase.ImportAsset(ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("GameSceneGenerator: wrote " + ScenePath);
        }

        /// <summary>Adds the map-1 terrain at world (0,0,0). Terrain.CreateTerrainGameObject adds the TerrainCollider.</summary>
        public static GameObject CreateTerrain()
        {
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (td == null) { Debug.LogError("GameSceneGenerator: no TerrainData at " + TerrainDataPath); return null; }
            var go = Terrain.CreateTerrainGameObject(td);
            go.name = TerrainName;
            go.transform.position = Vector3.zero;
            return go;
        }

        /// <summary>Adds the SimHost that owns the SimWorld and reads the baked testmap.lsmap at Start.</summary>
        public static GameObject CreateSimHost()
        {
            var go = new GameObject("SimHost");
            go.AddComponent<SimHost>();
            return go;
        }

        /// <summary>Adds the main camera. Placeholder pose; the RTS camera task replaces the controller.</summary>
        public static GameObject CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.AddComponent<Camera>();
            go.AddComponent<CameraRig>();
            go.transform.position = new Vector3(1024f, 150f, 700f);
            go.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            return go;
        }

        /// <summary>Adds the directional light.</summary>
        public static GameObject CreateLight()
        {
            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            return go;
        }

        /// <summary>Shared PanelSettings asset (core-3).</summary>
        public const string PanelSettingsPath = "Assets/Game/UI/PanelSettings.asset";
        /// <summary>Visual registry asset (view-2a).</summary>
        public const string RegistryPath = "Assets/Game/Visuals/VisualRegistry.asset";

        private static void SetRef(Component c, string field, Object value)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p == null) throw new System.InvalidOperationException("GameSceneGenerator: " + c.GetType().Name + " has no field " + field);
            if (value == null) throw new System.InvalidOperationException("GameSceneGenerator: " + c.GetType().Name + "." + field + " would be null");
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) throw new System.InvalidOperationException("GameSceneGenerator: missing asset " + path);
            return a;
        }

        private static GameObject CreateUiDocument(string name, string uxmlPath)
        {
            var go = new GameObject(name);
            var doc = go.AddComponent<UnityEngine.UIElements.UIDocument>();
            doc.panelSettings = Load<UnityEngine.UIElements.PanelSettings>(PanelSettingsPath);
            if (uxmlPath != null) doc.visualTreeAsset = Load<UnityEngine.UIElements.VisualTreeAsset>(uxmlPath);
            return go;
        }

        /// <summary>Adds the TreeRenderer wired to SimHost, the visual registry and the main camera.</summary>
        public static GameObject CreateTreeRenderer(SimHost host, Camera cam)
        {
            var go = new GameObject("TreeRenderer");
            var c = go.AddComponent<TreeRenderer>();
            SetRef(c, "host", host);
            SetRef(c, "registry", Load<LordsLegacy.Game.Visuals.VisualRegistry>(RegistryPath));
            SetRef(c, "cullCamera", cam);
            return go;
        }

        /// <summary>Adds the GroundPicker wired to the camera and SimHost.</summary>
        public static GameObject CreateGroundPicker(SimHost host, Camera cam)
        {
            var go = new GameObject("GroundPicker");
            var c = go.AddComponent<GroundPicker>();
            SetRef(c, "_camera", cam);
            SetRef(c, "_host", host);
            return go;
        }

        /// <summary>Adds the top bar UIDocument and controller.</summary>
        public static TopBarController CreateTopBar(SimHost host)
        {
            var go = CreateUiDocument("TopBar", "Assets/Game/UI/TopBar/TopBar.uxml");
            var c = go.AddComponent<TopBarController>();
            SetRef(c, "_host", host);
            SetRef(c, "_styleSheet", Load<UnityEngine.UIElements.StyleSheet>("Assets/Game/UI/TopBar/TopBar.uss"));
            return c;
        }

        /// <summary>Adds the build bar UIDocument and the ToolController wired to it and the camera rig.</summary>
        public static GameObject CreateBuildBar()
        {
            var go = CreateUiDocument("BuildBar", "Assets/Game/UI/BuildBar/BuildBar.uxml");
            var view = go.AddComponent<BuildBarView>();
            SetRef(view, "_styleSheet", Load<UnityEngine.UIElements.StyleSheet>("Assets/Game/UI/BuildBar/BuildBar.uss"));
            var tools = new GameObject("ToolController").AddComponent<LordsLegacy.Game.Tools.ToolController>();
            SetRef(tools, "_bar", view);
            SetRef(tools, "_camera", Object.FindFirstObjectByType<CameraRig>());
            return go;
        }

        /// <summary>Adds the RoadRenderer, the tool panel UIDocument and the RoadTool, all wired together (T-100).</summary>
        public static GameObject CreateRoadTool(SimHost host)
        {
            var rr = new GameObject("RoadRenderer").AddComponent<RoadRenderer>();
            SetRef(rr, "host", host);
            SetRef(rr, "registry", Load<VisualRegistry>(RegistryPath));

            var panelGo = CreateUiDocument("ToolPanel", "Assets/Game/UI/ToolPanel/ToolPanel.uxml");
            var panel = panelGo.AddComponent<ToolPanelView>();
            SetRef(panel, "_styleSheet", Load<UnityEngine.UIElements.StyleSheet>("Assets/Game/UI/ToolPanel/ToolPanel.uss"));

            var go = new GameObject("RoadTool");
            var tool = go.AddComponent<LordsLegacy.Game.Tools.RoadTool>();
            SetRef(tool, "_host", host);
            SetRef(tool, "_picker", Object.FindFirstObjectByType<GroundPicker>());
            SetRef(tool, "_roads", rr);
            SetRef(tool, "_panel", panel);
            SetRef(tool, "_controller", Object.FindFirstObjectByType<LordsLegacy.Game.Tools.ToolController>());
            return go;
        }

        /// <summary>Adds the cursor ground readout UIDocument and view.</summary>
        public static GameObject CreateGroundReadout()
        {
            var go = CreateUiDocument("GroundReadout", null);
            var c = go.AddComponent<GroundReadoutView>();
            SetRef(c, "_picker", Object.FindFirstObjectByType<GroundPicker>());
            SetRef(c, "_visualTree", Load<UnityEngine.UIElements.VisualTreeAsset>("Assets/Game/UI/GroundReadout/GroundReadout.uxml"));
            SetRef(c, "_styleSheet", Load<UnityEngine.UIElements.StyleSheet>("Assets/Game/UI/GroundReadout/GroundReadout.uss"));
            return go;
        }

        /// <summary>Adds the debug overlay UIDocument and view, wired to SimHost and the GroundPicker.</summary>
        public static GameObject CreateDebugOverlay(SimHost host)
        {
            var go = CreateUiDocument("DebugOverlay", "Assets/Game/UI/DebugOverlay/DebugOverlay.uxml");
            var c = go.AddComponent<DebugOverlayView>();
            SetRef(c, "_host", host);
            SetRef(c, "_picker", Object.FindFirstObjectByType<GroundPicker>());
            SetRef(c, "_visualTree", Load<UnityEngine.UIElements.VisualTreeAsset>("Assets/Game/UI/DebugOverlay/DebugOverlay.uxml"));
            SetRef(c, "_styleSheet", Load<UnityEngine.UIElements.StyleSheet>("Assets/Game/UI/DebugOverlay/DebugOverlay.uss"));
            return go;
        }

        /// <summary>Adds the SunLightController on the directional light's object.</summary>
        public static void CreateSunController(SimHost host, Light sun, TopBarController topBar)
        {
            var c = sun.gameObject.AddComponent<SunLightController>();
            SetRef(c, "_simHost", host);
            SetRef(c, "_sun", sun);
            SetRef(c, "_topBar", topBar);
        }

        /// <summary>Puts the scene first in Build Settings, keeping other scenes once each.</summary>
        public static void AddToBuildSettings(string path)
        {
            var list = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(path, true) };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != path) list.Add(s);
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
#endif
