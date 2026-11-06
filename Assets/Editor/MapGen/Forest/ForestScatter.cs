using System;
using System.Collections.Generic;
using System.IO;
using LordsLegacy.Game.Authoring;
using UnityEditor;
using UnityEngine;

namespace LordsLegacy.EditorTools
{
    /// <summary>Forest rectangle in world metres, as written to forest-region.json.</summary>
    [Serializable]
    public sealed class ForestRegion
    {
        public float minX;
        public float minZ;
        public float maxX;
        public float maxZ;
    }

    /// <summary>
    /// Scatters individual trees over the forest region into a <see cref="TreeAuthoringData"/> asset.
    /// Same seed, density and region give identical ids, positions and lengths.
    /// </summary>
    public static class ForestScatter
    {
        /// <summary>Map folder holding the region file and the output asset.</summary>
        public const string Folder = "Assets/Maps/TestMap";
        /// <summary>Region file path.</summary>
        public const string RegionPath = Folder + "/forest-region.json";
        /// <summary>Output asset path.</summary>
        public const string AssetPath = Folder + "/TestMapTrees.asset";
        /// <summary>Default seed.</summary>
        public const int DefaultSeed = 20260;
        /// <summary>Default density in trees per hectare (about 20k trees on the test map).</summary>
        public const float DefaultTreesPerHectare = 493f;
        /// <summary>Shortest usable trunk in metres.</summary>
        public const float MinTrunkM = 12f;
        /// <summary>Longest usable trunk in metres.</summary>
        public const float MaxTrunkM = 20f;

        [MenuItem("Lords Legacy/Map/Scatter Forest Trees")]
        public static void ScatterMenu()
        {
            var data = Generate(RegionPath, AssetPath, DefaultSeed, DefaultTreesPerHectare);
            Debug.Log("Scattered " + data.trees.Count + " trees into " + AssetPath);
        }

        /// <summary>Reads a region JSON file.</summary>
        public static ForestRegion ReadRegion(string path)
        {
            var region = JsonUtility.FromJson<ForestRegion>(File.ReadAllText(path));
            if (region == null || region.maxX <= region.minX || region.maxZ <= region.minZ)
                throw new InvalidDataException("Invalid forest region in " + path);
            return region;
        }

        /// <summary>Pure scatter: deterministic for a given region, seed and density.</summary>
        public static List<TreeEntry> Scatter(ForestRegion region, int seed, float treesPerHectare)
        {
            float width = region.maxX - region.minX;
            float depth = region.maxZ - region.minZ;
            int count = Mathf.Max(0, Mathf.RoundToInt(width * depth * treesPerHectare / 10000f));
            var rng = new System.Random(seed);
            var list = new List<TreeEntry>(count);
            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Min(region.minX + (float)(rng.NextDouble() * width), region.maxX);
                float z = Mathf.Min(region.minZ + (float)(rng.NextDouble() * depth), region.maxZ);
                float len = MinTrunkM + (float)(rng.NextDouble() * (MaxTrunkM - MinTrunkM));
                list.Add(new TreeEntry { id = i, x = x, z = z, trunkLengthM = len });
            }
            return list;
        }

        /// <summary>Scatters trees for the region file and writes (or overwrites in place) the asset.</summary>
        public static TreeAuthoringData Generate(string regionPath, string assetPath, int seed, float treesPerHectare)
        {
            var trees = Scatter(ReadRegion(regionPath), seed, treesPerHectare);
            var data = AssetDatabase.LoadAssetAtPath<TreeAuthoringData>(assetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<TreeAuthoringData>();
                AssetDatabase.CreateAsset(data, assetPath);
            }
            data.trees = trees;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return data;
        }
    }
}
