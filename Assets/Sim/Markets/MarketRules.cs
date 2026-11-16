using LordsLegacy.Sim.Core;

namespace LordsLegacy.Sim.Markets
{
    /// <summary>Pure market placement queries (MK-04).</summary>
    public static class MarketRules
    {
        /// <summary>Refusal text when the town already has a market.</summary>
        public const string OnlyOneText = "Only one market per town";

        /// <summary>
        /// Whether a market may be started now. Returns an empty string when it may, otherwise the reason for the
        /// build bar ("Only one market per town"). Never changes the world.
        /// </summary>
        public static string CanPlaceMarket(SimWorld world)
        {
            return world.Markets.Count > 0 ? OnlyOneText : string.Empty;
        }
    }
}
