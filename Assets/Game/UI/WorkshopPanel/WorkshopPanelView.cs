using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Operations.Workshops;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Info panel for a hewing station or sawpit chosen through <see cref="AgentPicker"/> (T-527): name, status,
    /// input and output stock, the assigned family with Assign / Unassign, and Deconstruct behind a confirm dialog.
    /// Everything shown comes from <see cref="WorkshopReport"/>; commands go through <see cref="SimHost"/>.
    /// Built in code, closes with its button or Escape (no tool active).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class WorkshopPanelView : MonoBehaviour
    {
        [SerializeField] private SimHost _host;
        [SerializeField] private AgentPicker _agents;
        [SerializeField] private Tools.ToolController _controller;

        private VisualElement _panel, _pickerBox;
        private Label _title, _status, _input, _output, _family, _message;
        private Button _assign, _unassign, _deconstruct, _close;
        private VisualElement _reserveRow;
        private IntegerField _reserveField;
        private Label _reserveItems;
        private bool _pickerOpen;
        private string _pickerSignature = "";
        private bool _subscribed;

        /// <summary>True while the panel is shown.</summary>
        public bool IsShown => _panel != null && _panel.style.display.value != DisplayStyle.None;
        /// <summary>Title line ("Hewing station" / "Sawpit").</summary>
        public string TitleText => _title?.text ?? "";
        /// <summary>Status line, e.g. "Waiting for logs".</summary>
        public string StatusText => _status?.text ?? "";
        /// <summary>Input line, e.g. "Input: 3 / 20 logs".</summary>
        public string InputText => _input?.text ?? "";
        /// <summary>Output line, e.g. "Output: 6 / 30 beams".</summary>
        public string OutputText => _output?.text ?? "";
        /// <summary>Family line, e.g. "Family: Archer" or "Family: none".</summary>
        public string FamilyText => _family?.text ?? "";
        /// <summary>Message line (a refused command); empty when none.</summary>
        public string MessageText => _message?.text ?? "";
        /// <summary>True while the Assign list is open.</summary>
        public bool PickerOpen => _pickerOpen;
        /// <summary>The panel element, for layout checks.</summary>
        public VisualElement PanelElement => _panel;
        /// <summary>The "Keep in storage" integer field, or null.</summary>
        public IntegerField ReserveField => _reserveField;
        /// <summary>The reserve row (caption, field, item name), or null.</summary>
        public VisualElement ReserveRow => _reserveRow;
        /// <summary>The item name after the reserve field, e.g. "logs".</summary>
        public string ReserveItemsText => _reserveItems?.text ?? "";
        /// <summary>The Assign button, or null.</summary>
        public Button AssignButton => _assign;
        /// <summary>The Unassign button, or null.</summary>
        public Button UnassignButton => _unassign;
        /// <summary>The Deconstruct button, or null.</summary>
        public Button DeconstructButton => _deconstruct;

        /// <summary>Sets the references (for code that adds the component at run time).</summary>
        public void Configure(SimHost host, AgentPicker agents, Tools.ToolController controller)
        {
            Unsubscribe();
            _host = host; _agents = agents; _controller = controller;
            Subscribe();
        }

        /// <summary>Presses the close button as if clicked.</summary>
        public void ClickClose() => Agents()?.ClearWorkshop();

        /// <summary>Assign button: opens or closes the list of unassigned families.</summary>
        public void ClickAssign()
        {
            if (!TryOperation(out _, out _)) return;
            _pickerOpen = !_pickerOpen;
            Refresh();
        }

        /// <summary>A family in the Assign list: sends AssignFamily.</summary>
        public void ClickAssignFamily(int familyId)
        {
            if (!TryOperation(out int op, out _)) return;
            _pickerOpen = false;
            Run(new AssignFamily(op, familyId));
        }

        /// <summary>Unassign button: sends UnassignFamily for the assigned family.</summary>
        public void ClickUnassign()
        {
            if (!TryOperation(out int op, out var report) || report.FamilyId == 0) return;
            Run(new UnassignFamily(op, report.FamilyId));
        }

        /// <summary>Deconstruct button: asks to confirm, then sends MarkForDeconstruction.</summary>
        public void ClickDeconstruct()
        {
            var host = Host();
            var agents = Agents();
            if (host == null || !host.IsInitialized || agents == null || !agents.HasWorkshop) return;
            int id = agents.SelectedWorkshopId;
            var world = host.World;
            if (world.Deconstruction.Get(id) != null) return;
            bool built = world.IsBuilt(id);
            ConfirmDialog.Show(built ? "Deconstruct this workshop? Its stock is lost." : "Cancel this workshop?",
                built ? "Deconstruct" : "Cancel workshop", () => Run(new MarkForDeconstruction(id)));
        }

        private bool TryOperation(out int operationId, out WorkshopReport report)
        {
            operationId = 0; report = null;
            var host = Host();
            var agents = Agents();
            if (host == null || !host.IsInitialized || agents == null || !agents.HasWorkshop) return false;
            report = WorkshopReport.Of(host.World, agents.SelectedWorkshopId);
            var op = host.World.Operations.FindByModule(agents.SelectedWorkshopId);
            if (report == null || op == null || !report.IsBuilt) return false;
            operationId = op.Id;
            return true;
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

        /// <summary>Re-reads the Sim and updates the labels and visibility.</summary>
        public void Refresh()
        {
            if (_panel == null) return;
            var agents = Agents();
            var host = Host();
            if (agents == null || !agents.HasWorkshop || host == null || !host.IsInitialized) { Hide(); return; }
            var r = WorkshopReport.Of(host.World, agents.SelectedWorkshopId);
            if (r == null) { Hide(); return; }
            _title.text = r.Name;
            _status.text = r.StatusText;
            _input.text = r.InputText;
            _output.text = r.OutputText;
            _family.text = "Family: " + (r.FamilyName ?? "none");
            var reserveOp = host.World.Operations.FindByModule(r.FootprintId);
            bool showReserve = r.IsBuilt && reserveOp != null;
            _reserveRow.style.display = showReserve ? DisplayStyle.Flex : DisplayStyle.None;
            if (showReserve)
            {
                _reserveItems.text = r.InputName + "s";
                int reserve = InputReserve.ReserveOf(host.World, reserveOp);
                if (!UiFocusRules.TextInputActive && _reserveField.value != reserve) _reserveField.SetValueWithoutNotify(reserve);
            }
            else if (_reserveField != null) UiFocusRules.ReleaseFocus(_reserveField);
            bool hasFamily = r.FamilyId != 0;
            _assign.style.display = !hasFamily ? DisplayStyle.Flex : DisplayStyle.None;
            _assign.SetEnabled(r.IsBuilt);
            _assign.text = r.IsBuilt ? "Assign" : "Not built yet";
            _unassign.style.display = hasFamily ? DisplayStyle.Flex : DisplayStyle.None;
            bool deconstructing = host.World.Deconstruction.Get(r.FootprintId) != null;
            _deconstruct.style.display = deconstructing ? DisplayStyle.None : DisplayStyle.Flex;
            _deconstruct.text = r.IsBuilt ? "Deconstruct" : "Cancel workshop";
            RebuildPicker(host.World, !hasFamily && r.IsBuilt);
            _panel.style.display = DisplayStyle.Flex;
        }

        // Hiding the panel ends any reserve edit, so the keyboard goes back to the camera (T-602).
        private void Hide()
        {
            if (_reserveField != null) UiFocusRules.ReleaseFocus(_reserveField);
            _panel.style.display = DisplayStyle.None;
        }

        private void RebuildPicker(SimWorld world, bool allowed)
        {
            bool open = _pickerOpen && allowed;
            var sb = new System.Text.StringBuilder().Append(open);
            if (open) foreach (var f in world.People.Families) if (f.WorkplaceId == null) sb.Append(',').Append(f.Id);
            if (sb.ToString() == _pickerSignature) return;
            _pickerSignature = sb.ToString();
            _pickerBox.Clear();
            _pickerBox.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!open) return;
            bool any = false;
            foreach (var f in world.People.Families)
            {
                if (f.WorkplaceId != null) continue;
                any = true;
                int fid = f.Id;
                var b = new Button { name = "workshop-assign-family-" + fid, text = f.Surname };
                b.clicked += () => ClickAssignFamily(fid);
                _pickerBox.Add(b);
            }
            if (!any) _pickerBox.Add(MakeLabel("workshop-assign-none", 12, false, "No unassigned families"));
            UiFocusRules.Apply(_panel);
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
            // Clicking anywhere outside the reserve field ends the edit.
            var mouse = Mouse.current;
            if (UiFocusRules.TextInputActive && mouse != null && mouse.leftButton.wasPressedThisFrame && _reserveField?.panel != null)
            {
                Vector2 p = mouse.position.ReadValue();
                Vector2 local = RuntimePanelUtils.ScreenToPanel(_reserveField.panel, new Vector2(p.x, Screen.height - p.y));
                if (!_reserveField.worldBound.Contains(local)) UiFocusRules.ReleaseFocus(_reserveField);
            }
            var kb = UiFocusRules.GameKeyboard;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !ConfirmDialog.OwnsEscape && IsShown)
            {
                var controller = _controller != null ? _controller : (_controller = FindFirstObjectByType<Tools.ToolController>());
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
            _pickerOpen = false; _pickerSignature = "";
            if (_reserveField != null) UiFocusRules.ReleaseFocus(_reserveField);
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
            _pickerSignature = "";
            UiFocusRules.ResetTextInput();
            var holder = new VisualElement { name = "workshop-panel-root", pickingMode = PickingMode.Ignore };
            holder.style.position = Position.Absolute;
            holder.style.right = 12; holder.style.bottom = 90;

            _panel = new VisualElement { name = "workshop-panel" };
            var s = _panel.style;
            s.minWidth = 220; s.paddingLeft = s.paddingRight = 12; s.paddingTop = s.paddingBottom = 10;
            s.backgroundColor = new Color(30f / 255f, 24f / 255f, 18f / 255f, 0.92f);
            s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = 1;
            var border = new Color(110f / 255f, 100f / 255f, 85f / 255f);
            s.borderTopColor = s.borderBottomColor = s.borderLeftColor = s.borderRightColor = border;
            s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = 4;
            s.display = DisplayStyle.None;

            _close = new Button { name = "workshop-close", text = "X" };
            _close.style.position = Position.Absolute;
            _close.style.right = 4; _close.style.top = 4;
            _close.clicked += ClickClose;

            _title = MakeLabel("workshop-title", 15, true);
            _status = MakeLabel("workshop-status", 12, false);
            _input = MakeLabel("workshop-input", 12, false);
            _output = MakeLabel("workshop-output", 12, false);
            _family = MakeLabel("workshop-family", 12, false);
            BuildReserveRow();
            _message = MakeLabel("workshop-message", 12, false);
            _message.style.color = new Color(1f, 0.55f, 0.45f);
            _message.style.display = DisplayStyle.None;

            _assign = MakeButton("workshop-assign", "Assign", ClickAssign);
            _unassign = MakeButton("workshop-unassign", "Unassign", ClickUnassign);
            _deconstruct = MakeButton("workshop-deconstruct", "Deconstruct", ClickDeconstruct);
            _pickerBox = new VisualElement { name = "workshop-picker" };
            _pickerBox.style.display = DisplayStyle.None;

            _panel.Add(_title); _panel.Add(_status); _panel.Add(_input); _panel.Add(_output); _panel.Add(_family);
            _panel.Add(_reserveRow);
            _panel.Add(_assign); _panel.Add(_unassign); _panel.Add(_pickerBox);
            _panel.Add(_deconstruct); _panel.Add(_message); _panel.Add(_close);
            holder.Add(_panel);
            root.Add(holder);
            UiFocusRules.Apply(root);
        }

        private void BuildReserveRow()
        {
            var light = new Color(240f / 255f, 230f / 255f, 210f / 255f);
            _reserveRow = new VisualElement { name = "workshop-reserve-row" };
            _reserveRow.style.flexDirection = FlexDirection.Row;
            _reserveRow.style.alignItems = Align.Center;
            _reserveRow.style.marginTop = 2;
            var caption = MakeLabel("workshop-reserve-caption", 12, false, "Keep in storage:");
            _reserveField = new IntegerField { name = "workshop-reserve", isDelayed = true };
            _reserveField.style.width = 64;
            _reserveField.style.marginLeft = _reserveField.style.marginRight = 4;
            var input = _reserveField.Q(className: TextInputBaseField<int>.inputUssClassName);
            if (input != null)
            {
                input.style.color = light;
                input.style.backgroundColor = new Color(55f / 255f, 46f / 255f, 36f / 255f);
            }
            _reserveField.RegisterValueChangedCallback(OnReserveChanged);
            UiFocusRules.AllowTextInput(_reserveField);
            _reserveItems = MakeLabel("workshop-reserve-items", 12, false);
            _reserveRow.Add(caption); _reserveRow.Add(_reserveField); _reserveRow.Add(_reserveItems);
            _reserveRow.style.display = DisplayStyle.None;
        }

        private void OnReserveChanged(ChangeEvent<int> e)
        {
            if (!TryOperation(out int op, out _)) return;
            var world = Host().World;
            var def = OperationTypes.WorkshopOf(world.Operations.Get(op).TypeId, world.Tuning);
            Run(new SetWorkshopReserve(op, def.Input.Id, e.newValue));
            // Accepted or refused, show what the Sim holds (a refused value snaps back).
            _reserveField.SetValueWithoutNotify(InputReserve.ReserveOf(world, world.Operations.Get(op)));
        }

        private static Button MakeButton(string name, string text, System.Action onClick)
        {
            var b = new Button { name = name, text = text };
            b.style.marginTop = 4;
            b.style.color = new Color(240f / 255f, 230f / 255f, 210f / 255f);
            b.style.backgroundColor = new Color(70f / 255f, 58f / 255f, 44f / 255f);
            b.clicked += onClick;
            return b;
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
