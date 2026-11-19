using System;
using System.Collections.Generic;
using System.Globalization;
using LordsLegacy.Sim.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Read-only Sim log overlay (F5): the newest <see cref="MaxShown"/> entries of <c>SimWorld.Log</c>, newest first, as
    /// "Day hh:mm [Category] text", with one toggle per category. A toggle also enables or disables writing for that
    /// category in the Sim log. Holds no game rules; modelled on <see cref="DebugOverlayView"/>.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class SimLogOverlayView : MonoBehaviour
    {
        /// <summary>Overlay root element name.</summary>
        public const string RootName = "simlog-overlay";
        /// <summary>Name of the container holding the entry labels.</summary>
        public const string EntriesName = "simlog-entries";
        /// <summary>Name of the container holding the category toggles.</summary>
        public const string TogglesName = "simlog-toggles";
        /// <summary>Prefix of each toggle's element name; the category name follows.</summary>
        public const string TogglePrefix = "simlog-toggle-";
        private const long TicksPerDay = 1440;
        private const float RefreshSeconds = 0.25f;

        [SerializeField] private SimHost _host;
        [SerializeField] private VisualTreeAsset _visualTree;
        [SerializeField] private StyleSheet _styleSheet;
        [SerializeField] private int _maxShown = 30;

        private VisualElement _root, _entries;
        private float _sinceRefresh;

        /// <summary>When true, Update toggles on F5 and refreshes. Tests set false.</summary>
        public bool Live { get; set; } = true;

        /// <summary>Sim host to read.</summary>
        public SimHost Host { get => _host; set => _host = value; }

        /// <summary>Most entries listed.</summary>
        public int MaxShown { get => _maxShown; set => _maxShown = Math.Max(1, value); }

        /// <summary>True when the overlay is shown.</summary>
        public bool Visible { get; private set; }

        /// <summary>The overlay root element (null before enable).</summary>
        public VisualElement Root => _root;

        /// <summary>Entry texts currently listed, newest first.</summary>
        public List<string> EntryTexts()
        {
            var list = new List<string>();
            if (_entries != null) foreach (var c in _entries.Children()) if (c is Label l) list.Add(l.text);
            return list;
        }

        /// <summary>Formats a log entry as "Day hh:mm [Category] text" (day starts at 1).</summary>
        public static string Format(LogEntry e)
        {
            var c = CultureInfo.InvariantCulture;
            long day = e.Tick / TicksPerDay + 1, m = e.Tick % TicksPerDay;
            return "Day " + day.ToString(c) + " " + (m / 60).ToString("00", c) + ":" + (m % 60).ToString("00", c)
                + " [" + e.Category + "] " + e.Text;
        }

        /// <summary>Shows or hides the overlay and refreshes the list when shown.</summary>
        public void SetVisible(bool visible)
        {
            Visible = visible;
            if (_root != null) _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible) Refresh();
        }

        /// <summary>Sets one category on or off in the Sim log (and its toggle), then refreshes the list.</summary>
        public void SetCategory(LogCategory category, bool enabled)
        {
            var host = ResolveHost();
            if (host != null && host.IsInitialized) host.World.Log.SetEnabled(category, enabled);
            var t = _root == null ? null : _root.Q<Toggle>(TogglePrefix + category);
            if (t != null && t.value != enabled) t.SetValueWithoutNotify(enabled);
            Refresh();
        }

        /// <summary>Rebuilds the entry list: newest first, only categories that are on.</summary>
        public void Refresh()
        {
            var host = ResolveHost();
            if (_entries == null || host == null || !host.IsInitialized) return;
            var log = host.World.Log;
            _entries.Clear();
            int shown = 0;
            for (int i = log.Count - 1; i >= 0 && shown < _maxShown; i--)
            {
                var e = log[i];
                if (!log.IsEnabled(e.Category)) continue;
                var l = new Label(Format(e)) { pickingMode = PickingMode.Ignore };
                l.AddToClassList("simlog__entry");
                _entries.Add(l);
                shown++;
            }
        }

        private SimHost ResolveHost()
        {
            if (_host == null) _host = FindFirstObjectByType<SimHost>();
            return _host;
        }

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;
            root.Clear();
            if (_visualTree != null) _visualTree.CloneTree(root);
            else BuildFallback(root);
            if (_styleSheet != null) root.styleSheets.Add(_styleSheet);
            _root = root.Q<VisualElement>(RootName);
            _entries = root.Q<VisualElement>(EntriesName);
            var toggles = root.Q<VisualElement>(TogglesName);
            if (toggles != null)
            {
                toggles.Clear();
                foreach (LogCategory cat in Enum.GetValues(typeof(LogCategory)))
                {
                    var category = cat;
                    var t = new Toggle(category.ToString()) { name = TogglePrefix + category, value = true };
                    t.AddToClassList("simlog__toggle");
                    t.RegisterValueChangedCallback(ev => SetCategory(category, ev.newValue));
                    toggles.Add(t);
                }
            }
            UiFocusRules.Apply(root);
            SetVisible(Visible);
        }

        private static void BuildFallback(VisualElement root)
        {
            var box = new VisualElement { name = RootName, pickingMode = PickingMode.Ignore };
            box.style.position = Position.Absolute;
            box.style.top = 48;
            box.style.left = 12;
            box.Add(new VisualElement { name = TogglesName, pickingMode = PickingMode.Ignore });
            box.Add(new VisualElement { name = EntriesName, pickingMode = PickingMode.Ignore });
            root.Add(box);
        }

        private void Update()
        {
            if (!Live) return;
            var kb = UiFocusRules.GameKeyboard;
            if (kb != null && kb.f5Key.wasPressedThisFrame) SetVisible(!Visible);
            if (!Visible) return;
            _sinceRefresh += Time.unscaledDeltaTime;
            if (_sinceRefresh < RefreshSeconds) return;
            _sinceRefresh = 0f;
            Refresh();
        }
    }
}
