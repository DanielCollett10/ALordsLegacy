using UnityEngine;
using UnityEngine.UIElements;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Top bar: date/time, pause, speed buttons, steady-light toggle. Reads SimHost state and sends
    /// pause/speed commands through it. Holds no game rules. If the UIDocument has no UXML assigned
    /// the same element names are built in code.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class TopBarController : MonoBehaviour
    {
        private const string ActiveClass = "top-bar__button--active";

        [SerializeField] private SimHost _host;
        [SerializeField] private StyleSheet _styleSheet;

        private Label _dateLabel;
        private Label _notice;
        private Label _foodLabel;
        private Label _populationLabel;
        private Label _populationTip;
        private Button _pauseButton;
        private Button _steadyButton;
        private Button _snappingButton;
        private Button[] _speedButtons = new Button[0];
        private VisualElement _speedsRoot;
        private SnappingHotkey _snappingHotkey;

        /// <summary>Presentation-only settings shared with the view.</summary>
        public ViewSettings ViewSettings { get; } = new ViewSettings();

        /// <summary>The text currently shown for date/time (or "Paused").</summary>
        public string DateText => _dateLabel == null ? "" : _dateLabel.text;

        /// <summary>The food readout text, e.g. "Food: 342 days" (FD-04).</summary>
        public string FoodReadoutText => _foodLabel == null ? "" : _foodLabel.text;

        /// <summary>The food readout tooltip: raw counts by type (FD-04). Empty before the first refresh.</summary>
        public string FoodReadoutTooltip => _foodLabel == null ? "" : _foodLabel.tooltip;

        /// <summary>Population readout, e.g. "Families: 3  Citizens: 12" (T-509).</summary>
        public string PopulationText => _populationLabel == null ? "" : _populationLabel.text;

        /// <summary>Population tooltip: arriving and leaving counts and the arrival reason (T-509).</summary>
        public string PopulationTooltip => _populationLabel == null ? "" : _populationLabel.tooltip;

        /// <summary>Builds the population tooltip from a growth report.</summary>
        public static string PopulationTooltipOf(Sim.Growth.GrowthReport r) =>
            "Arriving: " + r.Arriving + "\nLeaving: " + r.Leaving + "\nNext family: "
            + (r.ArrivalReason ?? "would be let in");

        /// <summary>Sets the SimHost to read from (for code that creates the component). Call before enabling.</summary>
        public void SetHost(SimHost host) { _host = host; }

        private void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            var root = doc.rootVisualElement;
            if (root == null) return;
            root.Clear();
            if (doc.visualTreeAsset != null) doc.visualTreeAsset.CloneTree(root);
            else BuildFallback(root);
            if (_styleSheet != null) root.styleSheets.Add(_styleSheet);
            UiFocusRules.Apply(root);

            _dateLabel = root.Q<Label>("date-label");
            _notice = root.Q<Label>("slowed-notice");
            _foodLabel = root.Q<Label>("food-label");
            _populationLabel = root.Q<Label>("population-label");
            if (_populationLabel == null && _foodLabel?.parent != null)
            {
                _populationLabel = new Label { name = "population-label" };
                _populationLabel.AddToClassList("top-bar__food");
                _foodLabel.parent.Insert(_foodLabel.parent.IndexOf(_foodLabel) + 1, _populationLabel);
            }
            BuildPopulationTip(root);
            _pauseButton = root.Q<Button>("pause-button");
            _steadyButton = root.Q<Button>("steady-light-toggle");
            _snappingButton = root.Q<Button>("snapping-toggle");
            var speeds = root.Q<VisualElement>("speed-buttons");

            _pauseButton.clicked += TogglePause;
            _steadyButton.clicked += ToggleSteadyLight;
            _snappingButton.clicked += ToggleSnapping;

            _snappingHotkey?.Dispose();
            _snappingHotkey = new SnappingHotkey(ToggleSnapping);

            _speedsRoot = speeds;
            _speedButtons = new Button[0];
            BuildSpeedButtons();
            UpdateSnappingLabel();
            Refresh();
        }

        // Runtime panels do not show the built-in tooltip, so the population hover text is drawn by this label (T-509).
        private void BuildPopulationTip(VisualElement root)
        {
            if (_populationLabel == null) return;
            _populationTip = new Label { name = "population-tooltip", pickingMode = PickingMode.Ignore };
            var s = _populationTip.style;
            s.position = Position.Absolute;
            s.top = 52;
            s.paddingLeft = s.paddingRight = 8; s.paddingTop = s.paddingBottom = 6;
            s.color = new Color(240f / 255f, 230f / 255f, 210f / 255f);
            s.backgroundColor = new Color(30f / 255f, 24f / 255f, 18f / 255f, 0.95f);
            s.fontSize = 14;
            s.display = DisplayStyle.None;
            root.Add(_populationTip);
            _populationLabel.RegisterCallback<PointerEnterEvent>(e => ShowPopulationTip());
            _populationLabel.RegisterCallback<PointerLeaveEvent>(e => HidePopulationTip());
        }

        /// <summary>Shows the population tooltip under the label (the hover path; also callable by tests).</summary>
        public void ShowPopulationTip()
        {
            if (_populationTip == null || _populationLabel == null) return;
            _populationTip.text = _populationLabel.tooltip;
            _populationTip.style.left = Mathf.Max(0f, _populationLabel.worldBound.x);
            _populationTip.style.display = DisplayStyle.Flex;
        }

        /// <summary>Hides the population tooltip.</summary>
        public void HidePopulationTip()
        {
            if (_populationTip != null) _populationTip.style.display = DisplayStyle.None;
        }

        /// <summary>True while the population tooltip is shown.</summary>
        public bool PopulationTipShown => _populationTip != null && _populationTip.style.display.value == DisplayStyle.Flex;

        /// <summary>Text of the shown population tooltip.</summary>
        public string PopulationTipText => _populationTip == null ? "" : _populationTip.text;

        private void OnDisable()
        {
            _snappingHotkey?.Dispose();
            _snappingHotkey = null;
        }

        // The SimHost may initialise after OnEnable (it loads the map in Start), so this is retried from Refresh.
        private void BuildSpeedButtons()
        {
            if (_speedsRoot == null || _speedButtons.Length > 0 || _host == null || !_host.IsInitialized) return;
            int count = _host.Pacer.Speeds.Count;
            _speedButtons = new Button[count];
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var b = new Button(() => SetSpeed(index)) { name = "speed-" + i, text = _host.Pacer.Speeds[i] + "x" };
                b.AddToClassList("top-bar__button");
                b.focusable = false;
                _speedsRoot.Add(b);
                _speedButtons[i] = b;
            }
        }

        private static void BuildFallback(VisualElement root)
        {
            var bar = new VisualElement { name = "top-bar" };
            bar.style.flexDirection = FlexDirection.Row;
            bar.Add(new Label { name = "date-label" });
            bar.Add(new Label { name = "food-label" });
            bar.Add(new Label { name = "population-label" });
            bar.Add(new Label("Running slower than real time") { name = "slowed-notice" });
            bar.Add(new Button { name = "pause-button", text = "Pause" });
            var speeds = new VisualElement { name = "speed-buttons" };
            speeds.style.flexDirection = FlexDirection.Row;
            bar.Add(speeds);
            bar.Add(new Button { name = "steady-light-toggle", text = "Steady light" });
            bar.Add(new Button { name = "snapping-toggle", text = "Snapping: On" });
            root.Add(bar);
        }

        private void Update() => Refresh();

        /// <summary>Toggles pause through the SimHost.</summary>
        public void TogglePause()
        {
            if (_host != null) _host.TogglePause();
            Refresh();
        }

        /// <summary>Selects the speed at the given index of TickPacer.Speeds.</summary>
        public void SetSpeed(int index)
        {
            if (_host != null && _host.Pacer != null) _host.Pacer.SetSpeed(index);
            Refresh();
        }

        /// <summary>Flips <see cref="ViewSettings.SteadyLight"/>. Does not touch the Sim.</summary>
        public void ToggleSteadyLight()
        {
            ViewSettings.SteadyLight = !ViewSettings.SteadyLight;
            Refresh();
        }

        /// <summary>
        /// Flips <see cref="ViewSettings.Snapping"/> and updates the button label. Called by the button and by the
        /// Alt key (ToggleSnapping action, once per press). Does not touch the Sim.
        /// </summary>
        public void ToggleSnapping()
        {
            ViewSettings.Snapping = !ViewSettings.Snapping;
            UpdateSnappingLabel();
        }

        private void UpdateSnappingLabel()
        {
            if (_snappingButton == null) return;
            _snappingButton.text = ViewSettings.Snapping ? "Snapping: On" : "Snapping: Off";
        }

        /// <summary>Re-reads Sim state into the labels and button highlights.</summary>
        public void Refresh()
        {
            if (_dateLabel == null || _host == null || !_host.IsInitialized) return;
            BuildSpeedButtons();
            _dateLabel.text = _host.World.Paused ? "Paused" : _host.World.Now.Format();
            if (_foodLabel != null)
            {
                _foodLabel.text = FoodText.DaysLine(_host.World);
                _foodLabel.tooltip = FoodText.Tooltip(_host.World);
            }
            if (_populationLabel != null)
            {
                var report = Sim.Growth.GrowthReport.Of(_host.World);
                _populationLabel.text = "Families: " + report.Families + "  Citizens: " + report.Citizens;
                _populationLabel.tooltip = PopulationTooltipOf(report);
                if (PopulationTipShown) _populationTip.text = _populationLabel.tooltip;
            }
            _pauseButton.EnableInClassList(ActiveClass, _host.World.Paused);
            _steadyButton.EnableInClassList(ActiveClass, ViewSettings.SteadyLight);
            _notice.style.display = _host.Pacer.SlowedDown ? DisplayStyle.Flex : DisplayStyle.None;
            for (int i = 0; i < _speedButtons.Length; i++)
                _speedButtons[i].EnableInClassList(ActiveClass, i == _host.Pacer.CurrentSpeedIndex);
        }
    }
}
