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

        /// <summary>Sets <c>focusable = false</c> on <paramref name="root"/> and every descendant.</summary>
        public static void DisableFocus(VisualElement root)
        {
            root.focusable = false;
            foreach (var child in root.Children()) DisableFocus(child);
        }

        private static void StopNavigation<T>(EventBase<T> evt) where T : EventBase<T>, new()
        {
            evt.StopImmediatePropagation();
        }
    }
}
