using System.Collections.Generic;
using System.Text;
using LordsLegacy.Game.Tools;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Game.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Operation panel (woodcutting.md "Operation panel", WCO-10). Opens when <see cref="AgentPicker"/> selects an
    /// operation (its hut or other module, its building area or its work area). Shows modules, the status cause, two
    /// family slots with Assign / Remove, each worker's current task, logs on the ground and standing trees left in the
    /// work area, Pause / Resume, Draw work area and Use default. The Oxen tab and stock rows are shown disabled
    /// ("Coming in M9"). Every action goes through <see cref="SimHost.Execute"/>; a refusal shows its text. Reads the Sim
    /// and holds no rules. Built in code; never takes keyboard focus (UiFocusRules).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class OperationPanelView : MonoBehaviour
    {
        /// <summary>Label on the disabled Oxen tab and stock rows.</summary>
        public const string ComingInM9 = "Coming in M9";

        [SerializeField] private SimHost _host;
        [SerializeField] private AgentPicker _agents;
        [SerializeField] private ToolController _controller;

        private VisualElement _panel, _familyBox, _pickerBox;
        private Label _title, _status, _modules, _workers, _logs, _trees, _workArea, _message;
        private Button _pause, _close;
        private string _signature = "";
        private bool _pickerOpen;
        private bool _subscribed;

        /// <summary>True while the panel is shown.</summary>
        public bool IsShown => _panel != null && _panel.style.display.value != DisplayStyle.None;

        /// <summary>The panel element, for layout checks.</summary>
        public VisualElement PanelElement => _panel;

        /// <summary>Status line, e.g. "Status: Working".</summary>
        public string StatusText => _status?.text ?? "";

        /// <summary>Module list text.</summary>
        public string ModulesText => _modules?.text ?? "";

        /// <summary>Worker task lines.</summary>
        public string WorkersText => _workers?.text ?? "";

        /// <summary>Logs-on-the-ground line.</summary>
        public string LogsText => _logs?.text ?? "";

        /// <summary>Standing-trees line.</summary>
        public string TreesText => _trees?.text ?? "";

        /// <summary>Work area line.</summary>
        public string WorkAreaText => _workArea?.text ?? "";

        /// <summary>Refusal / feedback line (empty when none).</summary>
        public string MessageText => _message?.text ?? "";

        /// <summary>Text of the Pause / Resume button.</summary>
        public string PauseButtonText => _pause?.text ?? "";

        /// <summary>The element with this name inside the panel, or null.</summary>
        public T Find<T>(string name) where T : VisualElement => _panel?.Q<T>(name);

        /// <summary>Sets the references (for code that adds the component at run time).</summary>
        public void Configure(SimHost host, AgentPicker agents, ToolController controller)
        {
            Unsubscribe();
            _host = host; _agents = agents; _controller = controller;
            Subscribe();
        }

        /// <summary>Close button: clears the operation selection.</summary>
        public void ClickClose() => Agents()?.ClearOperation();

        /// <summary>An empty family slot's Assign button: shows or hides the list of unassigned families.</summary>
        public void ClickAssignSlot()
        {
            _pickerOpen = !_pickerOpen;
            Refresh();
        }

        /// <summary>A family in the Assign list: sends AssignFamily.</summary>
        public void ClickAssignFamily(int familyId)
        {
            int? op = OperationId();
            if (!op.HasValue) return;
            _pickerOpen = false;
            Run(new AssignFamily(op.Value, familyId));
        }

        /// <summary>A family row's Remove button: sends UnassignFamily.</summary>
        public void ClickRemoveFamily(int familyId)
        {
            int? op = OperationId();
            if (op.HasValue) Run(new UnassignFamily(op.Value, familyId));
        }

        /// <summary>Pause / Resume button.</summary>
        public void ClickPauseResume()
        {
            int? op = OperationId();
            var host = Host();
            if (!op.HasValue || host == null || !host.IsInitialized) return;
            var o = host.World.Operations.Get(op.Value);
            if (o == null) return;
            Run(o.Paused ? (ISimCommand)new ResumeOperation(op.Value) : new PauseOperation(op.Value));
        }

        /// <summary>Draw work area button: starts the work area tool; confirming sends SetWorkArea.</summary>
        public void ClickDrawWorkArea()
        {
            int? op = OperationId();
            var host = Host();
            if (!op.HasValue || host == null || !host.IsInitialized) return;
            var o = host.World.Operations.Get(op.Value);
            var def = o == null ? null : OperationTypes.Find(o.TypeId);
            var controller = Controller();
            if (def == null || controller == null || !controller.BeginWorkAreaDraw(op.Value, def.WorkAreaTypeId))
            {
                SetMessage("The work area tool is not available.");
                return;
            }
            SetMessage("");
        }

        /// <summary>Use default button: sends UseDefaultWorkArea.</summary>
        public void ClickUseDefault()
        {
            int? op = OperationId();
            if (op.HasValue) Run(new UseDefaultWorkArea(op.Value));
        }

        /// <summary>Shows a message line (empty hides it).</summary>
        public void SetMessage(string text)
        {
            if (_message == null) return;
            _message.text = text ?? "";
            _message.style.display = _message.text.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void Run(ISimCommand cmd)
        {
            var host = Host();
            if (host == null) return;
            var r = host.Execute(cmd);
            SetMessage(r.IsRejected ? (string.IsNullOrEmpty(r.Text) ? r.Reason.ToString() : r.Text) : "");
            Refresh();
        }

        /// <summary>Re-reads the Sim and updates the labels and visibility.</summary>
        public void Refresh()
        {
            if (_panel == null) return;
            var agents = Agents();
            var host = Host();
            if (agents == null || !agents.HasOperation || host == null || !host.IsInitialized) { Hide(); return; }
            var world = host.World;
            Operation op = world.Operations.Get(agents.SelectedOperationId);
            if (op == null) { Hide(); return; }
            var def = OperationTypes.Find(op.TypeId);

            _title.text = "Woodcutter " + op.Id;
            _status.text = "Status: " + StateText(OperationStatus.Of(world, op));
            _modules.text = ModuleText(world, op);
            RebuildFamilies(world, op, def);
            _workers.text = WorkerText(world, op);
            _logs.text = "Logs on the ground: " + GroundLogs(world, op);
            var region = OperationStatus.WorkRegion(world, op);
            _trees.text = "Standing trees in work area: " + (region == null ? 0 : world.Trees.CountStandingIn(region));
            _workArea.text = op.WorkAreaId.HasValue ? "Work area: drawn (area " + op.WorkAreaId.Value + ")"
                : "Work area: default circle round the hut" + (def != null ? " (" + def.DefaultWorkRadiusM.ToString("0") + " m)" : "");
            _pause.text = op.Paused ? "Resume" : "Pause";
            _panel.style.display = DisplayStyle.Flex;
        }

        private void Hide()
        {
            _panel.style.display = DisplayStyle.None;
            _pickerOpen = false;
        }

        private static string StateText(OperationState s)
        {
            switch (s)
            {
                case OperationState.Working: return "Working";
                case OperationState.Paused: return "Paused";
                case OperationState.MissingHut: return "Missing hut";
                case OperationState.MissingStorage: return "Missing log storage";
                case OperationState.NoWorkers: return "No workers";
                case OperationState.NoTrees: return "No trees";
                case OperationState.StorageFull: return "Storage full";
                default: return s.ToString();
            }
        }

        private static string ModuleText(Sim.Core.SimWorld world, Operation op)
        {
            var def = OperationTypes.Find(op.TypeId);
            var sb = new StringBuilder();
            bool hut = false, storage = false;
            foreach (int id in op.ModuleIds)
            {
                Footprint f = world.Footprints.Get(id);
                if (f == null) continue;
                if (def != null && f.Type.Id == def.HutTypeId) { hut = true; sb.AppendLine("Hut"); }
                else if (def != null && f.Type.Id == def.LogStorageTypeId) { storage = true; sb.AppendLine("Log storage (" + f.Type.LogCapacity + " logs)"); }
                else sb.AppendLine(f.Type.Id);
            }
            if (!hut) sb.AppendLine("Hut: missing");
            if (!storage) sb.AppendLine("Log storage: missing");
            return sb.ToString().TrimEnd();
        }

        private static int GroundLogs(Sim.Core.SimWorld world, Operation op)
        {
            int n = 0;
            foreach (var l in world.Inventory.Locations)
                if (l.Kind == LocationKind.GroundPile && l.OwnerOperationId == op.Id) n += l.CountOf(Items.Log.Id);
            return n;
        }

        private static string WorkerText(Sim.Core.SimWorld world, Operation op)
        {
            var sb = new StringBuilder();
            foreach (int fid in op.FamilyIds)
            {
                Family fam = world.People.FindFamily(fid);
                if (fam == null) continue;
                foreach (int cid in fam.MemberIds)
                {
                    Citizen c = world.People.FindCitizen(cid);
                    if (c == null || c.AgeYears(world.Tick, world.Tuning.AgeingRate) < OperationStatus.WorkingAgeYears) continue;
                    sb.AppendLine(c.Name + " " + fam.Surname + ": " + TaskText(world, c));
                }
            }
            return sb.Length == 0 ? "No workers assigned" : sb.ToString().TrimEnd();
        }

        private static string TaskText(Sim.Core.SimWorld world, Citizen c)
        {
            foreach (TaskRecord t in world.Tasks.ByAssignee(c.Id))
            {
                if (t.IsFinished) continue;
                if (t.Def.Id == WoodcuttingSystem.FellTaskId)
                    return t.CurrentStep < t.Def.Steps.Count && t.Def.Steps[t.CurrentStep].Kind == StepKind.MoveTo
                        ? "Walking to a tree" : "Felling a tree";
                return t.Def.Id;
            }
            return c.Activity == Activity.MillingAbout ? "Milling about" : c.Activity.ToString();
        }

        // The family rows and the Assign list hold buttons, so they are rebuilt only when what they show changes.
        private void RebuildFamilies(Sim.Core.SimWorld world, Operation op, OperationTypeDef def)
        {
            var sb = new StringBuilder();
            sb.Append(op.Id).Append('|').Append(_pickerOpen);
            foreach (int id in op.FamilyIds) sb.Append(",a").Append(id);
            var free = new List<Family>();
            foreach (var f in world.People.Families)
                if (f.WorkplaceId == null) { free.Add(f); sb.Append(",f").Append(f.Id); }
            string sig = sb.ToString();
            if (sig == _signature) return;
            _signature = sig;

            _familyBox.Clear();
            int slots = def != null ? def.FamilySlots : 0;
            for (int i = 0; i < slots; i++)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                if (i < op.FamilyIds.Count)
                {
                    int fid = op.FamilyIds[i];
                    Family fam = world.People.FindFamily(fid);
                    row.Add(MakeLabel("family-slot-" + i, 12, fam != null ? "Family " + (i + 1) + ": " + fam.Surname : "Family " + (i + 1) + ": ?"));
                    var remove = new Button { name = "remove-family-" + fid, text = "Remove" };
                    remove.clicked += () => ClickRemoveFamily(fid);
                    row.Add(remove);
                }
                else
                {
                    row.Add(MakeLabel("family-slot-" + i, 12, "Family " + (i + 1) + ": empty"));
                    var assign = new Button { name = "assign-slot-" + i, text = "Assign" };
                    assign.clicked += ClickAssignSlot;
                    row.Add(assign);
                }
                _familyBox.Add(row);
            }

            _pickerBox.Clear();
            _pickerBox.style.display = _pickerOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (_pickerOpen)
            {
                if (free.Count == 0) _pickerBox.Add(MakeLabel("assign-none", 12, "No unassigned families"));
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

        private void OnEnable()
        {
            Build();
            Subscribe();
            Refresh();
            // The work area drawn while this panel is open (T-282). Added here so the scene file does not change.
            var work = GetComponent<WorkAreaRenderer>();
            if (work == null) work = gameObject.AddComponent<WorkAreaRenderer>();
            work.Configure(Host(), Agents(), this);
        }

        private void OnDisable() => Unsubscribe();

        private void Update()
        {
            if (!_subscribed) Subscribe();
            var doc = GetComponent<UIDocument>();
            if (_panel == null || _panel.panel == null || (doc.rootVisualElement != null && _panel.panel != doc.rootVisualElement.panel))
                Build();
            ShowWorkAreaRefusals();
            Refresh();
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && IsShown)
            {
                var c = Controller();
                if (c == null || c.ActiveTool == null) ClickClose();
            }
        }

        // A SetWorkArea sent by the area tool on confirm can be refused; show its text like any panel action.
        private void ShowWorkAreaRefusals()
        {
            if (!IsShown) return;
            foreach (var tool in FindObjectsByType<AreaTool>(FindObjectsSortMode.None))
            {
                if (tool.LastLinkResult == null) continue;
                var r = tool.TakeLinkResult();
                if (r.IsRejected) SetMessage(string.IsNullOrEmpty(r.Text) ? r.Reason.ToString() : r.Text);
            }
        }

        private AgentPicker Agents()
        {
            if (_agents == null) _agents = FindFirstObjectByType<AgentPicker>();
            return _agents;
        }

        private SimHost Host()
        {
            if (_host == null) _host = FindFirstObjectByType<SimHost>();
            return _host;
        }

        private ToolController Controller()
        {
            if (_controller == null) _controller = FindFirstObjectByType<ToolController>();
            return _controller;
        }

        private int? OperationId()
        {
            var a = Agents();
            return a != null && a.HasOperation ? a.SelectedOperationId : (int?)null;
        }

        private void Subscribe()
        {
            var agents = Agents();
            if (agents == null || _subscribed) return;
            agents.SelectionChanged += OnSelectionChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_agents != null && _subscribed) _agents.SelectionChanged -= OnSelectionChanged;
            _subscribed = false;
        }

        private void OnSelectionChanged()
        {
            _pickerOpen = false;
            _signature = "";
            SetMessage("");
            Refresh();
        }

        private void Build()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;
            root.Clear();
            _signature = "";
            var holder = new VisualElement { name = "operation-panel-root", pickingMode = PickingMode.Ignore };
            holder.style.position = Position.Absolute;
            holder.style.right = 12; holder.style.bottom = 90;

            _panel = new VisualElement { name = "operation-panel" };
            var s = _panel.style;
            s.minWidth = 280; s.paddingLeft = s.paddingRight = 12; s.paddingTop = s.paddingBottom = 10;
            s.backgroundColor = new Color(30f / 255f, 24f / 255f, 18f / 255f, 0.92f);
            s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = 1;
            var border = new Color(110f / 255f, 100f / 255f, 85f / 255f);
            s.borderTopColor = s.borderBottomColor = s.borderLeftColor = s.borderRightColor = border;
            s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = 4;
            s.display = DisplayStyle.None;

            _close = new Button { name = "operation-close", text = "X" };
            _close.style.position = Position.Absolute;
            _close.style.right = 4; _close.style.top = 4;
            _close.clicked += ClickClose;

            _title = MakeLabel("operation-title", 15, "");
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _status = MakeLabel("operation-status", 12, "");
            _modules = MakeLabel("operation-modules", 12, "");
            _familyBox = new VisualElement { name = "operation-families" };
            _pickerBox = new VisualElement { name = "operation-assign-list" };
            _pickerBox.style.display = DisplayStyle.None;
            _workers = MakeLabel("operation-workers", 12, "");
            _logs = MakeLabel("operation-logs", 12, "");
            _trees = MakeLabel("operation-trees", 12, "");
            _workArea = MakeLabel("operation-work-area", 12, "");

            var areaRow = new VisualElement();
            areaRow.style.flexDirection = FlexDirection.Row;
            var draw = new Button { name = "draw-work-area", text = "Draw work area" };
            draw.clicked += ClickDrawWorkArea;
            var useDefault = new Button { name = "use-default-work-area", text = "Use default" };
            useDefault.clicked += ClickUseDefault;
            areaRow.Add(draw); areaRow.Add(useDefault);

            _pause = new Button { name = "pause-resume", text = "Pause" };
            _pause.clicked += ClickPauseResume;

            var oxen = new Button { name = "oxen-tab", text = "Oxen · " + ComingInM9 };
            oxen.SetEnabled(false);
            var stock = new VisualElement { name = "stock-rows" };
            foreach (string[] row in new[] { new[] { "stock-row-pool", "Log pool (total / reserved / available)" }, new[] { "stock-row-camp", "Camp stockpile" } })
            {
                var l = MakeLabel(row[0], 12, row[1] + " · " + ComingInM9);
                l.SetEnabled(false);
                stock.Add(l);
            }

            _message = MakeLabel("operation-message", 12, "");
            _message.style.color = new Color(1f, 0.55f, 0.45f);
            _message.style.display = DisplayStyle.None;

            _panel.Add(_title); _panel.Add(_status); _panel.Add(_modules);
            _panel.Add(MakeLabel("operation-families-heading", 12, "Families"));
            _panel.Add(_familyBox); _panel.Add(_pickerBox);
            _panel.Add(MakeLabel("operation-workers-heading", 12, "Workers"));
            _panel.Add(_workers); _panel.Add(_logs); _panel.Add(_trees); _panel.Add(_workArea);
            _panel.Add(areaRow); _panel.Add(_pause); _panel.Add(oxen); _panel.Add(stock);
            _panel.Add(_message); _panel.Add(_close);
            holder.Add(_panel);
            root.Add(holder);
            UiFocusRules.Apply(root);
        }

        private static Label MakeLabel(string name, int size, string text)
        {
            var l = new Label { name = name, text = text };
            l.style.color = new Color(240f / 255f, 230f / 255f, 210f / 255f);
            l.style.fontSize = size;
            l.style.marginTop = 2;
            return l;
        }
    }
}
