using System.Collections.Generic;
using LordsLegacy.Game.Tools.Shared;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Operations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LordsLegacy.Game.Tools
{
    /// <summary>
    /// Module placement tool (T-278, T-279; woodcutting.md WC-03, WC-04). The area tool hands over to it after a woodcutter
    /// area is designated: it holds the operation id and a queue of module types to place (hut first, then log storage).
    /// A ghost follows the cursor; Q / E turn it 15° (Shift: 1°). With snapping on, a side of the ghost within the snap
    /// distance of the area edge snaps flush and turns to run along it (<see cref="ModuleSnap"/>, presentation only,
    /// decision 0017). The ghost is red and the panel shows the Sim's refusal text when
    /// <see cref="ModulePlacement.Preview"/> says no. A left click sends <see cref="PlaceModule"/>; a right click on a placed
    /// module of this operation sends <see cref="RemoveModule"/>. Panel buttons add a hut, log storage or oxen stand.
    /// Esc, Cancel or deselecting ends the tool. The tool holds no placement rules.
    /// </summary>
    public sealed class ModuleTool : MonoBehaviour, ITool
    {
        /// <summary>Tool id the controller registers the module tool under.</summary>
        public const string ToolId = "module";

        /// <summary>Degrees one Q / E press turns the ghost.</summary>
        public const double RotateStepDeg = 15.0;
        /// <summary>Degrees one Shift + Q / E press turns the ghost.</summary>
        public const double FineRotateStepDeg = 1.0;

        /// <summary>Panel action name: add a hut.</summary>
        public const string ActionAddHut = "add-hut";
        /// <summary>Panel action name: add log storage.</summary>
        public const string ActionAddLogStorage = "add-log-storage";
        /// <summary>Panel action name: add an oxen stand.</summary>
        public const string ActionAddOxenStand = "add-oxen-stand";

        private static readonly Color GhostOk = new Color(0.3f, 0.85f, 0.35f, 0.6f);
        private static readonly Color GhostBad = new Color(0.9f, 0.2f, 0.2f, 0.6f);

        [SerializeField] private SimHost _host;
        [SerializeField] private GroundPicker _picker;
        [SerializeField] private ToolPanelView _panel;
        [SerializeField] private ToolController _controller;
        [SerializeField] private TopBarController _topBar;

        private readonly List<string> _queue = new List<string>();
        private ViewSettings _settings;
        private string _rejection;
        private bool _subscribed;
        private bool _hasHover;
        private Vector2 _hoverScreen;
        private GameObject _ghostObject;
        private Mesh _ghostMesh;
        private Material _ghostMaterial;

        /// <inheritdoc />
        public string Id => ToolId;

        /// <inheritdoc />
        public bool HasDraft => false;

        /// <summary>The operation whose modules are being placed, or 0 when none.</summary>
        public int OperationId { get; private set; }

        /// <summary>Module type ids still to place, in order (first is next).</summary>
        public IReadOnlyList<string> Queue => _queue;

        /// <summary>True while this tool is the active tool.</summary>
        public bool IsActive { get; private set; }

        /// <summary>Ghost rotation in degrees, counter-clockwise, in [0, 360).</summary>
        public double Rotation { get; private set; }

        /// <summary>True while the ghost is shown (a module type is next and the cursor is over the ground).</summary>
        public bool GhostVisible { get; private set; }

        /// <summary>Where the ghost sits (after snapping).</summary>
        public PointMm GhostCentre { get; private set; }

        /// <summary>Rotation the ghost is drawn and placed with (after snapping).</summary>
        public double GhostRotation { get; private set; }

        /// <summary>True when the ghost snapped to an area edge.</summary>
        public bool GhostSnapped { get; private set; }

        /// <summary>The Sim's verdict on the ghost, or null when there is no ghost.</summary>
        public ModulePlacementResult GhostPreview { get; private set; }

        /// <summary>True while the ghost is drawn red (invalid).</summary>
        public bool GhostIsRed => GhostVisible && GhostPreview != null && !GhostPreview.Ok;

        /// <summary>Sets the references (for code that adds the component at run time).</summary>
        public void Configure(SimHost host, GroundPicker picker, ToolPanelView panel, ToolController controller)
        {
            Unsubscribe();
            _host = host; _picker = picker; _panel = panel; _controller = controller;
            if (IsActive) Subscribe();
        }

        /// <summary>Sets the view settings (snapping on/off, distance). Without them the top bar's, or defaults.</summary>
        public void SetViewSettings(ViewSettings settings) { _settings = settings; }

        private ViewSettings Settings
        {
            get
            {
                if (_settings != null) return _settings;
                if (_topBar == null) _topBar = FindFirstObjectByType<TopBarController>();
                return _topBar != null ? _topBar.ViewSettings : (_settings = new ViewSettings());
            }
        }

        /// <summary>Sets the operation and module order. Call before the controller activates the tool.</summary>
        public void Begin(int operationId, IEnumerable<string> moduleTypeIds)
        {
            OperationId = operationId;
            _queue.Clear();
            _queue.AddRange(moduleTypeIds);
            _rejection = null;
            Rotation = 0;
            _hasHover = false;
        }

        /// <inheritdoc />
        public void Activate()
        {
            IsActive = true;
            Subscribe();
            RefreshGhost();
            ShowPanel();
        }

        /// <inheritdoc />
        public void Deactivate()
        {
            IsActive = false;
            Unsubscribe();
            _hasHover = false;
            HideGhost();
            if (_panel != null) { _panel.SetActions(null); _panel.Hide(); }
        }

        /// <inheritdoc />
        public void Cancel() { }

        /// <inheritdoc />
        public bool HandleEscape() { return false; }

        /// <summary>Turns the ghost by <paramref name="degrees"/> (positive = counter-clockwise) and refreshes it.</summary>
        public void Rotate(double degrees)
        {
            Rotation = ModuleSnap.Normalize(Rotation + degrees);
            RefreshGhost();
            ShowPanel();
        }

        /// <summary>Moves the ghost to the ground under the screen position (pixels).</summary>
        public void HoverAt(Vector2 screenPos)
        {
            _hoverScreen = screenPos;
            _hasHover = true;
            RefreshGhost();
            ShowPanel();
        }

        /// <summary>A left click: places the next module where the ghost is. A refusal shows in the panel.</summary>
        public void ClickAt(Vector2 screenPos)
        {
            if (!IsActive || _host == null || !_host.IsInitialized || _picker == null) return;
            if (_controller != null && _controller.IsPointerOverUiAt(screenPos)) return;
            HoverAt(screenPos);
            PlaceAtGhost();
        }

        /// <summary>Sends <see cref="PlaceModule"/> for the ghost. The Sim decides; a refusal shows in the panel.</summary>
        public void PlaceAtGhost()
        {
            if (!IsActive || _queue.Count == 0 || !GhostVisible) return;
            var result = _host.Execute(new PlaceModule(OperationId, _queue[0], GhostCentre, GhostRotation));
            if (!result.IsAccepted)
            {
                _rejection = string.IsNullOrEmpty(result.Text) ? result.Reason.ToString() : result.Text;
                ShowPanel();
                return;
            }
            _rejection = null;
            _queue.RemoveAt(0);
            RefreshGhost();
            ShowPanel();
        }

        /// <summary>A right click: removes the placed module of this operation under the screen position, if any. True if one was removed.</summary>
        public bool RemoveAt(Vector2 screenPos)
        {
            if (!IsActive || _host == null || !_host.IsInitialized || _picker == null) return false;
            if (_controller != null && _controller.IsPointerOverUiAt(screenPos)) return false;
            if (!_picker.TryPickScreen(screenPos, out var s) || !s.HasValue) return false;
            var op = _host.World.Operations.Get(OperationId);
            if (op == null) return false;
            var point = PointMm.FromMetres(s.X, s.Z);
            int hit = -1;
            foreach (int id in op.ModuleIds)
            {
                var fp = _host.World.Footprints.Get(id);
                if (fp != null && Polygons.Locate(fp.Polygon, point) != PointLocation.Outside) { hit = id; break; }
            }
            if (hit < 0) return false;
            var result = _host.Execute(new RemoveModule(hit));
            _rejection = result.IsAccepted ? null : (string.IsNullOrEmpty(result.Text) ? result.Reason.ToString() : result.Text);
            RefreshGhost();
            ShowPanel();
            return result.IsAccepted;
        }

        /// <summary>Makes <paramref name="typeId"/> the next module to place (panel buttons).</summary>
        public void Select(string typeId)
        {
            _queue.Remove(typeId);
            _queue.Insert(0, typeId);
            _rejection = null;
            RefreshGhost();
            ShowPanel();
        }

        private static string NameOf(string typeId)
        {
            switch (typeId)
            {
                case ModuleTypes.WoodcuttingHutId: return "woodcutter's hut";
                case ModuleTypes.LogStorageId: return "log storage";
                case ModuleTypes.OxenStandId: return "oxen stand";
                default: return typeId;
            }
        }

        private void RefreshGhost()
        {
            GhostVisible = false; GhostPreview = null; GhostSnapped = false;
            if (!IsActive || !_hasHover || _queue.Count == 0 || _host == null || !_host.IsInitialized || _picker == null)
            { HideGhost(); return; }
            var world = _host.World;
            var op = world.Operations.Get(OperationId);
            var type = ModuleTypes.Find(_queue[0]);
            if (op == null || type == null || !_picker.TryPickScreen(_hoverScreen, out var s) || !s.HasValue) { HideGhost(); return; }

            var centre = PointMm.FromMetres(s.X, s.Z);
            double rot = Rotation;
            var settings = Settings;
            var area = world.Areas.Get(op.AreaId);
            if (settings.Snapping && area != null &&
                ModuleSnap.TrySnap(area.Polygon, type, centre, rot, SnapDistanceM(s.X, s.Z, settings.SnapDistancePx), out var sc, out var sr))
            {
                centre = sc; rot = sr; GhostSnapped = true;
            }
            GhostCentre = centre; GhostRotation = rot;
            GhostPreview = ModulePlacement.Preview(world, OperationId, type.Id, centre, rot);
            GhostVisible = true;
            DrawGhost(type, centre, rot, GhostPreview.Ok);
        }

        /// <summary>Converts the snap distance from pixels to metres at the cursor (the larger on-screen scale of x or z).</summary>
        private float SnapDistanceM(float x, float z, float px)
        {
            if (_picker.TryWorldToScreen(x, z, out var c) && _picker.TryWorldToScreen(x + 1f, z, out var ax) &&
                _picker.TryWorldToScreen(x, z + 1f, out var az))
            {
                float pxPerM = Mathf.Max((ax - c).magnitude, (az - c).magnitude);
                if (pxPerM > 1e-3f) return px / pxPerM;
            }
            return 1f;
        }

        private void DrawGhost(FootprintTypeDef type, PointMm centre, double rot, bool ok)
        {
            var poly = new Footprint(0, type, centre, rot).Polygon;
            var drape = TerrainDrape.Fill(_host.World.Terrain, poly, 0.15f, ok ? GhostOk : GhostBad);
            if (_ghostObject == null)
            {
                _ghostObject = new GameObject("ModuleGhost");
                _ghostObject.transform.SetParent(transform, false);
                _ghostObject.AddComponent<MeshFilter>();
                var mr = _ghostObject.AddComponent<MeshRenderer>();
                var shared = GroundOverlay.Material();
                _ghostMaterial = shared != null ? new Material(shared) : new Material(Shader.Find("Sprites/Default"));
                mr.sharedMaterial = _ghostMaterial;
            }
            if (_ghostMesh != null) Destroy(_ghostMesh);
            _ghostMesh = drape.ToMesh("ModuleGhost");
            _ghostObject.GetComponent<MeshFilter>().sharedMesh = _ghostMesh;
            _ghostObject.SetActive(true);
        }

        private void HideGhost()
        {
            GhostVisible = false; GhostPreview = null; GhostSnapped = false;
            if (_ghostObject != null) _ghostObject.SetActive(false);
        }

        private void ShowPanel()
        {
            if (_panel == null || !IsActive) return;
            var actions = new List<KeyValuePair<string, string>>();
            var op = _host != null && _host.IsInitialized ? _host.World.Operations.Get(OperationId) : null;
            if (op != null && !HasModule(op, ModuleTypes.WoodcuttingHutId))
                actions.Add(new KeyValuePair<string, string>(ActionAddHut, "Add hut"));
            actions.Add(new KeyValuePair<string, string>(ActionAddLogStorage, "Add log storage"));
            actions.Add(new KeyValuePair<string, string>(ActionAddOxenStand, "Add oxen stand"));
            _panel.SetActions(actions);

            string status;
            if (_queue.Count == 0) status = "All modules placed";
            else
            {
                status = "Place " + NameOf(_queue[0]) + " · " + Rotation.ToString("0.#") + "°";
                if (_queue.Count > 1) status += " · next: " + NameOf(_queue[1]);
                if (GhostVisible && GhostPreview != null) status += GhostPreview.Ok ? " · OK" : " · " + GhostPreview.Text;
            }
            if (_rejection != null) status += " · Rejected: " + _rejection;
            const string hint = "Click to place · Q / E turn (Shift: 1°) · right-click a module removes it · Esc stops";
            _panel.Show(status, hint, GhostVisible && GhostPreview != null && GhostPreview.Ok);
        }

        private bool HasModule(Operation op, string typeId)
        {
            foreach (int id in op.ModuleIds)
            {
                var fp = _host.World.Footprints.Get(id);
                if (fp != null && fp.Type.Id == typeId) return true;
            }
            return false;
        }

        private void OnAction(string name)
        {
            switch (name)
            {
                case ActionAddHut: Select(ModuleTypes.WoodcuttingHutId); break;
                case ActionAddLogStorage: Select(ModuleTypes.LogStorageId); break;
                case ActionAddOxenStand: Select(ModuleTypes.OxenStandId); break;
            }
        }

        private void Subscribe()
        {
            if (_subscribed || _panel == null) return;
            _panel.Cancelled += OnCancelled;
            _panel.Confirmed += PlaceAtGhost;
            _panel.ActionClicked += OnAction;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (_panel != null)
            {
                _panel.Cancelled -= OnCancelled;
                _panel.Confirmed -= PlaceAtGhost;
                _panel.ActionClicked -= OnAction;
            }
            _subscribed = false;
        }

        private void OnCancelled()
        {
            if (_controller != null && _controller.ActiveTool == (ITool)this) _controller.Deselect();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (_ghostMesh != null) Destroy(_ghostMesh);
            if (_ghostMaterial != null) Destroy(_ghostMaterial);
        }

        private void OnEnable()
        {
            var c = _controller != null ? _controller : FindFirstObjectByType<ToolController>();
            if (c != null) c.Register(this);
        }

        private void Update()
        {
            if (!IsActive) return;
            var kb = Keyboard.current;
            if (kb != null)
            {
                double step = kb.shiftKey.isPressed ? FineRotateStepDeg : RotateStepDeg;
                if (kb.qKey.wasPressedThisFrame) Rotate(step);
                if (kb.eKey.wasPressedThisFrame) Rotate(-step);
            }
            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 pos = mouse.position.ReadValue();
            bool overUi = _controller != null && _controller.IsPointerOverUiAt(pos);
            if (overUi) { _hasHover = false; HideGhost(); }
            else if (!_hasHover || pos != _hoverScreen) { HoverAt(pos); }
            if (mouse.leftButton.wasReleasedThisFrame) ClickAt(pos);
            if (mouse.rightButton.wasReleasedThisFrame) RemoveAt(pos);
        }
    }
}
