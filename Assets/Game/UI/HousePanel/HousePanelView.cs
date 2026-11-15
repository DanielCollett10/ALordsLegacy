using System;
using System.Globalization;
using System.Text;
using LordsLegacy.Game.Tools;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Homes;
using LordsLegacy.Sim.People;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Info panel for the house chosen through <see cref="AgentPicker"/> (HM-21): state (Delivering, Building n%, Built,
    /// Deconstructing n%), the household name and members or "Empty", the household larder by food type with days left
    /// and a Hungry line (T-420, FD-10, FD-16), a High priority toggle while unbuilt, and
    /// Deconstruct (with a confirm dialog). Read from the Sim every frame; commands go through <see cref="SimHost"/>.
    /// Built in code, never takes keyboard focus, closes with its button or Escape (no tool active).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HousePanelView : MonoBehaviour
    {
        [SerializeField] private SimHost _host;
        [SerializeField] private AgentPicker _agents;
        [SerializeField] private ToolController _controller;

        private VisualElement _panel;
        private Label _title, _state, _household, _members, _larder, _hungry, _message;
        private Button _priority, _deconstruct, _close;
        private bool _subscribed;

        /// <summary>True while the panel is shown.</summary>
        public bool IsShown => _panel != null && _panel.style.display.value != DisplayStyle.None;

        /// <summary>Title line text.</summary>
        public string TitleText => _title?.text ?? "";

        /// <summary>State line text, e.g. "Building 40%".</summary>
        public string StateText => _state?.text ?? "";

        /// <summary>Household line text: "Household: Archer" or "Empty".</summary>
        public string HouseholdText => _household?.text ?? "";

        /// <summary>Members line text (names, comma separated); empty when the house is Empty.</summary>
        public string MembersText => _members?.text ?? "";

        /// <summary>Larder lines: one per food type ("Meat: 4 (12 rations)") then "Days left: 4.8"; empty when Empty.</summary>
        public string LarderText => _larder?.style.display.value == DisplayStyle.Flex ? _larder.text : "";

        /// <summary>"Hungry" while the household's larder ran short at the last midnight (FD-16); empty otherwise.</summary>
        public string HungryText => _hungry?.style.display.value == DisplayStyle.Flex ? _hungry.text : "";

        /// <summary>Message line text (a refused command); empty when none.</summary>
        public string MessageText => _message?.text ?? "";

        /// <summary>True while the High priority button is visible (house unbuilt).</summary>
        public bool PriorityVisible => _priority != null && _priority.style.display.value != DisplayStyle.None;

        /// <summary>Text of the High priority button, e.g. "High priority: off".</summary>
        public string PriorityText => _priority?.text ?? "";

        /// <summary>True while the Deconstruct button is visible.</summary>
        public bool DeconstructVisible => _deconstruct != null && _deconstruct.style.display.value != DisplayStyle.None;

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
        public void ClickClose() => Agents()?.ClearHouse();

        /// <summary>Toggles High priority on the selected house site (CN-30).</summary>
        public void ClickPriority()
        {
            var host = Host();
            var agents = Agents();
            if (host == null || !host.IsInitialized || agents == null || !agents.HasHouse) return;
            int id = agents.SelectedHouseId;
            var site = host.World.Construction.Get(id);
            if (site == null) return;
            Run(new SetSitePriority(id, site.Priority == ConstructionPriority.High ? ConstructionPriority.Normal : ConstructionPriority.High));
        }

        /// <summary>Deconstruct button: asks to confirm, then sends MarkForDeconstruction (an unbuilt site is cancelled at once).</summary>
        public void ClickDeconstruct()
        {
            var host = Host();
            var agents = Agents();
            if (host == null || !host.IsInitialized || agents == null || !agents.HasHouse) return;
            int id = agents.SelectedHouseId;
            var world = host.World;
            if (world.Deconstruction.Get(id) != null) return;
            ConfirmDialog.Show(ConfirmText(world, id), world.IsBuilt(id) ? "Deconstruct" : "Cancel house",
                () => Run(new MarkForDeconstruction(id)));
        }

        /// <summary>Text of the deconstruct confirm dialog.</summary>
        public static string ConfirmText(SimWorld world, int houseId)
        {
            var fp = world.Footprints.Get(houseId);
            if (fp == null) return "";
            if (!world.IsBuilt(houseId)) return "Cancel this unbuilt house? The site and its plot are removed at once.";
            var home = world.Homes.Get(houseId);
            string family = home != null && home.FamilyId != 0 ? "\nThe family moves back to the camp." : "";
            return "Deconstruct this house?\nReturns " + Sim.Construction.DeconstructionWork.RefundLogs(world, fp) + " logs." + family;
        }

        /// <summary>
        /// Larder text for a household (FD-10, HM-21): every food type with its count and rations, then days left at
        /// the current need. Pure read of the Sim; "Days left: -" when nobody in the household eats.
        /// </summary>
        public static string LarderLines(SimWorld world, Family family)
        {
            var sb = new StringBuilder();
            foreach (FoodStock s in HouseholdLarder.CountsOf(world, family))
            {
                sb.Append(ItemLabel(s.Item.Id)).Append(": ").Append(s.Count);
                if (s.Count > 0) sb.Append(" (").Append(s.Rations).Append(s.Rations == 1 ? " ration)" : " rations)");
                sb.Append('\n');
            }
            double days = HouseholdLarder.DaysLeft(world, family);
            sb.Append("Days left: ").Append(double.IsInfinity(days) ? "-" : days.ToString("0.0", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>Display name for an item id, e.g. "item.meat" → "Meat".</summary>
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

        /// <summary>Re-reads the Sim and updates the labels and visibility.</summary>
        public void Refresh()
        {
            if (_panel == null) return;
            var agents = Agents();
            var host = Host();
            if (agents == null || !agents.HasHouse || host == null || !host.IsInitialized) { _panel.style.display = DisplayStyle.None; return; }
            var world = host.World;
            int id = agents.SelectedHouseId;
            HomeRecord home = world.Homes.Get(id);
            if (home == null || world.Footprints.Get(id) == null) { _panel.style.display = DisplayStyle.None; return; }

            _title.text = "House";
            _state.text = "State: " + OperationPanelView.BuildStateText(world, id);
            Family family = home.FamilyId != 0 ? world.People.FindFamily(home.FamilyId) : null;
            if (family == null)
            {
                _household.text = "Empty";
                _members.text = "";
                _members.style.display = DisplayStyle.None;
                _larder.style.display = DisplayStyle.None;
                _hungry.style.display = DisplayStyle.None;
            }
            else
            {
                _household.text = "Household: " + family.Surname;
                var sb = new StringBuilder("Members: ");
                bool first = true;
                foreach (int mid in family.MemberIds)
                {
                    var c = world.People.FindCitizen(mid);
                    if (c == null) continue;
                    if (!first) sb.Append(", ");
                    sb.Append(c.Name);
                    first = false;
                }
                _members.text = sb.ToString();
                _members.style.display = DisplayStyle.Flex;
                _larder.text = LarderLines(world, family);
                _larder.style.display = DisplayStyle.Flex;
                // FD-16: Hungry is shown only while the household is short; it changes nothing else.
                _hungry.text = "Hungry";
                _hungry.style.display = family.IsHungry ? DisplayStyle.Flex : DisplayStyle.None;
            }

            var site = world.Construction.Get(id);
            _priority.style.display = site != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (site != null) _priority.text = "High priority: " + (site.Priority == ConstructionPriority.High ? "on" : "off");
            bool going = world.Deconstruction.Get(id) != null;
            _deconstruct.style.display = going ? DisplayStyle.None : DisplayStyle.Flex;
            _deconstruct.text = world.IsBuilt(id) ? "Deconstruct" : "Cancel house";
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
            var holder = new VisualElement { name = "house-panel-root", pickingMode = PickingMode.Ignore };
            holder.style.position = Position.Absolute;
            holder.style.right = 12; holder.style.bottom = 90;

            _panel = new VisualElement { name = "house-panel" };
            var s = _panel.style;
            s.minWidth = 220; s.paddingLeft = s.paddingRight = 12; s.paddingTop = s.paddingBottom = 10;
            s.backgroundColor = new Color(30f / 255f, 24f / 255f, 18f / 255f, 0.92f);
            s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = 1;
            var border = new Color(110f / 255f, 100f / 255f, 85f / 255f);
            s.borderTopColor = s.borderBottomColor = s.borderLeftColor = s.borderRightColor = border;
            s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = 4;
            s.display = DisplayStyle.None;

            _close = new Button { name = "house-close", text = "X" };
            _close.style.position = Position.Absolute;
            _close.style.right = 4; _close.style.top = 4;
            _close.clicked += ClickClose;

            _title = MakeLabel("house-title", 15, true);
            _state = MakeLabel("house-state", 12, false);
            _household = MakeLabel("house-household", 12, false);
            _members = MakeLabel("house-members", 12, false);
            _larder = MakeLabel("house-larder", 12, false);
            _larder.style.display = DisplayStyle.None;
            _hungry = MakeLabel("house-hungry", 12, true);
            _hungry.style.color = new Color(1f, 0.55f, 0.45f);
            _hungry.style.display = DisplayStyle.None;
            _message = MakeLabel("house-message", 12, false);
            _message.style.color = new Color(1f, 0.55f, 0.45f);
            _message.style.display = DisplayStyle.None;

            _priority = MakeButton("house-priority", "High priority: off");
            _priority.clicked += ClickPriority;
            _deconstruct = MakeButton("house-deconstruct", "Deconstruct");
            _deconstruct.clicked += ClickDeconstruct;

            _panel.Add(_title); _panel.Add(_state); _panel.Add(_household); _panel.Add(_members);
            _panel.Add(_hungry); _panel.Add(_larder);
            _panel.Add(_priority); _panel.Add(_deconstruct); _panel.Add(_message); _panel.Add(_close);
            holder.Add(_panel);
            root.Add(holder);
            UiFocusRules.Apply(root);
        }

        private static Button MakeButton(string name, string text)
        {
            var b = new Button { name = name, text = text };
            b.style.marginTop = 4;
            b.style.color = new Color(240f / 255f, 230f / 255f, 210f / 255f);
            b.style.backgroundColor = new Color(70f / 255f, 58f / 255f, 44f / 255f);
            return b;
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
