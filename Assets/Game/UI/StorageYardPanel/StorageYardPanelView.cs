using System;
using System.Text;
using LordsLegacy.Game.Tools;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Operations;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Info panel for a storage yard chosen through <see cref="AgentPicker"/> (T-549, storage.md ST-10, ST-45): status, stock per
    /// General item with used / capacity, family assign and unassign, a greyed "Supplies market" toggle ("No demand yet") and
    /// Deconstruct behind a confirm dialog. Everything shown comes from <see cref="StorageYardReport"/>; commands go through
    /// <see cref="SimHost"/>. Built in code, never takes keyboard focus, closes with its button or Escape (no tool active).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class StorageYardPanelView : MonoBehaviour
    {
        [SerializeField] private SimHost _host;
        [SerializeField] private AgentPicker _agents;
        [SerializeField] private ToolController _controller;

        private VisualElement _panel, _familyBox, _pickerBox;
        private Label _title, _status, _stock, _stored, _message;
        private Toggle _supplies;
        private Button _deconstruct, _close;
        private string _signature = "";
        private bool _pickerOpen, _subscribed;

        /// <summary>Tooltip of the greyed supplies toggle.</summary>
        public const string SuppliesTooltip = "No demand yet";
        /// <summary>Text on a family Assign button while the yard is unbuilt.</summary>
        public const string NotBuiltText = "Yard not built yet";

        /// <summary>True while the panel is shown.</summary>
        public bool IsShown => _panel != null && _panel.style.display.value != DisplayStyle.None;
        /// <summary>Title line ("Storage yard").</summary>
        public string TitleText => _title?.text ?? "";
        /// <summary>Status line (ST-45), e.g. "No workers".</summary>
        public string StatusText => _status?.text ?? "";
        /// <summary>One line per General item, e.g. "Plank: 4".</summary>
        public string StockText => _stock?.text ?? "";
        /// <summary>Total line, e.g. "Stored: 43 / 300".</summary>
        public string StoredText => _stored?.text ?? "";
        /// <summary>Message line text (a refused command); empty when none.</summary>
        public string MessageText => _message?.text ?? "";
        /// <summary>The supplies market toggle, or null before the panel is built.</summary>
        public Toggle SuppliesToggle => _supplies;
        /// <summary>True while the Deconstruct button is visible.</summary>
        public bool DeconstructVisible => _deconstruct != null && _deconstruct.style.display.value != DisplayStyle.None;
        /// <summary>Text of the Deconstruct button.</summary>
        public string DeconstructText => _deconstruct?.text ?? "";
        /// <summary>The panel element, for layout checks.</summary>
        public VisualElement PanelElement => _panel;
        /// <summary>True while the Assign list is open.</summary>
        public bool PickerOpen => _pickerOpen;
        /// <summary>The Assign button of a family slot, or null.</summary>
        public Button AssignButton(int slot) => _panel?.Q<Button>("yard-assign-slot-" + slot);
        /// <summary>The label of a family slot ("Family 1: empty" / "Family 1: Archer"), or "".</summary>
        public string SlotText(int slot) => _panel?.Q<Label>("yard-family-slot-" + slot)?.text ?? "";

        /// <summary>Sets the references (for code that adds the component at run time).</summary>
        public void Configure(SimHost host, AgentPicker agents, ToolController controller)
        {
            Unsubscribe();
            _host = host; _agents = agents; _controller = controller;
            Subscribe();
        }

        /// <summary>Presses the close button as if clicked.</summary>
        public void ClickClose() => Agents()?.ClearWorkshop();

        private bool TryYard(out SimHost host, out int footprintId)
        {
            host = Host();
            var agents = Agents();
            footprintId = agents != null ? agents.SelectedWorkshopId : -1;
            return host != null && host.IsInitialized && agents != null && agents.HasYard;
        }

        /// <summary>Deconstruct button: asks to confirm, then sends MarkForDeconstruction.</summary>
        public void ClickDeconstruct()
        {
            if (!TryYard(out var host, out int id)) return;
            var world = host.World;
            if (world.Deconstruction.Get(id) != null) return;
            bool built = world.IsBuilt(id);
            ConfirmDialog.Show(built ? "Deconstruct this storage yard? Its stock is lost." : "Cancel this storage yard?",
                built ? "Deconstruct" : "Cancel yard", () => Run(new MarkForDeconstruction(id)));
        }

        /// <summary>A slot's Assign button: opens or closes the list of unassigned families (refused while unbuilt).</summary>
        public void ClickAssignSlot()
        {
            if (!TryYard(out var host, out int id)) return;
            if (!host.World.IsBuilt(id)) { SetMessage(NotBuiltText); return; }
            _pickerOpen = !_pickerOpen;
            Refresh();
        }

        /// <summary>A family in the Assign list: sends AssignFamily.</summary>
        public void ClickAssignFamily(int familyId)
        {
            if (!TryYard(out var host, out int id)) return;
            var op = host.World.Operations.FindByModule(id);
            if (op == null) return;
            _pickerOpen = false;
            Run(new AssignFamily(op.Id, familyId));
        }

        /// <summary>A family row's Remove button: sends UnassignFamily.</summary>
        public void ClickRemoveFamily(int familyId)
        {
            if (!TryYard(out var host, out int id)) return;
            var op = host.World.Operations.FindByModule(id);
            if (op != null) Run(new UnassignFamily(op.Id, familyId));
        }

        /// <summary>Food lines for a report, one per General item in the report's order.</summary>
        public static string StockLines(StorageYardReport r)
        {
            var sb = new StringBuilder();
            foreach (var kv in r.Stock)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(ItemLabel(kv.Key)).Append(": ").Append(kv.Value);
            }
            return sb.ToString();
        }

        /// <summary>Total line for a report, e.g. "Stored: 43 / 300".</summary>
        public static string StoredLine(StorageYardReport r) => "Stored: " + r.Total + " / " + r.Capacity;

        private static string ItemLabel(string id)
        {
            string name = id.StartsWith("item.", StringComparison.Ordinal) ? id.Substring(5) : id;
            return name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

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

        // Rows hold buttons, so they are rebuilt only when what they show changes.
        private void RebuildFamilies(StorageYardReport r)
        {
            var world = Host().World;
            bool unbuilt = !r.IsBuilt;
            var sig = new StringBuilder();
            sig.Append(r.OperationId).Append('|').Append(_pickerOpen).Append(unbuilt ? "|U" : "|B");
            foreach (int fid in r.FamilyIds) sig.Append(",a").Append(fid);
            var free = new System.Collections.Generic.List<LordsLegacy.Sim.People.Family>();
            foreach (var f in world.People.Families)
                if (f.WorkplaceId == null) { free.Add(f); sig.Append(",f").Append(f.Id); }
            string s = sig.ToString();
            if (s == _signature) return;
            _signature = s;

            _familyBox.Clear();
            for (int i = 0; i < OperationTypes.StorageYard.FamilySlots; i++)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                if (i < r.FamilyIds.Count)
                {
                    int fid = r.FamilyIds[i];
                    var fam = world.People.FindFamily(fid);
                    row.Add(MakeLabel("yard-family-slot-" + i, 12, false, "Family " + (i + 1) + ": " + (fam != null ? fam.Surname : "?")));
                    var remove = new Button { name = "yard-remove-family-" + fid, text = "Remove" };
                    remove.clicked += () => ClickRemoveFamily(fid);
                    Style(remove);
                    row.Add(remove);
                }
                else
                {
                    row.Add(MakeLabel("yard-family-slot-" + i, 12, false, "Family " + (i + 1) + ": empty"));
                    var assign = new Button { name = "yard-assign-slot-" + i, text = unbuilt ? NotBuiltText : "Assign" };
                    assign.SetEnabled(!unbuilt);
                    assign.clicked += ClickAssignSlot;
                    Style(assign);
                    row.Add(assign);
                }
                _familyBox.Add(row);
            }

            _pickerBox.Clear();
            _pickerBox.style.display = _pickerOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (_pickerOpen)
            {
                if (free.Count == 0) _pickerBox.Add(MakeLabel("yard-assign-none", 12, false, "No unassigned families"));
                foreach (var f in free)
                {
                    int fid = f.Id;
                    var b = new Button { name = "yard-assign-family-" + fid, text = f.Surname };
                    b.clicked += () => ClickAssignFamily(fid);
                    Style(b);
                    _pickerBox.Add(b);
                }
            }
            UiFocusRules.Apply(_panel);
        }

        /// <summary>Re-reads the Sim and updates the labels and visibility.</summary>
        public void Refresh()
        {
            if (_panel == null) return;
            if (!TryYard(out var host, out int id)) { _panel.style.display = DisplayStyle.None; return; }
            var r = StorageYardReport.Of(host.World, id);
            if (r == null) { _panel.style.display = DisplayStyle.None; return; }
            _title.text = "Storage yard";
            _status.text = r.StatusText;
            _stock.text = StockLines(r);
            _stored.text = StoredLine(r);
            _supplies.label = "Supplies market: " + r.SuppliesMarketText;
            _supplies.SetValueWithoutNotify(false);
            _supplies.SetEnabled(r.SuppliesMarketAvailable);
            _supplies.tooltip = r.SuppliesMarketAvailable ? "" : SuppliesTooltip;
            RebuildFamilies(r);
            bool deconstructing = host.World.Deconstruction.Get(id) != null;
            _deconstruct.style.display = deconstructing ? DisplayStyle.None : DisplayStyle.Flex;
            _deconstruct.text = r.IsBuilt ? "Deconstruct" : "Cancel yard";
            _panel.style.display = DisplayStyle.Flex;
        }

        private void OnEnable() { Build(); Subscribe(); Refresh(); }

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
            _pickerOpen = false; _signature = "";
            SetMessage("");
            Refresh();
        }

        private void Unsubscribe()
        {
            if (_agents != null && _subscribed) _agents.SelectionChanged -= OnSelectionChanged;
            _subscribed = false;
        }

        private static readonly Color Text = new Color(240f / 255f, 230f / 255f, 210f / 255f);
        private static readonly Color ButtonBg = new Color(70f / 255f, 58f / 255f, 44f / 255f);

        private static void Style(Button b)
        {
            b.style.color = Text;
            b.style.backgroundColor = ButtonBg;
        }

        private void Build()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;
            root.Clear();
            _signature = "";
            var holder = new VisualElement { name = "yard-panel-root", pickingMode = PickingMode.Ignore };
            holder.style.position = Position.Absolute;
            holder.style.right = 12; holder.style.bottom = 90;

            _panel = new VisualElement { name = "yard-panel" };
            var s = _panel.style;
            s.minWidth = 220; s.paddingLeft = s.paddingRight = 12; s.paddingTop = s.paddingBottom = 10;
            s.backgroundColor = new Color(30f / 255f, 24f / 255f, 18f / 255f, 0.92f);
            s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = 1;
            var border = new Color(110f / 255f, 100f / 255f, 85f / 255f);
            s.borderTopColor = s.borderBottomColor = s.borderLeftColor = s.borderRightColor = border;
            s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = 4;
            s.display = DisplayStyle.None;

            _close = new Button { name = "yard-close", text = "X" };
            _close.style.position = Position.Absolute;
            _close.style.right = 4; _close.style.top = 4;
            Style(_close);
            _close.clicked += ClickClose;

            _title = MakeLabel("yard-title", 15, true);
            _status = MakeLabel("yard-status", 12, false);
            _stock = MakeLabel("yard-stock", 12, false);
            _stored = MakeLabel("yard-stored", 12, false);
            _message = MakeLabel("yard-message", 12, false);
            _message.style.color = new Color(1f, 0.55f, 0.45f);
            _message.style.display = DisplayStyle.None;

            _supplies = new Toggle { name = "yard-supplies", label = "Supplies market: No demand yet" };
            _supplies.labelElement.style.color = Text;
            _supplies.labelElement.style.fontSize = 12;
            _supplies.style.marginTop = 2;
            _supplies.tooltip = SuppliesTooltip;
            _supplies.SetEnabled(false);

            _familyBox = new VisualElement { name = "yard-families" };
            _familyBox.style.marginTop = 4;
            _pickerBox = new VisualElement { name = "yard-picker" };
            _pickerBox.style.display = DisplayStyle.None;

            _deconstruct = new Button { name = "yard-deconstruct", text = "Deconstruct" };
            _deconstruct.style.marginTop = 4;
            Style(_deconstruct);
            _deconstruct.clicked += ClickDeconstruct;

            _panel.Add(_title); _panel.Add(_status); _panel.Add(_stock); _panel.Add(_stored);
            _panel.Add(_supplies);
            _panel.Add(_familyBox); _panel.Add(_pickerBox);
            _panel.Add(_deconstruct); _panel.Add(_message); _panel.Add(_close);
            holder.Add(_panel);
            root.Add(holder);
            UiFocusRules.Apply(root);
        }

        private static Label MakeLabel(string name, int size, bool bold, string text = "")
        {
            var l = new Label { name = name, text = text };
            l.style.color = Text;
            l.style.fontSize = size;
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginTop = 2;
            return l;
        }
    }
}
