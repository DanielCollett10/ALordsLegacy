#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LordsLegacy.EditorTools
{
    /// <summary>Creates the single shared PanelSettings asset used by every UIDocument.</summary>
    public static class CreatePanelSettings
    {
        /// <summary>Path of the shared asset. Do not create other PanelSettings assets.</summary>
        public const string AssetPath = "Assets/Game/UI/PanelSettings.asset";

        /// <summary>Creates the asset if missing (scale with screen size, 1920x1080 reference).</summary>
        [MenuItem("Lords Legacy/Create Shared PanelSettings")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<PanelSettings>(AssetPath) != null) return;
            System.IO.Directory.CreateDirectory("Assets/Game/UI");
            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            AssetDatabase.CreateAsset(ps, AssetPath);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
