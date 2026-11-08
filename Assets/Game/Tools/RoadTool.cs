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

        // Node drag (RD-17): the confirmed node under a left press (-1 if none), whether it is being dragged,
        // the latest preview and target, and whether a rejection message is on the panel.
        private int _pressNode = -1;
        private bool _nodeDragging;
        private NodeMovePreview _nodeMove;
        private PointMm _nodeTarget;
        private bool _moveMessageShown;
        private bool _ignoreRightRelease;

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
            EndNodeDrag();
            _moveMessageShown = false;
            ClearDraft();
            if (_cursor != null) _cursor.Hide();
        }

        /// <inheritdoc />
        public void Cancel() { CancelNodeDrag(); ClearDraft(); }

        /// <summary>True while a confirmed road node is being dragged (RD-17).</summary>
        public bool IsDraggingNode => _nodeDragging;

        /// <summary>The node move preview of the current drag, or null.</summary>
        public NodeMovePreview NodeMove => _nodeMove;

        /// <inheritdoc />
        public bool HandleEscape()
        {
            if (_nodeDragging) { CancelNodeDrag(); HidePanel(); return true; }
            if (!HasDraft) return false;
            ClearDraft();
            return true;
        }

        /// <summary>A mouse button went down at a screen position (pixels).</summary>
        public void PointerDown(PressButton button, Vector2 screenPos)
        {
            if (!_active) return;
            if (_controller != null && _controller.IsPointerOverUiAt(screenPos)) return;
            if (_nodeDragging && button == PressButton.Right)
            {
                // Right-click during a node drag cancels it; the matching release must not delete a road.
                CancelNodeDrag();
                HidePanel();
                _ignoreRightRelease = true;
                return;
            }
            if (_moveMessageShown && !HasDraft) { _moveMessageShown = false; HidePanel(); }
            _press.Press(button, screenPos);
            _pressVertex = button == PressButton.Left ? VertexUnder(screenPos) : -1;
            _pressNode = button == PressButton.Left && !HasDraft ? NodeUnder(screenPos) : -1;
        }

        /// <summary>The cursor moved. While a press that started on a placed point is dragged, that point follows the cursor.</summary>
        public void PointerMove(Vector2 screenPos)
        {
            if (!_active) return;
            _press.Move(screenPos);
            if (_controller != null && _controller.IsPointerOverUiAt(screenPos))
            {
                if (_cursor != null) _cursor.Hide();
                return;
            }
            if (_pressNode >= 0 && _press.IsDragging(PressButton.Left))
            {
                _nodeDragging = true;
                if (_roads != null) _roads.HideNodeHandles();
                ShowNodeDragCursor(screenPos);
                MoveDraggedNode(screenPos);
                return;
            }
            UpdateCursor(screenPos);
            UpdateHoverHandles(screenPos);
            if (_pressVertex < 0 || !_press.IsDragging(PressButton.Left)) return;
            _dragIndex = _pressVertex;
            MoveDraggedPoint(screenPos);
        }

        /// <summary>A mouse button was released. <paramref name="time"/> is real seconds (for double-click detection).</summary>
        public void PointerUp(PressButton button, Vector2 screenPos, float time)
        {
            if (!_active) return;
            var r = _press.Release(button, screenPos, time);
            if (button == PressButton.Right && _ignoreRightRelease)
            {
                _ignoreRightRelease = false;
                return;
            }
            if (button == PressButton.Left && _nodeDragging)
            {
                FinishNodeDrag(screenPos);
                return;
            }
            _pressNode = -1;
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
        /// Finds the road feature under the cursor. Snapping on: radius is RoadSnapDistancePx (16), ends before centre lines
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
                ? Settings.RoadSnapDistancePx
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

        /// <summary>During a node drag the ring sits on the ground point (node drags do not snap).</summary>
        private void ShowNodeDragCursor(Vector2 screenPos)
        {
            if (_host == null || !_host.IsInitialized || _picker == null || !_picker.TryPickScreen(screenPos, out var s) || !s.HasValue)
            {
                _cursor?.Hide();
                return;
            }
            if (_cursor == null) _cursor = CursorMarker.Create(transform, _host);
            _cursor.SetShape(CursorMarkerShape.Ring, (float)RoadTypes.Find(RoadTypes.DirtPathId).WidthM);
            _cursor.Show((float)s.X, (float)s.Z, false);
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

        /// <summary>Id of the road node under the screen position (within the hit radius), or -1.</summary>
        private int NodeUnder(Vector2 screenPos)
        {
            if (_host == null || !_host.IsInitialized || _picker == null) return -1;
            var candidates = new List<HitCandidate>();
            foreach (var n in _host.World.Roads.Nodes)
            {
                if (!_picker.TryWorldToScreen((float)n.Position.XMetres, (float)n.Position.ZMetres, out var s)) continue;
                candidates.Add(HitCandidate.Point(ToolHitKind.Vertex, n.Id, s));
            }
            var hit = ToolHitTester.Test(candidates, screenPos);
            return hit.Kind == ToolHitKind.Vertex ? hit.Id : -1;
        }

        /// <summary>With no draft, shows handles at the ends of the road under the cursor (or of the road of a hovered handle).</summary>
        private void UpdateHoverHandles(Vector2 screenPos)
        {
            if (_roads == null) return;
            if (HasDraft || _press.IsDown(PressButton.Left) || _host == null || !_host.IsInitialized || _picker == null)
            {
                _roads.HideNodeHandles();
                return;
            }
            var net = _host.World.Roads;
            int segId = -1;
            int node = NodeUnder(screenPos);
            if (node >= 0)
            {
                var n = net.GetNode(node);
                if (n != null && n.SegmentIds.Count > 0) segId = n.SegmentIds[0];
            }
            else if (_picker.TryPickScreen(screenPos, out var g) && g.HasValue && TryHitRoad(screenPos, g, out var hit))
                segId = hit.SegmentId;
            var seg = segId >= 0 ? net.GetSegment(segId) : null;
            if (seg == null) { _roads.HideNodeHandles(); return; }
            var list = new List<Vector2>(2);
            var a = net.GetNode(seg.NodeA);
            var b = net.GetNode(seg.NodeB);
            if (a != null) list.Add(new Vector2((float)a.Position.XMetres, (float)a.Position.ZMetres));
            if (b != null && seg.NodeB != seg.NodeA) list.Add(new Vector2((float)b.Position.XMetres, (float)b.Position.ZMetres));
            _roads.ShowNodeHandles(list);
        }

        private void MoveDraggedNode(Vector2 screenPos)
        {
            if (_host == null || !_host.IsInitialized || _picker == null) return;
            if (!_picker.TryPickScreen(screenPos, out var s) || !s.HasValue) return;
            _nodeTarget = PointMm.FromMetres(s.X, s.Z); // no snapping while dragging a node
            _nodeMove = RoadValidator.PreviewMove(_host.World, _pressNode, _nodeTarget);
            if (_roads != null) _roads.ShowNodeMovePreview(_nodeMove, (float)RoadTypes.Find(RoadTypes.DirtPathId).WidthM);
            if (_panel == null) return;
            string verdict = _nodeMove.IsValid ? "Valid" : (_nodeMove.NodeFound ? _nodeMove.ReasonText : "node not found");
            _panel.Show(MoveLine(_nodeMove) + verdict, IdleHint, false);
        }

        private static string MoveLine(NodeMovePreview m) =>
            "Move node · " + m.Segments.Count + (m.Segments.Count == 1 ? " road · " : " roads · ");

        private void FinishNodeDrag(Vector2 screenPos)
        {
            int node = _pressNode;
            MoveDraggedNode(screenPos);
            var plan = _nodeMove;
            var target = _nodeTarget;
            EndNodeDrag();
            if (plan == null || !plan.NodeFound) { HidePanel(); return; }
            var result = _host.Execute(new MoveRoadNode(node, target));
            if (result.IsAccepted) { HidePanel(); return; }
            string reason = string.IsNullOrEmpty(result.Text) ? result.Reason.ToString() : result.Text;
            _moveMessageShown = true;
            if (_panel != null) _panel.Show(MoveLine(plan) + "Rejected: " + reason, IdleHint, false);
        }

        private void CancelNodeDrag()
        {
            if (!_nodeDragging && _pressNode < 0) return;
            EndNodeDrag();
        }

        private void EndNodeDrag()
        {
            _nodeDragging = false;
            _pressNode = -1;
            _nodeMove = null;
            if (_roads != null) { _roads.HideNodeMovePreview(); _roads.HideNodeHandles(); }
        }

        /// <summary>UIP-01/UIP-03: with nothing started the panel is hidden; the cursor ring still shows (UIP-02).</summary>
        private void HidePanel()
        {
            if (_panel != null) _panel.Hide();
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
            // UIP-01/UIP-03: no draft means no panel, even while the tool is active.
            HidePanel();
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
