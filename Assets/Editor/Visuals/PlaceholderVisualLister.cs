using System.Collections.Generic;
using LordsLegacy.Game.Visuals;
using UnityEditor;
using UnityEngine;

namespace LordsLegacy.EditorTools.Visuals
{
    /// <summary>
    /// Lists every <see cref="VisualDefinition"/> still marked as placeholder (ART-03).
    /// Menu <c>LordsLegacy/Art/List placeholder visuals</c> logs one line per placeholder.
    /// </summary>
    public static class PlaceholderVisualLister
    {
        /// <summary>
        /// Returns the definitions whose <see cref="VisualDefinition.IsPlaceholder"/> is true, skipping nulls,
        /// in the order given.
        /// </summary>
        public static List<VisualDefinition> FindPlaceholders(IEnumerable<VisualDefinition> definitions)
        {
            var result = new List<VisualDefinition>();
            if (definitions == null) return result;
            foreach (var d in definitions)
            {
                if (d != null && d.IsPlaceholder) result.Add(d);
            }
            return result;
        }

        /// <summary>Loads every VisualDefinition asset in the project and logs the placeholders.</summary>
        [MenuItem("LordsLegacy/Art/List placeholder visuals")]
        public static void ListPlaceholders()
        {
            var all = new List<VisualDefinition>();
            foreach (var guid in AssetDatabase.FindAssets("t:VisualDefinition"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<VisualDefinition>(path);
                if (def != null) all.Add(def);
            }

            var placeholders = FindPlaceholders(all);
            foreach (var d in placeholders)
            {
                Debug.Log("PLACEHOLDER visual '" + d.Id + "' (" + AssetDatabase.GetAssetPath(d) + ")");
            }
            Debug.Log("Placeholder visuals: " + placeholders.Count + " of " + all.Count + " definitions.");
        }
    }
}
