#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace NetSim.EditorTools
{
    /// <summary>
    /// Keeps the project in the settings that AI agents (and git) need:
    /// scenes/prefabs serialized as readable YAML text, and visible .meta files.
    /// Runs every time the Editor loads or recompiles.
    /// </summary>
    [InitializeOnLoad]
    internal static class AgentFriendlyProjectSettings
    {
        static AgentFriendlyProjectSettings()
        {
            var changed = false;
            if (EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                EditorSettings.serializationMode = SerializationMode.ForceText;
                changed = true;
            }
            if (VersionControlSettings.mode != "Visible Meta Files")
            {
                VersionControlSettings.mode = "Visible Meta Files";
                changed = true;
            }
            if (changed)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[Agent NOC] Set Asset Serialization = Force Text and Version Control = Visible Meta Files.");
            }
        }
    }
}
#endif
