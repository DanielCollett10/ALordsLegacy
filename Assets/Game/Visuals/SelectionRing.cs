using System.Collections.Generic;
using LordsLegacy.Game.Visuals;
using UnityEngine;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Ring on the ground around the agent selected in <see cref="AgentPicker"/>. Built with <see cref="TerrainDrape"/>
    /// and drawn with <see cref="GroundOverlay"/>, so it follows the Sim terrain. Presentation only; no collider.
    /// </summary>
    public sealed class SelectionRing : MonoBehaviour
    {
        /// <summary>Ring radius in metres.</summary>
        public const float RadiusM = 0.9f;
        /// <summary>Ring width in metres.</summary>
        public const float WidthM = 0.18f;
        /// <summary>Lift above the Sim terrain in metres.</summary>
        public const float LiftM = 0.06f;
        private static readonly Color RingColor = new Color(1f, 0.85f, 0.2f, 0.95f);

        [SerializeField] private SimHost _host;
        [SerializeField] private AgentPicker _agents;

        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private Mesh _mesh;
        private float _lastX = float.NaN, _lastZ = float.NaN;

        /// <summary>True while a ring is drawn.</summary>
        public bool IsShown => _renderer != null && _renderer.enabled;

        /// <summary>Vertex count of the current ring mesh (0 when hidden).</summary>
        public int VertexCount => IsShown && _mesh != null ? _mesh.vertexCount : 0;

        /// <summary>Centre of the drawn ring.</summary>
        public Vector2 Centre => new Vector2(_lastX, _lastZ);

        /// <summary>Sets the references (for code that adds the component at run time).</summary>
        public void Configure(SimHost host, AgentPicker agents) { _host = host; _agents = agents; }

        private void Awake() => Ensure();

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }

        private void LateUpdate() => Draw();

        /// <summary>Updates the ring from the current selection.</summary>
        public void Draw()
        {
            Ensure();
            if (_host == null) _host = FindFirstObjectByType<SimHost>();
            if (_agents == null) _agents = FindFirstObjectByType<AgentPicker>();
            if (_host == null || !_host.IsInitialized || _agents == null || !_agents.HasSelection) { Hide(); return; }
            var p = _host.World.People;
            double x, z;
            if (_agents.Selected.IsOx)
            {
                var o = p.FindOx(_agents.Selected.Id);
                if (o == null) { Hide(); return; }
                x = o.X; z = o.Z;
            }
            else
            {
                var c = p.FindCitizen(_agents.Selected.Id);
                if (c == null) { Hide(); return; }
                x = c.X; z = c.Z;
            }
            if (!_renderer.enabled || (float)x != _lastX || (float)z != _lastZ)
            {
                _lastX = (float)x; _lastZ = (float)z;
                Rebuild(_lastX, _lastZ);
            }
            _renderer.enabled = true;
        }

        private void Hide()
        {
            if (_renderer != null) _renderer.enabled = false;
            _lastX = _lastZ = float.NaN;
        }

        private void Rebuild(float x, float z)
        {
            const int n = 28;
            var pts = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                pts.Add(new Vector2(x + Mathf.Cos(a) * RadiusM, z + Mathf.Sin(a) * RadiusM));
            }
            var drape = TerrainDrape.Ribbon(_host.World.Terrain, pts, WidthM, LiftM, RingColor, true, false);
            if (_mesh != null) Destroy(_mesh);
            _mesh = drape.ToMesh("SelectionRing");
            _filter.sharedMesh = _mesh;
        }

        private void Ensure()
        {
            if (_filter != null) return;
            _filter = gameObject.AddComponent<MeshFilter>();
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            var mat = GroundOverlay.Material();
            _renderer.sharedMaterial = mat != null ? mat : new Material(Shader.Find("Sprites/Default"));
            _renderer.enabled = false;
        }
    }
}
