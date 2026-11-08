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
    /// Road tool (roads.md steps 1-4): left click on the ground adds a point, a press on a placed point and a drag moves
    /// it (the preview follows), Backspace removes the last point, a double click adds one final point at the cursor and
    /// confirms, Enter or Confirm sends <see cref="DrawRoad"/>, a right click (not a right drag) or Escape cancels.
    /// Holds no road rules: <see cref="RoadValidator"/> and the Sim command decide everything. Branching and deleting
    /// are in the world-click tools (roads.md steps 5-6).
    /// </summary>
    public sealed class RoadTool : MonoBehaviour, ITool
    {
        /// <summary>Id matching the build bar entry.</summary>
        public const string ToolId = "road";

        private const string IdleHint = "Left-click the ground to place the first point · Esc leaves the tool";
        private const string DraftHint = "Drag a point to move it · Backspace removes the last point · Double-click or Enter confirms · Right-click or Esc cancels";

        [SerializeField] private SimHost _host;
        [SerializeField] private GroundPicker _picker;
        [SerializeField] private RoadRenderer _roads;
        [SerializeField] private ToolPanelView _panel;
        [SerializeField] private TopBarController _topBar;

        private ViewSettings _settings;
        private readonly List<PointMm> _points = new List<PointMm>();
        private readonly PressTracker _press = new PressTracker();
        [SerializeField] private ToolController _controller;
        private bool _active;
        private RoadPreview _preview;
        private CursorMarker _cursor;
        private string _rejection;
        private bool _subscribed;

        // Left-press state: the placed point under the press (-1 if none), the point being dragged (-1 if none),
        // and whether the last left release added a point (a double click then replaces that point with its own).
        private int _pressVertex = -1;
        private int _dragIndex = -1;
        private bool _lastReleaseAddedPoint;

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
            if (_cursor != null) _cursor.Configure(host);
            if (_active) Subscribe();
        }

        /// <summary>Sets the view settings (snapping on/off and distance). Without them the top bar's, or defaults (snapping on, 12 px).</summary>
        public void SetViewSettings(ViewSettings settings) { _settings = settings; }

        private ViewSettings Settings
        {
            get
            {
                if (_settings != null) return _settings;
                // Unwired scene: use the top bar's shared settings so the Snapping toggle still works.
                if (_topBar == null) _topBar = FindFirstObjectByType<TopBarController>();
                return _topBar != null ? _topBar.ViewSettings : (_settings = new ViewSettings());
            }
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
            if (_cursor != null) _cursor.Hide();
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
            UpdateCursor(screenPos);
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
            bool pressedVertex = _pressVertex >= 0;
            bool previousClickAddedPoint = _lastReleaseAddedPoint;
            _pressVertex = -1;
            _dragIndex = -1;
            _lastReleaseAddedPoint = false;
            if (r.Kind == PressKind.None || r.Kind == PressKind.Drag) return;
            if (button == PressButton.Right)
            {
                if (HasDraft) ClearDraft(); else DeleteRoadUnder(r.EndPosition);
                return;
            }
            if (r.Kind == PressKind.DoubleClick)
            {
                FinishWithDoubleClick(r.EndPosition, previousClickAddedPoint);
                return;
            }
            // A click on a placed point does nothing: only a drag moves it.
            if (pressedVertex) return;
            _lastReleaseAddedPoint = AddPointAt(r.EndPosition);
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

        /// <summary>
        /// Backspace (roads.md step 3): removes the last placed point. The draft is cleared when no point is left.
        /// </summary>
        public void RemoveLastPoint()
        {
            if (!_active || _points.Count == 0) return;
            _points.RemoveAt(_points.Count - 1);
            _rejection = null;
            _pressVertex = -1;
            _dragIndex = -1;
            _lastReleaseAddedPoint = false;
            Refresh();
        }

        /// <summary>
        /// Double click (roads.md step 4): the first click of the pair has already added a point at (almost) the same
        /// place, so that point is replaced by one final point at the cursor, and the road is confirmed. The pair adds
        /// exactly one point.
        /// </summary>
        private void FinishWithDoubleClick(Vector2 screenPos, bool replacePreviousClickPoint)
        {
            if (replacePreviousClickPoint && _points.Count > 0) _points.RemoveAt(_points.Count - 1);
            _rejection = null;
            AddPointAt(screenPos);
            Refresh();
            Confirm();
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

        /// <summary>Metres per screen pixel near the position (0 if unknown).</summary>
        private double MetresPerPixel(Vector2 screenPos, GroundSample g)
        {
            if (!_picker.TryPickScreen(screenPos + Vector2.right, out var g2) || !g2.HasValue) return 0;
            double dx = g2.X - g.X, dz = g2.Z - g.Z;
            return System.Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Finds the road feature under the cursor. Snapping on: radius is SnapDistancePx, ends before centre lines
        /// (RoadNetwork.FindNearest + SnapPicker). Snapping off: only the drawn road width counts, so a click on a road
        /// still hits it (roads.md steps 5-6) but nothing near it snaps.
        /// </summary>
        private bool TryHitRoad(Vector2 screenPos, GroundSample g, out RoadHit hit)
        {
            hit = RoadHit.None;
            if (_host == null || !_host.IsInitialized || _picker == null) return false;
            double mpp = MetresPerPixel(screenPos, g);
            if (mpp <= 0) return false;
            double pxLimit = Settings.Snapping
                ? Settings.SnapDistancePx
                : RoadTypes.Find(RoadTypes.DirtPathId).WidthM * 0.5 / mpp;
            double maxMm = pxLimit * mpp * 1000.0 * 1.5 + 1;
            var h = _host.World.Roads.FindNearest(PointMm.FromMetres(g.X, g.Z), maxMm);
            if (h.Kind == RoadHitKind.None) return false;
            if (!_picker.TryWorldToScreen((float)h.Point.XMetres, (float)h.Point.ZMetres, out var hs)) return false;
            var cand = new List<SnapCandidate> { new SnapCandidate((int)h.Kind, h.SegmentId, (hs - screenPos).magnitude) };
            var order = new[] { (int)RoadHitKind.End, (int)RoadHitKind.Line };
            if (!SnapPicker.TryPick(cand, order, (float)pxLimit, true, out _)) return false;
            hit = h;
            return true;
        }

        /// <summary>
        /// Shows the cursor ring (road width) at the point the next click would use; snapped colour when it snaps.
        /// </summary>
        private void UpdateCursor(Vector2 screenPos)
        {
            if (_host == null || !_host.IsInitialized || _picker == null || !_picker.TryPickScreen(screenPos, out var s) || !s.HasValue)
            {
                if (_cursor != null) _cursor.Hide();
                return;
            }
            if (!TryResolvePoint(screenPos, out var p)) { _cursor?.Hide(); return; }
            if (_cursor == null) _cursor = CursorMarker.Create(transform, _host);
            _cursor.SetShape(CursorMarkerShape.Ring, (float)RoadTypes.Find(RoadTypes.DirtPathId).WidthM);
            bool snapped = !p.Equals(PointMm.FromMetres(s.X, s.Z));
            _cursor.Show((float)p.XMetres, (float)p.ZMetres, snapped);
        }

        /// <summary>The point for a click or drag: the snapped road point when snapping applies, else the ground point.</summary>
        private bool TryResolvePoint(Vector2 screenPos, out PointMm point)
        {
            point = default;
            if (_picker == null || !_picker.TryPickScreen(screenPos, out var s) || !s.HasValue) return false;
            point = PointMm.FromMetres(s.X, s.Z);
            // Snapping off: only a click on the road itself with no draft starts a branch.
            if ((Settings.Snapping || !HasDraft) && TryHitRoad(screenPos, s, out var hit)) point = hit.Point;
            return true;
        }

        private void DeleteRoadUnder(Vector2 screenPos)
        {
            if (_host == null || _picker == null || !_picker.TryPickScreen(screenPos, out var s) || !s.HasValue) return;
            if (!TryHitRoad(screenPos, s, out var hit)) return;
            _host.Execute(new DeleteRoadSegment(hit.SegmentId));
        }

        private void MoveDraggedPoint(Vector2 screenPos)
        {
            if (_dragIndex < 0 || _dragIndex >= _points.Count) return;
            if (!TryResolvePoint(screenPos, out var p)) return;
            if (p == _points[_dragIndex]) return;
            _points[_dragIndex] = p;
            _rejection = null;
            Refresh();
        }

        private bool AddPointAt(Vector2 screenPos)
        {
            if (!TryResolvePoint(screenPos, out var p)) return false;
            _points.Add(p);
            _rejection = null;
            Refresh();
            return true;
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
            _pressVertex = -1;
            _dragIndex = -1;
            _lastReleaseAddedPoint = false;
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
            if (kb != null)
            {
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Confirm();
                if (kb.backspaceKey.wasPressedThisFrame) RemoveLastPoint();
            }
        }
    }
}
