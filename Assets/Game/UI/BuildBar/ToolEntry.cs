namespace LordsLegacy.Game
{
    /// <summary>One build bar button: id, label, whether it can be used and its tooltip (ui.md UIB-01).</summary>
    public sealed class ToolEntry
    {
        /// <summary>Tool id, matched against <c>ITool.Id</c>.</summary>
        public string Id { get; }
        /// <summary>Button text.</summary>
        public string Label { get; }
        /// <summary>False shows the button greyed and ignores clicks.</summary>
        public bool Enabled { get; }
        /// <summary>Tooltip text; empty for none.</summary>
        public string Tooltip { get; }

        /// <summary>Creates an entry.</summary>
        public ToolEntry(string id, string label, bool enabled, string tooltip = "")
        {
            Id = id; Label = label; Enabled = enabled; Tooltip = tooltip ?? "";
        }

        /// <summary>The M3 entry list (ui.md UIB-02).</summary>
        public static ToolEntry[] Defaults()
        {
            return new[]
            {
                new ToolEntry("road", "Road", true),
                new ToolEntry("test-area", "Test area", false),
                new ToolEntry("test-work-area", "Test work area", false),
                new ToolEntry("woodcutting", "Woodcutting", false, "Coming later"),
                new ToolEntry("storage-yard", "Storage yard", false, "Coming later"),
                new ToolEntry("house", "House", false, "Coming later"),
                new ToolEntry("granary", "Granary", false, "Coming later"),
            };
        }
    }
}
