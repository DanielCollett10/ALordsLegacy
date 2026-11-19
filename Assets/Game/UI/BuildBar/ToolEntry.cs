using System;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Markets;
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
        /// <summary>
        /// Live gate on Sim state: returns the reason the entry cannot be used now (shown as its tooltip) or an empty
        /// string when it can. Null means no such gate. The market uses it for MK-04.
        /// </summary>
        public Func<SimWorld, string> DisabledReason { get; }
        /// <summary>
        /// Full item recipe the entry costs (house: logs, thatch, wattle panels). When set and the Sim is up, the bar gates and
        /// words the tooltip from <see cref="BuildEntryState.OfRecipe"/> instead of the logs-only <see cref="CostLogs"/>.
        /// </summary>
        public ConstructionRecipe Recipe { get; }

        /// <summary>Build bar id of the Market entry (MK-01).</summary>
        public const string MarketToolId = "market";

        /// <summary>Creates an entry. Pass <paramref name="costLogs"/> to gate it on available logs.</summary>
        public ToolEntry(string id, string label, bool enabled, string tooltip = "", Func<int> costLogs = null,
            Func<SimWorld, string> disabledReason = null, ConstructionRecipe recipe = null)
        {
            Id = id; Label = label; Enabled = enabled; Tooltip = tooltip ?? ""; CostLogs = costLogs; DisabledReason = disabledReason;
            Recipe = recipe;
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
                new ToolEntry("forager", "Forager", true, "Draw the forager camp's area, then place its hut",
                    () => BuildCost.MinimumFor(OperationTypes.Foraging)),
                new ToolEntry(LordsLegacy.Game.Tools.OperationDraftTool.CoppiceToolId, "Coppice", true,
                    "Draw the coppice area, then place its hut: hazel and willow rods",
                    () => BuildCost.MinimumFor(OperationTypes.Coppice)),
                new ToolEntry(LordsLegacy.Game.Tools.OperationDraftTool.ThatcherToolId, "Thatcher's hut", true,
                    "Draw the thatcher's area, then place its hut: reed into thatch sheaves",
                    () => BuildCost.MinimumFor(OperationTypes.Thatching)),
                new ToolEntry(LordsLegacy.Game.Tools.StructureTool.StorageYardToolId, "Storage yard", true, "Place a storage yard: holds village goods",
                    () => ModuleTypes.StorageYard.ConstructionRecipe.Logs),
                new ToolEntry("house", "House plot", true, "Draw a plot on a road; odd-jobbers build the house",
                    () => ModuleTypes.House.ConstructionRecipe.Logs, null, ModuleTypes.House.ConstructionRecipe),
                new ToolEntry("granary", "Granary", true, "Place a granary with its 5 m yard",
                    () => ModuleTypes.Granary.ConstructionRecipe.Logs),
                new ToolEntry(MarketToolId, "Market", true, "Draw the town's market; its stall slots are shown (free)",
                    null, MarketRules.CanPlaceMarket),
                new ToolEntry(LordsLegacy.Game.Tools.StructureTool.HewingToolId,"Hewing station", true, "Place a hewing station: logs into beams",
                    () => ModuleTypes.HewingStation.ConstructionRecipe.Logs),
                new ToolEntry(LordsLegacy.Game.Tools.StructureTool.SawpitToolId,"Sawpit", true, "Place a sawpit: beams into planks",
                    () => ModuleTypes.Sawpit.ConstructionRecipe.Logs),
                new ToolEntry(LordsLegacy.Game.Tools.StructureTool.WeaverToolId, "Weaver", true, "Place a weaver: willow rods into baskets",
                    () => ModuleTypes.Weaver.ConstructionRecipe.Logs),
                new ToolEntry(LordsLegacy.Game.Tools.StructureTool.WattleYardToolId, "Wattle yard", true, "Place a wattle yard: hazel and willow rods into wattle panels",
                    () => ModuleTypes.WattleYard.ConstructionRecipe.Logs),
                new ToolEntry(LordsLegacy.Game.Tools.StructureTool.DaubPitToolId, "Daub pit", true, "Place a daub pit: earth and water into daub for houses",
                    () => ModuleTypes.DaubPit.ConstructionRecipe.Logs),
            };
        }
    }
}
