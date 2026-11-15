using System.Collections.Generic;
using LordsLegacy.Game.Tools.Shared;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Terrain;
using UnityEngine;

namespace LordsLegacy.Game.Tools
{
    /// <summary>
    /// "House plot" build bar tool (homes.md HM-01..05, building-tool.md). Composes an <see cref="AreaTool"/> of the plot
    /// type (via its <see cref="AreaTool.LockHandler"/>, as <see cref="OperationDraftTool"/> does) to draw and validate the
    /// plot, shows the house ghost the Sim would place on the frontage while the draft is valid, and on Confirm sends one
    /// <see cref="DesignatePlot"/>. Refusals (no frontage, too small, no room) come from the Sim's validators; this class
    /// holds no rules. Cancel discards the draft and leaves the tool.
    /// </summary>
    public sealed class PlotTool : MonoBehaviour, ITool
    {
        /// <summary>Build bar id of the House plot entry.</summary>
        public const string HouseToolId = "house";

        private static readonly Color GhostOk = new Color(0.3f, 0.85f, 0.35f, 0.6f);

        [SerializeField] private string _toolId = HouseToolId;
        [SerializeField] private SimHost _host;
        [SerializeField] private AreaTool _areaTool;
        [SerializeField] private ToolPanelView _panel;
        [SerializeField] private ToolController _controller;

        private AreaPreview _lastPreview;
        private bool _subscribed;
        private string _message;
        private HouseGhostVisual _ghost;

        /// <inheritdoc />
        public string Id => _toolId;

        /// <inheritdoc />
        public bool HasDraft => _areaTool != null && _areaTool.HasDraft;

        /// <summary>True while this tool is the active tool.</summary>
        public bool IsActive { get; private set; }

        /// <summary>True while the house ghost is drawn on the plot's frontage.</summary>
        public bool GhostVisible => _ghost != null && _ghost.IsShown;

        /// <summary>Player-facing refusal for the current draft (no frontage, too small, no room), or null when it can be confirmed.</summary>
        public string Refusal { get; private set; }

        /// <summary>The composed area tool that draws the plot.</summary>
        public AreaTool AreaStageTool => _areaTool;

        /// <summary>Sets the references (for code that adds the component at run time).</summary>
        public void Configure(string toolId, SimHost host, AreaTool areaTool, ToolPanelView panel, ToolController controller)
        {
            Unsubscribe();
            _toolId = toolId; _host = host; _areaTool = areaTool; _panel = panel; _controller = controller;
            if (IsActive) Subscribe();
        }

        /// <inheritdoc />
        public void Activate()
        {
            if (_host == null || !_host.IsInitialized || _areaTool == null) return;
            IsActive = true;
            _message = null; Refusal = null; _lastPreview = null;
            _areaTool.LockHandler = OnLocked;
            _areaTool.Activate();
            Subscribe();
        }

        /// <inheritdoc />
        public void Deactivate()
        {
            IsActive = false;
            Unsubscribe();
            if (_areaTool != null) { _areaTool.LockHandler = null; _areaTool.Deactivate(); }
            HideGhost();
            Refusal = null; _message = null; _lastPreview = null;
        }

        /// <inheritdoc />
        public void Cancel()
        {
            if (_areaTool != null) _areaTool.Cancel();
            HideGhost();
            Refusal = null; _message = null; _lastPreview = null;
        }

        /// <inheritdoc />
        public bool HandleEscape()
        {
            if (!HasDraft) return false;
            Cancel();
            return false; // like the woodcutter: Esc drops the draft and the controller then leaves the tool
        }

        /// <summary>Confirm (button, Enter or closing the shape): sends <see cref="DesignatePlot"/> when the Sim accepts it, then the tool ends.</summary>
        public void Confirm()
        {
            if (IsActive && _areaTool != null) _areaTool.Confirm();
        }

        private void OnLocked(AreaShape shape)
        {
            if (!IsActive || _host == null) return;
            var result = _host.Execute(new DesignatePlot(shape));
            if (!result.IsAccepted)
            {
                _message = string.IsNullOrEmpty(result.Text) ? result.Reason.ToString() : result.Text;
                Refusal = _message;
                _lastPreview = _areaTool.CurrentPreview;
                OverridePanel(_areaTool.CurrentPreview, false);
                return;
            }
            if (_controller != null && _controller.ActiveTool == (ITool)this) _controller.Deselect();
            else { Cancel(); Deactivate(); }
        }

