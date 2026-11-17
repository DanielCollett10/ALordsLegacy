using System;
using System.Collections.Generic;
using System.Text;
using LordsLegacy.Game.Tools;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;
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
        private VisualElement _familyBox, _pickerBox;
        private string _signature = "";
        private bool _pickerOpen;
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

        /// <summary>Text on a family Assign button while the granary is unbuilt.</summary>
        public const string NotBuiltText = "Granary not built yet";

        /// <summary>The Assign button of a family slot, or null.</summary>
        public Button AssignButton(int slot) => _panel?.Q<Button>("assign-slot-" + slot);

        /// <summary>The label of a family slot ("Family 1: empty" / "Family 1: Archer"), or "".</summary>
        public string SlotText(int slot) => _panel?.Q<Label>("family-slot-" + slot)?.text ?? "";

        /// <summary>The members line of a family slot, or "".</summary>
        public string MembersText(int slot) => _panel?.Q<Label>("family-members-" + slot)?.text ?? "";

        /// <summary>True while the Assign list is open.</summary>
        public bool PickerOpen => _pickerOpen;

        private int? OperationOf(out SimHost host)
        {
            host = Host();
            var agents = Agents();
            if (host == null || !host.IsInitialized || agents == null || !agents.HasGranary) return null;
            var op = host.World.Operations.FindByModule(agents.SelectedGranaryId);
            return op?.Id;
        }

        /// <summary>A slot's Assign button: opens or closes the list of unassigned families (refused while unbuilt).</summary>
        public void ClickAssignSlot()
        {
            if (OperationOf(out var host) == null) return;
            if (!host.World.IsBuilt(Agents().SelectedGranaryId)) { SetMessage(NotBuiltText); return; }
            _pickerOpen = !_pickerOpen;
            Refresh();
        }

        /// <summary>A family in the Assign list: sends AssignFamily.</summary>
        public void ClickAssignFamily(int familyId)
        {
            int? op = OperationOf(out _);
            if (!op.HasValue) return;
            _pickerOpen = false;
            Run(new AssignFamily(op.Value, familyId));
        }

        /// <summary>A family row's Remove button: sends UnassignFamily.</summary>
        public void ClickRemoveFamily(int familyId)
        {
            int? op = OperationOf(out _);
            if (op.HasValue) Run(new UnassignFamily(op.Value, familyId));
        }

        private static string MembersLine(LordsLegacy.Sim.Core.SimWorld world, Family fam)
        {
            var sb = new StringBuilder();
            foreach (int cid in fam.MemberIds)
            {
                Citizen c = world.People.FindCitizen(cid);
                if (c == null || c.AgeYears(world.Tick, world.Tuning.AgeingRate) < OperationStatus.WorkingAgeYears) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(c.Name).Append(": ").Append(CitizenTaskText.Task(world, c));
            }
            return sb.ToString();
        }

        // Rows hold buttons, so they are rebuilt only when what they show changes.
        private void RebuildFamilies(LordsLegacy.Sim.Core.SimWorld world, int granaryId)
        {
            var op = world.Operations.FindByModule(granaryId);
            if (op == null) { _familyBox.Clear(); _pickerBox.Clear(); _signature = ""; return; }
            bool unbuilt = !world.IsBuilt(granaryId);
            var sb = new StringBuilder();
            sb.Append(op.Id).Append('|').Append(_pickerOpen).Append(unbuilt ? "|U" : "|B");
            var members = new List<string>();
            foreach (int id in op.FamilyIds)
            {
                Family fam = world.People.FindFamily(id);
                string m = fam != null ? MembersLine(world, fam) : "";
                members.Add(m);
                sb.Append(",a").Append(id).Append(':').Append(m);
            }
            var free = new List<Family>();
            foreach (var f in world.People.Families)
                if (f.WorkplaceId == null) { free.Add(f); sb.Append(",f").Append(f.Id); }
            string sig = sb.ToString();
            if (sig == _signature) return;
            _signature = sig;

            _familyBox.Clear();
            for (int i = 0; i < OperationTypes.Granary.FamilySlots; i++)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                if (i < op.FamilyIds.Count)
                {
                    int fid = op.FamilyIds[i];
                    Family fam = world.People.FindFamily(fid);
                    row.Add(MakeLabel("family-slot-" + i, 12, false, "Family " + (i + 1) + ": " + (fam != null ? fam.Surname : "?")));
                    var remove = new Button { name = "remove-family-" + fid, text = "Remove" };
                    remove.clicked += () => ClickRemoveFamily(fid);
                    row.Add(remove);
                    _familyBox.Add(row);
                    if (members[i].Length > 0) _familyBox.Add(MakeLabel("family-members-" + i, 11, false, members[i]));
                }
                else
                {
                    row.Add(MakeLabel("family-slot-" + i, 12, false, "Family " + (i + 1) + ": empty"));
                    var assign = new Button { name = "assign-slot-" + i, text = unbuilt ? NotBuiltText : "Assign" };
                    assign.SetEnabled(!unbuilt);
                    assign.clicked += ClickAssignSlot;
                    row.Add(assign);
                    _familyBox.Add(row);
                }
            }

            _pickerBox.Clear();
            _pickerBox.style.display = _pickerOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (_pickerOpen)
            {
                if (free.Count == 0) _pickerBox.Add(MakeLabel("assign-none", 12, false, "No unassigned families"));
                foreach (var f in free)
                {
                    int fid = f.Id;
                    var b = new Button { name = "assign-family-" + fid, text = f.Surname };
                    b.clicked += () => ClickAssignFamily(fid);
                    _pickerBox.Add(b);
                }
            }
            UiFocusRules.Apply(_panel);
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
            RebuildFamilies(host.World, agents.SelectedGranaryId);
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
            _pickerOpen = false; _signature = "";
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

            _familyBox = new VisualElement { name = "granary-families" };
            _familyBox.style.marginTop = 4;
            _pickerBox = new VisualElement { name = "granary-picker" };
            _pickerBox.style.display = DisplayStyle.None;

            _deconstruct = new Button { name = "granary-deconstruct", text = "Deconstruct" };
            _deconstruct.style.marginTop = 4;
            _deconstruct.style.color = new Color(240f / 255f, 230f / 255f, 210f / 255f);
            _deconstruct.style.backgroundColor = new Color(70f / 255f, 58f / 255f, 44f / 255f);
            _deconstruct.clicked += ClickDeconstruct;

            _panel.Add(_title); _panel.Add(_state); _panel.Add(_stock); _panel.Add(_stored);
            _panel.Add(_familyBox); _panel.Add(_pickerBox);
            _panel.Add(_deconstruct); _panel.Add(_message); _panel.Add(_close);
            holder.Add(_panel);
            root.Add(holder);
            UiFocusRules.Apply(root);
        }

        private static Label MakeLabel(string name, int size, bool bold, string text = "")
        {
            var l = new Label { name = name, text = text };
            l.style.color = new Color(240f / 255f, 230f / 255f, 210f / 255f);
            l.style.fontSize = size;
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginTop = 2;
            return l;
        }
    }
}
