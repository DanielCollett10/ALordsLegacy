using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Tool panel shown above the build bar while a drawing tool has a draft (docs/design/ui.md, "Tool panel").
    /// Shows a status line, a hint line and Cancel / Confirm buttons. Confirm is disabled while the draft is invalid.
    /// Holds no tool rules: the tool decides what to show and reacts to <see cref="Cancelled"/> and <see cref="Confirmed"/>.
    /// Buttons are non-focusable (UIK-01). If the UIDocument has no UXML assigned the same element names are built in code.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ToolPanelView : MonoBehaviour
    {
        [SerializeField] private StyleSheet _styleSheet;

        private VisualElement _panel;
        private Label _statusLabel;
        private Label _hintLabel;
        private Button _cancelButton;
        private Button _confirmButton;
        private VisualElement _actionsRow;
        private readonly List<KeyValuePair<string, string>> _actions = new List<KeyValuePair<string, string>>();

        // Model state, kept so a rebuild (OnEnable again) shows the same panel.
        private bool _visible;
        private string _status = "";
        private string _hint = "";
        private bool _valid;

        /// <summary>Raised when Cancel is pressed while the panel is shown.</summary>
        public event Action Cancelled;

        /// <summary>Raised when Confirm is pressed while the panel is shown and the draft is valid.</summary>
        public event Action Confirmed;

        /// <summary>Raised with the action name when an extra action button (see <see cref="SetActions"/>) is pressed.</summary>
        public event Action<string> ActionClicked;

        /// <summary>Names of the extra action buttons currently set, in order.</summary>
        public IReadOnlyList<string> ActionNames
        {
            get { var l = new List<string>(); foreach (var a in _actions) l.Add(a.Key); return l; }
        }

        /// <summary>
        /// Replaces the extra action buttons (name, label) shown between the text and Cancel; null or empty removes them.
        /// Pressing one raises <see cref="ActionClicked"/>. The panel holds no tool rules.
        /// </summary>
        public void SetActions(IEnumerable<KeyValuePair<string, string>> actions)
        {
            var next = actions == null ? new List<KeyValuePair<string, string>>() : new List<KeyValuePair<string, string>>(actions);
            bool same = next.Count == _actions.Count;
            for (int i = 0; same && i < next.Count; i++) same = next[i].Key == _actions[i].Key && next[i].Value == _actions[i].Value;
            if (same) return;
            _actions.Clear();
            _actions.AddRange(next);
            Render();
        }

        /// <summary>Presses an extra action button as if clicked. Ignored while hidden or if the name is not set.</summary>
        public void ClickAction(string name)
        {
            if (!_visible) return;
            foreach (var a in _actions)
                if (a.Key == name) { ActionClicked?.Invoke(name); return; }
        }

        /// <summary>True while the panel is shown.</summary>
        public bool IsShown => _visible;

        /// <summary>The status line text (for example "Road · 3 points · 142 m · max grade 9% · Valid").</summary>
        public string Status => _status;

        /// <summary>The hint line text.</summary>
        public string Hint => _hint;

        /// <summary>True when Confirm is enabled (shown and valid).</summary>
        public bool IsConfirmEnabled => _confirmButton != null && _confirmButton.enabledSelf;

        /// <summary>Shows the panel with the given texts. Confirm is enabled only when <paramref name="isValid"/> is true.</summary>
        public void Show(string status, string hint, bool isValid)
        {
            _status = status ?? "";
            _hint = hint ?? "";
            _valid = isValid;
            _visible = true;
            Render();
        }

        /// <summary>Hides the panel. Does not raise any event.</summary>
        public void Hide()
        {
            _visible = false;
            _valid = false; // a hidden panel must not leave Confirm enabled for the next draft
            Render();
        }

        /// <summary>Presses Cancel as if the player clicked it. Ignored while hidden.</summary>
        public void ClickCancel()
        {
            if (!_visible) return;
            Cancelled?.Invoke();
        }

        /// <summary>Presses Confirm as if the player clicked it. Ignored while hidden or invalid.</summary>
        public void ClickConfirm()
        {
            if (!_visible || !_valid) return;
            Confirmed?.Invoke();
        }

        private void OnEnable()
        {
            EnsureBuilt();
            Render();
        }

        private void EnsureBuilt()
        {
            var doc = GetComponent<UIDocument>();
            var root = doc.rootVisualElement;
            if (root == null) return;
            root.Clear();
            if (doc.visualTreeAsset != null) doc.visualTreeAsset.CloneTree(root);
            else BuildFallback(root);
            if (_styleSheet != null) root.styleSheets.Add(_styleSheet);

            _panel = root.Q<VisualElement>("tool-panel");
            _statusLabel = root.Q<Label>("tool-panel-status");
            _hintLabel = root.Q<Label>("tool-panel-hint");
            _cancelButton = root.Q<Button>("tool-panel-cancel");
            _confirmButton = root.Q<Button>("tool-panel-confirm");
            _cancelButton.clicked += ClickCancel;
            _confirmButton.clicked += ClickConfirm;
            _actionsRow = new VisualElement { name = "tool-panel-actions" };
            _actionsRow.AddToClassList("tool-panel__actions");
            _panel.Insert(1, _actionsRow);
            UiFocusRules.Apply(root);
        }

        private void Render()
        {
            if (_panel == null) EnsureBuilt();
            if (_panel == null) return;
            _statusLabel.text = _status;
            _hintLabel.text = _hint;
            _confirmButton.SetEnabled(_valid);
            RenderActions();
            _panel.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RenderActions()
        {
            if (_actionsRow == null) return;
            _actionsRow.Clear();
            foreach (var a in _actions)
            {
                string name = a.Key;
                var b = new Button { name = "tool-panel-action-" + name, text = a.Value };
                b.AddToClassList("tool-panel__button");
                b.clicked += () => ClickAction(name);
                _actionsRow.Add(b);
            }
            UiFocusRules.Apply(_actionsRow);
            _actionsRow.style.display = _actions.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static void BuildFallback(VisualElement root)
        {
            var rootEl = new VisualElement { name = "tool-panel-root", pickingMode = PickingMode.Ignore };
            rootEl.AddToClassList("tool-panel-root");
            var panel = new VisualElement { name = "tool-panel" };
            panel.AddToClassList("tool-panel");
            var text = new VisualElement();
            text.AddToClassList("tool-panel__text");
            var status = new Label { name = "tool-panel-status", text = "" };
            status.AddToClassList("tool-panel__status");
            var hint = new Label { name = "tool-panel-hint", text = "" };
            hint.AddToClassList("tool-panel__hint");
            text.Add(status);
            text.Add(hint);
            panel.Add(text);
            var cancel = new Button { name = "tool-panel-cancel", text = "Cancel" };
            cancel.AddToClassList("tool-panel__button");
            cancel.AddToClassList("tool-panel__button--cancel");
            var confirm = new Button { name = "tool-panel-confirm", text = "Confirm" };
            confirm.AddToClassList("tool-panel__button");
            confirm.AddToClassList("tool-panel__button--confirm");
            panel.Add(cancel);
            panel.Add(confirm);
            rootEl.Add(panel);
            root.Add(rootEl);
        }
    }
}
