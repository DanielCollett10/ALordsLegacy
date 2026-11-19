using System;

namespace LordsLegacy.Sim.Construction
{
    /// <summary>
    /// Result of <see cref="BuildCost.Check"/>: either affordable, or the first short item (in item-id order) with the
    /// stock available to construction (<see cref="Have"/>) and the amount the recipe needs (<see cref="Need"/>).
    /// <see cref="Text"/> is the refusal line, e.g. "Not enough thatch (have 4, need 10)"; null when affordable.
    /// </summary>
    public sealed class RecipeAffordability
    {
        /// <summary>A result that found nothing short.</summary>
        public static readonly RecipeAffordability Affordable = new RecipeAffordability(null, 0, 0);

        /// <summary>Creates a short result for <paramref name="itemId"/>.</summary>
        public RecipeAffordability(string itemId, int have, int need)
        {
            ItemId = itemId;
            Have = have;
            Need = need;
            Text = itemId == null ? null : "Not enough " + DisplayName(itemId) + " (have " + have + ", need " + need + ")";
        }

        /// <summary>Id of the first short item, or null when the recipe is affordable.</summary>
        public string ItemId { get; }

        /// <summary>True when every item of the recipe is available.</summary>
        public bool IsAffordable => ItemId == null;

        /// <summary>Available stock of the short item (0 when it is unknown).</summary>
        public int Have { get; }

        /// <summary>Amount the recipe needs of the short item.</summary>
        public int Need { get; }

        /// <summary>Refusal text for the short item, or null when affordable.</summary>
        public string Text { get; }

        /// <summary>
        /// Player-facing item name: "item.wattle_panel" becomes "wattle panels"; "thatch" and "daub" are mass nouns and stay as they are.
        /// </summary>
        public static string DisplayName(string itemId)
        {
            string name = itemId.StartsWith("item.", StringComparison.Ordinal) ? itemId.Substring(5) : itemId;
            name = name.Replace('_', ' ');
            return name == "thatch" || name == "daub" ? name : name + "s";
        }
    }
}
