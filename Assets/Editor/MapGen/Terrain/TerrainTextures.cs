#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LordsLegacy.EditorTools.MapGen
{
    /// <summary>Generates flat-colour-plus-noise placeholder ground textures and TerrainLayer assets.</summary>
    public static class TerrainTextures
    {
        const int Size = 128;

        /// <summary>Creates (or refreshes) a PNG and a TerrainLayer for one ground type. PLACEHOLDER art.</summary>
        public static TerrainLayer CreateLayer(string folder, string name, Color baseColor, float variation, int seed)
        {
            string pngPath = folder + "/" + name + ".png";
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            float ox = seed * 17.31f, oz = seed * 5.77f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // Circular sampling keeps the texture roughly tileable.
                    float u = x / (float)Size * Mathf.PI * 2f, v = y / (float)Size * Mathf.PI * 2f;
                    float n = Mathf.PerlinNoise(ox + 3f + 2f * Mathf.Cos(u), oz + 3f + 2f * Mathf.Sin(u) + 2f * Mathf.Cos(v)) * 0.6f
                            + Mathf.PerlinNoise(ox + 9f * Mathf.Sin(v) + 20f, oz + 9f * Mathf.Cos(v) + 20f) * 0.4f;
                    float k = 1f + (n - 0.5f) * 2f * variation;
                    tex.SetPixel(x, y, new Color(baseColor.r * k, baseColor.g * k, baseColor.b * k, 1f));
                }
            }
            tex.Apply();
            File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(pngPath);
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.SaveAndReimport();

            string layerPath = folder + "/" + name + ".terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, layerPath);
            }
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            layer.tileSize = new Vector2(8f, 8f);
            EditorUtility.SetDirty(layer);
            return layer;
        }
    }
}
#endif
