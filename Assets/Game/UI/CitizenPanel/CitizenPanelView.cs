using LordsLegacy.Game.Tools;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Info panel for the agent chosen through <see cref="AgentPicker"/> (CMD-09, CMD-10, PPL-14). Citizen: name, age,
    /// family, activity. Ox: name, activity. Refreshed every frame from the Sim. Closes with its button or Escape
    /// (Escape only when no tool is active, so tools keep their own Escape). Built in code; never takes keyboard focus.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class CitizenPanelView : MonoBehaviour
    {
        [SerializeField] private SimHost _host;
        [SerializeField] private AgentPicker _agents;
        [SerializeField] private ToolController _controller;

        private VisualElement _panel;
        private Label _name, _age, _family, _activity, _workplace, _task, _message;
        private string _pendingMessage = "";
        private Button _close;
        private bool _subscribed;

        /// <summary>True while the panel is shown.</summary>
        public bool IsShown => _panel != null && _panel.style.display.value != DisplayStyle.None;

        /// <summary>Name line text.</summary>
        public string NameText => _name?.text ?? "";

        /// <summary>Age line text (empty for an ox).</summary>
        public string AgeText => _age?.text ?? "";

        /// <summary>Family line text (empty for an ox).</summary>
        public string FamilyText => _family?.text ?? "";

        /// <summary>Activity line text.</summary>
        public string ActivityText => _activity?.text ?? "";

        /// <summary>Workplace line text, e.g. "Work: Woodcutter 1" (empty for an ox).</summary>
        public string WorkplaceText => _workplace?.text ?? "";

        /// <summary>Task line text, e.g. "Task: Felling (Buck 2/4)" (empty for an ox).</summary>
        public string TaskText => _task?.text ?? "";

        /// <summary>Debug message line text (send-to refusal, T-264); empty when none.</summary>
        public string MessageText => _message?.text ?? "";

        /// <summary>Shows a message line in the panel (empty hides it). Cleared when the selection changes.</summary>
        public void ShowMessage(string text)
        {
            _pendingMessage = text ?? "";
            if (_message == null) return;
            _message.text = _pendingMessage;
            _message.style.display = _pendingMessage.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }

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
        public void ClickClose() => Agents()?.Clear();

        /// <summary>Re-reads the Sim and updates the labels and visibility.</summary>
        public void Refresh()
        {
            if (_panel == null) return;
            var agents = Agents();
            var host = _host != null ? _host : (_host = FindFirstObjectByType<SimHost>());
            if (agents == null || !agents.HasSelection || host == null || !host.IsInitialized)
            {
                _panel.style.display = DisplayStyle.None;
                return;
            }
            var world = host.World;
            var sel = agents.Selected;
            if (sel.IsOx)
            {
                var ox = world.People.FindOx(sel.Id);
                if (ox == null) { _panel.style.display = DisplayStyle.None; return; }
                _name.text = ox.Name;
                _age.text = ""; _family.text = ""; _workplace.text = ""; _task.text = "";
                _age.style.display = DisplayStyle.None;
                _family.style.display = DisplayStyle.None;
                _workplace.style.display = DisplayStyle.None;
                _task.style.display = DisplayStyle.None;
                _activity.text = "Doing: " + ActivityName(ox.Activity);
            }
            else
            {
                var c = world.People.FindCitizen(sel.Id);
                if (c == null) { _panel.style.display = DisplayStyle.None; return; }
                var family = world.People.FindFamily(c.FamilyId);
                _name.text = family != null ? c.Name + " " + family.Surname : c.Name;
                int age = (int)System.Math.Floor(c.AgeYears(world.Tick, world.Tuning.AgeingRate));
                _age.text = "Age: " + age;
                _family.text = "Family: " + (family != null ? family.Surname : "none");
                _age.style.display = DisplayStyle.Flex;
                _family.style.display = DisplayStyle.Flex;
                _workplace.text = "Work: " + CitizenTaskText.Workplace(world, c);
                _task.text = "Task: " + CitizenTaskText.Task(world, c);
                _workplace.style.display = DisplayStyle.Flex;
                _task.style.display = DisplayStyle.Flex;
                _activity.text = "Doing: " + ActivityName(c.Activity);
            }
            _panel.style.display = DisplayStyle.Flex;
        }

        private static string ActivityName(Activity a)
        {
            switch (a)
            {
                case Activity.MillingAbout: return "Milling about";
                default: return a.ToString();
            }
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
            // The UIDocument can recreate its root (panel settings reload, late attach); rebuild if ours was dropped.
            var doc = GetComponent<UIDocument>();
            if (_panel == null || _panel.panel == null || (doc.rootVisualElement != null && _panel.panel != doc.rootVisualElement.panel))
                Build();
            Refresh();
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && _panel != null
                && _panel.style.display.value != DisplayStyle.None)
            {
                var controller = _controller != null ? _controller : (_controller = FindFirstObjectByType<ToolController>());
                if (controller == null || controller.ActiveTool == null) ClickClose();
            }
        }

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
            ShowMessage("");
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
            var holder = new VisualElement { name = "citizen-panel-root", pickingMode = PickingMode.Ignore };
            holder.style.position = Position.Absolute;
            holder.style.right = 12; holder.style.bottom = 90;

            _panel = new VisualElement { name = "citizen-panel" };
            var s = _panel.style;
            s.minWidth = 220; s.paddingLeft = s.paddingRight = 12; s.paddingTop = s.paddingBottom = 10;
            s.backgroundColor = new Color(30f / 255f, 24f / 255f, 18f / 255f, 0.92f);
            s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = 1;
            var border = new Color(110f / 255f, 100f / 255f, 85f / 255f);
            s.borderTopColor = s.borderBottomColor = s.borderLeftColor = s.borderRightColor = border;
            s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = 4;
            s.display = DisplayStyle.None;

            _close = new Button { name = "citizen-close", text = "X" };
            _close.style.position = Position.Absolute;
            _close.style.right = 4; _close.style.top = 4;
            _close.clicked += ClickClose;

            _name = MakeLabel("citizen-name", 15, true);
            _age = MakeLabel("citizen-age", 12, false);
            _family = MakeLabel("citizen-family", 12, false);
            _activity = MakeLabel("citizen-activity", 12, false);
            _workplace = MakeLabel("citizen-workplace", 12, false);
            _task = MakeLabel("citizen-task", 12, false);
            _message = MakeLabel("citizen-message", 12, false);
            _message.style.color = new Color(1f, 0.55f, 0.45f);
            _message.style.display = DisplayStyle.None;

            _panel.Add(_name); _panel.Add(_age); _panel.Add(_family); _panel.Add(_activity);
            _panel.Add(_workplace); _panel.Add(_task); _panel.Add(_message); _panel.Add(_close);
            ShowMessage(_pendingMessage);
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
