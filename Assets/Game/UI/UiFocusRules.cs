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
        // The text fields that currently hold focus. A set (not a counter) so a FocusIn that bubbles up from the
        // inner input can be seen twice without drifting, and so a field that is removed cannot leave an open edit behind.
        private static readonly System.Collections.Generic.HashSet<VisualElement> _editing = new System.Collections.Generic.HashSet<VisualElement>();

        /// <summary>
        /// True while a game text field is being edited, and for the rest of the frame in which it was released.
        /// Camera and tool hotkeys (which read the keyboard directly) must ignore the keyboard while this is true.
        /// </summary>
        public static bool TextInputActive => _editing.Count > 0 || _releasedFrame == UnityEngine.Time.frameCount;

        /// <summary>The keyboard for game hotkeys: null while a text field is being edited.</summary>
        public static UnityEngine.InputSystem.Keyboard GameKeyboard => TextInputActive ? null : UnityEngine.InputSystem.Keyboard.current;

        /// <summary>
        /// Marks a text field as the exception to the no-focus rule (UIK-01): it takes focus only while the player edits
        /// it, and gives it back on Enter or Escape. Call before <see cref="Apply"/>; Apply then leaves it focusable.
        /// </summary>
        public static void AllowTextInput(VisualElement field)
        {
            field.AddToClassList(TextInputClass);
            // Neither the field nor its inner input may take focus from navigation keys (WASD, arrows, Tab).
            // Only a pointer-down on the field opens the edit (BeginTextInput), so a navigation key can never land in it.
            var input = FirstFocusableDescendant(field);
            field.userData = input;
            field.focusable = false;
            if (input != null) input.focusable = false;
            field.RegisterCallback<PointerDownEvent>(_ => BeginTextInput(field), TrickleDown.TrickleDown);
            // FocusIn/FocusOut bubble from the inner input, so the field sees them for its own edit; Set makes repeats harmless.
            field.RegisterCallback<FocusInEvent>(_ => _editing.Add(field));
            field.RegisterCallback<FocusOutEvent>(_ =>
            {
                _editing.Remove(field);
                if (input != null) input.focusable = false;
                _releasedFrame = UnityEngine.Time.frameCount;
            });
            field.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode != UnityEngine.KeyCode.Return && e.keyCode != UnityEngine.KeyCode.KeypadEnter && e.keyCode != UnityEngine.KeyCode.Escape) return;
                e.StopPropagation();
                ReleaseFocus(field);
            }, TrickleDown.TrickleDown);
        }

        /// <summary>
        /// Opens an edit on a text field registered with <see cref="AllowTextInput"/>: its inner input becomes focusable and
        /// takes focus. Called on pointer-down over the field; tests call it to stand in for a click.
        /// </summary>
        public static void BeginTextInput(VisualElement field)
        {
            if (field?.userData is not VisualElement input) return;
            input.focusable = true;
            input.Focus();
        }

        // The inner input is the first focusable element under the field (found before the field is made unfocusable).
        private static VisualElement FirstFocusableDescendant(VisualElement root)
        {
            foreach (var child in root.Children())
            {
                if (child.focusable) return child;
                var found = FirstFocusableDescendant(child);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Forgets edits in fields that are no longer in a panel (call when the UI is rebuilt, so a destroyed field cannot leave typing stuck on).</summary>
        public static void ResetTextInput() => _editing.RemoveWhere(f => f == null || f.panel == null);

        /// <summary>
        /// Takes keyboard focus off <paramref name="field"/> if it (or something inside it) is focused. Leaves focus
        /// alone when it is somewhere else, so one field never blurs another control.
        /// </summary>
        public static void ReleaseFocus(VisualElement field)
        {
            if (field?.panel?.focusController?.focusedElement is VisualElement focused && IsWithin(focused, field))
                focused.Blur();
        }

        /// <summary>True when <paramref name="element"/> is <paramref name="ancestor"/> or one of its descendants.</summary>
        public static bool IsWithin(VisualElement element, VisualElement ancestor)
        {
            for (var e = element; e != null; e = e.parent)
                if (e == ancestor) return true;
            return false;
        }

        /// <summary>Sets <c>focusable = false</c> on <paramref name="root"/> and every descendant, except text fields.</summary>
        public static void DisableFocus(VisualElement root)
        {
            if (root.ClassListContains(TextInputClass)) return;
            root.focusable = false;
            foreach (var child in root.Children()) DisableFocus(child);
        }

        // StopImmediatePropagation alone does not stop the focus change: the default action still moves focus to the
        // only focusable element (the reserve box's input). PreventDefault and IgnoreEvent stop that default action.
        private static void StopNavigation<T>(EventBase<T> evt) where T : EventBase<T>, new()
        {
            evt.StopImmediatePropagation();
            evt.PreventDefault();
            (evt.target as VisualElement)?.panel?.focusController?.IgnoreEvent(evt);
        }
    }
}
