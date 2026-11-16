using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Bottom build bar (UI Toolkit). Builds one non-focusable button per <see cref="ToolEntry"/>, highlights the
    /// active one and raises <see cref="ToolClicked"/> for enabled buttons. Holds no tool logic.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class BuildBarView : MonoBehaviour
    {
        private const string ActiveClass = "build-bar__button--active";
        private const string DisabledClass = "build-bar__button--disabled";

        [SerializeField] private StyleSheet _styleSheet;
        [SerializeField] private SimHost _host;

        private readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>();
        private readonly Dictionary<string, (bool Enabled, string Tooltip)> _shown = new Dictionary<string, (bool, string)>();
        private VisualElement _bar;
        private bool _hover;
        private IReadOnlyList<ToolEntry> _entries;

        /// <summary>Raised with the entry id when an enabled button is clicked.</summary>
        public event Action<string> ToolClicked;

        /// <summary>
        /// Logs available to construction now (CN-11). Set by code that builds the bar; when null the bar reads the
        /// <see cref="SimHost"/> wired in the scene, and counts 0 logs if there is none.
        /// </summary>
        public Func<int> AvailableLogs { get; set; }

        /// <summary>True while the pointer is over the bar (clicks there must not reach the ground, UIB-05).</summary>
        public bool IsPointerOver => _hover;

        /// <summary>Entries shown; call before enabling to override <see cref="ToolEntry.Defaults"/>.</summary>
        public void SetEntries(IReadOnlyList<ToolEntry> entries)
        {
            _entries = entries;
            if (isActiveAndEnabled) Rebuild();
        }

        /// <summary>The button for an entry id, or null.</summary>
        public Button GetButton(string id) { _buttons.TryGetValue(id, out var b); return b; }

        /// <summary>True if the entry is shown enabled right now (its cost gate, if any, is met).</summary>
        public bool IsEntryEnabled(string id)
        {
            var e = FindEntry(id);
            return e != null && Evaluate(e).Enabled;
        }

        /// <summary>Sends a click to an entry as if the player pressed it. Ignored for unknown or disabled ids.</summary>
        public void Click(string id)
        {
            var e = FindEntry(id);
            if (e != null && Evaluate(e).Enabled) ToolClicked?.Invoke(id);
        }

        private ToolEntry FindEntry(string id)
        {
            if (_entries == null) return null;
            foreach (var e in _entries)
                if (e.Id == id) return e;
            return null;
        }

        /// <summary>
        /// The entry's shown state now: disabled if the entry is disabled, or if its cost is not covered by available
        /// logs (then the tooltip is the "Needs N logs (have M)" text, BT-02).
        /// </summary>
        private (bool Enabled, string Tooltip) Evaluate(ToolEntry e)
        {
            if (!e.Enabled) return (false, e.Tooltip);
            if (e.DisabledReason != null && _host != null && _host.IsInitialized)
            {
                string reason = e.DisabledReason(_host.World);
                if (!string.IsNullOrEmpty(reason)) return (false, reason);
            }
            if (e.CostLogs == null) return (true, e.Tooltip);
            var gate = BuildEntryState.Of(e.CostLogs(), AvailableNow());
            return gate.Enabled ? (true, e.Tooltip) : (false, gate.Tooltip);
        }

        private int AvailableNow()
        {
            if (AvailableLogs != null) return AvailableLogs();
            if (_host == null || !_host.IsInitialized) return 0;
            return LordsLegacy.Sim.Construction.ConstructionSources.Available(_host.World, LordsLegacy.Sim.Inventory.Items.Log);
        }

        private void Update()
        {
            if (_entries == null) return;
            foreach (var e in _entries)
            {
                if (!_buttons.TryGetValue(e.Id, out var b)) continue;
                var state = Evaluate(e);
                if (_shown.TryGetValue(e.Id, out var old) && old == state) continue;
                ApplyState(e, b, state);
            }
        }

        private void ApplyState(ToolEntry e, Button b, (bool Enabled, string Tooltip) state)
        {
            _shown[e.Id] = state;
            b.EnableInClassList(DisabledClass, !state.Enabled);
            b.tooltip = state.Tooltip.Length > 0 ? state.Tooltip : e.Tooltip;
        }

        /// <summary>Highlights the button for <paramref name="id"/> (null clears all).</summary>
        public void SetActive(string id)
        {
            foreach (var kv in _buttons) kv.Value.EnableInClassList(ActiveClass, kv.Key == id);
        }

        private void OnEnable()
        {
            if (_entries == null) _entries = ToolEntry.Defaults();
            Rebuild();
        }

        private void Rebuild()
        {
            var doc = GetComponent<UIDocument>();
            var root = doc.rootVisualElement;
            if (root == null) return;
            root.Clear();
            _buttons.Clear();
            _shown.Clear();
            if (doc.visualTreeAsset != null) doc.visualTreeAsset.CloneTree(root);
            else
            {
                var holder = new VisualElement { name = "build-bar-root" };
                holder.Add(new VisualElement { name = "build-bar" });
                root.Add(holder);
            }
            if (_styleSheet != null) root.styleSheets.Add(_styleSheet);
            _bar = root.Q<VisualElement>("build-bar");
            _bar.RegisterCallback<PointerEnterEvent>(_ => _hover = true);
            _bar.RegisterCallback<PointerLeaveEvent>(_ => _hover = false);
            foreach (var entry in _entries)
            {
                string id = entry.Id;
                var b = new Button(() => Click(id)) { name = "tool-" + id, text = entry.Label };
                b.AddToClassList("build-bar__button");
                _bar.Add(b);
                _buttons[id] = b;
                ApplyState(entry, b, Evaluate(entry));
            }
            UiFocusRules.Apply(root);
        }
    }
}
