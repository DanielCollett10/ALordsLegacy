using System;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Operations;

namespace LordsLegacy.Game
{
    /// <summary>
    /// One build bar button: id, label, whether it can be used, its tooltip (ui.md UIB-01) and, for buildings that
    /// cost logs, its cost source (building-tool.md BT-02, BT-03). The bar gates the entry live against available logs.
    /// </summary>
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
        /// <summary>
        /// Minimum logs the entry needs (for example <see cref="BuildCost.MinimumFor"/>), read live. Null means no cost gate.
        /// </summary>
        public Func<int> CostLogs { get; }

        /// <summary>Creates an entry. Pass <paramref name="costLogs"/> to gate it on available logs.</summary>
        public ToolEntry(string id, string label, bool enabled, string tooltip = "", Func<int> costLogs = null)
        {
            Id = id; Label = label; Enabled = enabled; Tooltip = tooltip ?? ""; CostLogs = costLogs;
        }

        /// <summary>The M3 entry list (ui.md UIB-02).</summary>
        public static ToolEntry[] Defaults()
        {
            return new[]
            {
                new ToolEntry("road", "Road", true),
                new ToolEntry("test-area", "Test area", true),
                new ToolEntry("test-work-area", "Test work area", true),
                new ToolEntry("woodcutter", "Woodcutter", true, "Draw the woodcutter's area, then place its modules",
                    () => BuildCost.MinimumFor(OperationTypes.Woodcutting)),
                new ToolEntry("storage-yard", "Storage yard", false, "Coming later"),
                new ToolEntry("house", "House plot", true, "Draw a plot on a road; odd-jobbers build the house",
                    () => ModuleTypes.House.ConstructionRecipe.Logs),
                new ToolEntry("granary", "Granary", false, "Coming later"),
            };
        }
    }
}
