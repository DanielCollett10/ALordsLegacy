using System.Collections.Generic;
using LordsLegacy.Game.Tools.Shared;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LordsLegacy.Game.Tools
{
    /// <summary>
    /// Road tool, part 1 (roads.md steps 1-4): left click on the ground adds a point, the preview and tool panel follow
    /// the points, Enter or Confirm sends <see cref="DrawRoad"/>, a right click (not a right drag) or Escape cancels.
    /// Holds no road rules: <see cref="RoadValidator"/> and the Sim command decide everything. Dragging points,
    /// Backspace, double-click finish, branching and deleting come later.
    /// </summary>
    public sealed class RoadTool : MonoBehaviour, ITool
    {
        /// <summary>Id matching the build bar entry.</summary>
        public const string ToolId = "road";

        private const string IdleHint = "Left-click the ground to place the first point · Esc leaves the tool";
        private const string DraftHint = "Left-click adds a point · Enter confirms · Right-click or Esc cancels";

        [SerializeField] private SimHost _host;
        [SerializeField] private GroundPicker _picker;
        [SerializeField] private RoadRenderer _roads;
        [SerializeField] private ToolPanelView _panel;

        private readonly List<PointMm> _points = new List<PointMm>();
        private readonly PressTracker _press = new PressTracker();
        [SerializeField] private ToolController _controller;
        private bool _active;
        private RoadPreview _preview;
        private string _rejection;
        private bool _subscribed;

        /// <inheritdoc />
        public string Id => ToolId;

        /// <inheritdoc />
        public bool HasDraft => _points.Count > 0;

        /// <summary>Number of placed points.</summary>
        public int PointCount => _points.Count;

        /// <summary>The placed points (read-only view).</summary>
        public IReadOnlyList<PointMm> Points => _points;

        /// <summary>The preview for the current points, or null with fewer than two.</summary>
        public RoadPreview CurrentPreview => _preview;

        /// <summary>True while this tool is the active tool.</summary>
        public bool IsActive => _active;

        /// <summary>Sets the references (for code that adds the component at run time). The controller is optional.</summary>
        public void Configure(SimHost host, GroundPicker picker, RoadRenderer roads, ToolPanelView panel, ToolController controller = null)
        {
            if (_subscribed) Unsubscribe();
            _host = host; _picker = picker; _roads = roads; _panel = panel; _controller = controller;
            if (_active) Subscribe();
        }

        /// <inheritdoc />
        public void Activate()
        {
            _active = true;
            _press.Reset();
            Subscribe();
            Refresh();
        }

        /// <inheritdoc />
        public void Deactivate()
        {
            _active = false;
            Unsubscribe();
            ClearDraft();
        }

        /// <inheritdoc />
        public void Cancel() { ClearDraft(); }

        /// <inheritdoc />
        public bool HandleEscape()
        {
            if (!HasDraft) return false;
            ClearDraft();
            return true;
        }

        /// <summary>A mouse button went down at a screen position (pixels).</summary>
        public void PointerDown(PressButton button, Vector2 screenPos)
        {
            if (!_active) return;
            if (_controller != null && _controller.IsPointerOverUi) return;
            _press.Press(button, screenPos);
        }

        /// <summary>The cursor moved.</summary>
        public void PointerMove(Vector2 screenPos) { if (_active) _press.Move(screenPos); }

        /// <summary>A mouse button was released. <paramref name="time"/> is real seconds (for double-click detection).</summary>
        public void PointerUp(PressButton button, Vector2 screenPos, float time)
        {
            if (!_active) return;
            var r = _press.Release(button, screenPos, time);
            if (r.Kind == PressKind.None || r.Kind == PressKind.Drag) return;
            if (button == PressButton.Right) { if (HasDraft) ClearDraft(); return; }
            // Double-click finishing is later work; for now it is just another click.
            AddPointAt(r.EndPosition);
        }

        /// <summary>Enter pressed or Confirm clicked: sends <see cref="DrawRoad"/> if the draft is valid.</summary>
        public void Confirm()
        {
            if (!_active || _preview == null || !_preview.IsValid) return;
            var result = _host.Execute(new DrawRoad(_points.ToArray(), RoadTypes.DirtPathId));
            if (result.IsAccepted)
            {
                ClearDraft();
                return;
            }
            _rejection = string.IsNullOrEmpty(result.Text) ? result.Reason.ToString() : result.Text;
            ShowPanel();
        }

        private void AddPointAt(Vector2 screenPos)
        {
            if (_picker == null || !_picker.TryPickScreen(screenPos, out var s) || !s.HasValue) return;
            _points.Add(PointMm.FromMetres(s.X, s.Z));
            _rejection = null;
            Refresh();
        }

        private void Refresh()
        {
            if (_points.Count == 0) { ClearDraft(); return; }
            _preview = _points.Count >= 2 && _host != null && _host.IsInitialized
                ? RoadValidator.Validate(_host.World, _points, RoadTypes.DirtPathId)
                : null;
            if (_roads != null)
            {
                if (_preview != null) _roads.ShowPreview(_preview, (float)RoadTypes.Find(RoadTypes.DirtPathId).WidthM);
                else _roads.HidePreview();
            }
            ShowPanel();
        }

        private void ShowPanel()
        {
            if (_panel == null) return;
            string n = _points.Count + (_points.Count == 1 ? " point" : " points");
            string status;
            bool valid = false;
            if (_preview == null) status = "Road · " + n + " · add another point";
            else
            {
                string verdict = _rejection != null ? "Rejected: " + _rejection
                    : _preview.IsValid ? "Valid" : _preview.ReasonText;
                status = "Road · " + n + " · " + _preview.LengthM.ToString("0") + " m · max grade "
                    + (_preview.MaxGrade * 100).ToString("0") + "% · " + verdict;
                valid = _preview.IsValid;
            }
            _panel.Show(status, DraftHint, valid);
        }

        private void ClearDraft()
        {
            _points.Clear();
            _preview = null;
            _rejection = null;
            if (_roads != null) _roads.HidePreview();
            if (_panel == null) return;
            // While the tool is active the panel stays up so the player can see it is waiting for a first click.
            if (_active) _panel.Show("Road · no points yet", IdleHint, false);
            else _panel.Hide();
        }

        private void Subscribe()
        {
            if (_subscribed || _panel == null) return;
            _panel.Cancelled += OnPanelCancelled;
            _panel.Confirmed += Confirm;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (_panel != null) { _panel.Cancelled -= OnPanelCancelled; _panel.Confirmed -= Confirm; }
            _subscribed = false;
        }

        private void OnPanelCancelled() { ClearDraft(); }

        private void OnEnable()
        {
            if (_controller != null) _controller.Register(this);
        }

        private void OnDestroy() { Unsubscribe(); }

        private void Update()
        {
            if (!_active) return;
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
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) Confirm();
        }
    }
}
