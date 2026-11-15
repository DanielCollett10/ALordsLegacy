using System;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Shared modal confirm dialog (UI Toolkit): a message, a Cancel button and one confirm button with a caller's text.
    /// Used for deconstruct and for deleting a save. While it is open <see cref="GameMenuState.IsOpen"/> is true, so tool,
    /// camera and picking input are ignored. Escape cancels it. Its buttons are non-focusable (UIK-01).
    /// The UI is built by <see cref="ConfirmDialogHost"/>, which this class creates on first use, so no scene wiring is needed.
    /// Showing while already open replaces the message and the pending confirm action.
    /// </summary>
    public static class ConfirmDialog
    {
        private static ConfirmDialogHost _host;
        private static bool _open;
        private static int _escapeFrame = -1;
        private static Action _onConfirm;

        /// <summary>True while the dialog is shown.</summary>
        public static bool IsOpen => _open;

        /// <summary>
        /// Opens the dialog with <paramref name="message"/>, a Cancel button and a confirm button labelled
        /// <paramref name="confirmText"/>. Confirm hides the dialog and then calls <paramref name="onConfirm"/> once.
        /// Cancel or Escape hides it and calls nothing.
        /// </summary>
        public static void Show(string message, string confirmText, Action onConfirm)
        {
            EnsureHost();
            _open = true;
            _onConfirm = onConfirm;
            _host.Render(message ?? "", string.IsNullOrEmpty(confirmText) ? "Confirm" : confirmText);
        }

        /// <summary>
        /// True while an Escape press belongs to the dialog: it is open, or it was cancelled by Escape in this frame.
        /// Every other Escape handler must ignore the key when this is true, whatever the Update order.
        /// </summary>
        public static bool OwnsEscape => _open || _escapeFrame == UnityEngine.Time.frameCount;

        /// <summary>Cancels because Escape was pressed, and marks the key as consumed for the rest of this frame.</summary>
        internal static void CancelWithEscape()
        {
            if (!_open) return;
            _escapeFrame = UnityEngine.Time.frameCount;
            Cancel();
        }

        /// <summary>Closes the dialog without calling the confirm action. Ignored while closed.</summary>
        public static void Cancel()
        {
            if (!_open) return;
            _open = false;
            _onConfirm = null;
            _host?.Hide();
        }

        /// <summary>Closes the dialog and then calls the confirm action once. Ignored while closed.</summary>
        public static void Confirm()
        {
            if (!_open) return;
            var action = _onConfirm;
            _open = false;
            _onConfirm = null;
            _host?.Hide();
            action?.Invoke();
        }

        /// <summary>Forgets the host and the open state. Called when the host is destroyed (scene unload).</summary>
        internal static void Forget()
        {
            _host = null;
            _open = false;
            _onConfirm = null;
        }

        private static void EnsureHost()
        {
            if (_host != null) return;
            var go = new UnityEngine.GameObject("ConfirmDialog");
            var doc = go.AddComponent<UnityEngine.UIElements.UIDocument>();
            doc.panelSettings = ConfirmDialogHost.CreatePanelSettings();
            _host = go.AddComponent<ConfirmDialogHost>();
        }
    }
}
