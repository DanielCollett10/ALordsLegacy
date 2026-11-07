using System.Globalization;
using LordsLegacy.Sim.Forestry;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Read-only debug overlay (F3): FPS, standing tree count, Sim tick cost (ms), ticks per second, and the tree
    /// nearest the cursor (id, trunk length, log yield) via the Sim tree spatial query. Holds no game rules.
    /// The UIDocument should use the shared PanelSettings. If no UXML is assigned the labels are built in code.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class DebugOverlayView : MonoBehaviour
    {
        /// <summary>Label element names.</summary>
        public const string FpsName = "debug-fps";
        /// <summary>Label element names.</summary>
        public const string TreeCountName = "debug-tree-count";
        /// <summary>Label element names.</summary>
        public const string TickMsName = "debug-tick-ms";
        /// <summary>Label element names.</summary>
        public const string TpsName = "debug-tps";
        /// <summary>Label element names.</summary>
        public const string TreeIdName = "debug-tree-id";
        /// <summary>Label element names.</summary>
        public const string TreeTrunkName = "debug-tree-trunk";
        /// <summary>Label element names.</summary>
        public const string TreeLogsName = "debug-tree-logs";
        /// <summary>Label element names (road network counts).</summary>
        public const string RoadNodesName = "debug-road-nodes";
        /// <summary>Label element names (road network counts).</summary>
        public const string RoadSegmentsName = "debug-road-segments";
        /// <summary>Label element names (road network counts).</summary>
        public const string RoadNetworksName = "debug-road-networks";
        /// <summary>Label element names (road segment under the cursor).</summary>
        public const string SegIdName = "debug-seg-id";
        /// <summary>Label element names (road segment under the cursor).</summary>
        public const string SegEndsName = "debug-seg-ends";
        /// <summary>Label element names (road segment under the cursor).</summary>
        public const string SegReachName = "debug-seg-reach";
        private const string RootName = "debug-overlay";

        [SerializeField] private GroundPicker _picker;
        [SerializeField] private SimHost _host;
        [SerializeField] private VisualTreeAsset _visualTree;
        [SerializeField] private StyleSheet _styleSheet;
        [SerializeField] private float _treeRadiusM = 3f;
        [SerializeField] private float _roadRadiusM = 4f;

        private VisualElement _root;
        private Label _fps, _count, _tickMs, _tps, _treeId, _trunk, _logs;
        private Label _roadNodes, _roadSegments, _roadNetworks, _segId, _segEnds, _segReach;
        private float _fpsSmoothed;

        /// <summary>When true, Update toggles on F3, measures FPS and picks under the mouse. Tests set false.</summary>
        public bool Live { get; set; } = true;

        /// <summary>Picker used for the tree hover.</summary>
        public GroundPicker Picker { get => _picker; set => _picker = value; }

        /// <summary>Sim host to read.</summary>
        public SimHost Host { get => _host; set => _host = value; }

        /// <summary>Max distance in metres from the cursor ground point to a tree that counts as hovered.</summary>
        public float TreeRadiusM { get => _treeRadiusM; set => _treeRadiusM = value; }

        /// <summary>Max distance in metres from the cursor ground point to a road feature that counts as the segment under the cursor.</summary>
        public float RoadRadiusM { get => _roadRadiusM; set => _roadRadiusM = value; }

        /// <summary>True when the overlay is shown.</summary>
        public bool Visible { get; private set; }

        /// <summary>Text of the named label (empty if missing).</summary>
        public string TextOf(string labelName)
        {
            var l = _root == null ? null : _root.Q<Label>(labelName);
            return l == null ? "" : l.text;
        }

        /// <summary>Shows or hides the overlay.</summary>
        public void SetVisible(bool visible)
        {
            Visible = visible;
            if (_root != null) _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Updates FPS, tree count, tick cost and ticks/s labels.</summary>
        public void RefreshStats(float fps)
        {
            var c = CultureInfo.InvariantCulture;
            var host = ResolveHost();
            if (_fps != null) _fps.text = "FPS " + fps.ToString("F0", c);
            if (_count == null || host == null || !host.IsInitialized) return;
            _count.text = "Trees " + host.World.Trees.Count.ToString(c);
            _tickMs.text = "Tick " + host.Stats.LastStepMs.ToString("F3", c) + " ms";
            _tps.text = "Ticks/s " + host.Stats.TicksPerSecond.ToString("F1", c);
            var roads = host.World.Roads;
            _roadNodes.text = "Road nodes " + roads.NodeCount.ToString(c);
            _roadSegments.text = "Road segments " + roads.SegmentCount.ToString(c);
            _roadNetworks.text = "Road networks " + roads.ComponentCount.ToString(c);
        }

        /// <summary>Finds the tree nearest the ground under a screen position and shows it. Clears the tree labels if none.</summary>
        public void InspectScreen(Vector2 screenPos)
        {
            var picker = _picker != null ? _picker : (_picker = FindFirstObjectByType<GroundPicker>());
            var host = ResolveHost();
            if (picker == null || host == null || !host.IsInitialized
                || !picker.TryPickScreen(screenPos, out var g)) { ShowTree(null); ShowSegment(null, null); return; }
            InspectRoad(host, g);
            TreeRecord best = null;
            double bestD = double.MaxValue;
            foreach (var t in host.World.Trees.QueryRadius(g.X, g.Z, _treeRadiusM))
            {
                double dx = t.X - g.X, dz = t.Z - g.Z, d = dx * dx + dz * dz;
                if (d < bestD) { bestD = d; best = t; }
            }
            ShowTree(best);
        }

        private void InspectRoad(SimHost host, GroundSample g)
        {
            var roads = host.World.Roads;
            var hit = roads.FindNearest(LordsLegacy.Sim.Geometry.PointMm.FromMetres(g.X, g.Z), _roadRadiusM * 1000.0);
            var seg = hit.Kind == LordsLegacy.Sim.Roads.RoadHitKind.None ? null : roads.GetSegment(hit.SegmentId);
            ShowSegment(seg, roads);
        }

        private void ShowSegment(LordsLegacy.Sim.Roads.RoadSegment seg, LordsLegacy.Sim.Roads.RoadNetwork roads)
        {
            if (_segId == null) return;
            var c = CultureInfo.InvariantCulture;
            if (seg == null)
            {
                _segId.text = "Segment id --"; _segEnds.text = "Ends -- --"; _segReach.text = "Reachable segments --";
                return;
            }
            _segId.text = "Segment id " + seg.Id.ToString(c);
            _segEnds.text = "Ends " + seg.NodeA.ToString(c) + " " + seg.NodeB.ToString(c);
            _segReach.text = "Reachable segments " + roads.ReachableSegmentCount(seg.Id).ToString(c);
        }

        private void ShowTree(TreeRecord t)
        {
            if (_treeId == null) return;
            var c = CultureInfo.InvariantCulture;
            if (t == null) { _treeId.text = "Tree id --"; _trunk.text = "Trunk -- m"; _logs.text = "Logs --"; return; }
            _treeId.text = "Tree id " + t.Id.Value.ToString(c);
            _trunk.text = "Trunk " + t.TrunkLengthM.ToString("F1", c) + " m";
            _logs.text = "Logs " + t.LogYield(ResolveHost().World.Tuning).ToString(c);
        }

        private SimHost ResolveHost()
        {
            if (_host == null) _host = FindFirstObjectByType<SimHost>();
            return _host;
        }

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;
            root.Clear();
            if (_visualTree != null) _visualTree.CloneTree(root);
            else BuildFallback(root);
            if (_styleSheet != null) root.styleSheets.Add(_styleSheet);
            UiFocusRules.Apply(root);
            _root = root.Q<VisualElement>(RootName);
            _fps = root.Q<Label>(FpsName);
            _count = root.Q<Label>(TreeCountName);
            _tickMs = root.Q<Label>(TickMsName);
            _tps = root.Q<Label>(TpsName);
            _treeId = root.Q<Label>(TreeIdName);
            _trunk = root.Q<Label>(TreeTrunkName);
            _logs = root.Q<Label>(TreeLogsName);
            _roadNodes = root.Q<Label>(RoadNodesName);
            _roadSegments = root.Q<Label>(RoadSegmentsName);
            _roadNetworks = root.Q<Label>(RoadNetworksName);
            _segId = root.Q<Label>(SegIdName);
            _segEnds = root.Q<Label>(SegEndsName);
            _segReach = root.Q<Label>(SegReachName);
            SetVisible(Visible);
        }

        private static void BuildFallback(VisualElement root)
        {
            var box = new VisualElement { name = RootName };
            box.style.position = Position.Absolute;
            box.style.top = 48;
            box.style.right = 12;
            foreach (var n in new[] { FpsName, TreeCountName, TickMsName, TpsName, TreeIdName, TreeTrunkName, TreeLogsName,
                RoadNodesName, RoadSegmentsName, RoadNetworksName, SegIdName, SegEndsName, SegReachName })
                box.Add(new Label(n) { name = n });
            root.Add(box);
        }

        private void Update()
        {
            if (!Live) return;
            var kb = Keyboard.current;
            if (kb != null && kb.f3Key.wasPressedThisFrame) SetVisible(!Visible);
            if (!Visible) return;
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f) _fpsSmoothed = _fpsSmoothed <= 0f ? 1f / dt : Mathf.Lerp(_fpsSmoothed, 1f / dt, 0.1f);
            RefreshStats(_fpsSmoothed);
            var mouse = Mouse.current;
            if (mouse != null) InspectScreen(mouse.position.ReadValue());
        }
    }
}
