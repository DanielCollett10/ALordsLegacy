#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using LordsLegacy.Game;
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
            CreateSimHost();
            CreateCamera();
            CreateLight();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
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
