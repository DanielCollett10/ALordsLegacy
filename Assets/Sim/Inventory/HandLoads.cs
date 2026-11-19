using LordsLegacy.Sim.Tuning;

namespace LordsLegacy.Sim.Inventory
{
    /// <summary>
    /// The hand load per trip for each item: how many one person carries by hand (ST-43, PL-16).
    /// A General item uses <see cref="SimTuning.HandCarryItems"/> unless it has its own row here.
    /// Add a row when an item gets its own value (rods, thatch, panels, daub).
    /// </summary>
    public static class HandLoads
    {

        /// <summary>
        /// Items one person carries by hand per trip, or null when the item has no hand load:
        /// beams go by ox only (ST-43), and logs and food are not General goods, so this lookup does not cover them.
        /// </summary>
        /// <param name="item">The item. Null gives null.</param>
        /// <param name="tuning">The tuning the world runs with.</param>
        public static int? For(ItemDef item, SimTuning tuning)
        {
            if (item == null || tuning == null) return null;
            if (item.Id == Items.Beam.Id) return null;
            if (item.Id == Items.Plank.Id) return tuning.HandCarryPlanks;
            if (item.Id == Items.WillowRod.Id || item.Id == Items.HazelRod.Id) return tuning.HandCarryRods;
            if (item.Id == Items.Basket.Id) return tuning.HandCarryBaskets;
            if (item.Id == Items.WattlePanel.Id) return tuning.HandCarryPanels;
            if (item.Id == Items.Thatch.Id) return tuning.HandCarryThatch;
            if (item.Id == Items.Daub.Id) return tuning.HandCarryDaub;
            return item.Category == ItemCategory.General ? tuning.HandCarryItems : (int?)null;
        }
    }
}
