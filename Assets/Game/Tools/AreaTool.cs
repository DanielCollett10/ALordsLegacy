using System.Collections.Generic;
using LordsLegacy.Game.Tools.Shared;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Geometry;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LordsLegacy.Game.Tools
{
    /// <summary>
    /// Area tool, part 1 (areas.md "What the player does", steps 1-3, 5, 6): one instance per area type (Test area,
    /// Test work area). Left click on the ground adds a point (up to the type's maximum). Closing (a click on point 1,
    /// a double click, Enter or Confirm) only works from the type's minimum point count. A press on a point and a drag
    /// moves it, Backspace removes the last point and a right <i>click</i> on a point removes it. A right click
    /// elsewhere or Escape or Cancel drops the draft; with no draft a right click on an area of this tool's kind sends
    /// <see cref="DeleteArea"/>. Right drags do nothing. Holds no area rules: <see cref="AreaValidator"/> and the Sim
    /// commands decide everything. Bending, edge insertion and snapping are later work.
    /// </summary>
    public sealed class AreaTool : MonoBehaviour, ITool
    {
        /// <summary>Build bar id of the Test area tool.</summary>
        public const string TestToolId = "test-area";
        /// <summary>Build bar id of the Test work area tool.</summary>
        public const string TestWorkToolId = "test-work-area";

        private const string IdleHint = "Left-click the ground to place the first point · Esc leaves the tool";
        private const string DraftHint = "Drag a point to move it · Click point 1, double-click or Enter closes (4+ points) · Backspace removes the last point · Right-click a point removes it · Right-click or Esc cancels";

        [SerializeField] private string _toolId = TestToolId;
        [SerializeField] private string _typeId = AreaTypes.TestId;
        [SerializeField] private SimHost _host;
        [SerializeField] private GroundPicker _picker;
        [SerializeField] private AreaRenderer _areas;
        [SerializeField] private ToolPanelView _panel;
        [SerializeField] private ToolController _controller;

        private readonly List<PointMm> _points = new List<PointMm>();
        private readonly PressTracker _press = new PressTracker();
        private bool _active;
        private bool _subscribed;
        private AreaPreview _preview;
        private string _rejection;
        private int _pressVertex = -1;
        private int _dragIndex = -1;
        private bool _lastReleaseAddedPoint;

        /// <inheritdoc />
        public string Id => _toolId;

        /// <summary>The Sim area type this tool draws.</summary>
        public string TypeId => _typeId;

        /// <inheritdoc />
        public bool HasDraft => _points.Count > 0;

        /// <summary>Number of placed points.</summary>
        public int PointCount => _points.Count;

        /// <summary>The placed points (read-only view).</summary>
        public IReadOnlyList<PointMm> Points => _points;

        /// <summary>The validator's preview for the current points, or null with no points.</summary>
        public AreaPreview CurrentPreview => _preview;

        /// <summary>True while this tool is the active tool.</summary>
        public bool IsActive => _active;

        private AreaTypeDef Type => AreaTypes.Find(_typeId);

        /// <summary>Sets the references and which area type this tool draws (for code that adds the component at run time).</summary>
        public void Configure(string toolId, string typeId, SimHost host, GroundPicker picker, AreaRenderer areas,
            ToolPanelView panel, ToolController controller = null)
        {
            if (_subscribed) Unsubscribe();
            _toolId = toolId; _typeId = typeId;
            _host = host; _picker = picker; _areas = areas; _panel = panel; _controller = controller;
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
            if (_controller != null && _controller.IsPointerOverUiAt(screenPos)) return;
            _press.Press(button, screenPos);
            _pressVertex = button == PressButton.Left ? VertexUnder(screenPos) : -1;
        }

        /// <summary>The cursor moved. While a press that started on a placed point is dragged, that point follows the cursor.</summary>
        public void PointerMove(Vector2 screenPos)
        {
            if (!_active) return;
            _press.Move(screenPos);
            if (_pressVertex < 0 || !_press.IsDragging(PressButton.Left)) return;
            _dragIndex = _pressVertex;
            MoveDraggedPoint(screenPos);
        }

        /// <summary>A mouse button was released. <paramref name="time"/> is real seconds (for double-click detection).</summary>
        public void PointerUp(PressButton button, Vector2 screenPos, float time)
        {
            if (!_active) return;
            var r = _press.Release(button, screenPos, time);
            int pressedVertex = _pressVertex;
            _pressVertex = -1;
            _dragIndex = -1;
            _lastReleaseAddedPoint = false;
            // A drag (of either button) never acts on release; right mouse is click-only.
            if (r.Kind == PressKind.None || r.Kind == PressKind.Drag) return;

            if (button == PressButton.Right)
            {
                RightClick(r.EndPosition);
                return;
            }
            if (r.Kind == PressKind.DoubleClick)
            {
                // The first click of the pair already added its point (or hit one); closing needs enough points.
                if (CanClose) Confirm();
                return;
            }
            if (pressedVertex >= 0)
            {
                // A click on point 1 closes the shape; a click on any other point does nothing (only a drag moves it).
                if (pressedVertex == 0 && CanClose) Confirm();
                return;
            }
            _lastReleaseAddedPoint = AddPointAt(r.EndPosition);
        }

        /// <summary>Enter pressed or Confirm clicked: sends <see cref="DesignateArea"/> if the draft is valid (4+ points).</summary>
        public void Confirm()
        {
            if (!_active || _host == null || !_host.IsInitialized || !CanClose) return;
            if (_preview == null || !_preview.IsValid) return;
            var result = _host.Execute(new DesignateArea(CurrentShape(), _typeId));
            if (result.IsAccepted)
            {
                ClearDraft();
                return;
            }
            _rejection = string.IsNullOrEmpty(result.Text) ? result.Reason.ToString() : result.Text;
            ShowPanel();
        }

        /// <summary>Backspace: removes the last placed point. The draft is cleared when no point is left.</summary>
        public void RemoveLastPoint()
        {
            if (!_active || _points.Count == 0) return;
            RemovePoint(_points.Count - 1);
        }

        private bool CanClose
        {
            get
            {
                var t = Type;
                return t != null && _points.Count >= t.MinPoints;
            }
        }

        private void RightClick(Vector2 screenPos)
        {
            int v = VertexUnder(screenPos);
            if (v >= 0) { RemovePoint(v); return; }
            if (HasDraft) ClearDraft(); else DeleteAreaUnder(screenPos);
        }

        private void RemovePoint(int index)
        {
            _points.RemoveAt(index);
            _rejection = null;
            _pressVertex = -1;
            _dragIndex = -1;
            _lastReleaseAddedPoint = false;
            Refresh();
        }

        /// <summary>Index of the placed point under the screen position (within the hit radius), or -1.</summary>
        private int VertexUnder(Vector2 screenPos)
        {
            if (_picker == null || _points.Count == 0) return -1;
            var candidates = new List<HitCandidate>(_points.Count);
            for (int i = 0; i < _points.Count; i++)
            {
                if (!_picker.TryWorldToScreen((float)_points[i].XMetres, (float)_points[i].ZMetres, out var s)) continue;
                candidates.Add(HitCandidate.Point(ToolHitKind.Vertex, i, s));
            }
            var hit = ToolHitTester.Test(candidates, screenPos);
            return hit.Kind == ToolHitKind.Vertex ? hit.Id : -1;
        }

        private bool TryGround(Vector2 screenPos, out PointMm point)
        {
            point = default;
            if (_picker == null || !_picker.TryPickScreen(screenPos, out var s) || !s.HasValue) return false;
            point = PointMm.FromMetres(s.X, s.Z);
            return true;
        }

        private void DeleteAreaUnder(Vector2 screenPos)
        {
            var t = Type;
            if (_host == null || !_host.IsInitialized || t == null || !TryGround(screenPos, out var p)) return;
            var area = _host.World.Areas.FindAt(p, t.Kind);
            if (area != null) _host.Execute(new DeleteArea(area.Id));
        }

        private void MoveDraggedPoint(Vector2 screenPos)
        {
            if (_dragIndex < 0 || _dragIndex >= _points.Count) return;
            if (!TryGround(screenPos, out var p) || p == _points[_dragIndex]) return;
            _points[_dragIndex] = p;
            _rejection = null;
            Refresh();
        }

        private bool AddPointAt(Vector2 screenPos)
        {
            var t = Type;
            if (t == null || _points.Count >= t.MaxPoints) return false;
            if (!TryGround(screenPos, out var p)) return false;
            _points.Add(p);
            _rejection = null;
            Refresh();
            return true;
        }

        private AreaShape CurrentShape() { return AreaShape.Straight(_points); }

        private void Refresh()
        {
            if (_points.Count == 0) { ClearDraft(); return; }
            var shape = CurrentShape();
            _preview = _host != null && _host.IsInitialized ? AreaValidator.Validate(_host.World, shape, _typeId) : null;
            if (_areas != null)
            {
                if (_preview != null) _areas.ShowPreview(_preview, shape);
                else _areas.HidePreview();
            }
            ShowPanel();
        }

        private string TypeName
        {
            get
            {
                var t = Type;
                return t == null ? "Area" : t.NamePrefix;
            }
        }

        private void ShowPanel()
        {
            if (_panel == null) return;
            string n = _points.Count + (_points.Count == 1 ? " point" : " points");
            bool valid = false;
            string status;
            if (_preview == null) status = TypeName + " · " + n;
            else
            {
                string verdict = _rejection != null ? "Rejected: " + _rejection
                    : _preview.IsValid ? "Valid" : _preview.ReasonText;
                status = TypeName + " · " + n + " · " + _preview.AreaM2.ToString("#,0") + " m² · " + verdict;
                valid = _preview.IsValid && _rejection == null;
            }
            _panel.Show(status, DraftHint, valid);
        }

        private void ClearDraft()
        {
            _points.Clear();
            _preview = null;
            _rejection = null;
            _pressVertex = -1;
            _dragIndex = -1;
            _lastReleaseAddedPoint = false;
            if (_areas != null) _areas.HidePreview();
            if (_panel == null) return;
            if (_active) _panel.Show(TypeName + " · no points yet", IdleHint, false);
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
            if (kb != null)
            {
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Confirm();
                if (kb.backspaceKey.wasPressedThisFrame) RemoveLastPoint();
            }
        }
    }
}
