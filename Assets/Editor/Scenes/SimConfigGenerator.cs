#if UNITY_EDITOR
using System.IO;
using LordsLegacy.Game;
using UnityEditor;
using UnityEngine;

namespace LordsLegacy.EditorTools.Scenes
{
    /// <summary>Creates the default SimConfig asset (decision 0019). Idempotent: an existing asset is left untouched.</summary>
    public static class SimConfigGenerator
    {
        /// <summary>Path of the default config asset.</summary>
        public const string AssetPath = "Assets/Game/Config/SimConfig.asset";

        /// <summary>Menu entry: creates the asset if it does not exist.</summary>
        [MenuItem("LordsLegacy/Config/Create default SimConfig")]
        public static void CreateMenu() => EnsureAsset();

        /// <summary>Returns the config asset, creating it with code defaults if missing.</summary>
        public static SimConfig EnsureAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<SimConfig>(AssetPath);
            if (existing != null) return existing;
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            var cfg = ScriptableObject.CreateInstance<SimConfig>();
            cfg.ResetToDefaults();
            AssetDatabase.CreateAsset(cfg, AssetPath);
            AssetDatabase.SaveAssets();
            return cfg;
        }
    }
}
#endif
