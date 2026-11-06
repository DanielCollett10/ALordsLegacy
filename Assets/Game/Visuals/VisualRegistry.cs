using System.Collections.Generic;
using UnityEngine;

namespace LordsLegacy.Game.Visuals
{
    /// <summary>
    /// Maps visual ids to <see cref="VisualDefinition"/> assets. Asset: <c>Assets/Game/Visuals/VisualRegistry.asset</c>.
    /// Lookup by id: <see cref="TryGet"/> returns false for unknown ids; <see cref="Get"/> logs an error and
    /// returns a fallback placeholder so the view still draws something.
    /// </summary>
    [CreateAssetMenu(menuName = "Lords Legacy/Visuals/Visual Registry", fileName = "VisualRegistry")]
    public sealed class VisualRegistry : ScriptableObject
    {
        /// <summary>Id of the built-in fallback definition returned for unknown ids.</summary>
        public const string FallbackId = "visual.fallback";

        [SerializeField] private List<VisualDefinition> definitions = new List<VisualDefinition>();

        private Dictionary<string, VisualDefinition> _byId;
        private VisualDefinition _fallback;

        /// <summary>All registered definitions, in registration order.</summary>
        public IReadOnlyList<VisualDefinition> Definitions => definitions;

        /// <summary>
        /// Adds a definition. Used by the editor generator and by tests that build a registry in code.
        /// Duplicate ids and null entries are rejected with an error log and not added.
        /// </summary>
        public void Register(VisualDefinition definition)
        {
            if (definition == null)
            {
                Debug.LogError("VisualRegistry.Register: definition is null.");
                return;
            }
            if (definition.Id.Length == 0)
            {
                Debug.LogError("VisualRegistry.Register: definition has an empty id.", definition);
                return;
            }
            if (TryGet(definition.Id, out var existing) && existing != definition)
            {
                Debug.LogError("VisualRegistry.Register: duplicate id '" + definition.Id + "'.", definition);
                return;
            }
            if (!definitions.Contains(definition)) definitions.Add(definition);
            _byId = null;
        }

        /// <summary>Returns the definition with this id, or false when no definition has it.</summary>
        public bool TryGet(string id, out VisualDefinition definition)
        {
            EnsureIndex();
            return _byId.TryGetValue(id ?? "", out definition);
        }

        /// <summary>
        /// Returns the definition with this id. An unknown id logs an error and returns the fallback
        /// placeholder definition.
        /// </summary>
        public VisualDefinition Get(string id)
        {
            if (TryGet(id, out var definition)) return definition;
            Debug.LogError("VisualRegistry: unknown visual id '" + id + "'. Using the fallback placeholder.");
            return Fallback;
        }

        /// <summary>
        /// The placeholder drawn for unknown ids: a built-in cube, marked as placeholder. Created once per registry.
        /// </summary>
        public VisualDefinition Fallback
        {
            get
            {
                if (_fallback == null)
                {
                    _fallback = CreateInstance<VisualDefinition>();
                    _fallback.name = FallbackId;
                    var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                    _fallback.Configure(FallbackId, cube, new Material[0], 1f, true);
                }
                return _fallback;
            }
        }

        /// <summary>Definitions still marked as placeholder (ART-03).</summary>
        public List<VisualDefinition> Placeholders()
        {
            var result = new List<VisualDefinition>();
            foreach (var d in definitions)
            {
                if (d != null && d.IsPlaceholder) result.Add(d);
            }
            return result;
        }

        private void OnValidate()
        {
            _byId = null;
        }

        private void EnsureIndex()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, VisualDefinition>();
            foreach (var d in definitions)
            {
                if (d == null || d.Id.Length == 0) continue;
                if (!_byId.ContainsKey(d.Id)) _byId.Add(d.Id, d);
            }
        }
    }
}
