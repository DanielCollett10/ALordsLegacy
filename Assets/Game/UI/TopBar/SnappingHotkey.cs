using System;
using UnityEngine.InputSystem;

namespace LordsLegacy.Game
{
    /// <summary>
    /// The Input System action <c>ToggleSnapping</c>, bound to Alt (<c>&lt;Keyboard&gt;/alt</c>). It is a button action,
    /// so <see cref="Action"/> fires once per press and never repeats while Alt is held (ui.md, roads.md Snapping).
    /// Created in code so it can be written as text. Call <see cref="Dispose"/> to unbind.
    /// </summary>
    public sealed class SnappingHotkey : IDisposable
    {
        /// <summary>The action name, as shown in the Input System debugger.</summary>
        public const string ActionName = "ToggleSnapping";

        /// <summary>The binding path for Alt.</summary>
        public const string BindingPath = "<Keyboard>/alt";

        private readonly Action _onPressed;
        private readonly InputAction _action;

        /// <summary>Creates and enables the action. <paramref name="onPressed"/> runs once per Alt press.</summary>
        public SnappingHotkey(Action onPressed)
        {
            _onPressed = onPressed;
            _action = new InputAction(ActionName, InputActionType.Button, BindingPath);
            _action.performed += OnPerformed;
            _action.Enable();
        }

        /// <summary>The underlying Input System action (for tests and debugging).</summary>
        public InputAction Action => _action;

        /// <summary>Disables and disposes the action.</summary>
        public void Dispose()
        {
            _action.performed -= OnPerformed;
            _action.Dispose();
        }

        private void OnPerformed(InputAction.CallbackContext context)
        {
            _onPressed?.Invoke();
        }
    }
}
