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
        private readonly List<bool> _locked = new List<bool>(); // edge straightened by the player: never NearRoad (AR-23)
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
        private const float CursorSphereM = 0.6f;

        /// <summary>
        /// AR-21 ghost polygon (the shape the next click would make). Off by default after the T-164 playtest: it
        /// followed the mouse over the panel buttons and ignored bent and road-following edges, so it needs more polish.
        /// The cursor sphere (AR-20) is unaffected.
        /// </summary>
        public bool GhostEnabled { get; set; }
        private CursorMarker _cursor;
        private PointMm _hover;
        private bool _hasHover, _hoverSnapped, _lastSnapped, _dragging;

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

        private AreaTypeDef Type => _host != null && _host.IsInitialized ? _host.World.AreaTypes.Find(_typeId) : null;

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
            _hasHover = false;
            ShowSnapMarkers(default);
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
            if (_press.IsDragging(PressButton.Left) && (_pressHandle >= 0 || _pressVertex >= 0))
            {
                _dragging = true;
                if (_pressHandle >= 0) BendEdge(_pressHandle, screenPos);
                else { _dragIndex = _pressVertex; MoveDraggedPoint(screenPos); }
            }
            UpdateCursor(screenPos);
        }

        /// <summary>The 0.6 m cursor sphere (AR-20), or null before the tool first saw the cursor.</summary>
        public CursorMarker Cursor => _cursor;

        private void UpdateCursor(Vector2 screenPos)
        {
            if (_host == null || !_host.IsInitialized
                || (_controller != null && _controller.IsPointerOverUiAt(screenPos))
                || !TryGround(screenPos, out var p))
            {
                _hasHover = false;
                HideCursor();
                ShowSnapMarkers(default);
                return;
            }
            _lastSnapped = false;
            _hasHover = true;
            ShowSnapMarkers(p);
            SnapPoint(screenPos, ref p, out _, out _);
            _hover = p; _hoverSnapped = _lastSnapped; _hasHover = true;
            if (_cursor == null) _cursor = CursorMarker.Create(transform, _host);
            _cursor.SetShape(CursorMarkerShape.Sphere, CursorSphereM);
            _cursor.Show((float)p.XMetres, (float)p.ZMetres, _hoverSnapped);
            UpdateGhost();
        }

        private void HideCursor()
        {
            if (_cursor != null) _cursor.Hide();
            if (_areas != null) _areas.HideGhost();
        }

        /// <summary>AR-21: faint last -> sphere -> point 1 edges while placing; hidden when dragging, with no points or no room.</summary>
        private void UpdateGhost()
        {
            if (_areas == null) return;
            var t = Type;
            bool show = GhostEnabled && _active && _hasHover && _points.Count >= 1 && !_dragging && t != null && _points.Count < t.MaxPoints;
            if (show) _areas.ShowGhost(_points, _hover);
            else _areas.HideGhost();
        }

        /// <summary>A mouse button was released. <paramref name="time"/> is real seconds (for double-click detection).</summary>
        public void PointerUp(PressButton button, Vector2 screenPos, float time)
        {
            if (!_active) return;
            var r = _press.Release(button, screenPos, time);
            int pressedVertex = _pressVertex, pressedHandle = _pressHandle, pressedEdge = _pressEdge;
            _pressVertex = -1; _pressHandle = -1; _pressEdge = -1;
            _dragIndex = -1;
            _dragging = false;
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
            if (n > 1) { _edges[(index - 1 + n) % n] = AreaEdge.Straight(); _locked[(index - 1 + n) % n] = false; }
            _points.RemoveAt(index); _edges.RemoveAt(index); _roadSeg.RemoveAt(index); _roadSide.RemoveAt(index); _locked.RemoveAt(index);
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
            _locked[edge] = false;
            _rejection = null;
            Refresh();
        }

        private void Straighten(int edge)
        {
            if (edge >= EdgeCount) return;
            _edges[edge] = AreaEdge.Straight();
            _locked[edge] = true; // stays Straight even if the ends are still near a road (AR-23)
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
            _locked.Insert(edge + 1, false); _locked[edge] = false;
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
            var markers = SnapMarkerBuilder.Build(_host.World, p, settings.SnapDotSpacingM, settings.SnapMarkerRadiusM);
            var cands = new List<SnapCandidate>(markers.Count);
            for (int i = 0; i < markers.Count; i++)
                if (ToScreen(markers[i].Point, out var s))
                    cands.Add(new SnapCandidate((int)markers[i].Kind, i, (s - screenPos).magnitude));
            // Corner, then road-side dot, then area-edge dot (AR-22).
            if (!SnapPicker.TryPick(cands, new[] { KindAreaVertex, KindRoad, KindAreaEdge }, px, true, out var best)) return;
            var m = markers[best.Id];
            p = m.Point; seg = m.SegmentId; side = m.Side;
            _lastSnapped = true;
        }

        private readonly List<CursorMarker> _markerPool = new List<CursorMarker>();
        private readonly List<SnapMarker> _shownMarkers = new List<SnapMarker>();
        private const float CornerMarkerM = 1.0f, DotMarkerM = 0.35f;

        /// <summary>The snap markers currently drawn (AR-22): empty with snapping off or the tool inactive.</summary>
        public IReadOnlyList<SnapMarker> ShownMarkers => _shownMarkers;

        /// <summary>Number of marker objects currently visible.</summary>
        public int VisibleMarkerCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _markerPool.Count; i++) if (_markerPool[i] != null && _markerPool[i].IsShown) n++;
                return n;
            }
        }

        private void ShowSnapMarkers(PointMm centre)
        {
            var settings = Settings;
            _shownMarkers.Clear();
            if (_active && _hasHover && settings.Snapping && _host != null && _host.IsInitialized)
                _shownMarkers.AddRange(SnapMarkerBuilder.Build(_host.World, centre, settings.SnapDotSpacingM, settings.SnapMarkerRadiusM));
            for (int i = 0; i < _shownMarkers.Count; i++)
            {
                if (i == _markerPool.Count) _markerPool.Add(CursorMarker.Create(transform, _host));
                var mk = _markerPool[i];
                bool corner = _shownMarkers[i].Kind == SnapMarkerKind.Corner;
                mk.SetShape(CursorMarkerShape.Sphere, corner ? CornerMarkerM : DotMarkerM);
                var pt = _shownMarkers[i].Point;
                mk.Show((float)pt.XMetres, (float)pt.ZMetres, corner);
            }
            for (int i = _shownMarkers.Count; i < _markerPool.Count; i++) _markerPool[i].Hide();
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
            _locked[_dragIndex] = false; _locked[(_dragIndex - 1 + _points.Count) % _points.Count] = false; // re-check (AR-23)
            _rejection = null;
            Refresh();
        }

        private bool AddPointAt(Vector2 screenPos)
        {
            var t = Type;
            if (t == null || _points.Count >= t.MaxPoints) return false;
            if (!TryGround(screenPos, out var p)) return false;
            SnapPoint(screenPos, ref p, out int seg, out RoadSide side);
            _points.Add(p); _edges.Add(AreaEdge.Straight()); _roadSeg.Add(seg); _roadSide.Add(side); _locked.Add(false);
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
                else if (snapping && edges[i].Kind == AreaEdgeKind.Straight && i < EdgeCount && !_locked[i])
                    edges[i] = ChooseNearRoad(i, j);
            }
            return new AreaShape(_points, edges);
        }

        /// <summary>
        /// AR-23: Straight edge i (point i to point j) becomes NearRoad when one end is snapped to a road side and the
        /// other, not snapped to that side, lies within the type's reach on the far side from the road. The Sim
        /// (<see cref="AreaValidator"/>) still enforces the rules; this only picks the kind.
        /// </summary>
        private AreaEdge ChooseNearRoad(int i, int j)
        {
            if (_host == null || !_host.IsInitialized || Type == null) return AreaEdge.Straight();
            if (_roadSeg[i] >= 0 && IsNearSide(_roadSeg[i], _roadSide[i], i, j)) return AreaEdge.NearRoad(_roadSeg[i], _roadSide[i]);
            if (_roadSeg[j] >= 0 && IsNearSide(_roadSeg[j], _roadSide[j], j, i)) return AreaEdge.NearRoad(_roadSeg[j], _roadSide[j]);
            // AR-25: any area's outline counts, not just the one the cursor snapped to (a point on a shared boundary lies
            // on several). Newest area first, so a third area follows the second rather than the one under it.
            var onI = AreasThrough(_points[i]);
            var onJ = AreasThrough(_points[j]);
            for (int k = 0; k < onI.Count; k++)
                if (onJ.Contains(onI[k])) return AreaEdge.NearArea(onI[k]); // both on one outline: hug it exactly
            for (int k = 0; k < onI.Count; k++)
                if (IsNearArea(onI[k], i, j)) return AreaEdge.NearArea(onI[k]);
            for (int k = 0; k < onJ.Count; k++)
                if (IsNearArea(onJ[k], j, i)) return AreaEdge.NearArea(onJ[k]);
            // AR-33: anything else is Straight. (The old partial road hug, AR-26, is removed: it stopped at the road
            // and ran back along it.)
            return AreaEdge.Straight();
        }

        /// <summary>Ids (newest first) of the areas whose outline passes within 5 cm of the point.</summary>
        private List<int> AreasThrough(PointMm p)
        {
            var ids = new List<int>();
            foreach (var rec in _host.World.Areas.All)
            {
                if (rec.Polygon.Count < 3) continue;
                if (Polylines.Nearest(Ring(rec), p).DistanceMm <= 50.0) ids.Add(rec.Id);
            }
            ids.Sort((x, y) => y.CompareTo(x));
            return ids;
        }

        private static PointMm[] Ring(LordsLegacy.Sim.Areas.AreaRecord rec)
        {
            var ring = new PointMm[rec.Polygon.Count + 1];
            for (int k = 0; k < rec.Polygon.Count; k++) ring[k] = rec.Polygon[k];
            ring[ring.Length - 1] = ring[0];
            return ring;
        }

        /// <summary>AR-25: the other end lies outside the neighbour area, within reach of its outline.</summary>
        private bool IsNearArea(int areaId, int snapped, int other)
        {
            var rec = _host.World.Areas.Get(areaId);
            if (rec == null || rec.Polygon.Count < 3) return false;
            var ring = Ring(rec);
            // CCW outline: outside is the right-hand side
            return IsOutsideWithin(ring, -1, _points[other]) && !DoublesBack(ring, snapped, other);
        }

        /// <summary>
        /// AR-33: a NearRoad/NearArea edge from the snapped vertex to <paramref name="far"/> is Straight when it would
        /// double back along the line. That is when the far vertex's nearest point on the line lies on the same side of
        /// the snapped vertex (along the line) as the snapped vertex's other neighbour: the edge would then run back
        /// over ground the neighbouring edge already covers. "Same side" is the sign of the arc-length offset from the snapped
        /// vertex's nearest point to each one's nearest point on the line. A neighbour more than the type's reach (10 m) from the
        /// line, or under 3 m along from the snapped vertex, is ignored.
        /// </summary>
        private bool DoublesBack(PointMm[] line, int snapped, int far)
        {
            int n = _points.Count;
            int prev = (snapped - 1 + n) % n, next = (snapped + 1) % n;
            int other = prev == far ? next : prev;
            if (other == far || other == snapped) return false;
            var hs = Polylines.Nearest(line, _points[snapped]);
            var hf = Polylines.Nearest(line, _points[far]);
            double delta = hf.ArcLengthMm - hs.ArcLengthMm;
            if (System.Math.Abs(delta) < 1.0) return false;
            // Where the neighbour's nearest point on the line sits, relative to the snapped vertex, measured along the line
            // (not the raw tangent, which would mistake a tilted outline for a neighbour that lies "ahead").
            var ho = Polylines.Nearest(line, _points[other]);
            // A neighbour beyond reach of the line isn't hugging it, so it can't be what the edge doubles back over.
            if (ho.DistanceMm > Type.NearRoadReachM * PointMm.MmPerMetre) return false;
            double along = ho.ArcLengthMm - hs.ArcLengthMm;
            if (System.Math.Abs(along) < 3.0 * PointMm.MmPerMetre) return false;
            return (along > 0) == (delta > 0);
        }

        private bool IsOutsideWithin(PointMm[] line, double sign, PointMm p)
        {
            var h = Polylines.Nearest(line, p);
            int k = Mathf.Min(h.SegmentIndex, line.Length - 2);
            double dx = line[k + 1].X - line[k].X, dz = line[k + 1].Z - line[k].Z;
            double dot = sign * ((p.X - h.X) * -dz + (p.Z - h.Z) * dx);
            return dot > 0 && h.DistanceMm <= Type.NearRoadReachM * PointMm.MmPerMetre;
        }

        private bool IsNearSide(int segId, RoadSide side, int snapped, int other)
        {
            if (_roadSeg[other] == segId && _roadSide[other] == side) return false; // snapped to it: FollowsRoad's job
            var seg = _host.World.Roads.GetSegment(segId);
            if (seg == null) return false;
            var line = seg.SideLine(side);
            if (line.Length < 2) return false;
            return IsOutsideWithin(line, side == RoadSide.Left ? 1 : -1, _points[other]) && !DoublesBack(line, snapped, other);
        }

        private void Refresh()
        {
            if (_points.Count == 0) { ClearDraft(); return; }
            if (TryUntangleDraft()) _rejection = null;
            var shape = CurrentShape();
            _preview = _host != null && _host.IsInitialized ? AreaValidator.Validate(_host.World, shape, _typeId) : null;
            if (_areas != null)
            {
                if (_preview != null) _areas.ShowPreview(_preview, shape);
                else _areas.HidePreview();
            }
            ShowPanel();
            UpdateGhost();
        }

        /// <summary>
        /// AR-11 auto-fix: when the draft's straight edges cross (an hourglass), reorders the points so the polygon is
        /// simple (<see cref="AreaUntangler"/>). Drafts with bent or road-following edges are left to be refused.
        /// </summary>
        private bool TryUntangleDraft()
        {
            int n = _points.Count;
            if (n < 3) return false;
            // Only the player's own Bent edges stop it; road/area-following edges are re-derived after the reorder.
            for (int i = 0; i < n; i++)
                if (_edges[i].Kind != AreaEdgeKind.Straight) return false;
            if (!AreaUntangler.TryUntangle(_points, out int[] order)) return false;
            var points = new List<PointMm>(n);
            var roadSeg = new List<int>(n);
            var roadSide = new List<RoadSide>(n);
            for (int k = 0; k < n; k++)
            {
                points.Add(_points[order[k]]); roadSeg.Add(_roadSeg[order[k]]); roadSide.Add(_roadSide[order[k]]);
            }
            if (_dragIndex >= 0) _dragIndex = System.Array.IndexOf(order, _dragIndex);
            if (_pressVertex >= 0) _pressVertex = System.Array.IndexOf(order, _pressVertex);
            _points.Clear(); _points.AddRange(points);
            _roadSeg.Clear(); _roadSeg.AddRange(roadSeg);
            _roadSide.Clear(); _roadSide.AddRange(roadSide);
            for (int i = 0; i < n; i++) { _edges[i] = AreaEdge.Straight(); _locked[i] = false; }
            return true;
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
            _points.Clear(); _edges.Clear(); _roadSeg.Clear(); _roadSide.Clear(); _locked.Clear();
            _pressHandle = -1; _pressEdge = -1;
            _preview = null;
            _rejection = null;
            _pressVertex = -1;
            _dragIndex = -1;
            _lastReleaseAddedPoint = false;
            _dragging = false;
            if (_areas != null) { _areas.HidePreview(); _areas.HideGhost(); }
            if (!_active && _cursor != null) _cursor.Hide();
            // UIP-01/UIP-03: no draft means no panel, even while the tool is active. The cursor sphere (UIP-02) is unaffected.
            if (_panel != null) _panel.Hide();
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
