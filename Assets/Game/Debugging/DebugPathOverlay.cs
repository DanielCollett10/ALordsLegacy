using System.Collections.Generic;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.People;
using UnityEngine;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Debug only (PPL-33): draws the selected citizen's remaining path as a line draped on the Sim terrain
    /// (<see cref="TerrainDrape"/>, <see cref="GroundOverlay"/>). Rebuilt when the citizen's position or order changes.
    /// Hidden in release builds. Presentation only.
    /// </summary>
    public sealed class DebugPathOverlay : MonoBehaviour
    {
        /// <summary>Line width in metres.</summary>
        public const float WidthM = 0.2f;
        /// <summary>Lift above the Sim terrain in metres.</summary>
        public const float LiftM = 0.08f;
        private static readonly Color LineColor = new Color(0.2f, 0.9f, 1f, 0.95f);

        [SerializeField] private SimHost _host;
        [SerializeField] private AgentPicker _agents;

        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private Mesh _mesh;
        private int _lastCitizen = -1, _lastSeg = -1;
        private double _lastX = double.NaN, _lastZ = double.NaN, _lastProgress = double.NaN;
        private MoveOrder _lastOrder;

        /// <summary>True while a path line is drawn.</summary>
        public bool IsShown => _renderer != null && _renderer.enabled;

        /// <summary>Vertex count of the current mesh (0 when hidden).</summary>
        public int VertexCount => IsShown && _mesh != null ? _mesh.vertexCount : 0;

        /// <summary>Sets the references (for code that adds the component at run time).</summary>
        public void Configure(SimHost host, AgentPicker agents) { _host = host; _agents = agents; }

        private void Awake() => Ensure();

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }

        private void LateUpdate() => Draw();

        /// <summary>Updates the line from the current selection.</summary>
        public void Draw()
        {
            Ensure();
            if (_host == null) _host = FindFirstObjectByType<SimHost>();
            if (_agents == null) _agents = FindFirstObjectByType<AgentPicker>();
            if (!DebugSendController.DebugAvailable || _host == null || !_host.IsInitialized
                || _agents == null || !_agents.HasSelection || _agents.Selected.IsOx) { Hide(); return; }
            var c = _host.World.People.FindCitizen(_agents.Selected.Id);
            var order = c?.Move;
            if (c == null || order == null || order.IsFinished) { Hide(); return; }

            if (_renderer.enabled && ReferenceEquals(order, _lastOrder) && c.Id == _lastCitizen && order.SegmentIndex == _lastSeg
                && c.X == _lastX && c.Z == _lastZ && order.ProgressM == _lastProgress) return;
            _lastOrder = order; _lastCitizen = c.Id; _lastSeg = order.SegmentIndex;
            _lastX = c.X; _lastZ = c.Z; _lastProgress = order.ProgressM;

            var pts = RemainingPoints(c);
            if (pts.Count < 2) { Hide(); return; }
            var drape = TerrainDrape.Ribbon(_host.World.Terrain, pts, WidthM, LiftM, LineColor, false, false);
            if (_mesh != null) Destroy(_mesh);
            _mesh = drape.ToMesh("DebugPath");
            _filter.sharedMesh = _mesh;
            _renderer.enabled = true;
        }

        /// <summary>The citizen's position followed by the path points it has not reached yet (the target if unplanned).</summary>
        public static List<Vector2> RemainingPoints(Citizen c)
        {
            var pts = new List<Vector2> { new Vector2((float)c.X, (float)c.Z) };
            var order = c.Move;
            if (order == null) return pts;
            var path = order.Path;
            if (path == null || path.Count == 0)
            {
                pts.Add(new Vector2((float)order.TargetX, (float)order.TargetZ));
                return pts;
            }
            for (int i = Mathf.Max(order.SegmentIndex + 1, 0); i < path.Count; i++)
                pts.Add(new Vector2((float)path[i].X, (float)path[i].Z));
            return pts;
        }

        private void Hide()
        {
            if (_renderer != null) _renderer.enabled = false;
            _lastOrder = null;
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
