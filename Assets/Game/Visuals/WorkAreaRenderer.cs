using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Operations;
using UnityEngine;

namespace LordsLegacy.Game.Visuals
{
    /// <summary>
    /// Draws the work area of the operation whose panel is open (T-282, WC-13): a dashed draped circle of the type's
    /// <c>DefaultWorkRadiusM</c> round the hut when the operation has no linked work area, or the linked work area's
    /// outline (shown through <see cref="AreaRenderer.ShownWorkAreaIds"/>). Closing the panel hides it, and every other
    /// work area stays hidden. Added at run time by <see cref="OperationPanelView"/>. Reads the Sim and holds no rules.
    /// </summary>
    public sealed class WorkAreaRenderer : MonoBehaviour
    {
        /// <summary>Number of points on the default circle before draping (the ribbon densifies to 1 m).</summary>
        public const int CirclePoints = 72;

        private SimHost _host;
        private AgentPicker _agents;
        private OperationPanelView _panel;
        private AreaRenderer _areas;
        private GameObject _circle;
        private Mesh _mesh;
        private Material _material;
        private string _signature = "";

        /// <summary>The operation whose work area is shown now, or null when the panel is closed.</summary>
        public int? ShownOperationId { get; private set; }

        /// <summary>True while the default circle object is drawn.</summary>
        public bool IsCircleShown => _circle != null && _circle.activeSelf;

        /// <summary>Sets the references (for code that adds the component at run time).</summary>
        public void Configure(SimHost host, AgentPicker agents, OperationPanelView panel)
        {
            _host = host; _agents = agents; _panel = panel;
        }

        /// <summary>
        /// Sets the <see cref="AreaRenderer"/> this component drives. Without it the first one in the scene is used.
        /// Call <see cref="Sync"/> afterwards to apply the visibility at once.
        /// </summary>
        public void UseAreaRenderer(AreaRenderer areas)
        {
            _areas = areas;
        }

        private void Update() { Sync(); }

        private void OnDisable()
        {
            ShownOperationId = null;
            HideCircle();
            var areas = Areas();
            if (areas != null) { areas.ShownWorkAreaIds.Clear(); areas.ApplyWorkAreaVisibility(); }
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }

        /// <summary>Brings the drawn work area in line with the open operation panel.</summary>
        public void Sync()
        {
            Operation op = ShownOperation(out SimHost host);
            ShownOperationId = op?.Id;

            var areas = Areas();
            if (areas != null)
            {
                areas.ShownWorkAreaIds.Clear();
                if (op != null && op.WorkAreaId.HasValue) areas.ShownWorkAreaIds.Add(op.WorkAreaId.Value);
                areas.ApplyWorkAreaVisibility();
            }

            if (op == null || op.WorkAreaId.HasValue) { HideCircle(); return; }
            var world = host.World;
            Footprint hut = OperationStatus.Hut(world, op);
            OperationTypeDef type = OperationTypes.Find(op.TypeId);
            if (hut == null || type == null) { HideCircle(); return; }

            string sig = op.Id + ":" + hut.Id + ":" + type.DefaultWorkRadiusM.ToString("R");
            if (sig != _signature || _circle == null)
            {
                _signature = sig;
                BuildCircle(world, hut, (float)type.DefaultWorkRadiusM);
            }
            _circle.SetActive(true);
        }

        private void BuildCircle(Sim.Core.SimWorld world, Footprint hut, float radius)
        {
            float cx = (float)hut.Centre.XMetres, cz = (float)hut.Centre.ZMetres;
            var pts = new List<Vector2>(CirclePoints);
            for (int i = 0; i < CirclePoints; i++)
            {
                double a = 2.0 * Math.PI * i / CirclePoints;
                pts.Add(new Vector2(cx + radius * (float)Math.Cos(a), cz + radius * (float)Math.Sin(a)));
            }
            var mesh = TerrainDrape.Ribbon(world.Terrain, pts, AreaRenderer.OutlineWidthM, AreaRenderer.HeightOffsetM + 0.01f,
                AreaRenderer.WorkOutline, true, true, AreaRenderer.DashM, AreaRenderer.GapM);
            if (_circle == null)
            {
                _circle = new GameObject("Default work area");
                _circle.transform.SetParent(transform, false);
                _circle.AddComponent<MeshFilter>();
                var mr = _circle.AddComponent<MeshRenderer>();
                mr.sharedMaterial = GetMaterial();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            if (_mesh != null) Destroy(_mesh);
            _mesh = mesh.ToMesh("Default work area");
            _circle.GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        private void HideCircle()
        {
            if (_circle != null) _circle.SetActive(false);
        }

        // The operation whose panel is open and that is selected, or null. Sets host to the Sim host (may be null).
        private Operation ShownOperation(out SimHost host)
        {
            host = Host();
            var panel = Panel();
            var agents = Agents();
            if (host == null || !host.IsInitialized || panel == null || agents == null) return null;
            if (!panel.IsShown || !agents.HasOperation) return null;
            return host.World.Operations.Get(agents.SelectedOperationId);
        }

        private SimHost Host()
        {
            if (_host == null) _host = FindFirstObjectByType<SimHost>();
            return _host;
        }

        private AgentPicker Agents()
        {
            if (_agents == null) _agents = FindFirstObjectByType<AgentPicker>();
            return _agents;
        }

        private OperationPanelView Panel()
        {
            if (_panel == null) _panel = GetComponent<OperationPanelView>();
            return _panel;
        }

        // The AreaRenderer this component drives: the one given through Configure, else the first one in the scene.
        private AreaRenderer Areas()
        {
            if (_areas == null) _areas = FindFirstObjectByType<AreaRenderer>();
            return _areas;
        }

        private Material GetMaterial()
        {
            if (_material == null)
            {
                var shared = GroundOverlay.Material();
                _material = shared != null
                    ? new Material(shared) { name = "WorkAreaMaterial" }
                    : new Material(Shader.Find("Sprites/Default")) { name = "WorkAreaMaterial" };
            }
            return _material;
        }
    }
}
