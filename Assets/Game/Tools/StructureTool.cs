using System.Collections.Generic;
using LordsLegacy.Game.Tools.Shared;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Placement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LordsLegacy.Game.Tools
{
    /// <summary>
    /// Standalone structure tool, used for the granary (food.md FD-30, FD-30b; building-tool.md BT-03). One piece, no draft
    /// stage: the ghost follows the cursor and shows the building plus its yard outline, Q / E turn it 15° (Shift 1°), a
    /// left click asks <see cref="StructurePlacement.Preview"/> and sends <see cref="PlaceStructure"/>, a right click or
    /// Escape leaves the tool. Green when placeable, red with the Sim's reason otherwise. Holds no placement rules.
    /// </summary>
    public sealed class StructureTool : MonoBehaviour, ITool
    {
        /// <summary>Build bar id of the granary entry.</summary>
        public const string GranaryToolId = "granary";
        /// <summary>Degrees one Q / E press turns the ghost.</summary>
        public const double RotateStepDeg = 15.0;
        /// <summary>Degrees one Shift + Q / E press turns the ghost.</summary>
        public const double FineRotateStepDeg = 1.0;

        private static readonly Color Ok = new Color(0.3f, 0.85f, 0.35f, 0.6f);
        private static readonly Color Bad = new Color(0.9f, 0.25f, 0.2f, 0.6f);
        private static readonly Color YardOk = new Color(0.3f, 0.85f, 0.35f, 0.9f);
        private static readonly Color YardBad = new Color(0.9f, 0.25f, 0.2f, 0.9f);

        [SerializeField] private string _toolId = GranaryToolId;
        [SerializeField] private string _typeId = ModuleTypes.GranaryId;
        [SerializeField] private SimHost _host;
        [SerializeField] private GroundPicker _picker;
        [SerializeField] private ToolPanelView _panel;
        [SerializeField] private ToolController _controller;

        private readonly PressTracker _press = new PressTracker();
        private bool _active, _hasHover, _subscribed;
        private PointMm _centre;
        private GhostPart _fill, _yard;
        private string _rejection;

        /// <inheritdoc />
        public string Id => _toolId;
        /// <inheritdoc />
        public bool HasDraft => false;
        /// <summary>True while this tool is the active tool.</summary>
        public bool IsActive => _active;
        /// <summary>Rotation of the ghost in degrees, counter-clockwise.</summary>
        public double Rotation { get; private set; }
        /// <summary>True while the ghost (building and yard outline) is drawn.</summary>
        public bool GhostVisible => _fill != null && _fill.IsShown && _yard != null && _yard.IsShown;
        /// <summary>The Sim's answer for the ghost's current position, or null while the cursor is off the ground.</summary>
        public StructurePlacementResult Preview { get; private set; }
        /// <summary>True when the ghost is drawn in the invalid (red) colour.</summary>
        public bool GhostIsRed { get; private set; }

        /// <summary>Sets the references (for code that adds the component at run time).</summary>
        public void Configure(string toolId, string typeId, SimHost host, GroundPicker picker, ToolPanelView panel, ToolController controller)
        {
            Unsubscribe();
            _toolId = toolId; _typeId = typeId; _host = host; _picker = picker; _panel = panel; _controller = controller;
            if (_active) Subscribe();
        }

        /// <inheritdoc />
        public void Activate()
        {
            _active = true; _press.Reset(); _hasHover = false; Preview = null; _rejection = null; Rotation = 0;
            Subscribe();
            ShowPanel();
        }

        /// <inheritdoc />
        public void Deactivate()
        {
            _active = false;
            Unsubscribe();
            HideGhost();
            Preview = null; _rejection = null;
            if (_panel != null) _panel.Hide();
        }

        /// <inheritdoc />
        public void Cancel() { HideGhost(); Preview = null; }

        /// <inheritdoc />
        public bool HandleEscape() { return false; } // nothing to cancel: the controller leaves the tool

        /// <summary>Turns the ghost by <paramref name="degrees"/> (counter-clockwise) and refreshes the preview.</summary>
        public void Rotate(double degrees)
        {
            Rotation = ((Rotation + degrees) % 360.0 + 360.0) % 360.0;
            Refresh();
        }

        /// <summary>The cursor moved to a screen position (pixels).</summary>
        public void PointerMove(Vector2 screenPos)
        {
            if (!_active) return;
            _press.Move(screenPos);
            if (_controller != null && _controller.IsPointerOverUiAt(screenPos)) { _hasHover = false; HideGhost(); return; }
            if (_picker == null || !_picker.TryPickScreen(screenPos, out var g) || !g.HasValue) { _hasHover = false; HideGhost(); return; }
            var c = PointMm.FromMetres(g.X, g.Z);
            if (_hasHover && c.Equals(_centre)) return;
            _centre = c; _hasHover = true; _rejection = null;
            Refresh();
        }

        /// <summary>A mouse button went down.</summary>
        public void PointerDown(PressButton button, Vector2 screenPos)
        {
            if (!_active || GameMenuState.IsOpen) return;
            if (_controller != null && _controller.IsPointerOverUiAt(screenPos)) return;
            _press.Press(button, screenPos);
        }

        /// <summary>A mouse button was released. A left click places, a right click leaves the tool.</summary>
        public void PointerUp(PressButton button, Vector2 screenPos, float time)
        {
            if (!_active) return;
            var r = _press.Release(button, screenPos, time);
            if (r.Kind != PressKind.Click && r.Kind != PressKind.DoubleClick) return;
            if (button == PressButton.Right) { Leave(); return; }
            PointerMove(screenPos);
            Place();
        }

        /// <summary>Asks the Sim to place at the ghost's position. Shows the refusal text when it says no.</summary>
        public void Place()
        {
            if (!_active || !_hasHover || _host == null || !_host.IsInitialized) return;
            var p = StructurePlacement.Preview(_host.World, _typeId, _centre, Rotation);
            if (!p.Ok) { _rejection = p.Text; Refresh(); return; }
            var result = _host.Execute(new PlaceStructure(_typeId, _centre, Rotation));
            if (!result.IsAccepted)
            {
                _rejection = string.IsNullOrEmpty(result.Text) ? result.Reason.ToString() : result.Text;
                ShowPanel();
                return;
            }
            _rejection = null;
            Refresh();
        }

        private void Leave()
        {
            if (_controller != null && _controller.ActiveTool == (ITool)this) _controller.Deselect();
            else { Cancel(); Deactivate(); }
        }

        private void Refresh()
        {
            if (!_active || !_hasHover || _host == null || !_host.IsInitialized) return;
            Preview = StructurePlacement.Preview(_host.World, _typeId, _centre, Rotation);
            if (Preview.Building.Length < 3) { HideGhost(); ShowPanel(); return; }
            GhostIsRed = !Preview.Ok;
            if (_fill == null) _fill = new GhostPart(transform, "StructureGhostFill");
            if (_yard == null) _yard = new GhostPart(transform, "StructureGhostYard");
            var terrain = _host.World.Terrain;
            _fill.Show(TerrainDrape.Fill(terrain, Preview.Building, 0.15f, GhostIsRed ? Bad : Ok).ToMesh("StructureGhostFill"));
            var outline = new List<Vector2>(Preview.Plot.Length);
            foreach (var pt in Preview.Plot) outline.Add(new Vector2((float)pt.XMetres, (float)pt.ZMetres));
            _yard.Show(TerrainDrape.Ribbon(terrain, outline, 0.3f, 0.2f, GhostIsRed ? YardBad : YardOk, true, false).ToMesh("StructureGhostYard"));
            ShowPanel();
        }

        private void ShowPanel()
        {
            if (_panel == null) return;
            string status = "Granary · " + Rotation.ToString("0") + "°";
            if (!string.IsNullOrEmpty(_rejection)) status += " · " + _rejection;
            else if (Preview != null) status += Preview.Ok ? " · Valid · click to place" : " · " + Preview.Text;
            _panel.Show(status, "Click places · Q / E turn 15° (Shift 1°) · Right-click or Esc leaves the tool", false);
        }

        private void HideGhost() { if (_fill != null) _fill.Hide(); if (_yard != null) _yard.Hide(); }

        private void Subscribe()
        {
            if (_subscribed || _panel == null) return;
            _panel.Cancelled += Leave;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (_panel != null) _panel.Cancelled -= Leave;
            _subscribed = false;
        }

        private void OnEnable()
        {
            var c = _controller != null ? _controller : FindFirstObjectByType<ToolController>();
            if (c != null) c.Register(this);
        }

        private void OnDestroy() { Unsubscribe(); if (_fill != null) _fill.Destroy(); if (_yard != null) _yard.Destroy(); }

        private void Update()
        {
            if (!_active || GameMenuState.IsOpen) return;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 pos = mouse.position.ReadValue();
                float t = Time.unscaledTime;
                if (mouse.leftButton.wasPressedThisFrame) PointerDown(PressButton.Left, pos);
                if (mouse.rightButton.wasPressedThisFrame) PointerDown(PressButton.Right, pos);
                PointerMove(pos);
                if (mouse.leftButton.wasReleasedThisFrame) PointerUp(PressButton.Left, pos, t);
                if (mouse.rightButton.wasReleasedThisFrame) PointerUp(PressButton.Right, pos, t);
            }
            var kb = Keyboard.current;
            if (kb != null)
            {
                double step = kb.shiftKey.isPressed ? FineRotateStepDeg : RotateStepDeg;
                if (kb.qKey.wasPressedThisFrame) Rotate(step);
                if (kb.eKey.wasPressedThisFrame) Rotate(-step);
            }
        }

        /// <summary>One drawn, terrain-draped mesh object.</summary>
        private sealed class GhostPart
        {
            private readonly GameObject _go;
            private readonly Material _material;
            private Mesh _mesh;

            public GhostPart(Transform parent, string name)
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

            public void Show(Mesh mesh)
            {
                if (_mesh != null) Object.Destroy(_mesh);
                _mesh = mesh;
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
