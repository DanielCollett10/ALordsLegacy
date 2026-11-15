using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace LordsLegacy.Game
{
    /// <summary>
    /// The UI and Escape handling for <see cref="ConfirmDialog"/>. Built in code (no UXML) with explicit colours, so it reads
    /// the same without the default theme. The full-screen backdrop takes pointer input while the dialog is shown, which
    /// blocks clicks reaching the game (<c>ToolController.IsPointerOverUiAt</c>), and it is hidden (display none) otherwise.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ConfirmDialogHost : MonoBehaviour
    {
        private VisualElement _backdrop;
        private Label _message;
        private Button _cancel;
        private Button _confirm;

        /// <summary>Creates the PanelSettings used by the dialog's UIDocument. Drawn above the other UI documents.</summary>
        public static PanelSettings CreatePanelSettings()
        {
            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.sortingOrder = 1000f;
            return ps;
        }

        /// <summary>Sets the message and the confirm button text, and shows the dialog.</summary>
        public void Render(string message, string confirmText)
        {
            EnsureBuilt();
            _message.text = message;
            _confirm.text = confirmText;
            _backdrop.style.display = DisplayStyle.Flex;
        }

        /// <summary>Hides the dialog.</summary>
        public void Hide()
        {
            if (_backdrop == null) return;
            _backdrop.style.display = DisplayStyle.None;
        }

        private void OnEnable()
        {
            EnsureBuilt();
        }

        private void OnDestroy()
        {
            ConfirmDialog.Forget();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (ConfirmDialog.IsOpen && kb != null && kb.escapeKey.wasPressedThisFrame) ConfirmDialog.Cancel();
        }

        private void EnsureBuilt()
        {
            var doc = GetComponent<UIDocument>();
            var root = doc.rootVisualElement;
            if (root == null || _backdrop != null && _backdrop.parent == root) return;
            root.Clear();
            Build(root);
        }

        private void Build(VisualElement root)
        {
            _backdrop = new VisualElement { name = "confirm-dialog-backdrop" };
            _backdrop.style.position = Position.Absolute;
            _backdrop.style.left = 0;
            _backdrop.style.right = 0;
            _backdrop.style.top = 0;
            _backdrop.style.bottom = 0;
            _backdrop.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
            _backdrop.style.justifyContent = Justify.Center;
            _backdrop.style.alignItems = Align.Center;
            _backdrop.pickingMode = PickingMode.Position;
            _backdrop.style.display = DisplayStyle.None;

            var panel = new VisualElement { name = "confirm-dialog" };
            panel.style.width = 420;
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.paddingLeft = panel.style.paddingRight = panel.style.paddingTop = panel.style.paddingBottom = 16;
            panel.style.backgroundColor = new Color(0.16f, 0.13f, 0.11f, 1f);
            panel.style.borderTopWidth = panel.style.borderBottomWidth = panel.style.borderLeftWidth = panel.style.borderRightWidth = 2;
            panel.style.borderTopColor = panel.style.borderBottomColor = panel.style.borderLeftColor = panel.style.borderRightColor
                = new Color(0.47f, 0.39f, 0.27f, 1f);

            _message = new Label { name = "confirm-dialog-message" };
            _message.style.color = new Color(0.94f, 0.91f, 0.84f, 1f);
            _message.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _message.style.fontSize = 16;
            _message.style.whiteSpace = WhiteSpace.Normal;
            _message.style.marginBottom = 16;

            var row = new VisualElement { name = "confirm-dialog-buttons" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.FlexEnd;

            _cancel = MakeButton("confirm-dialog-cancel", "Cancel", new Color(0.27f, 0.24f, 0.2f, 1f));
            _cancel.clicked += ConfirmDialog.Cancel;
            _confirm = MakeButton("confirm-dialog-confirm", "Confirm", new Color(0.55f, 0.24f, 0.16f, 1f));
            _confirm.clicked += ConfirmDialog.Confirm;
            row.Add(_cancel);
            row.Add(_confirm);

            panel.Add(_message);
            panel.Add(row);
            _backdrop.Add(panel);
            root.Add(_backdrop);
            UiFocusRules.Apply(root);
        }

        private static Button MakeButton(string name, string text, Color background)
        {
            var b = new Button { name = name, text = text };
            b.style.marginLeft = 8;
            b.style.paddingLeft = b.style.paddingRight = 14;
            b.style.paddingTop = b.style.paddingBottom = 6;
            b.style.backgroundColor = background;
            b.style.color = new Color(0.97f, 0.95f, 0.9f, 1f);
            b.style.borderTopWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 0;
            return b;
        }
    }
}
