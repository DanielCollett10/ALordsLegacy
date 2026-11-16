using System.Collections.Generic;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Markets;
using UnityEngine;

namespace LordsLegacy.Game.Tools
{
    /// <summary>
    /// "Market" build bar tool (markets.md MK-01, MK-04, MK-05, MK-41). Composes an <see cref="AreaTool"/> of the market
    /// type (via its <see cref="AreaTool.LockHandler"/>, as <see cref="PlotTool"/> does) to draw and validate the area. While
    /// the draft is valid it outlines the stall slots the Sim would make (<see cref="StallGrid"/>) and tints each house green
    /// (in <see cref="MarketRange"/>) or red (out of range). Confirm sends <see cref="DesignateArea"/> for the market type,
    /// which registers the market. Refusals (too small, one market per town, overlaps) come from the Sim; this class holds
    /// no rules. The build bar entry's disabled state is <see cref="MarketRules.CanPlaceMarket"/>.
    /// </summary>
    public sealed class MarketTool : MonoBehaviour, ITool
    {
        /// <summary>Colour of a house in market range.</summary>
        public static readonly Color InRangeColor = new Color(0.25f, 0.85f, 0.3f, 0.55f);
        /// <summary>Colour of a house out of market range.</summary>
        public static readonly Color OutOfRangeColor = new Color(0.9f, 0.2f, 0.2f, 0.55f);

        [SerializeField] private string _toolId = ToolEntry.MarketToolId;
        [SerializeField] private SimHost _host;
        [SerializeField] private AreaTool _areaTool;
        [SerializeField] private ToolPanelView _panel;
        [SerializeField] private ToolController _controller;

        private AreaPreview _lastPreview;
        private bool _subscribed;
        private GameObject _overlay;
        private Mesh _mesh;
        private Material _material;

        /// <inheritdoc />
        public string Id => _toolId;

        /// <inheritdoc />
        public bool HasDraft => _areaTool != null && _areaTool.HasDraft;

        /// <summary>True while this tool is the active tool.</summary>
        public bool IsActive { get; private set; }

        /// <summary>Player-facing refusal for the current draft, or null when it can be confirmed.</summary>
        public string Refusal { get; private set; }

        /// <summary>Stall slot outlines drawn for the current draft.</summary>
        public int SlotCount { get; private set; }

        /// <summary>Houses tinted green (in range) for the current draft.</summary>
        public int InRangeCount { get; private set; }

        /// <summary>Houses tinted red (out of range) for the current draft.</summary>
        public int OutOfRangeCount { get; private set; }

        /// <summary>True while the slot and range overlay is drawn.</summary>
        public bool OverlayVisible => _overlay != null && _overlay.activeSelf;

        /// <summary>The composed area tool that draws the market.</summary>
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
            Refusal = null; _lastPreview = null;
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
            HideOverlay();
            Refusal = null; _lastPreview = null;
        }

        /// <inheritdoc />
        public void Cancel()
        {
            if (_areaTool != null) _areaTool.Cancel();
            HideOverlay();
            Refusal = null; _lastPreview = null;
        }

        /// <inheritdoc />
        public bool HandleEscape()
        {
            if (!HasDraft) return false;
            Cancel();
            return false; // Esc drops the draft and the controller then leaves the tool
        }

        /// <summary>Confirm (button, Enter or closing the shape): sends <see cref="DesignateArea"/> when the Sim accepts it, then the tool ends.</summary>
        public void Confirm()
        {
            if (IsActive && _areaTool != null) _areaTool.Confirm();
        }

        private void OnLocked(AreaShape shape)
        {
            if (!IsActive || _host == null) return;
            var result = _host.Execute(new DesignateArea(shape, AreaTypes.MarketId));
            if (!result.IsAccepted)
            {
                Refusal = string.IsNullOrEmpty(result.Text) ? result.Reason.ToString() : result.Text;
                _lastPreview = _areaTool.CurrentPreview;
                if (_panel != null) _panel.Show("Market · Rejected: " + Refusal, "Right-click or Esc cancels", false);
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
            RefreshDraft(preview);
        }

        private void RefreshDraft(AreaPreview preview)
        {
            Refusal = null;
            if (preview == null || !_areaTool.HasDraft) { HideOverlay(); return; }
            if (!preview.IsValid) { HideOverlay(); Refusal = preview.ReasonText; return; } // the area tool's panel says why
            DrawOverlay(preview.Polygon);
        }

        private void DrawOverlay(IReadOnlyList<PointMm> polygon)
        {
            var world = _host.World;
            var terrain = world.Terrain;
            var mesh = new DrapeMesh();
            var slots = StallGrid.Slots(polygon, world.Tuning);
            foreach (var s in slots)
                mesh.Append(TerrainDrape.Ribbon(terrain, Plan(s.Stall), AreaRenderer.SlotOutlineWidthM, AreaRenderer.HeightOffsetM + 0.1f,
                    AreaRenderer.SlotOutline, true, false));
            var range = MarketRange.HousesInRange(world, Polygons.Centroid(polygon));
            foreach (int id in range.InRange) AddHouse(mesh, terrain, id, InRangeColor);
            foreach (int id in range.OutOfRange) AddHouse(mesh, terrain, id, OutOfRangeColor);
            SlotCount = slots.Count; InRangeCount = range.InRange.Count; OutOfRangeCount = range.OutOfRange.Count;

            if (_overlay == null)
            {
                _overlay = new GameObject("MarketPreviewOverlay");
                _overlay.transform.SetParent(transform, false);
                _overlay.AddComponent<MeshFilter>();
                var mr = _overlay.AddComponent<MeshRenderer>();
                var shared = GroundOverlay.Material();
                _material = shared != null ? new Material(shared) : new Material(Shader.Find("Sprites/Default"));
                mr.sharedMaterial = _material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            if (_mesh != null) Destroy(_mesh);
            _mesh = mesh.ToMesh("MarketPreviewOverlay");
            _overlay.GetComponent<MeshFilter>().sharedMesh = _mesh;
            _overlay.SetActive(true);
        }

        private void AddHouse(DrapeMesh mesh, LordsLegacy.Sim.Terrain.IHeightField terrain, int footprintId, Color colour)
        {
            var fp = _host.World.Footprints.Get(footprintId);
            if (fp != null) mesh.Append(TerrainDrape.Fill(terrain, fp.Polygon, AreaRenderer.HeightOffsetM + 0.12f, colour));
        }

        private static List<Vector2> Plan(IReadOnlyList<PointMm> pts)
        {
            var l = new List<Vector2>(pts.Count);
            foreach (var p in pts) l.Add(new Vector2((float)p.XMetres, (float)p.ZMetres));
            return l;
        }

        private void HideOverlay()
        {
            SlotCount = 0; InRangeCount = 0; OutOfRangeCount = 0;
            if (_overlay != null) _overlay.SetActive(false);
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

        private void OnDestroy()
        {
            Unsubscribe();
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
            if (_overlay != null) Destroy(_overlay);
        }
    }
}
