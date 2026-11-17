using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;

namespace LordsLegacy.Sim.Construction
{
    /// <summary>
    /// Reserves every item of a <see cref="ConstructionRecipe"/> at once (decision 0046): nearest sources first per item,
    /// all or nothing. Reserved stock stays in its store's total but leaves its available count.
    /// </summary>
    public static class SiteMaterials
    {
        /// <summary>
        /// Reserves all recipe items nearest <paramref name="point"/> first. Returns the reservation ids in the order taken
        /// (empty for an item-free recipe), or null with nothing left reserved when any item is short.
        /// </summary>
        public static List<int> TryReserveAll(SimWorld world, ConstructionRecipe recipe, PointMm point, int owner)
        {
            if (world == null) throw new System.ArgumentNullException(nameof(world));
            if (recipe == null) throw new System.ArgumentNullException(nameof(recipe));
            var all = new List<int>();
            foreach (var kv in recipe.Items)
            {
                ItemDef item = Items.Find(kv.Key);
                List<int> ids = item == null ? null : ConstructionSources.TryReserve(world, item, kv.Value, point, owner);
                if (ids == null)
                {
                    foreach (int id in all) world.Inventory.Release(id);
                    return null;
                }
                all.AddRange(ids);
            }
            return all;
        }

        /// <summary>
        /// Refusal text for the first short item, e.g. "Not enough planks (have 4, need 6)", or null when nothing is short.
        /// </summary>
        public static string Shortfall(SimWorld world, ConstructionRecipe recipe)
        {
            if (world == null) throw new System.ArgumentNullException(nameof(world));
            if (recipe == null) throw new System.ArgumentNullException(nameof(recipe));
            foreach (var kv in recipe.Items)
            {
                ItemDef item = Items.Find(kv.Key);
                int have = item == null ? 0 : ConstructionSources.Available(world, item);
                if (have < kv.Value) return "Not enough " + Plural(kv.Key) + " (have " + have + ", need " + kv.Value + ")";
            }
            return null;
        }

        private static string Plural(string itemId)
        {
            string name = itemId.StartsWith("item.") ? itemId.Substring(5) : itemId;
            return name + "s";
        }
    }
}
