using UnityEngine.UIElements;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Keyboard focus rules for game UI (docs/design/ui.md UIK-01, UIK-02). Game buttons never take focus, and
    /// navigation events (WASD/arrows/Tab from the Input System UI Navigate action, Submit, Cancel) are stopped at the
    /// panel root so they never move a UI selection. The camera and tool keys read the keyboard directly and are unaffected.
    /// </summary>
    public static class UiFocusRules
    {
        private static readonly EventCallback<NavigationMoveEvent> StopMove = StopNavigation;
        private static readonly EventCallback<NavigationSubmitEvent> StopSubmit = StopNavigation;
        private static readonly EventCallback<NavigationCancelEvent> StopCancel = StopNavigation;

        /// <summary>
        /// Makes every focusable element under <paramref name="root"/> non-focusable (buttons included) and installs
        /// the navigation stop on <paramref name="root"/>. Safe to call again on the same root (no duplicate callbacks).
        /// </summary>
        public static void Apply(VisualElement root)
        {
            if (root == null) return;
            DisableFocus(root);
            root.UnregisterCallback(StopMove, TrickleDown.TrickleDown);
            root.UnregisterCallback(StopSubmit, TrickleDown.TrickleDown);
            root.UnregisterCallback(StopCancel, TrickleDown.TrickleDown);
            root.RegisterCallback(StopMove, TrickleDown.TrickleDown);
            root.RegisterCallback(StopSubmit, TrickleDown.TrickleDown);
            root.RegisterCallback(StopCancel, TrickleDown.TrickleDown);
        }

        /// <summary>USS class that exempts a text field (and its children) from <see cref="DisableFocus"/>.</summary>
        public const string TextInputClass = "ui-text-input";

        private static int _releasedFrame = -1;
        private static int _focusedFields;

        /// <summary>
        /// True while a game text field is being edited, and for the rest of the frame in which it was released.
        /// Camera and tool hotkeys (which read the keyboard directly) must ignore the keyboard while this is true.
        /// </summary>
        public static bool TextInputActive => _focusedFields > 0 || _releasedFrame == UnityEngine.Time.frameCount;

        /// <summary>The keyboard for game hotkeys: null while a text field is being edited.</summary>
        public static UnityEngine.InputSystem.Keyboard GameKeyboard => TextInputActive ? null : UnityEngine.InputSystem.Keyboard.current;

        /// <summary>
        /// Marks a text field as the exception to the no-focus rule (UIK-01): it takes focus only while the player edits
        /// it, and gives it back on Enter or Escape. Call before <see cref="Apply"/>; Apply then leaves it focusable.
        /// </summary>
        public static void AllowTextInput(VisualElement field)
        {
            field.AddToClassList(TextInputClass);
            field.RegisterCallback<FocusInEvent>(_ => _focusedFields++);
            field.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (_focusedFields > 0) _focusedFields--;
                _releasedFrame = UnityEngine.Time.frameCount;
            });
            field.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode != UnityEngine.KeyCode.Return && e.keyCode != UnityEngine.KeyCode.KeypadEnter && e.keyCode != UnityEngine.KeyCode.Escape) return;
                e.StopPropagation();
                ReleaseFocus(field);
            }, TrickleDown.TrickleDown);
        }

        /// <summary>Forgets any edit in progress (call when the UI is rebuilt, so a destroyed field cannot leave typing stuck on).</summary>
        public static void ResetTextInput() => _focusedFields = 0;

        /// <summary>Takes keyboard focus off whatever is focused under <paramref name="field"/>'s panel.</summary>
        public static void ReleaseFocus(VisualElement field)
        {
            field.panel?.focusController?.focusedElement?.Blur();
        }

        /// <summary>Sets <c>focusable = false</c> on <paramref name="root"/> and every descendant, except text fields.</summary>
        public static void DisableFocus(VisualElement root)
        {
            if (root.ClassListContains(TextInputClass)) return;
            root.focusable = false;
            foreach (var child in root.Children()) DisableFocus(child);
        }

        private static void StopNavigation<T>(EventBase<T> evt) where T : EventBase<T>, new()
        {
            evt.StopImmediatePropagation();
        }
    }
}
