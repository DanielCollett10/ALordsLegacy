using System;
using System.Text;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Text for the top bar food readout (FD-04): "Food: N days" and the tooltip with raw counts per type.
    /// Pure formatting of Sim queries; holds no rules.
    /// </summary>
    public static class FoodText
    {
        /// <summary>Top bar label, e.g. "Food: 342 days" (whole days covered). "Food: 0 days" when stock is empty.</summary>
        public static string DaysLine(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            double days = FoodStats.DaysLeft(world);
            // Nobody to feed: there is no daily need, so there are no days to count.
            if (double.IsPositiveInfinity(days)) return "Food: –";
            return "Food: " + (long)Math.Floor(days) + " days";
        }

        /// <summary>Tooltip with raw counts per food type, one line each, e.g. "Meat: 4 (12 rations)".</summary>
        public static string Tooltip(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var sb = new StringBuilder();
            foreach (FoodStock stock in FoodStats.CountsByType(world))
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(DisplayName(stock.Item.Id)).Append(": ").Append(stock.Count)
                    .Append(" (").Append(stock.Rations).Append(stock.Rations == 1 ? " ration)" : " rations)");
            }
            return sb.ToString();
        }

        /// <summary>Turns an item id such as "item.mushrooms" into "Mushrooms".</summary>
        public static string DisplayName(string itemId)
        {
            string s = itemId ?? "";
            int dot = s.LastIndexOf('.');
            if (dot >= 0) s = s.Substring(dot + 1);
            return s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
    }
}
