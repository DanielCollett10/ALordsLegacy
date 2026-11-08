using System.Collections.Generic;
using LordsLegacy.Game.Tools.Shared;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;
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
    /// commands decide everything. Part 2: a click on an edge inserts a point, dragging an edge's middle handle bends it, a double click on the
    /// handle straightens it, and with snapping on points snap to area vertices, road side lines and area edges (two
    /// consecutive points on the same road side make that edge FollowsRoad).
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
        private readonly List<AreaEdge> _edges = new List<AreaEdge>();
        private readonly List<int> _roadSeg = new List<int>();
        private readonly List<RoadSide> _roadSide = new List<RoadSide>();
        private ViewSettings _settings;
        [SerializeField] private TopBarController _topBar;
        private int _pressHandle = -1;
        private int _pressEdge = -1;
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

        /// <summary>Sets the view settings (snapping on/off and distance). Without them the top bar's, or defaults (snapping on, 12 px).</summary>
        public void SetViewSettings(ViewSettings settings) { _settings = settings; }

        /// <summary>The draft's edges (one per point; edge i joins point i to point i+1), with road-following edges resolved.</summary>
        public IReadOnlyList<AreaEdge> Edges => CurrentShape().Edges;

        private ViewSettings Settings
        {
            get
            {
                if (_settings != null) return _settings;
                if (_topBar == null) _topBar = FindFirstObjectByType<TopBarController>();
                return _topBar != null ? _topBar.ViewSettings : (_settings = new ViewSettings());
            }
        }

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
            _pressVertex = -1; _pressHandle = -1; _pressEdge = -1;
            if (button != PressButton.Left) return;
            var hit = HitTest(screenPos);
            if (hit.Kind == ToolHitKind.Vertex) _pressVertex = hit.Id;
            else if (hit.Kind == ToolHitKind.Handle) _pressHandle = hit.Id;
            else if (hit.Kind == ToolHitKind.Edge) _pressEdge = hit.Id;
        }

        /// <summary>The cursor moved. While a press that started on a placed point is dragged, that point follows the cursor.</summary>
        public void PointerMove(Vector2 screenPos)
        {
            if (!_active) return;
            _press.Move(screenPos);
            if (!_press.IsDragging(PressButton.Left)) return;
            if (_pressHandle >= 0) { BendEdge(_pressHandle, screenPos); return; }
            if (_pressVertex < 0) return;
            _dragIndex = _pressVertex;
            MoveDraggedPoint(screenPos);
        }

        /// <summary>A mouse button was released. <paramref name="time"/> is real seconds (for double-click detection).</summary>
        public void PointerUp(PressButton button, Vector2 screenPos, float time)
        {
            if (!_active) return;
            var r = _press.Release(button, screenPos, time);
            int pressedVertex = _pressVertex, pressedHandle = _pressHandle, pressedEdge = _pressEdge;
            _pressVertex = -1; _pressHandle = -1; _pressEdge = -1;
            _dragIndex = -1;
            _lastReleaseAddedPoint = false;
            // A drag (of either button) never acts on release; right mouse is click-only.
            if (r.Kind == PressKind.None) return;
            if (r.Kind == PressKind.Drag)
            {
                if (HasDraft) Refresh(); // re-validate on release of a drag too (AR-11)
                return;
            }

            if (button == PressButton.Right)
            {
                RightClick(r.EndPosition);
                return;
            }
            if (r.Kind == PressKind.DoubleClick && pressedHandle >= 0)
            {
                Straighten(pressedHandle);
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
            if (pressedHandle >= 0) return; // a click on a handle does nothing
            if (pressedEdge >= 0) { _lastReleaseAddedPoint = InsertPointOnEdge(pressedEdge, r.EndPosition); return; }
            _lastReleaseAddedPoint = AddPointAt(r.EndPosition);
        }

        /// <summary>Enter pressed or Confirm clicked: sends <see cref="DesignateArea"/> if the draft is valid (4+ points).</summary>
        public void Confirm()
        {
            if (!_active || _host == null || !_host.IsInitialized || !CanClose) return;
            Refresh(); // AR-11: never trust a stale preview; re-run the validator on the draft as it is now
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
            int n = _points.Count;
            if (n > 1) _edges[(index - 1 + n) % n] = AreaEdge.Straight();
            _points.RemoveAt(index); _edges.RemoveAt(index); _roadSeg.RemoveAt(index); _roadSide.RemoveAt(index);
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

        private int EdgeCount => _points.Count >= 3 ? _points.Count : _points.Count - 1;

        private PointMm EdgeMiddlePoint(int i)
        {
            if (_edges[i].Kind == AreaEdgeKind.Bent) return _edges[i].Handle;
            var a = _points[i]; var b = _points[(i + 1) % _points.Count];
            return new PointMm((a.X + b.X) / 2, (a.Z + b.Z) / 2);
        }

        private bool ToScreen(PointMm p, out Vector2 s)
        {
            s = default;
            return _picker != null && _picker.TryWorldToScreen((float)p.XMetres, (float)p.ZMetres, out s);
        }

        /// <summary>What a press hits: a point, an edge's middle handle (id = edge), an edge (id = edge) or the ground.</summary>
        private ToolHit HitTest(Vector2 screenPos)
        {
            var cands = new List<HitCandidate>();
            for (int i = 0; i < _points.Count; i++)
                if (ToScreen(_points[i], out var s)) cands.Add(HitCandidate.Point(ToolHitKind.Vertex, i, s));
            for (int i = 0; i < EdgeCount; i++)
            {
                if (ToScreen(EdgeMiddlePoint(i), out var m)) cands.Add(HitCandidate.Point(ToolHitKind.Handle, i, m));
                PointMm a = _points[i], b = _points[(i + 1) % _points.Count];
                if (_edges[i].Kind == AreaEdgeKind.Bent)
                {
                    PointMm prev = a; const int steps = 12;
                    double cx = 2.0 * _edges[i].Handle.X - (a.X + b.X) / 2.0, cz = 2.0 * _edges[i].Handle.Z - (a.Z + b.Z) / 2.0;
                    for (int k = 1; k <= steps; k++)
                    {
                        double t = k / (double)steps, u = 1 - t;
                        var q = k == steps ? b : new PointMm((long)(u * u * a.X + 2 * u * t * cx + t * t * b.X),
                                                              (long)(u * u * a.Z + 2 * u * t * cz + t * t * b.Z));
                        if (ToScreen(prev, out var sa) && ToScreen(q, out var sb))
                            cands.Add(new HitCandidate(ToolHitKind.Edge, i, sa, sb));
                        prev = q;
                    }
                }
                else if (ToScreen(a, out var ea) && ToScreen(b, out var eb))
                    cands.Add(new HitCandidate(ToolHitKind.Edge, i, ea, eb));
            }
            return ToolHitTester.Test(cands, screenPos);
        }

        private void BendEdge(int edge, Vector2 screenPos)
        {
            if (edge >= EdgeCount || !TryGround(screenPos, out var p)) return;
            _edges[edge] = AreaEdge.Bent(p);
            _rejection = null;
            Refresh();
        }

        private void Straighten(int edge)
        {
            if (edge >= EdgeCount) return;
            _edges[edge] = AreaEdge.Straight();
            _rejection = null;
            Refresh();
        }

        private bool InsertPointOnEdge(int edge, Vector2 screenPos)
        {
            var t = Type;
            if (t == null || _points.Count >= t.MaxPoints || edge >= EdgeCount) return false;
            if (!TryGround(screenPos, out var p)) return false;
            SnapPoint(screenPos, ref p, out int seg, out RoadSide side);
            _points.Insert(edge + 1, p);
            _roadSeg.Insert(edge + 1, seg); _roadSide.Insert(edge + 1, side);
            _edges[edge] = AreaEdge.Straight();
            _edges.Insert(edge + 1, AreaEdge.Straight());
            _rejection = null;
            Refresh();
            return true;
        }

        private const int KindAreaVertex = 0, KindRoad = 1, KindAreaEdge = 2;

        /// <summary>
        /// Snaps a ground point when snapping is on (areas.md Snapping): area vertices, then road side lines, then area
        /// edges, within SnapDistancePx. <paramref name="seg"/> is the road segment id when it landed on a road side, else -1.
        /// </summary>
        private void SnapPoint(Vector2 screenPos, ref PointMm p, out int seg, out RoadSide side)
        {
            seg = -1; side = RoadSide.Left;
            var settings = Settings;
            if (!settings.Snapping || _host == null || !_host.IsInitialized || _picker == null) return;
            float px = settings.SnapDistancePx;
            if (!TryGround(screenPos + new Vector2(px, 0f), out var q)) return;
            double radiusMm = p.DistanceTo(q) * 1.5 + 1;
            var cands = new List<SnapCandidate>();
            var points = new List<PointMm>();
            var segs = new List<int>();
            var sides = new List<RoadSide>();
            void Add(int kind, PointMm at, int sg, RoadSide sd)
            {
                if (!ToScreen(at, out var s)) return;
                cands.Add(new SnapCandidate(kind, points.Count, (s - screenPos).magnitude));
                points.Add(at); segs.Add(sg); sides.Add(sd);
            }
            var world = _host.World;
            var hit = world.Areas.FindNearestSnap(p, radiusMm);
            if (hit.HasValue)
                Add(hit.Value.Kind == AreaSnapKind.Vertex ? KindAreaVertex : KindAreaEdge, hit.Value.Point, -1, RoadSide.Left);
            foreach (var segment in world.Roads.Segments)
            {
                for (int k = 0; k < 2; k++)
                {
                    RoadSide sd = k == 0 ? RoadSide.Left : RoadSide.Right;
                    var line = segment.SideLine(sd);
                    if (line.Length < 2) continue;
                    var n = Polylines.Nearest(line, p);
                    if (n.DistanceMm > radiusMm) continue;
                    Add(KindRoad, new PointMm((long)System.Math.Round(n.X), (long)System.Math.Round(n.Z)), segment.Id, sd);
                }
            }
            if (!SnapPicker.TryPick(cands, new[] { KindAreaVertex, KindRoad, KindAreaEdge }, px, true, out var best)) return;
            p = points[best.Id]; seg = segs[best.Id]; side = sides[best.Id];
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
            if (!TryGround(screenPos, out var p)) return;
            SnapPoint(screenPos, ref p, out int seg, out RoadSide side);
            _roadSeg[_dragIndex] = seg; _roadSide[_dragIndex] = side;
            if (p == _points[_dragIndex]) { Refresh(); return; }
            _points[_dragIndex] = p;
            _rejection = null;
            Refresh();
        }

        private bool AddPointAt(Vector2 screenPos)
        {
            var t = Type;
            if (t == null || _points.Count >= t.MaxPoints) return false;
            if (!TryGround(screenPos, out var p)) return false;
            SnapPoint(screenPos, ref p, out int seg, out RoadSide side);
            _points.Add(p); _edges.Add(AreaEdge.Straight()); _roadSeg.Add(seg); _roadSide.Add(side);
            _rejection = null;
            Refresh();
            return true;
        }

        private AreaShape CurrentShape()
        {
            int n = _points.Count;
            var edges = new AreaEdge[n];
            bool snapping = Settings.Snapping;
            for (int i = 0; i < n; i++)
            {
                edges[i] = _edges[i];
                int j = (i + 1) % n;
                // A straight edge between two points snapped to the same side of the same road follows it (AR-04).
                if (snapping && edges[i].Kind == AreaEdgeKind.Straight && j != i && _roadSeg[i] >= 0
                    && _roadSeg[i] == _roadSeg[j] && _roadSide[i] == _roadSide[j])
                    edges[i] = AreaEdge.FollowsRoad(_roadSeg[i], _roadSide[i]);
            }
            return new AreaShape(_points, edges);
        }

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
            _points.Clear(); _edges.Clear(); _roadSeg.Clear(); _roadSide.Clear();
            _pressHandle = -1; _pressEdge = -1;
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
