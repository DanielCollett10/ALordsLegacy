using System.Text;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Inventory;

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

        /// <summary>
        /// Gate for an entry whose building costs a whole recipe (decision 0046, T-581). Enabled when the Sim query
        /// <see cref="BuildCost.Check"/> says nothing is short. The tooltip lists every recipe item as "Name: have / need";
        /// when short it starts with the Sim's refusal text (the same text a refused placement shows).
        /// </summary>
        public static (bool Enabled, string Tooltip) OfRecipe(SimWorld world, ConstructionRecipe recipe, string baseTooltip = "")
        {
            RecipeAffordability check = BuildCost.Check(world, recipe);
            var sb = new StringBuilder();
            if (check.IsAffordable) { if (!string.IsNullOrEmpty(baseTooltip)) sb.Append(baseTooltip).Append('\n'); }
            else sb.Append(check.Text).Append('\n');
            bool first = true;
            foreach (var kv in recipe.Items)
            {
                ItemDef item = Items.Find(kv.Key);
                int have = item == null ? 0 : ConstructionSources.Available(world, item);
                if (!first) sb.Append('\n');
                first = false;
                sb.Append(Capitalise(RecipeAffordability.DisplayName(kv.Key))).Append(": ").Append(have).Append(" / ").Append(kv.Value);
            }
            return (check.IsAffordable, sb.ToString());
        }

        private static string Capitalise(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
