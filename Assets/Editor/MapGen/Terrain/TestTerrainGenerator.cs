#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LordsLegacy.EditorTools.MapGen
{
    /// <summary>
    /// Builds the 2,048 x 2,048 m placeholder test terrain (decision 0003): rolling hills, a valley,
    /// a flat corridor, painted ground layers, and forest-region.json. Same seed, same result.
    /// Uses only UnityEngine/UnityEditor types.
    /// </summary>
    public static class TestTerrainGenerator
    {
        /// <summary>Default seed used by the menu item.</summary>
        public const int DefaultSeed = 1;
        /// <summary>Output folder.</summary>
        public const string Folder = "Assets/Maps/TestMap";
        const string ObjectName = "TestMapTerrain";
        /// <summary>Terrain LOD error in pixels (T-196). Matches GameSceneGenerator.TerrainHeightmapPixelError.</summary>
        const float HeightmapPixelError = 1f;
        const float SizeXZ = 2048f, SizeY = 120f, BaseHeight = 40f;
        const int HeightRes = 1025, AlphaRes = 512;
        // Flat corridor for trunk road and camp-site: full width of the map.
        const float CorridorMinZ = 900f, CorridorMaxZ = 1150f, CorridorBlend = 150f;

        /// <summary>Menu entry: generates with the default seed.</summary>
        [MenuItem("Lords Legacy/Map/Generate Test Terrain")]
        public static void GenerateMenu() { Generate(DefaultSeed); }

        /// <summary>Creates or regenerates the terrain asset, scene object and forest-region.json.</summary>
        public static void Generate(int seed)
        {
            Directory.CreateDirectory(Folder);
            var rng = new System.Random(seed);
            float ox1 = (float)rng.NextDouble() * 1000f, oz1 = (float)rng.NextDouble() * 1000f;
            float ox2 = (float)rng.NextDouble() * 1000f, oz2 = (float)rng.NextDouble() * 1000f;
            float valleyPhase = (float)rng.NextDouble() * Mathf.PI * 2f;
            // Forest sits north of the corridor, away from the valley (valley is on the low-x side).
            float fMinX = 1000f + rng.Next(0, 200), fMinZ = 1300f + rng.Next(0, 100);
            float fMaxX = Mathf.Min(fMinX + 600f + rng.Next(0, 200), 2000f);
            float fMaxZ = Mathf.Min(fMinZ + 500f + rng.Next(0, 150), 2000f);

            var heights = new float[HeightRes, HeightRes]; // [z, x]
            float step = SizeXZ / (HeightRes - 1);
            for (int zi = 0; zi < HeightRes; zi++)
            {
                for (int xi = 0; xi < HeightRes; xi++)
                {
                    float x = xi * step, z = zi * step;
                    float h = (Mathf.PerlinNoise(ox1 + x / 700f, oz1 + z / 700f) - 0.5f) * 36f
                            + (Mathf.PerlinNoise(ox2 + x / 260f, oz2 + z / 260f) - 0.5f) * 8f;
                    // Valley: smooth trench winding along z.
                    float vx = 500f + 180f * Mathf.Sin(z / 420f + valleyPhase);
                    float d = (x - vx) / 130f;
                    h -= 22f * Mathf.Exp(-d * d);
                    // Flat corridor: damp relief to a gentle plain.
                    float dz = z < CorridorMinZ ? CorridorMinZ - z : (z > CorridorMaxZ ? z - CorridorMaxZ : 0f);
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(dz / CorridorBlend));
                    h *= Mathf.Lerp(0.12f, 1f, t);
                    heights[zi, xi] = Mathf.Clamp01((BaseHeight + h) / SizeY);
                }
            }

            string dataPath = Folder + "/TestMapTerrain.asset";
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
            if (td == null)
            {
                td = new TerrainData();
                AssetDatabase.CreateAsset(td, dataPath);
            }
            td.heightmapResolution = HeightRes;
            td.size = new Vector3(SizeXZ, SizeY, SizeXZ);
            td.SetHeights(0, 0, heights);

            var grass = TerrainTextures.CreateLayer(Folder, "Grass", new Color(0.30f, 0.50f, 0.20f), 0.25f, seed + 1);
            var dirt = TerrainTextures.CreateLayer(Folder, "Dirt", new Color(0.45f, 0.34f, 0.22f), 0.25f, seed + 2);
            var floor = TerrainTextures.CreateLayer(Folder, "ForestFloor", new Color(0.18f, 0.28f, 0.13f), 0.35f, seed + 3);
            td.terrainLayers = new[] { grass, dirt, floor };
            td.alphamapResolution = AlphaRes;
            PaintLayers(td, fMinX, fMinZ, fMaxX, fMaxZ);

            float maxSlope = 0f, minH = 1f, maxH = 0f;
            int steep = 0;
            for (int zi = 1; zi < HeightRes - 1; zi++)
            {
                for (int xi = 1; xi < HeightRes - 1; xi++)
                {
                    float gx = (heights[zi, xi + 1] - heights[zi, xi - 1]) * SizeY / (2f * step);
                    float gz = (heights[zi + 1, xi] - heights[zi - 1, xi]) * SizeY / (2f * step);
                    float s = Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz)) * Mathf.Rad2Deg;
                    if (s > maxSlope) maxSlope = s;
                    if (s > 15f) steep++;
                    minH = Mathf.Min(minH, heights[zi, xi]);
                    maxH = Mathf.Max(maxH, heights[zi, xi]);
                }
            }
            EditorUtility.SetDirty(td);

            File.WriteAllText(Folder + "/forest-region.json",
                "{\"minX\":" + F(fMinX) + ",\"minZ\":" + F(fMinZ) + ",\"maxX\":" + F(fMaxX) + ",\"maxZ\":" + F(fMaxZ) + "}\n");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            PlaceInScene(td);
            Debug.Log("TestTerrainGenerator: seed " + seed + ", relief " + ((maxH - minH) * SizeY).ToString("F1")
                + " m, max slope " + maxSlope.ToString("F1") + " deg, "
                + (100f * steep / (HeightRes * HeightRes)).ToString("F2") + "% of samples over 15 deg, forest "
                + fMinX + "," + fMinZ + " to " + fMaxX + "," + fMaxZ);
        }

        static string F(float v) { return v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); }

        static void PaintLayers(TerrainData td, float minX, float minZ, float maxX, float maxZ)
        {
            var a = new float[AlphaRes, AlphaRes, 3]; // [z, x, layer]
            float cell = SizeXZ / (AlphaRes - 1);
            float mid = (CorridorMinZ + CorridorMaxZ) * 0.5f;
            for (int zi = 0; zi < AlphaRes; zi++)
            {
                for (int xi = 0; xi < AlphaRes; xi++)
                {
                    float x = xi * cell, z = zi * cell;
                    float slope = td.GetSteepness(xi / (float)(AlphaRes - 1), zi / (float)(AlphaRes - 1));
                    bool inForest = x >= minX && x <= maxX && z >= minZ && z <= maxZ;
                    float wF = inForest ? 1f : 0f;
                    float wD = (1f - wF) * Mathf.Clamp01((slope - 8f) / 6f);
                    // Dirt along the middle of the flat corridor (future trunk road).
                    if (!inForest && Mathf.Abs(z - mid) < 12f) wD = 1f;
                    a[zi, xi, 0] = 1f - wF - wD;
                    a[zi, xi, 1] = wD;
                    a[zi, xi, 2] = wF;
                }
            }
            td.SetAlphamaps(0, 0, a);
        }

        static void PlaceInScene(TerrainData td)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
                EditorSceneManager.SaveScene(scene, Folder + "/TestMap.unity");
            var old = GameObject.Find(ObjectName);
            if (old != null) Object.DestroyImmediate(old);
            var go = Terrain.CreateTerrainGameObject(td); // adds Terrain + TerrainCollider
            go.name = ObjectName;
            go.transform.position = Vector3.zero;
            go.GetComponent<Terrain>().heightmapPixelError = HeightmapPixelError;
            EditorSceneManager.MarkSceneDirty(go.scene);
            EditorSceneManager.SaveScene(go.scene);
        }
    }
}
#endif
