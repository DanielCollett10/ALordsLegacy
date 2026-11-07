using UnityEngine;
using UnityEngine.UIElements;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>
    /// Makes throw-away PanelSettings for tests. They have no theme on purpose (so test panels never render or block
    /// clicks), and the "No Theme Style Sheet" console warning is switched off on them.
    /// </summary>
    internal static class TestPanelSettings
    {
        /// <summary>A new PanelSettings with no theme and the missing-theme warning disabled. The caller destroys it.</summary>
        public static PanelSettings Create()
        {
            var ps = ScriptableObject.CreateInstance<PanelSettings>();
#if UNITY_EDITOR
            var so = new UnityEditor.SerializedObject(ps);
            var prop = so.FindProperty("m_DisableNoThemeWarning");
            if (prop != null)
            {
                prop.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
#endif
            return ps;
        }
    }
}
