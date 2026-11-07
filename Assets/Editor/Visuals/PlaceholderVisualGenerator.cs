using LordsLegacy.Game.Visuals;
using UnityEditor;
using UnityEngine;

namespace LordsLegacy.EditorTools.Visuals
{
    /// <summary>
    /// Writes the placeholder visual assets: the tree material (GPU instancing on), the
    /// <c>visual.tree.generic</c> definition and <c>Assets/Game/Visuals/VisualRegistry.asset</c>.
    /// Menu <c>LordsLegacy/Art/Generate placeholder visuals</c>. Idempotent: existing assets are updated in
    /// place, so re-running with no change leaves the files byte-identical.
    /// </summary>
    public static class PlaceholderVisualGenerator
    {
        /// <summary>Folder for all visual assets.</summary>
        public const string Folder = "Assets/Game/Visuals";
        /// <summary>Path of the registry asset.</summary>
        public const string RegistryPath = Folder + "/VisualRegistry.asset";
        /// <summary>Path of the placeholder tree definition.</summary>
        public const string TreeDefinitionPath = Folder + "/Definitions/visual.tree.generic.asset";
        /// <summary>Path of the placeholder tree material.</summary>
        public const string TreeMaterialPath = Folder + "/Materials/PlaceholderTree.mat";
        /// <summary>Path of the placeholder dirt path definition.</summary>
        public const string RoadDefinitionPath = Folder + "/Definitions/visual.road.dirt_path.asset";
        /// <summary>Path of the placeholder dirt path material.</summary>
        public const string RoadMaterialPath = Folder + "/Materials/PlaceholderRoadDirtPath.mat";
        /// <summary>Id of the placeholder dirt path definition.</summary>
        public const string RoadId = "visual.road.dirt_path";
        /// <summary>Id of the placeholder tree definition.</summary>
        public const string TreeId = "visual.tree.generic";

        /// <summary>Creates or updates the placeholder assets and saves them.</summary>
        [MenuItem("LordsLegacy/Art/Generate placeholder visuals")]
        public static void Generate()
        {
            EnsureFolder("Assets/Game", "Visuals");
            EnsureFolder(Folder, "Definitions");
            EnsureFolder(Folder, "Materials");

            var material = LoadOrCreateMaterial();
            var definition = LoadOrCreateDefinition();
            var registry = LoadOrCreateRegistry();

            // Capsule is a Unity built-in primitive: a simple stand-in until the owner's tree model arrives.
            var capsule = Resources.GetBuiltinResource<Mesh>("Capsule.fbx");
            definition.Configure(TreeId, capsule, new[] { material }, 1f, true);
            EditorUtility.SetDirty(definition);

            registry.Register(definition);

            // Road: the renderer builds its own strip meshes; the definition carries the material (and a quad
            // stand-in mesh so the definition is never mesh-less).
            var roadMaterial = LoadOrCreateRoadMaterial();
            var roadDefinition = LoadOrCreateRoadDefinition();
            roadDefinition.Configure(RoadId, Resources.GetBuiltinResource<Mesh>("Quad.fbx"), new[] { roadMaterial }, 1f, true);
            EditorUtility.SetDirty(roadDefinition);
            registry.Register(roadDefinition);
            EditorUtility.SetDirty(registry);

            AssetDatabase.SaveAssets();
            Debug.Log("Placeholder visuals written: " + TreeDefinitionPath + ", " + RegistryPath);
        }

        private static Material LoadOrCreateMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(TreeMaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard")) { name = "PlaceholderTree" };
                AssetDatabase.CreateAsset(mat, TreeMaterialPath);
            }
            mat.enableInstancing = true;
            mat.color = new Color(0.25f, 0.5f, 0.2f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material LoadOrCreateRoadMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(RoadMaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard")) { name = "PlaceholderRoadDirtPath" };
                AssetDatabase.CreateAsset(mat, RoadMaterialPath);
            }
            mat.color = new Color(0.55f, 0.42f, 0.28f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static VisualDefinition LoadOrCreateRoadDefinition()
        {
            var def = AssetDatabase.LoadAssetAtPath<VisualDefinition>(RoadDefinitionPath);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<VisualDefinition>();
                def.name = RoadId;
                AssetDatabase.CreateAsset(def, RoadDefinitionPath);
            }
            return def;
        }

        private static VisualDefinition LoadOrCreateDefinition()
        {
            var def = AssetDatabase.LoadAssetAtPath<VisualDefinition>(TreeDefinitionPath);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<VisualDefinition>();
                def.name = "visual.tree.generic";
                AssetDatabase.CreateAsset(def, TreeDefinitionPath);
            }
            return def;
        }

        private static VisualRegistry LoadOrCreateRegistry()
        {
            var reg = AssetDatabase.LoadAssetAtPath<VisualRegistry>(RegistryPath);
            if (reg == null)
            {
                reg = ScriptableObject.CreateInstance<VisualRegistry>();
                AssetDatabase.CreateAsset(reg, RegistryPath);
            }
            return reg;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
