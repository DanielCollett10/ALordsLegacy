namespace LordsLegacy.Game
{
    /// <summary>
    /// Decides whether a cost-gated build bar entry is enabled, and its tooltip while it is not (building-tool.md BT-02, BT-03).
    /// Plain C#: no Unity references, so it is tested in EditMode. Costs and logs are whole logs.
    /// </summary>
    public static class BuildEntryState
    {
        /// <summary>
        /// Enabled when <paramref name="availableLogs"/> covers <paramref name="costLogs"/> (equal counts as enough).
        /// When disabled the tooltip reads "Needs N logs (have M)"; when enabled the tooltip is empty.
        /// </summary>
        public static (bool Enabled, string Tooltip) Of(int costLogs, int availableLogs)
        {
            if (availableLogs >= costLogs) return (true, "");
            return (false, "Needs " + costLogs + " logs (have " + availableLogs + ")");
        }
    }
}
