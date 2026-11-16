using System.Collections.Generic;
using LordsLegacy.Game.Tools.Shared;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Terrain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LordsLegacy.Game.Tools
{
    /// <summary>
    /// Build tool for an operation placed in one go (building-tool.md BT-10..18). Area stage: an <see cref="AreaTool"/>
    /// (composition, its <see cref="AreaTool.LockHandler"/>) draws and validates the area. Module stage: a palette lists the
    /// type's modules with costs; the picked module follows the cursor (Q / E turn 15°, Shift 1°, side snapping to the area edge),
    /// a left click adds a ghost (red with the Sim's reason when refused), a right click on a ghost
    /// removes it, Escape drops the module in hand and then cancels, Confirm sends the one command. Expansion (BT-20..21):
    /// <see cref="PrepareExpand"/> then activate starts in the module stage on an existing operation's area, its modules drawn
    /// solid, Confirm sends <see cref="PlaceModules"/>. Rules live in
    /// <see cref="OperationDraft"/> and the Sim; nothing reaches the Sim until Confirm.
    /// </summary>
    public sealed class OperationDraftTool : MonoBehaviour, ITool
    {
        /// <summary>Build bar id of the woodcutter entry.</summary>
        public const string WoodcutterToolId = "woodcutter";
        /// <summary>Build bar id of the forager entry.</summary>
        public const string ForagerToolId = "forager";
        /// <summary>Degrees one Q / E press turns the ghost.</summary>
        public const double RotateStepDeg = 15.0;
        /// <summary>Degrees one Shift + Q / E press turns the ghost.</summary>
        public const double FineRotateStepDeg = 1.0;
        /// <summary>Prefix of the palette action names; the module type id follows.</summary>
        public const string PickPrefix = "pick:";

        private static readonly Color GhostOk = new Color(0.3f, 0.85f, 0.35f, 0.6f);
        private static readonly Color GhostBad = new Color(0.9f, 0.2f, 0.2f, 0.6f);
        private static readonly Color GhostPlaced = new Color(0.35f, 0.65f, 0.95f, 0.6f);

        [SerializeField] private string _toolId = WoodcutterToolId;
        [SerializeField] private string _operationTypeId = OperationTypes.WoodcuttingId;
        [SerializeField] private SimHost _host;
        [SerializeField] private GroundPicker _picker;
        [SerializeField] private AreaTool _areaTool;
        [SerializeField] private AreaRenderer _areas;
        [SerializeField] private ToolPanelView _panel;
        [SerializeField] private ToolController _controller;
        [SerializeField] private TopBarController _topBar;

        private ViewSettings _settings;
        private OperationDraft _draft;
        private OperationTypeDef _type;
        private IReadOnlyList<PointMm> _areaPolygon;
        private bool _subscribed, _hasHover;
        private Vector2 _hoverScreen;
        private string _message;
        private readonly List<GhostVisual> _placed = new List<GhostVisual>();
        private GhostVisual _hover;

        /// <inheritdoc />
        public string Id => _toolId;

        /// <inheritdoc />
        public bool HasDraft => _draft != null && (_draft.Stage == DraftStage.ModuleStage || (_areaTool != null && _areaTool.HasDraft));

        /// <summary>True while this tool is the active tool.</summary>
        public bool IsActive { get; private set; }

        /// <summary>The draft model, or null while inactive.</summary>
        public OperationDraft Draft => _draft;

        /// <summary>Ghost rotation in degrees, counter-clockwise, in [0, 360).</summary>
        public double Rotation { get; private set; }

        /// <summary>True while the module in hand is drawn at the cursor.</summary>
        public bool GhostVisible { get; private set; }

        /// <summary>Where the module in hand sits (after snapping).</summary>
        public PointMm GhostCentre { get; private set; }

        /// <summary>Rotation the module in hand is drawn and placed with (after snapping).</summary>
        public double GhostRotation { get; private set; }

        /// <summary>True when the module in hand snapped to an area edge.</summary>
        public bool GhostSnapped { get; private set; }

        /// <summary>True while the module in hand is drawn red (the Sim refuses it here).</summary>
        public bool GhostIsRed { get; private set; }

        /// <summary>The Sim's reason for the red ghost or the last refused click, or null.</summary>
        public string Message => _message ?? _hoverReason;

        /// <summary>The composed area tool used for the area stage.</summary>
        public AreaTool AreaStageTool => _areaTool;

        /// <summary>Sets the references (for code that adds the component at run time).</summary>
        public void Configure(string toolId, string operationTypeId, SimHost host, GroundPicker picker, AreaTool areaTool,
            AreaRenderer areas, ToolPanelView panel, ToolController controller)
        {
            Unsubscribe();
            _toolId = toolId; _operationTypeId = operationTypeId;
            _host = host; _picker = picker; _areaTool = areaTool; _areas = areas; _panel = panel; _controller = controller;
            if (IsActive && _draft != null && _draft.Stage == DraftStage.ModuleStage) Subscribe();
        }

        /// <summary>Sets the view settings (snapping). Without them the top bar's, or defaults.</summary>
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

        /// <summary>The operation type this tool places (e.g. woodcutting).</summary>
        public string OperationTypeId => _operationTypeId;

        /// <summary>True while the draft expands an existing operation (module stage on its area, building-tool.md BT-20).</summary>
        public bool IsExpansion => _draft != null && _draft.IsExpansion;

        /// <summary>Number of existing modules drawn solid.</summary>
        public int ExistingDrawn
        {
            get { int n = 0; foreach (var g in _existingVis) if (g.IsShown) n++; return n; }
        }

        /// <summary>Marks the next <see cref="Activate"/> as an expansion of this operation (Expand modules button).</summary>
        public void PrepareExpand(int operationId) { _expandOperationId = operationId; }

        private int? _expandOperationId;
        private readonly List<GhostVisual> _existingVis = new List<GhostVisual>();

        private void ActivateExpansion(int operationId)
        {
            _draft = OperationDraft.ForExpansion(_host.World, _type, operationId);
            if (_draft == null) return;
            IsActive = true;
            _areaPolygon = _draft.AreaPolygon; _message = null; Rotation = 0; _hasHover = false;
            RedrawExisting(); // the area itself is already drawn by the area renderer

            Subscribe();
            ShowPanel();
        }

        private void RedrawExisting()
        {
            var existing = _draft.Existing;
            while (_existingVis.Count < existing.Count) _existingVis.Add(new GhostVisual(transform, "DraftExisting" + _existingVis.Count));
            for (int i = 0; i < _existingVis.Count; i++)
            {
                if (i < existing.Count) _existingVis[i].Draw(_host.World.Terrain, existing[i].Polygon, ExistingSolid);
                else _existingVis[i].Hide();
            }
        }

        private static readonly Color ExistingSolid = new Color(0.55f, 0.42f, 0.28f, 0.95f);

        /// <inheritdoc />
        public void Activate()
        {
            int? expand = _expandOperationId;
            _expandOperationId = null;
            if (_host == null || !_host.IsInitialized || (_areaTool == null && !expand.HasValue)) return;
            _type = OperationTypes.Find(_operationTypeId);
            if (_type == null) return;
            if (expand.HasValue) { ActivateExpansion(expand.Value); return; }
            IsActive = true;
            _draft = new OperationDraft(_host.World, _type);
            _areaPolygon = null; _message = null; Rotation = 0; _hasHover = false;
            _areaTool.LockHandler = OnAreaLocked;
            _areaTool.Activate();
        }

        /// <inheritdoc />
        public void Deactivate()
        {
            IsActive = false;
            _expandOperationId = null;
            bool expansion = IsExpansion;
            if (_areaTool != null && !expansion) { _areaTool.LockHandler = null; _areaTool.Deactivate(); }
            Unsubscribe();
            if (_areas != null) _areas.HidePreview();
            DiscardVisuals();
            if (_panel != null) { _panel.SetActions(null); _panel.Hide(); }
            _draft = null;
            _areaPolygon = null;
        }

        /// <inheritdoc />
        public void Cancel()
        {
            bool expansion = IsExpansion;
            if (_draft != null) _draft.Cancel();
            if (_areaTool != null && !expansion) _areaTool.Cancel();
            DiscardVisuals();
        }

        /// <inheritdoc />
        public bool HandleEscape()
        {
            if (_draft == null) return false;
            if (_draft.Stage == DraftStage.ModuleStage && _draft.Escape() == DraftEscape.DroppedModule)
            {
                _message = null;
                RefreshHover();
                ShowPanel();
                return true;
            }
            return false; // area stage or nothing in hand: the controller cancels (Cancel, then Deactivate)
        }

        private void OnAreaLocked(AreaShape shape)
        {
            if (_draft == null || !_draft.LockArea(shape, out string reason)) return;
            AreaPreview preview = AreaValidator.Validate(_host.World, shape, _type.BuildingAreaTypeId);
            _areaPolygon = preview.Polygon;
            _areaTool.LockHandler = null;
            _areaTool.Deactivate(); // clears the area stage's draft, preview and panel
            _areaTool.LockHandler = OnAreaLocked;
            if (_areas != null) _areas.ShowPreview(preview, shape);
            Subscribe();
            _message = null;
            ShowPanel();
        }

        /// <summary>Picks a module to place (palette). Ignored in the area stage.</summary>
        public void Pick(string moduleTypeId)
        {
            if (_draft == null || !_draft.PickModule(moduleTypeId)) return;
            _message = null;
            RefreshHover();
            ShowPanel();
        }

        /// <summary>Turns the module in hand by <paramref name="degrees"/> (positive = counter-clockwise).</summary>
        public void Rotate(double degrees)
        {
            Rotation = ModuleSnap.Normalize(Rotation + degrees);
            RefreshHover();
            ShowPanel();
        }

        /// <summary>Moves the module in hand to the ground under the screen position (pixels).</summary>
        public void HoverAt(Vector2 screenPos)
        {
            _hoverScreen = screenPos; _hasHover = true;
            RefreshHover();
            ShowPanel();
        }

        /// <summary>A left click in the module stage: adds a ghost where the module in hand is, or shows the Sim's reason.</summary>
        public void ClickAt(Vector2 screenPos)
        {
            if (!ModuleStageReady(screenPos)) return;
            HoverAt(screenPos);
            if (!GhostVisible) return;
            if (!_draft.AddGhost(GhostCentre, GhostRotation, out string reason)) _message = reason;
            else _message = null;
            RefreshHover();
            RedrawPlaced();
            ShowPanel();
        }

        /// <summary>A right click in the module stage: removes the topmost ghost under the cursor. True if one went; on empty ground nothing happens.</summary>
        public bool RemoveAt(Vector2 screenPos)
        {
            if (!ModuleStageReady(screenPos)) return false;
            if (!_picker.TryPickScreen(screenPos, out var s) || !s.HasValue) return false;
            if (!_draft.RemoveGhostAt(PointMm.FromMetres(s.X, s.Z))) return false;
            _message = null;
            RefreshHover();
            RedrawPlaced();
            ShowPanel();
            return true;
        }

        /// <summary>Confirm (button or Enter): sends the one command when <see cref="OperationDraft.CanConfirm"/>; then the tool ends.</summary>
        public void Confirm()
        {
            if (!IsActive || _draft == null || _draft.Stage != DraftStage.ModuleStage || !_draft.CanConfirm) return;
            ISimCommand cmd = _draft.IsExpansion ? (ISimCommand)_draft.BuildExpandCommand() : _draft.BuildCommand();
            if (cmd == null) return;
            var result = _host.Execute(cmd);
            if (!result.IsAccepted)
            {
                _message = string.IsNullOrEmpty(result.Text) ? result.Reason.ToString() : result.Text;
                ShowPanel();
                return;
            }
            if (_controller != null && _controller.ActiveTool == (ITool)this) _controller.Deselect();
            else { Cancel(); Deactivate(); }
        }

        private bool ModuleStageReady(Vector2 screenPos)
        {
            if (!IsActive || _draft == null || _draft.Stage != DraftStage.ModuleStage || GameMenuState.IsOpen) return false;
            if (_host == null || !_host.IsInitialized || _picker == null) return false;
            return _controller == null || !_controller.IsPointerOverUiAt(screenPos);
        }

        private void RefreshHover()
        {
            GhostVisible = false; GhostSnapped = false; GhostIsRed = false; _hoverReason = null;
            if (_draft == null || _draft.Stage != DraftStage.ModuleStage || _draft.ModuleInHand == null || !_hasHover
                || _picker == null || !_picker.TryPickScreen(_hoverScreen, out var s) || !s.HasValue)
            { if (_hover != null) _hover.Hide(); return; }
            FootprintTypeDef type = ModuleTypes.Find(_draft.ModuleInHand);
            var centre = PointMm.FromMetres(s.X, s.Z);
            double rot = Rotation;
            var settings = Settings;
            if (settings.Snapping && _areaPolygon != null &&
                ModuleSnap.TrySnap(_areaPolygon, type, centre, rot, SnapDistanceM(s.X, s.Z, settings.SnapDistancePx), out var sc, out var sr))
            { centre = sc; rot = sr; GhostSnapped = true; }
            GhostCentre = centre; GhostRotation = rot;
            bool ok = _draft.CheckGhost(centre, rot, out string reason);
            GhostVisible = true; GhostIsRed = !ok;
            _hoverReason = ok ? null : reason;
            if (_hover == null) _hover = new GhostVisual(transform, "DraftHoverGhost");
            _hover.Draw(_host.World.Terrain, new Footprint(0, type, centre, rot).Polygon, ok ? GhostOk : GhostBad);
        }

        private string _hoverReason;

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

        private void RedrawPlaced()
        {
            var ghosts = _draft.Ghosts;
            while (_placed.Count < ghosts.Count) _placed.Add(new GhostVisual(transform, "DraftGhost" + _placed.Count));
            for (int i = 0; i < _placed.Count; i++)
            {
                if (i < ghosts.Count) _placed[i].Draw(_host.World.Terrain, ghosts[i].Polygon, GhostPlaced);
                else _placed[i].Hide();
            }
        }

        /// <summary>Number of placed ghosts currently drawn.</summary>
        public int PlacedGhostsDrawn
        {
            get { int n = 0; foreach (var g in _placed) if (g.IsShown) n++; return n; }
        }

        private void DiscardVisuals()
        {
            foreach (var g in _placed) g.Destroy();
            _placed.Clear();
            foreach (var g in _existingVis) g.Destroy();
            _existingVis.Clear();
            if (_hover != null) { _hover.Destroy(); _hover = null; }
            GhostVisible = false; GhostIsRed = false; GhostSnapped = false;
        }

        private static string NameOf(string typeId)
        {
            switch (typeId)
            {
                case ModuleTypes.WoodcuttingHutId: return "Hut";
                case ModuleTypes.ForagerHutId: return "Forager hut";
                case ModuleTypes.LogStorageId: return "Log storage";
                case ModuleTypes.OxenStandId: return "Oxen stand";
                default: return typeId;
            }
        }

        private void ShowPanel()
        {
            if (_panel == null || !IsActive || _draft == null || _draft.Stage != DraftStage.ModuleStage) return;
            var actions = new List<KeyValuePair<string, string>>();
            foreach (string id in new[] { _type.HutTypeId, _type.LogStorageTypeId, _type.OxenStandTypeId })
            {
                FootprintTypeDef def = ModuleTypes.Find(id);
                if (def == null) continue;
                if (_draft.IsExpansion && !_draft.CanOffer(id)) continue; // BT-20: no second hut
                actions.Add(new KeyValuePair<string, string>(PickPrefix + id,
                    NameOf(id) + " · " + def.ConstructionRecipe.Logs + " logs"));
            }
            _panel.SetActions(actions);
            string status = "Cost " + _draft.RunningCost + " logs, have " + _draft.Have;
            status += _draft.ModuleInHand != null ? " · Placing " + NameOf(_draft.ModuleInHand) + " · " + Rotation.ToString("0.#") + "°" : " · Pick a module";
            string missing = _draft.MissingText;
            if (missing.Length > 0) status += " · " + missing;
            if (Message != null) status += " · " + Message;
            const string hint = "Click places a ghost · Q / E turn (Shift: 1°) · Right-click a ghost removes it · Esc drops the module, then cancels";
            _panel.Show(status, hint, _draft.CanConfirm);
        }

        private void OnAction(string name)
        {
            if (name != null && name.StartsWith(PickPrefix)) Pick(name.Substring(PickPrefix.Length));
        }

        private void OnCancelled()
        {
            if (_controller != null && _controller.ActiveTool == (ITool)this) _controller.Deselect();
            else { Cancel(); Deactivate(); }
        }

        private void Subscribe()
        {
            if (_subscribed || _panel == null) return;
            _panel.Cancelled += OnCancelled;
            _panel.Confirmed += Confirm;
            _panel.ActionClicked += OnAction;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (_panel != null)
            {
                _panel.Cancelled -= OnCancelled;
                _panel.Confirmed -= Confirm;
                _panel.ActionClicked -= OnAction;
            }
            _subscribed = false;
        }

        private void OnEnable()
        {
            var c = _controller != null ? _controller : FindFirstObjectByType<ToolController>();
            if (c != null) c.Register(this);
        }

        private void OnDestroy() { Unsubscribe(); DiscardVisuals(); }

        private void Update()
        {
            if (!IsActive || _draft == null || _draft.Stage != DraftStage.ModuleStage || GameMenuState.IsOpen) return;
            var kb = Keyboard.current;
            if (kb != null)
            {
                double step = kb.shiftKey.isPressed ? FineRotateStepDeg : RotateStepDeg;
                if (kb.qKey.wasPressedThisFrame) Rotate(step);
                if (kb.eKey.wasPressedThisFrame) Rotate(-step);
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Confirm();
            }
            var mouse = Mouse.current;
            if (mouse == null || !IsActive) return;
            Vector2 pos = mouse.position.ReadValue();
            bool overUi = _controller != null && _controller.IsPointerOverUiAt(pos);
            if (overUi) { _hasHover = false; RefreshHover(); }
            else if (!_hasHover || pos != _hoverScreen) HoverAt(pos);
            if (mouse.leftButton.wasReleasedThisFrame) ClickAt(pos);
            if (IsActive && mouse.rightButton.wasReleasedThisFrame) RemoveAt(pos);
        }

        /// <summary>One drawn module outline (a terrain-draped fill).</summary>
        private sealed class GhostVisual
        {
            private readonly GameObject _go;
            private readonly Material _material;
            private Mesh _mesh;

            public GhostVisual(Transform parent, string name)
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

            public void Draw(IHeightField terrain, IReadOnlyList<PointMm> polygon, Color color)
            {
                var drape = TerrainDrape.Fill(terrain, polygon, 0.15f, color);
                if (_mesh != null) Object.Destroy(_mesh);
                _mesh = drape.ToMesh("DraftGhost");
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
