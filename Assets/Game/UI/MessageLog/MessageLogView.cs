using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Settlement message log under the top bar (T-508, GR-30). Shows the last <see cref="VisibleCount"/> entries of
    /// <c>SimWorld.SettlementLog</c>, each fading after <see cref="MessageLogFade.LifetimeS"/> real seconds.
    /// The labels are rebuilt only when the log's <c>Version</c> moves; fading only changes opacity and display.
    /// Holds no rules and never writes Sim state. If no UXML is assigned the labels are built in code.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MessageLogView : MonoBehaviour
    {
        /// <summary>How many entries are shown at once.</summary>
        public const int VisibleCount = 4;
        /// <summary>Root element name.</summary>
        public const string RootName = "message-log";

        [SerializeField] private SimHost _host;
        [SerializeField] private VisualTreeAsset _visualTree;
        [SerializeField] private StyleSheet _styleSheet;

        private readonly List<Shown> _shown = new List<Shown>();
        private readonly Label[] _labels = new Label[VisibleCount];
        private VisualElement _root;
        private SettlementLog _log;
        private int _seenVersion = -1;

        /// <summary>Clock in real seconds. Defaults to unscaled time (keeps running while paused). Tests replace it.</summary>
        public Func<float> Clock { get; set; } = () => Time.unscaledTime;

        /// <summary>Number of times the labels were rebuilt. Tests use it to check rebuilds only on Version change.</summary>
        public int RebuildCount { get; private set; }

        /// <summary>Sim host to read the log from.</summary>
        public SimHost Host { get => _host; set => _host = value; }

        /// <summary>Label name of the entry in a slot (0 is the oldest visible entry).</summary>
        public static string EntryName(int slot) => "message-log-entry-" + slot;

        /// <summary>Text shown in a slot, or empty.</summary>
        public string TextOf(int slot)
        {
            var l = SlotLabel(slot);
            return l == null ? "" : l.text;
        }

        /// <summary>True when a slot is displayed (not expired and filled).</summary>
        public bool IsSlotVisible(int slot)
        {
            var l = SlotLabel(slot);
            return l != null && l.style.display.value != DisplayStyle.None;
        }

        /// <summary>Current opacity of a slot's label (1 when fully shown).</summary>
        public float OpacityOf(int slot)
        {
            var l = SlotLabel(slot);
            return l == null ? 0f : l.style.opacity.value;
        }

        /// <summary>Number of slots currently displayed.</summary>
        public int VisibleSlotCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < VisibleCount; i++) if (IsSlotVisible(i)) n++;
                return n;
            }
        }

        /// <summary>
        /// Reads the Sim log. Rebuilds the labels only when its Version moved (or the log object changed), and
        /// applies fade and expiry to the shown entries every call.
        /// </summary>
        public void Refresh()
        {
            if (_root == null || _host == null || !_host.IsInitialized) return;
            var log = _host.World.SettlementLog;
            if (log != _log || log.Version < _seenVersion)
            {
                _shown.Clear();
                _log = log;
                _seenVersion = 0;
                AddNew(log, log.Entries.Count);
                Rebuild();
            }
            else if (log.Version != _seenVersion)
            {
                AddNew(log, Math.Min(log.Version - _seenVersion, log.Entries.Count));
                _seenVersion = log.Version;
                Rebuild();
            }
            ApplyFade();
        }

        private void AddNew(SettlementLog log, int count)
        {
            float now = Clock();
            for (int i = log.Entries.Count - count; i < log.Entries.Count; i++)
                _shown.Add(new Shown(log.Entries[i].Text, now));
            if (_shown.Count > VisibleCount) _shown.RemoveRange(0, _shown.Count - VisibleCount);
        }

        private void Rebuild()
        {
            RebuildCount++;
            _seenVersion = _log.Version;
            for (int i = 0; i < VisibleCount; i++)
            {
                if (_labels[i] == null) continue;
                _labels[i].text = i < _shown.Count ? _shown[i].Text : "";
            }
        }

        private void ApplyFade()
        {
            float now = Clock();
            for (int i = 0; i < VisibleCount; i++)
            {
                var l = _labels[i];
                if (l == null) continue;
                bool show = false;
                if (i < _shown.Count)
                {
                    float age = now - _shown[i].ShownAt;
                    if (!MessageLogFade.Expired(age))
                    {
                        show = true;
                        l.style.opacity = MessageLogFade.Opacity(age);
                    }
                }
                l.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (_root != null) _root.style.display = VisibleSlotCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private Label SlotLabel(int slot) => slot < 0 || slot >= VisibleCount ? null : _labels[slot];

        private void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            var root = doc.rootVisualElement;
            if (root == null) return;
            root.Clear();
            if (_visualTree != null) _visualTree.CloneTree(root);
            else BuildFallback(root);
            if (_styleSheet != null) root.styleSheets.Add(_styleSheet);
            UiFocusRules.Apply(root);
            _root = root.Q<VisualElement>(RootName);
            for (int i = 0; i < VisibleCount; i++) _labels[i] = root.Q<Label>(EntryName(i));
            _log = null;
            _seenVersion = -1;
            _shown.Clear();
            RebuildCount = 0;
            Refresh();
        }

        private void Update() => Refresh();

        private static void BuildFallback(VisualElement root)
        {
            var box = new VisualElement { name = RootName };
            box.style.position = Position.Absolute;
            box.style.top = 56;
            box.style.left = 12;
            for (int i = 0; i < VisibleCount; i++) box.Add(new Label { name = EntryName(i) });
            root.Add(box);
        }

        private struct Shown
        {
            public Shown(string text, float shownAt)
            {
                Text = text;
                ShownAt = shownAt;
            }

            public string Text { get; }
            public float ShownAt { get; }
        }
    }
}
