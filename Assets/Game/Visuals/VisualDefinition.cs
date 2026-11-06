using UnityEngine;

namespace LordsLegacy.Game.Visuals
{
    /// <summary>
    /// Describes how one kind of drawn thing looks: its mesh, materials and scale. Sim data refers to
    /// a definition only by its <see cref="Id"/> string, so placeholder art can be swapped for final
    /// models by editing this asset, without touching Sim data.
    /// </summary>
    [CreateAssetMenu(menuName = "Lords Legacy/Visuals/Visual Definition", fileName = "VisualDefinition")]
    public sealed class VisualDefinition : ScriptableObject
    {
        [SerializeField] private string id = "";
        [SerializeField] private Mesh mesh;
        [SerializeField] private Material[] materials = new Material[0];
        [SerializeField] private float scale = 1f;
        [SerializeField] private bool isPlaceholder = true;

        /// <summary>Stable id referenced from Sim data, e.g. 'visual.tree.generic'.</summary>
        public string Id => id;

        /// <summary>Mesh drawn for this definition. May be null only for the fallback definition's materials.</summary>
        public Mesh Mesh => mesh;

        /// <summary>Materials, one per mesh sub-mesh. Materials that enable GPU instancing are drawn instanced.</summary>
        public Material[] Materials => materials;

        /// <summary>Uniform scale applied to the mesh when drawn.</summary>
        public float Scale => scale;

        /// <summary>True while the art is a stand-in that still needs owner-supplied final art (ART-03).</summary>
        public bool IsPlaceholder => isPlaceholder;

        /// <summary>
        /// Sets the fields. Used by the editor generator and by tests that build definitions in code.
        /// </summary>
        public void Configure(string definitionId, Mesh definitionMesh, Material[] definitionMaterials, float definitionScale, bool placeholder)
        {
            id = definitionId ?? "";
            mesh = definitionMesh;
            materials = definitionMaterials ?? new Material[0];
            scale = definitionScale;
            isPlaceholder = placeholder;
        }
    }
}
