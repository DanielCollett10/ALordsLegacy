using System.Collections.Generic;
using System.Text;
using LordsLegacy.Game.Tools;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
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
    /// work area, Pause / Resume, Draw work area and Use default. The Oxen tab lists assigned and free oxen
    /// with their state (Remove / Assign) and the stock line shows total / reserved / available of capacity (T-294). Every action goes through <see cref="SimHost.Execute"/>; a refusal shows its text. Reads the Sim
    /// and holds no rules. Built in code; never takes keyboard focus (UiFocusRules).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class OperationPanelView : MonoBehaviour
    {
        [SerializeField] private SimHost _host;
        [SerializeField] private AgentPicker _agents;
        [SerializeField] private ToolController _controller;

        private VisualElement _panel, _familyBox, _pickerBox;
        private VisualElement _oxenBox, _priorityBox, _deconBox;
        private string _deconSignature = "";
        private string _prioritySignature = "";
        private Label _title, _status, _modules, _workers, _logs, _trees, _workArea, _message, _stock;
        private Button _pause, _close, _oxenTab;
        private string _signature = "", _oxenSignature = "";
        private bool _oxenOpen;

        /// <summary>Stock line, e.g. "Log stock: 28 / 4 / 24 of 40 (...)".</summary>
        public string StockLineText => _stock?.text ?? "";
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
            var host = Host();
            var op = OperationId();
            if (host != null && host.IsInitialized && op.HasValue)
            {
                var o = host.World.Operations.Get(op.Value);
                if (o != null && HutUnbuilt(host.World, o, OperationTypes.Find(o.TypeId))) { SetMessage(HutNotBuiltText); return; }
            }
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

        /// <summary>Expand modules button (BT-20): opens the draft tool in the module stage on this operation's area.</summary>
        public void ClickExpandModules()
        {
            int? op = OperationId();
            var host = Host();
            if (!op.HasValue || host == null || !host.IsInitialized) return;
            var o = host.World.Operations.Get(op.Value);
            var controller = Controller();
            if (o == null || controller == null || !controller.BeginExpandModules(op.Value, o.TypeId))
            {
                SetMessage("The module tool is not available.");
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

            bool forage = op.TypeId == OperationTypes.ForagingId;
            _title.text = (forage ? "Forager camp " : "Woodcutter ") + op.Id;
            _status.text = "Status: " + StateText(OperationStatus.Reported(world, op));
            _modules.text = ModuleText(world, op);
            RebuildPriority(world, op);
            RebuildDeconstruct(world, op);
            RebuildFamilies(world, op, def);
            _workers.text = WorkerText(world, op);
            _logs.text = forage ? "" : "Logs waiting at tree sites: " + GroundLogs(world, op);
            _logs.style.display = forage ? DisplayStyle.None : DisplayStyle.Flex;
            _stock.text = forage ? HutStoreText(world, op) : StockText(world, op);
            RebuildOxen(world, op);
            var region = OperationStatus.WorkRegion(world, op);
            _trees.text = forage ? BushText(world, region)
                : "Standing trees in work area: " + (region == null ? 0 : world.Trees.CountStandingIn(region));
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

        // Oxen tab: assigned oxen (state + Remove) and free pool oxen (state + Assign). Rebuilt when the rows or states change.
        private void RebuildOxen(Sim.Core.SimWorld world, Operation op)
        {
            _oxenTab.text = _oxenOpen ? "Oxen (hide)" : "Oxen (" + op.OxIds.Count + ")";
            _oxenBox.style.display = _oxenOpen ? DisplayStyle.Flex : DisplayStyle.None;
            var sb = new StringBuilder();
            sb.Append(op.Id).Append('|').Append(_oxenOpen);
            if (_oxenOpen)
            {
                foreach (var ox in world.People.Oxen)
                {
                    var owner = world.Operations.FindOxOwner(ox.Id);
                    if (owner == null || owner.Id == op.Id)
                        sb.Append(',').Append(ox.Id).Append(owner == null ? 'f' : 'a').Append(OxText.State(world, ox));
                }
            }
            string sig = sb.ToString();
            if (sig == _oxenSignature) return;
            _oxenSignature = sig;
            _oxenBox.Clear();
            if (!_oxenOpen) return;
            _oxenBox.Add(MakeLabel("oxen-assigned-heading", 12, "Assigned oxen"));
            int assigned = 0, free = 0;
            foreach (var ox in world.People.Oxen)
            {
                if (world.Operations.FindOxOwner(ox.Id)?.Id != op.Id) continue;
                assigned++;
                int oxId = ox.Id;
                var row = new VisualElement { name = "oxen-assigned-" + oxId };
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.Add(MakeLabel("oxen-assigned-label-" + oxId, 12, ox.Name + ": " + OxText.State(world, ox)));
                var remove = new Button { name = "remove-ox-" + oxId, text = "Remove" };
                remove.clicked += () => ClickRemoveOx(oxId);
                row.Add(remove);
                _oxenBox.Add(row);
            }
            if (assigned == 0) _oxenBox.Add(MakeLabel("oxen-assigned-none", 12, "None assigned"));
            _oxenBox.Add(MakeLabel("oxen-pool-heading", 12, "Free oxen in the pool"));
            foreach (var ox in world.People.Oxen)
            {
                if (world.Operations.FindOxOwner(ox.Id) != null) continue;
                free++;
                int oxId = ox.Id;
                var row = new VisualElement { name = "oxen-free-" + oxId };
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.Add(MakeLabel("oxen-free-label-" + oxId, 12, ox.Name + ": " + OxText.State(world, ox)));
                var assign = new Button { name = "assign-ox-" + oxId, text = "Assign" };
                assign.clicked += () => ClickAssignOx(oxId);
                row.Add(assign);
                _oxenBox.Add(row);
            }
            if (free == 0) _oxenBox.Add(MakeLabel("oxen-free-none", 12, "No free oxen"));
            UiFocusRules.Apply(_oxenBox);
        }

        /// <summary>The Oxen tab button: shows or hides the oxen lists.</summary>
        public void ClickOxenTab()
        {
            _oxenOpen = !_oxenOpen;
            Refresh();
        }

        /// <summary>A free ox's Assign button: sends AssignOx.</summary>
        public void ClickAssignOx(int oxId)
        {
            int? op = OperationId();
            if (op.HasValue) Run(new AssignOx(op.Value, oxId));
        }

        /// <summary>An assigned ox's Remove button: sends UnassignOx.</summary>
        public void ClickRemoveOx(int oxId)
        {
            int? op = OperationId();
            if (op.HasValue) Run(new UnassignOx(op.Value, oxId));
        }

        private static string StockText(Sim.Core.SimWorld world, Operation op)
        {
            var pool = world.OperationPool(op.Id, Items.Log);
            return "Log stock: " + pool.Total + " / " + pool.Reserved + " / " + pool.Available + " of " + pool.Capacity
                + " (total / reserved / available of capacity)";
        }

        /// <summary>Forager hut store line, e.g. "Hut store: 12 / 30" or "Hut store: 30 / 30 - Storage full" (FD-23b).</summary>
        public static string HutStoreText(Sim.Core.SimWorld world, Operation op)
        {
            var hut = OperationStatus.Hut(world, op);
            var store = hut == null ? null : world.Inventory.FindFoodStore(hut.Id);
            if (store == null) return "Hut store: none";
            string s = "Hut store: " + store.Total + " / " + store.Capacity;
            if (store.FreeSpace <= 0) s += " - Storage full";
            return s;
        }

        private static string BushText(Sim.Core.SimWorld world, TreeRegion region)
        {
            int full = 0, empty = 0;
            if (region != null)
                foreach (var b in world.Bushes.All)
                    if (region.Contains(b.X, b.Z)) { if (b.Baskets > 0) full++; else empty++; }
            return "Bushes in work area: " + full + " full, " + empty + " empty";
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
                case OperationState.NoBushes: return "No bushes";
                case OperationState.StorageFull: return "Storage full";
                case OperationState.WaitingForOx: return "Waiting for ox";
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
                string state = " - " + BuildStateText(world, id);
                if (def != null && f.Type.Id == def.HutTypeId) { hut = true; sb.AppendLine((def.HutTypeId == ModuleTypes.ForagerHutId ? "Forager hut" : "Hut") + state); }
                else if (def != null && f.Type.Id == def.LogStorageTypeId) { storage = true; sb.AppendLine("Log storage (" + f.Type.LogCapacity + " logs)" + state); }
                else sb.AppendLine(f.Type.Id + state);
            }
            if (!hut) sb.AppendLine("Hut: missing");
            if (!storage && def != null && def.LogStorageTypeId != null) sb.AppendLine("Log storage: missing");
            return sb.ToString().TrimEnd();
        }

        /// <summary>Construction state of a module from <see cref="ConstructionProgress.Of"/> (CN-40).</summary>
        public static string BuildStateText(Sim.Core.SimWorld world, int footprintId)
        {
            var decon = world.Deconstruction.Get(footprintId);
            var dfp = world.Footprints.Get(footprintId);
            if (decon != null && dfp != null)
            {
                int total = DeconstructionWork.TotalMinutes(world, dfp);
                int pct = total <= 0 ? 100 : Mathf.Clamp(decon.ProgressMinutes * 100 / total, 0, 100);
                return "Deconstructing " + pct + "%";
            }
            var p = ConstructionProgress.Of(world, footprintId);
            if (p == null) return "Built";
            if (p.State == ConstructionState.Delivering) return "Delivering " + p.LogsDelivered + " / " + p.LogsNeeded + " logs";
            return "Building " + Mathf.FloorToInt((float)(p.Fraction * 100.0)) + "%";
        }

        /// <summary>Text on a family Assign button while the operation's hut is unbuilt (CN-04b).</summary>
        public const string HutNotBuiltText = "Hut not built yet";

        private static bool HutUnbuilt(Sim.Core.SimWorld world, Operation op, OperationTypeDef def)
        {
            if (def == null) return false;
            foreach (int id in op.ModuleIds)
            {
                Footprint f = world.Footprints.Get(id);
                if (f != null && f.Type.Id == def.HutTypeId && !world.IsBuilt(id)) return true;
            }
            return false;
        }

        /// <summary>A module's High priority toggle: sends SetSitePriority.</summary>
        public void ClickPriority(int footprintId)
        {
            var host = Host();
            if (host == null || !host.IsInitialized) return;
            var site = host.World.Construction.Get(footprintId);
            if (site == null) return;
            Run(new SetSitePriority(footprintId,
                site.Priority == ConstructionPriority.High ? ConstructionPriority.Normal : ConstructionPriority.High));
        }

        private void RebuildPriority(Sim.Core.SimWorld world, Operation op)
        {
            var sb = new StringBuilder();
            sb.Append(op.Id);
            foreach (int id in op.ModuleIds)
            {
                var site = world.Construction.Get(id);
                if (site != null) sb.Append(',').Append(id).Append(site.Priority == ConstructionPriority.High ? 'H' : 'n');
            }
            string sig = sb.ToString();
            if (sig == _prioritySignature) return;
            _prioritySignature = sig;
            _priorityBox.Clear();
            foreach (int id in op.ModuleIds)
            {
                var site = world.Construction.Get(id);
                if (site == null) continue;
                int fid = id;
                bool high = site.Priority == ConstructionPriority.High;
                var b = new Button { name = "priority-" + fid, text = "High priority: " + (high ? "on" : "off") };
                b.clicked += () => ClickPriority(fid);
                _priorityBox.Add(b);
            }
            UiFocusRules.Apply(_priorityBox);
        }

        /// <summary>Text of the confirm dialog for deconstructing one module: the refund, and the hut's family warning (CN-51, CN-56).</summary>
        public static string ConfirmTextForModule(Sim.Core.SimWorld world, Operation op, int footprintId)
        {
            Footprint fp = world.Footprints.Get(footprintId);
            if (fp == null) return "";
            string name = ModuleTypes.DisplayName(fp.Type.Id);
            if (!world.IsBuilt(footprintId)) return "Cancel the unbuilt " + name + "? The site is removed at once.";
            return "Deconstruct the " + name + "?\nReturns " + DeconstructionWork.RefundLogs(world, fp) + " logs.";
        }

        /// <summary>Text of the confirm dialog for deconstructing a whole operation.</summary>
        public static string ConfirmTextForOperation(Sim.Core.SimWorld world, Operation op)
        {
            int logs = 0;
            foreach (int id in op.ModuleIds)
            {
                Footprint fp = world.Footprints.Get(id);
                if (fp == null) continue;
                if (world.IsBuilt(id) && world.Deconstruction.Get(id) == null) logs += DeconstructionWork.RefundLogs(world, fp);
            }
            return "Deconstruct the whole operation?\nReturns " + logs + " logs.";
        }

        /// <summary>A module row's Deconstruct button: asks to confirm, then sends MarkForDeconstruction. While the
        /// module is being deconstructed it sends CancelDeconstruction (after a confirm too).</summary>
        public void ClickDeconstructModule(int footprintId)
        {
            var host = Host();
            int? opId = OperationId();
            if (host == null || !host.IsInitialized || !opId.HasValue) return;
            var world = host.World;
            var op = world.Operations.Get(opId.Value);
            if (op == null || world.Footprints.Get(footprintId) == null) return;
            if (world.Deconstruction.Get(footprintId) != null)
                ConfirmDialog.Show("Cancel the deconstruction? Its progress is lost and the module works again.",
                    "Cancel deconstruction", () => Run(new CancelDeconstruction(footprintId)));
            else
                ConfirmDialog.Show(ConfirmTextForModule(world, op, footprintId), "Deconstruct",
                    () => Run(new MarkForDeconstruction(footprintId)));
        }

        /// <summary>Deconstruct operation button: asks to confirm, then sends DeconstructOperation.</summary>
        public void ClickDeconstructOperation()
        {
            var host = Host();
            int? opId = OperationId();
            if (host == null || !host.IsInitialized || !opId.HasValue) return;
            var op = host.World.Operations.Get(opId.Value);
            if (op == null) return;
            int id = opId.Value;
            ConfirmDialog.Show(ConfirmTextForOperation(host.World, op), "Deconstruct operation",
                () => Run(new DeconstructOperation(id)));
        }

        private void RebuildDeconstruct(Sim.Core.SimWorld world, Operation op)
        {
            var sb = new StringBuilder();
            sb.Append(op.Id);
            foreach (int id in op.ModuleIds)
                sb.Append(',').Append(id).Append(world.Deconstruction.Get(id) != null ? 'D' : 'n');
            string sig = sb.ToString();
            if (sig == _deconSignature) return;
            _deconSignature = sig;
            _deconBox.Clear();
            foreach (int id in op.ModuleIds)
            {
                Footprint fp = world.Footprints.Get(id);
                if (fp == null) continue;
                int fid = id;
                bool going = world.Deconstruction.Get(id) != null;
                var b = new Button
                {
                    name = "deconstruct-" + fid,
                    text = going ? "Cancel deconstruction: " + ModuleTypes.DisplayName(fp.Type.Id)
                                 : "Deconstruct " + ModuleTypes.DisplayName(fp.Type.Id)
                };
                b.clicked += () => ClickDeconstructModule(fid);
                _deconBox.Add(b);
            }
            UiFocusRules.Apply(_deconBox);
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
            bool hutUnbuilt = HutUnbuilt(world, op, def);
            sb.Append(op.Id).Append('|').Append(_pickerOpen).Append(hutUnbuilt ? "|U" : "|B");
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
                    var assign = new Button { name = "assign-slot-" + i, text = hutUnbuilt ? HutNotBuiltText : "Assign" };
                    assign.SetEnabled(!hutUnbuilt);
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
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !ConfirmDialog.OwnsEscape && IsShown)
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
            _signature = ""; _oxenSignature = "";
            SetMessage("");
            Refresh();
        }

        private void Build()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;
            root.Clear();
            _signature = ""; _oxenSignature = "";
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
            _priorityBox = new VisualElement { name = "operation-priority" };
            _prioritySignature = "";
            _deconBox = new VisualElement { name = "operation-deconstruct" };
            _deconSignature = "";
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
            var deconOp = new Button { name = "deconstruct-operation", text = "Deconstruct operation" };
            deconOp.clicked += ClickDeconstructOperation;
            var expand = new Button { name = "expand-modules", text = "Expand modules" };
            expand.clicked += ClickExpandModules;

            _oxenTab = new Button { name = "oxen-tab", text = "Oxen" };
            _oxenTab.clicked += ClickOxenTab;
            _oxenBox = new VisualElement { name = "oxen-box" };
            _oxenBox.style.display = DisplayStyle.None;
            _stock = MakeLabel("stock-line", 12, "");
            var stock = new VisualElement { name = "stock-rows" };
            stock.Add(_stock);

            _message = MakeLabel("operation-message", 12, "");
            _message.style.color = new Color(1f, 0.55f, 0.45f);
            _message.style.display = DisplayStyle.None;

            _panel.Add(_title); _panel.Add(_status); _panel.Add(_modules); _panel.Add(_priorityBox); _panel.Add(_deconBox);
            _panel.Add(MakeLabel("operation-families-heading", 12, "Families"));
            _panel.Add(_familyBox); _panel.Add(_pickerBox);
            _panel.Add(MakeLabel("operation-workers-heading", 12, "Workers"));
            _panel.Add(_workers); _panel.Add(_logs); _panel.Add(_trees); _panel.Add(_workArea);
            _panel.Add(areaRow); _panel.Add(expand); _panel.Add(deconOp); _panel.Add(_pause); _panel.Add(_oxenTab); _panel.Add(_oxenBox); _panel.Add(stock);
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
