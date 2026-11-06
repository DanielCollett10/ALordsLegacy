using System;
using System.IO;
using System.Linq;
using LordsLegacy.Game.Authoring;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Terrain;
using UnityEditor;
using UnityEngine;

namespace LordsLegacy.EditorTools
{
    /// <summary>
    /// Bakes Unity Terrain heights and <see cref="TreeAuthoringData"/> into the versioned Sim map
    /// file (decision 0003). Re-baking after map changes is a manual menu step.
    /// </summary>
    public static class MapBaker
    {
        /// <summary>Map id of the test map.</summary>
        public const string MapId = "testmap";
        /// <summary>Grid cell size in metres the bake produces.</summary>
        public const float BakeCellSize = 2f;
        /// <summary>Committed terrain data asset.</summary>
        public const string TerrainAssetPath = "Assets/Maps/TestMap/TestMapTerrain.asset";
        /// <summary>Tree authoring asset.</summary>
        public const string TreesAssetPath = ForestScatter.AssetPath;
        /// <summary>Output map file.</summary>
        public const string OutputPath = "Assets/StreamingAssets/Maps/testmap.lsmap";

        [MenuItem("Lords Legacy/Map/Bake Test Map")]
        public static void BakeMenu()
        {
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainAssetPath);
            if (td == null) { Debug.LogError("Map bake failed: no TerrainData at " + TerrainAssetPath); return; }
            var terrain = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.terrainData == td);
            if (terrain == null) { Debug.LogError("Map bake failed: open TestMap.unity so the terrain is in the scene."); return; }
            try
            {
                var bytes = MapDataIO.ToBytes(Build(terrain, AssetDatabase.LoadAssetAtPath<TreeAuthoringData>(TreesAssetPath)));
                Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
                File.WriteAllBytes(OutputPath, bytes);
                AssetDatabase.ImportAsset(OutputPath);
                Debug.Log("Baked " + OutputPath + " (" + bytes.Length + " bytes)");
            }
            catch (Exception e) { Debug.LogError("Map bake failed: " + e.Message); }
        }

        /// <summary>Throws InvalidOperationException unless the terrain sits at the origin and size/(res-1) equals the cell size.</summary>
        public static void Validate(Terrain terrain)
        {
            if (terrain == null || terrain.terrainData == null) throw new InvalidOperationException("No terrain to bake.");
            var p = terrain.transform.position;
            if (p != Vector3.zero)
                throw new InvalidOperationException("Terrain must be at world (0,0,0) but is at " + p + ".");
            var td = terrain.terrainData;
            float cx = td.size.x / (td.heightmapResolution - 1);
            float cz = td.size.z / (td.heightmapResolution - 1);
            if (Mathf.Abs(cx - BakeCellSize) > 1e-4f || Mathf.Abs(cz - BakeCellSize) > 1e-4f)
                throw new InvalidOperationException("Terrain size / (heightmapResolution - 1) must equal " + BakeCellSize
                    + " m but is " + cx + " x " + cz + " (size " + td.size + ", resolution " + td.heightmapResolution + ").");
        }

        /// <summary>Builds the map data from a terrain and tree list after validating the terrain.</summary>
        public static MapData Build(Terrain terrain, TreeAuthoringData trees)
        {
            Validate(terrain);
            if (trees == null) throw new InvalidOperationException("No TreeAuthoringData at " + TreesAssetPath + ".");
            var td = terrain.terrainData;
            int res = td.heightmapResolution;
            float[,] raw = td.GetHeights(0, 0, res, res); // [z, x], normalised 0..1
            float scale = td.size.y;
            var flat = new float[res * res];
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                    flat[z * res + x] = raw[z, x] * scale;
            var grid = new HeightGrid(0f, 0f, BakeCellSize, res, res, flat);
            var mapTrees = trees.trees.Select(t => new MapTree(new TreeId(t.id), t.x, t.z, t.trunkLengthM));
            return new MapData(MapId, grid, mapTrees);
        }
    }
}
