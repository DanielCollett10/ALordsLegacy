using System.Text;
using LordsLegacy.Game.Tools;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Markets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Info panel for the market chosen through <see cref="AgentPicker"/> (T-476, MK-40): food on stalls by type and total,
    /// stalls built / slots with family and items, supplying granaries, households served and short last Sunday, a
    /// Deconstruct button per unstaffed stall (MK-23b, the reason for a staffed one) and Remove market (MK-04), both behind
    /// the confirm dialog. While open, houses in range are tinted green and the rest red (MK-41). Everything shown comes
    /// from <see cref="MarketReport"/>; commands go through <see cref="SimHost"/>. Built in code, never takes keyboard focus.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MarketPanelView : MonoBehaviour
    {
        [SerializeField] private SimHost _host;
        [SerializeField] private AgentPicker _agents;
        [SerializeField] private ToolController _controller;

        private VisualElement _panel, _stallBox;
        private Label _title, _stock, _stalls, _granaries, _served, _message;
        private Button _remove, _close;
        private string _signature = "";
        private bool _subscribed;

        private GameObject _overlay;
        private Mesh _mesh;
        private Material _material;
        private string _tintKey = "";

        /// <summary>True while the panel is shown.</summary>
        public bool IsShown => _panel != null && _panel.style.display.value != DisplayStyle.None;

        /// <summary>Title line ("Market 1").</summary>
        public string TitleText => _title?.text ?? "";

        /// <summary>One line per food type plus the total.</summary>
        public string StockText => _stock?.text ?? "";

        /// <summary>"Stalls: built / slots" line.</summary>
        public string StallsText => _stalls?.text ?? "";

        /// <summary>Supplying granaries line.</summary>
        public string GranariesText => _granaries?.text ?? "";

        /// <summary>Households served / short line.</summary>
        public string ServedText => _served?.text ?? "";

        /// <summary>Message line (a refused command); empty when none.</summary>
        public string MessageText => _message?.text ?? "";

        /// <summary>The panel element, for layout checks.</summary>
        public VisualElement PanelElement => _panel;

        /// <summary>Houses tinted green in the overlay.</summary>
        public int InRangeCount { get; private set; }

        /// <summary>Houses tinted red in the overlay.</summary>
        public int OutOfRangeCount { get; private set; }

        /// <summary>True while the range tint overlay is drawn.</summary>
        public bool TintVisible => _overlay != null && _overlay.activeSelf;

        /// <summary>Confirm text for removing the market.</summary>
        public const string RemoveConfirmText = "Remove the market? Its stalls come down, food on them drops on the ground, and granaries stop supplying it.";

        /// <summary>Confirm text for deconstructing a stall.</summary>
        public const string StallConfirmText = "Deconstruct this stall? Food on it drops on the ground.";

        /// <summary>Sets the references (for code that adds the component at run time).</summary>
        public void Configure(SimHost host, AgentPicker agents, ToolController controller)
        {
            Unsubscribe();
            _host = host; _agents = agents; _controller = controller;
            Subscribe();
        }

        /// <summary>Presses the close button as if clicked.</summary>
        public void ClickClose() => Agents()?.ClearMarket();

        /// <summary>Deconstruct button of a stall: asks to confirm, then sends DeconstructStall.</summary>
        public void ClickDeconstructStall(int stallId)
        {
            var host = Host();
            if (host == null || !host.IsInitialized) return;
            var check = new DeconstructStall(stallId).Validate(host.World);
            if (check.IsRejected) { SetMessage(check.Text); return; }
            ConfirmDialog.Show(StallConfirmText, "Deconstruct", () => Run(new DeconstructStall(stallId)));
        }

        /// <summary>Remove market button: asks to confirm, then sends the area delete (MK-04).</summary>
        public void ClickRemoveMarket()
        {
            var host = Host();
            var agents = Agents();
            if (host == null || !host.IsInitialized || agents == null || !agents.HasMarket) return;
            Market m = host.World.Markets.Get(agents.SelectedMarketId);
            if (m == null) return;
            int areaId = m.AreaId;
            ConfirmDialog.Show(RemoveConfirmText, "Remove market", () => Run(new DeleteArea(areaId)));
        }

        /// <summary>The Deconstruct button of a stall row, or null (staffed, unbuilt or already marked).</summary>
        public Button DeconstructButton(int stallId) => _panel?.Q<Button>("market-deconstruct-" + stallId);

        /// <summary>The text of a stall row (family, items and any reason), or "".</summary>
        public string StallRowText(int stallId) => _panel?.Q<Label>("market-stall-" + stallId)?.text ?? "";

        /// <summary>The Remove market button.</summary>
        public Button RemoveButton => _remove;

        private void Run(ISimCommand cmd)
        {
            var host = Host();
            if (host == null) return;
            var r = host.Execute(cmd);
            SetMessage(r.IsRejected ? (string.IsNullOrEmpty(r.Text) ? r.Reason.ToString() : r.Text) : "");
            Refresh();
        }

        private void SetMessage(string text)
        {
            if (_message == null) return;
            _message.text = text ?? "";
            _message.style.display = _message.text.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>Re-reads the Sim and updates the labels, rows and tint.</summary>
        public void Refresh()
        {
            if (_panel == null) return;
            var agents = Agents();
            var host = Host();
            if (agents == null || !agents.HasMarket || host == null || !host.IsInitialized) { Hide(); return; }
            var world = host.World;
            var r = MarketReport.Of(world, agents.SelectedMarketId);
            if (r == null) { Hide(); return; }
            _title.text = r.Name;
            _stock.text = StockLines(r);
            _stalls.text = "Stalls: " + r.StallsBuilt + " built / " + r.Slots + " slots";
            _granaries.text = r.SupplyingGranaryIds.Count == 0 ? "Supplied by: no granary" : "Supplied by: " + r.SupplyingGranaryIds.Count + " granar" + (r.SupplyingGranaryIds.Count == 1 ? "y" : "ies");
            _served.text = "Last Sunday: " + r.ServedLastSunday + " households served, " + r.ShortLastSunday + " short";
            RebuildStalls(r);
            _panel.style.display = DisplayStyle.Flex;
            RefreshTint(world, agents.SelectedMarketId);
        }

        private void Hide()
        {
            _panel.style.display = DisplayStyle.None;
            HideTint();
        }

        /// <summary>Food lines for a report: one per type then the total.</summary>
        public static string StockLines(MarketReport r)
        {
            var sb = new StringBuilder();
            foreach (var kv in r.Stock)
            {
                string name = kv.Key.StartsWith("item.") ? kv.Key.Substring(5) : kv.Key;
                if (name.Length > 0) name = char.ToUpperInvariant(name[0]) + name.Substring(1);
                sb.Append(name).Append(": ").Append(kv.Value).Append('\n');
            }
            sb.Append("Food on stalls: ").Append(r.TotalFood);
            return sb.ToString();
        }

        // Rows hold buttons, so they are rebuilt only when what they show changes.
        private void RebuildStalls(MarketReport r)
        {
            var sb = new StringBuilder();
            foreach (var s in r.Stalls)
                sb.Append(s.FootprintId).Append(':').Append(s.FamilyText).Append(':').Append(s.IsBuilt).Append(':').Append(s.Items)
                  .Append(':').Append(s.CanDeconstruct).Append(':').Append(s.DeconstructReason).Append('|');
            string sig = sb.ToString();
            if (sig == _signature) return;
            _signature = sig;
            _stallBox.Clear();
            int n = 0;
            foreach (var s in r.Stalls)
            {
                n++;
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                string state = s.IsBuilt ? s.FamilyText + ", " + s.Items + " items" : "Not built yet (" + s.FamilyText + ")";
                row.Add(MakeLabel("market-stall-" + s.FootprintId, 12, false, "Stall " + n + ": " + state));
                int id = s.FootprintId;
                if (s.CanDeconstruct)
                {
                    var b = new Button { name = "market-deconstruct-" + id, text = "Deconstruct" };
                    Style(b);
                    b.clicked += () => ClickDeconstructStall(id);
                    row.Add(b);
                    _stallBox.Add(row);
                }
                else
                {
                    _stallBox.Add(row);
                    if (!string.IsNullOrEmpty(s.DeconstructReason) && s.IsBuilt)
                        _stallBox.Add(MakeLabel("market-reason-" + id, 11, false, s.DeconstructReason, new Color(200f / 255f, 170f / 255f, 120f / 255f)));
                }
            }
            UiFocusRules.Apply(_panel);
        }

        // MK-41: houses in range green, out of range red, while the panel is open.
        private void RefreshTint(SimWorld world, int marketId)
        {
            Market market = world.Markets.Get(marketId);
            if (market == null || world.Areas.Get(market.AreaId) == null) { HideTint(); return; }
            string key = marketId + ":" + world.Footprints.Version + ":" + world.Areas.Get(market.AreaId).Polygon.Count;
            if (key == _tintKey && _overlay != null && _overlay.activeSelf) return;
            _tintKey = key;
            var range = MarketRange.HousesInRange(world, MarketRange.CentreOf(world, market));
            var mesh = new DrapeMesh();
            foreach (int id in range.InRange) AddHouse(mesh, world, id, MarketTool.InRangeColor);
            foreach (int id in range.OutOfRange) AddHouse(mesh, world, id, MarketTool.OutOfRangeColor);
            InRangeCount = range.InRange.Count; OutOfRangeCount = range.OutOfRange.Count;
            if (_overlay == null)
            {
                _overlay = new GameObject("MarketRangeTint");
                _overlay.transform.SetParent(transform, false);
                _overlay.AddComponent<MeshFilter>();
                var mr = _overlay.AddComponent<MeshRenderer>();
                var shared = GroundOverlay.Material();
                _material = shared != null ? new Material(shared) : new Material(Shader.Find("Sprites/Default"));
                mr.sharedMaterial = _material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            if (_mesh != null) Destroy(_mesh);
            _mesh = mesh.ToMesh("MarketRangeTint");
            _overlay.GetComponent<MeshFilter>().sharedMesh = _mesh;
            _overlay.SetActive(true);
        }

        private static void AddHouse(DrapeMesh mesh, SimWorld world, int footprintId, Color colour)
        {
            var fp = world.Footprints.Get(footprintId);
            if (fp != null) mesh.Append(TerrainDrape.Fill(world.Terrain, fp.Polygon, AreaRenderer.HeightOffsetM + 0.12f, colour));
        }

        private void HideTint()
        {
            _tintKey = "";
            InRangeCount = 0; OutOfRangeCount = 0;
            if (_overlay != null) _overlay.SetActive(false);
        }

        private void OnEnable()
        {
            Build();
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
            HideTint();
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
            if (_overlay != null) Destroy(_overlay);
        }

        private void Update()
        {
            if (!_subscribed) Subscribe();
            var doc = GetComponent<UIDocument>();
            if (_panel == null || _panel.panel == null || (doc.rootVisualElement != null && _panel.panel != doc.rootVisualElement.panel))
                Build();
            Refresh();
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !ConfirmDialog.OwnsEscape && IsShown)
            {
                var controller = _controller != null ? _controller : (_controller = FindFirstObjectByType<ToolController>());
                if (controller == null || controller.ActiveTool == null) ClickClose();
            }
        }

        private SimHost Host() => _host != null ? _host : (_host = FindFirstObjectByType<SimHost>());

        private AgentPicker Agents()
        {
            if (_agents == null) _agents = FindFirstObjectByType<AgentPicker>();
            return _agents;
        }

        private void Subscribe()
        {
            var agents = Agents();
            if (agents == null || _subscribed) return;
            agents.SelectionChanged += OnSelectionChanged;
            _subscribed = true;
        }

        private void OnSelectionChanged()
        {
            _signature = "";
            SetMessage("");
            Refresh();
        }

        private void Unsubscribe()
        {
            if (_agents != null && _subscribed) _agents.SelectionChanged -= OnSelectionChanged;
            _subscribed = false;
        }

        private void Build()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;
            root.Clear();
            _signature = "";
            var holder = new VisualElement { name = "market-panel-root", pickingMode = PickingMode.Ignore };
            holder.style.position = Position.Absolute;
            holder.style.right = 12; holder.style.bottom = 90;

            _panel = new VisualElement { name = "market-panel" };
            var s = _panel.style;
            s.minWidth = 240; s.paddingLeft = s.paddingRight = 12; s.paddingTop = s.paddingBottom = 10;
            s.backgroundColor = new Color(30f / 255f, 24f / 255f, 18f / 255f, 0.92f);
            s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = 1;
            var border = new Color(110f / 255f, 100f / 255f, 85f / 255f);
            s.borderTopColor = s.borderBottomColor = s.borderLeftColor = s.borderRightColor = border;
            s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = 4;
            s.display = DisplayStyle.None;

            _close = new Button { name = "market-close", text = "X" };
            _close.style.position = Position.Absolute;
            _close.style.right = 4; _close.style.top = 4;
            _close.clicked += ClickClose;

            _title = MakeLabel("market-title", 15, true);
            _stock = MakeLabel("market-stock", 12, false);
            _stalls = MakeLabel("market-stalls", 12, false);
            _stallBox = new VisualElement { name = "market-stall-list" };
            _granaries = MakeLabel("market-granaries", 12, false);
            _served = MakeLabel("market-served", 12, false);
            _message = MakeLabel("market-message", 12, false);
            _message.style.color = new Color(1f, 0.55f, 0.45f);
            _message.style.display = DisplayStyle.None;

            _remove = new Button { name = "market-remove", text = "Remove market" };
            _remove.style.marginTop = 4;
            Style(_remove);
            _remove.clicked += ClickRemoveMarket;

            _panel.Add(_title); _panel.Add(_stock); _panel.Add(_stalls); _panel.Add(_stallBox);
            _panel.Add(_granaries); _panel.Add(_served); _panel.Add(_remove); _panel.Add(_message); _panel.Add(_close);
            holder.Add(_panel);
            root.Add(holder);
            UiFocusRules.Apply(root);
        }

        private static void Style(Button b)
        {
            b.style.color = new Color(240f / 255f, 230f / 255f, 210f / 255f);
            b.style.backgroundColor = new Color(70f / 255f, 58f / 255f, 44f / 255f);
        }

        private static Label MakeLabel(string name, int size, bool bold, string text = "", Color? colour = null)
        {
            var l = new Label { name = name, text = text };
            l.style.color = colour ?? new Color(240f / 255f, 230f / 255f, 210f / 255f);
            l.style.fontSize = size;
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginTop = 2;
            return l;
        }
    }
}
