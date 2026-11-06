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
        private Button _pauseButton;
        private Button _steadyButton;
        private Button[] _speedButtons = new Button[0];

        /// <summary>Presentation-only settings shared with the view.</summary>
        public ViewSettings ViewSettings { get; } = new ViewSettings();

        /// <summary>The text currently shown for date/time (or "Paused").</summary>
        public string DateText => _dateLabel == null ? "" : _dateLabel.text;

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

            _dateLabel = root.Q<Label>("date-label");
            _notice = root.Q<Label>("slowed-notice");
            _pauseButton = root.Q<Button>("pause-button");
            _steadyButton = root.Q<Button>("steady-light-toggle");
            var speeds = root.Q<VisualElement>("speed-buttons");

            _pauseButton.clicked += TogglePause;
            _steadyButton.clicked += ToggleSteadyLight;

            int count = _host != null && _host.Pacer != null ? _host.Pacer.Speeds.Count : 0;
            _speedButtons = new Button[count];
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var b = new Button(() => SetSpeed(index)) { name = "speed-" + i, text = _host.Pacer.Speeds[i] + "x" };
                b.AddToClassList("top-bar__button");
                speeds.Add(b);
                _speedButtons[i] = b;
            }
            Refresh();
        }

        private static void BuildFallback(VisualElement root)
        {
            var bar = new VisualElement { name = "top-bar" };
            bar.style.flexDirection = FlexDirection.Row;
            bar.Add(new Label { name = "date-label" });
            bar.Add(new Label("Running slower than real time") { name = "slowed-notice" });
            bar.Add(new Button { name = "pause-button", text = "Pause" });
            var speeds = new VisualElement { name = "speed-buttons" };
            speeds.style.flexDirection = FlexDirection.Row;
            bar.Add(speeds);
            bar.Add(new Button { name = "steady-light-toggle", text = "Steady light" });
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

        /// <summary>Re-reads Sim state into the labels and button highlights.</summary>
        public void Refresh()
        {
            if (_dateLabel == null || _host == null || !_host.IsInitialized) return;
            _dateLabel.text = _host.World.Paused ? "Paused" : _host.World.Now.Format();
            _pauseButton.EnableInClassList(ActiveClass, _host.World.Paused);
            _steadyButton.EnableInClassList(ActiveClass, ViewSettings.SteadyLight);
            _notice.style.display = _host.Pacer.SlowedDown ? DisplayStyle.Flex : DisplayStyle.None;
            for (int i = 0; i < _speedButtons.Length; i++)
                _speedButtons[i].EnableInClassList(ActiveClass, i == _host.Pacer.CurrentSpeedIndex);
        }
    }
}
