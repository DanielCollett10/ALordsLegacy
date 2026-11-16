using System;
using System.Text;
using LordsLegacy.Game.Tools;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Food;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Info panel for the granary chosen through <see cref="AgentPicker"/> (T-449): state and progress, the stored
    /// food by type with total / capacity, and Deconstruct (Cancel granary while unbuilt) behind a confirm dialog.
    /// Everything shown comes from <see cref="GranaryReport"/>; commands go through <see cref="SimHost"/>.
    /// Built in code, never takes keyboard focus, closes with its button or Escape (no tool active).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GranaryPanelView : MonoBehaviour
    {
        [SerializeField] private SimHost _host;
        [SerializeField] private AgentPicker _agents;
        [SerializeField] private ToolController _controller;

        private VisualElement _panel;
        private Label _title, _state, _stock, _stored, _message;
        private Button _deconstruct, _close;
        private bool _subscribed;

        /// <summary>True while the panel is shown.</summary>
        public bool IsShown => _panel != null && _panel.style.display.value != DisplayStyle.None;

        /// <summary>Title line text ("Granary").</summary>
        public string TitleText => _title?.text ?? "";

        /// <summary>State line, e.g. "Delivering 3 / 8 logs", "Building 40%", "Built", "Deconstructing 20%".</summary>
        public string StateText => _state?.text ?? "";

        /// <summary>One line per food type, e.g. "Meat: 0\nBerries: 40\nMushrooms: 0".</summary>
        public string StockText => _stock?.text ?? "";

        /// <summary>Total line, e.g. "Stored: 43 / 100 (3 reserved)".</summary>
        public string StoredText => _stored?.text ?? "";

        /// <summary>Message line text (a refused command); empty when none.</summary>
        public string MessageText => _message?.text ?? "";

        /// <summary>True while the Deconstruct button is visible.</summary>
        public bool DeconstructVisible => _deconstruct != null && _deconstruct.style.display.value != DisplayStyle.None;

        /// <summary>Text of the Deconstruct button ("Deconstruct" or "Cancel granary").</summary>
        public string DeconstructText => _deconstruct?.text ?? "";

        /// <summary>The panel element, for layout checks.</summary>
        public VisualElement PanelElement => _panel;

        /// <summary>Sets the references (for code that adds the component at run time).</summary>
        public void Configure(SimHost host, AgentPicker agents, ToolController controller)
        {
            Unsubscribe();
            _host = host; _agents = agents; _controller = controller;
            Subscribe();
        }

        /// <summary>Presses the close button as if clicked.</summary>
        public void ClickClose() => Agents()?.ClearGranary();

        /// <summary>Deconstruct button: asks to confirm, then sends MarkForDeconstruction.</summary>
        public void ClickDeconstruct()
        {
            var host = Host();
            var agents = Agents();
            if (host == null || !host.IsInitialized || agents == null || !agents.HasGranary) return;
            int id = agents.SelectedGranaryId;
            var world = host.World;
            if (world.Deconstruction.Get(id) != null) return;
            string text = GranaryReport.ConfirmText(world, id);
            if (text == null) return;
            ConfirmDialog.Show(text, world.IsBuilt(id) ? "Deconstruct" : "Cancel granary",
                () => Run(new MarkForDeconstruction(id)));
        }

        /// <summary>State line text for a report.</summary>
        public static string StateLine(GranaryReport r)
        {
            switch (r.State)
            {
                case GranaryState.Delivering: return "Delivering " + r.LogsDelivered + " / " + r.LogsNeeded + " logs";
                case GranaryState.Building: return "Building " + r.BuildPercent + "%";
                case GranaryState.Deconstructing: return "Deconstructing " + r.DeconstructPercent + "%";
                default: return "Built";
            }
        }

        /// <summary>Food lines for a report, one per type, in the report's order.</summary>
        public static string StockLines(GranaryReport r)
        {
            var sb = new StringBuilder();
            foreach (var kv in r.Stock)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(ItemLabel(kv.Key)).Append(": ").Append(kv.Value);
            }
            return sb.ToString();
        }

        /// <summary>Total line for a report, e.g. "Stored: 43 / 100 (3 reserved)".</summary>
        public static string StoredLine(GranaryReport r)
        {
            string s = "Stored: " + r.Total + " / " + r.Capacity;
            if (r.Reserved > 0) s += " (" + r.Reserved + " reserved)";
            return s;
        }

        private static string ItemLabel(string id)
        {
            string name = id.StartsWith("item.", StringComparison.Ordinal) ? id.Substring(5) : id;
            return name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        private void Run(LordsLegacy.Sim.Commands.ISimCommand cmd)
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

        /// <summary>Re-reads the Sim and updates the labels and visibility.</summary>
        public void Refresh()
        {
            if (_panel == null) return;
            var agents = Agents();
            var host = Host();
            if (agents == null || !agents.HasGranary || host == null || !host.IsInitialized) { _panel.style.display = DisplayStyle.None; return; }
            var r = GranaryReport.Of(host.World, agents.SelectedGranaryId);
            if (r == null) { _panel.style.display = DisplayStyle.None; return; }
            _title.text = "Granary";
            _state.text = StateLine(r);
            _stock.text = StockLines(r);
            _stored.text = StoredLine(r);
            _deconstruct.style.display = r.State == GranaryState.Deconstructing ? DisplayStyle.None : DisplayStyle.Flex;
            _deconstruct.text = r.IsBuilt ? "Deconstruct" : "Cancel granary";
            _panel.style.display = DisplayStyle.Flex;
        }

        private void OnEnable()
        {
            Build();
            Subscribe();
            Refresh();
        }

        private void OnDisable() => Unsubscribe();

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
            var holder = new VisualElement { name = "granary-panel-root", pickingMode = PickingMode.Ignore };
            holder.style.position = Position.Absolute;
            holder.style.right = 12; holder.style.bottom = 90;

            _panel = new VisualElement { name = "granary-panel" };
            var s = _panel.style;
            s.minWidth = 220; s.paddingLeft = s.paddingRight = 12; s.paddingTop = s.paddingBottom = 10;
            s.backgroundColor = new Color(30f / 255f, 24f / 255f, 18f / 255f, 0.92f);
            s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = 1;
            var border = new Color(110f / 255f, 100f / 255f, 85f / 255f);
            s.borderTopColor = s.borderBottomColor = s.borderLeftColor = s.borderRightColor = border;
            s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = 4;
            s.display = DisplayStyle.None;

            _close = new Button { name = "granary-close", text = "X" };
            _close.style.position = Position.Absolute;
            _close.style.right = 4; _close.style.top = 4;
            _close.clicked += ClickClose;

            _title = MakeLabel("granary-title", 15, true);
            _state = MakeLabel("granary-state", 12, false);
            _stock = MakeLabel("granary-stock", 12, false);
            _stored = MakeLabel("granary-stored", 12, false);
            _message = MakeLabel("granary-message", 12, false);
            _message.style.color = new Color(1f, 0.55f, 0.45f);
            _message.style.display = DisplayStyle.None;

            _deconstruct = new Button { name = "granary-deconstruct", text = "Deconstruct" };
            _deconstruct.style.marginTop = 4;
            _deconstruct.style.color = new Color(240f / 255f, 230f / 255f, 210f / 255f);
            _deconstruct.style.backgroundColor = new Color(70f / 255f, 58f / 255f, 44f / 255f);
            _deconstruct.clicked += ClickDeconstruct;

            _panel.Add(_title); _panel.Add(_state); _panel.Add(_stock); _panel.Add(_stored);
            _panel.Add(_deconstruct); _panel.Add(_message); _panel.Add(_close);
            holder.Add(_panel);
            root.Add(holder);
            UiFocusRules.Apply(root);
        }

        private static Label MakeLabel(string name, int size, bool bold)
        {
            var l = new Label { name = name };
            l.style.color = new Color(240f / 255f, 230f / 255f, 210f / 255f);
            l.style.fontSize = size;
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginTop = 2;
            return l;
        }
    }
}
