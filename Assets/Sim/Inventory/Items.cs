using System.Collections.Generic;

namespace LordsLegacy.Sim.Inventory
{
    /// <summary>The built-in item definitions (INV-A1).</summary>
    public static class Items
    {
        /// <summary>A felled log.</summary>
        public static readonly ItemDef Log = new ItemDef("item.log", ItemCategory.Log);

        /// <summary>Meat: 3 rations each (FD-01).</summary>
        public static readonly ItemDef Meat = new ItemDef("item.meat", ItemCategory.Food, 3);

        /// <summary>Berries: 1 ration each (FD-01).</summary>
        public static readonly ItemDef Berries = new ItemDef("item.berries", ItemCategory.Food, 1);

        /// <summary>Mushrooms: 1 ration each (FD-01).</summary>
        public static readonly ItemDef Mushrooms = new ItemDef("item.mushrooms", ItemCategory.Food, 1);

        /// <summary>A sawn plank (decision 0046).</summary>
        public static readonly ItemDef Plank = new ItemDef("item.plank", ItemCategory.General);

        /// <summary>A hewed beam (decision 0046).</summary>
        public static readonly ItemDef Beam = new ItemDef("item.beam", ItemCategory.General);

        /// <summary>All built-in items.</summary>
        public static IReadOnlyList<ItemDef> All { get; } = new[] { Log, Meat, Berries, Mushrooms, Plank, Beam };

        /// <summary>Finds an item by id, or null.</summary>
        public static ItemDef Find(string id)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Id == id) return All[i];
            return null;
        }
    }
}