        private void OnCancelled()
        {
            if (_controller != null && _controller.ActiveTool == (ITool)this) _controller.Deselect();
            else { Cancel(); Deactivate(); }
        }

        private void LateUpdate()
        {
            if (!IsActive || _areaTool == null || _host == null || !_host.IsInitialized) return;
            AreaPreview preview = _areaTool.CurrentPreview;
            if (ReferenceEquals(preview, _lastPreview)) return;
            _lastPreview = preview;
            _message = null;
            RefreshDraft(preview);
        }

        private void RefreshDraft(AreaPreview preview)
        {
            Refusal = null;
            if (preview == null || !_areaTool.HasDraft) { HideGhost(); return; }
            if (!preview.IsValid) { HideGhost(); Refusal = preview.ReasonText; return; } // area tool's panel already says why
            AreaShape shape = _areaTool.DraftShape;
            HousePlacementResult house = HousePlacement.Place(shape, preview.Polygon, _host.World.Footprints.All);
            if (!house.Ok)
            {
                HideGhost();
                Refusal = house.Text;
                OverridePanel(preview, false);
                return;
            }
            if (_ghost == null) _ghost = new HouseGhostVisual(transform, "PlotHouseGhost");
            _ghost.Draw(_host.World.Terrain, house.Polygon, GhostOk);
            OverridePanel(preview, true);
        }

        private void HideGhost() { if (_ghost != null) _ghost.Hide(); }

        private void OverridePanel(AreaPreview preview, bool canConfirm)
        {
            if (_panel == null || preview == null) return;
            string status = "House plot · " + preview.PointCount + " points · " + preview.AreaM2.ToString("#,0") + " m²";
            status += canConfirm ? " · Valid · house on the frontage" : " · " + (_message ?? Refusal);
            _panel.Show(status, "Drag a point to move it · Click point 1, double-click or Enter closes (4+ points) · Right-click or Esc cancels", canConfirm);
        }

        private void Subscribe()
        {
            if (_subscribed || _panel == null) return;
            _panel.Cancelled += OnCancelled;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (_panel != null) _panel.Cancelled -= OnCancelled;
            _subscribed = false;
        }

        private void OnEnable()
        {
            var c = _controller != null ? _controller : FindFirstObjectByType<ToolController>();
            if (c != null) c.Register(this);
        }

        private void OnDestroy() { Unsubscribe(); if (_ghost != null) _ghost.Destroy(); }

        /// <summary>One drawn house outline (a terrain-draped fill).</summary>
        private sealed class HouseGhostVisual
        {
            private readonly GameObject _go;
            private readonly Material _material;
            private Mesh _mesh;

            public HouseGhostVisual(Transform parent, string name)
            {
                _go = new GameObject(name);
                _go.transform.SetParent(parent, false);
                _go.AddComponent<MeshFilter>();
                var mr = _go.AddComponent<MeshRenderer>();
                var shared = GroundOverlay.Material();
                _material = shared != null ? new Material(shared) : new Material(Shader.Find("Sprites/Default"));
                mr.sharedMaterial = _material;
                _go.SetActive(false);
            }

            public bool IsShown => _go != null && _go.activeSelf;

            public void Draw(IHeightField terrain, IReadOnlyList<PointMm> polygon, Color color)
            {
                var drape = TerrainDrape.Fill(terrain, polygon, 0.15f, color);
                if (_mesh != null) Object.Destroy(_mesh);
                _mesh = drape.ToMesh("PlotHouseGhost");
                _go.GetComponent<MeshFilter>().sharedMesh = _mesh;
                _go.SetActive(true);
            }

            public void Hide() { if (_go != null) _go.SetActive(false); }

            public void Destroy()
            {
                if (_mesh != null) Object.Destroy(_mesh);
                if (_material != null) Object.Destroy(_material);
                if (_go != null) Object.Destroy(_go);
            }
        }
    }
}
