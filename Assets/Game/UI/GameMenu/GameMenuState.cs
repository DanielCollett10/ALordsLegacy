namespace LordsLegacy.Game
{
    /// <summary>
    /// Whether the Esc game menu is open (SV-02). Tool, camera and picking input read this and ignore the player while it is
    /// true. Presentation state only; the pause itself lives in the Sim (decision 0006).
    /// </summary>
    public static class GameMenuState
    {
        /// <summary>True while the game menu is open.</summary>
        public static bool IsOpen { get; internal set; }
    }
}
