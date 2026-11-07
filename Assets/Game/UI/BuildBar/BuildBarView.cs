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

        private readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>();
        private VisualElement _bar;
        private bool _hover;
        private IReadOnlyList<ToolEntry> _entries;

        /// <summary>Raised with the entry id when an enabled button is clicked.</summary>
        public event Action<string> ToolClicked;

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

        /// <summary>Sends a click to an entry as if the player pressed it. Ignored for unknown or disabled ids.</summary>
        public void Click(string id)
        {
            if (_entries == null) return;
            foreach (var e in _entries)
                if (e.Id == id && e.Enabled) { ToolClicked?.Invoke(id); return; }
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
                var b = new Button(() => Click(id)) { name = "tool-" + id, text = entry.Label, tooltip = entry.Tooltip };
                b.AddToClassList("build-bar__button");
                if (!entry.Enabled) b.AddToClassList(DisabledClass);
                _bar.Add(b);
                _buttons[id] = b;
            }
            UiFocusRules.Apply(root);
        }
    }
}
