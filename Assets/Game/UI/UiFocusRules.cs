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
            // The field and its inner input stay focusable: IntegerField delegates focus to its input, so making the field
            // unfocusable stops Focus() on the input from working at all. Focus that did not come from a pointer-down on
            // the field (WASD, arrows, Tab) is blurred again in FocusIn, so a navigation key never opens an edit.
            field.userData = FirstFocusableDescendant(field);
            field.RegisterCallback<PointerDownEvent>(_ => Allow(field), TrickleDown.TrickleDown);
            // FocusIn/FocusOut bubble from the inner input, so the field sees them for its own edit; Set makes repeats harmless.
            field.RegisterCallback<FocusInEvent>(e =>
            {
                var target = e.target as VisualElement;
                if (target == null) return;
                // Accept focus on any element inside this field, but only within a few frames of a click or BeginTextInput.
                bool allowed = _allowedField == field && UnityEngine.Time.frameCount - _allowedFrame <= 2;
                if (allowed && IsWithin(target, field))
                {
                    _allowedField = null;
                    _editing.Add(field);
                }
                else if (!_editing.Contains(field)) target.Blur();
            });
            field.RegisterCallback<FocusOutEvent>(_ =>
            {
                // Only a real edit ending releases the camera; a rejected focus never added the field.
                if (_editing.Remove(field)) _releasedFrame = UnityEngine.Time.frameCount;
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
            Allow(field);
            input.Focus();
        }

        // The field the player has just pointed at, valid for a few frames after the pointer-down (see FocusIn above).
        private static VisualElement _allowedField;
        private static int _allowedFrame = -1;

        private static void Allow(VisualElement field)
        {
            _allowedField = field;
            _allowedFrame = UnityEngine.Time.frameCount;
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
        // only focusable element (the reserve box). IgnoreEvent stops that default action.
        private static void StopNavigation<T>(EventBase<T> evt) where T : EventBase<T>, new()
        {
            evt.StopImmediatePropagation();
            (evt.target as VisualElement)?.panel?.focusController?.IgnoreEvent(evt);
        }
    }
}
