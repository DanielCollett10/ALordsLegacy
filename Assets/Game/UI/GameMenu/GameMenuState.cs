namespace LordsLegacy.Game
{
    /// <summary>
    /// Whether game input is blocked by a modal: the Esc game menu (SV-02) or a <see cref="ConfirmDialog"/>. Tool, camera and
    /// picking input read <see cref="IsOpen"/> and ignore the player while it is true. Presentation state only; the pause
    /// itself lives in the Sim (decision 0006).
    /// </summary>
    public static class GameMenuState
    {
        /// <summary>True while the Esc game menu is open. Set only by <c>GameMenuView</c>.</summary>
        internal static bool MenuOpen { get; set; }

        /// <summary>True while the game menu or a confirm dialog is open, so game input is blocked.</summary>
        public static bool IsOpen => MenuOpen || ConfirmDialog.IsOpen;
    }
}
