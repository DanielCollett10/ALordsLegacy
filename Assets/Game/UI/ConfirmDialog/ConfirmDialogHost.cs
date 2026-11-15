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
        private VisualElement _dialog;
        private float _layoutW = -1f;
        private float _layoutH = -1f;

        /// <summary>Creates the PanelSettings used by the dialog's UIDocument. Drawn above the other UI documents.</summary>
        public static PanelSettings CreatePanelSettings()
        {
            // A PanelSettings with no theme stylesheet has no default font, so labels and buttons draw no text. Copy the
            // shared PanelSettings (it carries the runtime theme) from any UIDocument already in the scene.
            PanelSettings ps = null;
            foreach (var other in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                if (other.panelSettings != null && other.panelSettings.themeStyleSheet != null)
                {
                    ps = Object.Instantiate(other.panelSettings);
                    break;
                }
            }
            if (ps == null)
            {
                ps = ScriptableObject.CreateInstance<PanelSettings>();
#if UNITY_EDITOR
                ps.themeStyleSheet = UnityEditor.AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
                    "Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss");
#endif
            }
            ps.sortingOrder = 1000f;
#if UNITY_EDITOR
            // Everything is styled in code, so the missing-theme warning is noise (same switch as the test PanelSettings).
            var so = new UnityEditor.SerializedObject(ps);
            var prop = so.FindProperty("m_DisableNoThemeWarning");
            if (prop != null)
            {
                prop.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
#endif
            return ps;
        }

        /// <summary>Sets the message and the confirm button text, and shows the dialog.</summary>
        public void Render(string message, string confirmText)
        {
            EnsureBuilt();
            _message.text = message;
            _confirm.text = confirmText;
            _backdrop.style.display = DisplayStyle.Flex;
            ApplyLayout();
        }

        // Centres the dialog on the screen: root and backdrop get the screen size in pixels, the dialog sits at the middle
        // and is shifted back by half its own size.
        private void ApplyLayout()
        {
            if (_backdrop == null || _dialog == null) return;
            var scale = GetComponent<UIDocument>().panelSettings != null ? Mathf.Max(0.01f, GetComponent<UIDocument>().panelSettings.scale) : 1f;
            float w = Screen.width / scale;
            float h = Screen.height / scale;
            if (Mathf.Approximately(w, _layoutW) && Mathf.Approximately(h, _layoutH)) return;
            _layoutW = w;
            _layoutH = h;
            var root = _backdrop.parent;
            if (root != null)
            {
                root.style.width = w;
                root.style.height = h;
            }
            _backdrop.style.width = w;
            _backdrop.style.height = h;
            _dialog.style.left = w * 0.5f;
            _dialog.style.top = h * 0.5f;
            _dialog.style.translate = new Translate(new Length(-50, LengthUnit.Percent), new Length(-50, LengthUnit.Percent));
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
            if (ConfirmDialog.IsOpen) ApplyLayout();
            if (ConfirmDialog.IsOpen && kb != null && kb.escapeKey.wasPressedThisFrame) ConfirmDialog.CancelWithEscape();
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
            // The UIDocument root can collapse to its content height, which put the centred dialog at the top of the
            // screen. Make the root fill the panel explicitly so the backdrop's centring is relative to the whole screen.
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.right = 0;
            root.style.top = 0;
            root.style.bottom = 0;
            root.style.width = new Length(100, LengthUnit.Percent);
            root.style.height = new Length(100, LengthUnit.Percent);
            root.pickingMode = PickingMode.Ignore;
            _layoutW = _layoutH = -1f;

            _backdrop = new VisualElement { name = "confirm-dialog-backdrop" };
            _backdrop.style.position = Position.Absolute;
            _backdrop.style.width = new Length(100, LengthUnit.Percent);
            _backdrop.style.height = new Length(100, LengthUnit.Percent);
            _backdrop.style.left = 0;
            _backdrop.style.right = 0;
            _backdrop.style.top = 0;
            _backdrop.style.bottom = 0;
            _backdrop.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
            _backdrop.pickingMode = PickingMode.Position;
            _backdrop.style.display = DisplayStyle.None;

            // Positioned in pixels from the screen size (see ApplyLayout), not by flex centring, so it cannot depend on
            // how the UIDocument root is sized.
            var panel = new VisualElement { name = "confirm-dialog" };
            _dialog = panel;
            panel.style.position = Position.Absolute;
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
