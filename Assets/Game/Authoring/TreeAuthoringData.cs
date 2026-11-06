using System;
using System.Collections.Generic;
using UnityEngine;

namespace LordsLegacy.Game.Authoring
{
    /// <summary>One authored tree: stable id, world x/z in metres and usable trunk length in metres.</summary>
    [Serializable]
    public struct TreeEntry
    {
        /// <summary>Stable, unique tree id.</summary>
        public int id;
        /// <summary>World x in metres (equals Sim x).</summary>
        public float x;
        /// <summary>World z in metres (equals Sim z).</summary>
        public float z;
        /// <summary>Usable trunk length in metres.</summary>
        public float trunkLengthM;
    }

    /// <summary>
    /// Authoring-only tree list the map bake reads (decision 0003). Not used at runtime and
    /// independent of Unity's terrain trees.
    /// </summary>
    [CreateAssetMenu(menuName = "Lords Legacy/Tree Authoring Data", fileName = "TreeAuthoringData")]
    public sealed class TreeAuthoringData : ScriptableObject
    {
        /// <summary>The authored trees.</summary>
        public List<TreeEntry> trees = new List<TreeEntry>();
    }
}
