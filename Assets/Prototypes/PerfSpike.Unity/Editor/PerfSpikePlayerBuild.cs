using System.IO;
using LordsLegacy.Prototypes.PerfSpike.Unity;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LordsLegacy.Prototypes.PerfSpike.UnityEditorMenu
{
    /// <summary>
    /// THROWAWAY (T-013). Generates the benchmark-only scene and builds it as a Windows IL2CPP development player.
    /// The scene is deliberately NOT added to EditorBuildSettings.
    /// </summary>
    public static class PerfSpikePlayerBuild
    {
        /// <summary>Scene asset path.</summary>
        public const string ScenePath = "Assets/Prototypes/PerfSpike.Unity/Player/PerfSpikeBenchmark.unity";

        /// <summary>Build output relative to the project root.</summary>
        public const string ExePath = "Builds/PerfSpike/PerfSpike.exe";

        /// <summary>Creates (or recreates) the benchmark scene.</summary>
        [MenuItem("LordsLegacy/Perf/Generate benchmark scene")]
        public static void GenerateScene()
        {
            // Unity assigns random object IDs, so regenerating would churn the file. Delete the asset to force it.
            if (File.Exists(ScenePath)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("PerfSpikePlayer");
            go.AddComponent<PerfSpikePlayer>();
            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Builds the benchmark player.</summary>
        [MenuItem("LordsLegacy/Perf/Build benchmark (Windows IL2CPP)")]
        public static void Build()
        {
            GenerateScene();

            ScriptingImplementation oldBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            try
            {
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = ExePath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result == BuildResult.Succeeded)
                    Debug.Log("[PerfSpike] Build OK: " + ExePath + " (" + report.summary.totalSize + " bytes)");
                else
                    Debug.LogError("[PerfSpike] Build " + report.summary.result + " with "
                        + report.summary.totalErrors + " errors. Is 'Windows Build Support (IL2CPP)' installed?");
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, oldBackend);
            }
        }
    }
}
