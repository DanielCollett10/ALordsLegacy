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

        /// <summary>A willow rod cut from a willow stool (MAT-01).</summary>
        public static readonly ItemDef WillowRod = new ItemDef("item.willow_rod", ItemCategory.General);

        /// <summary>A hazel rod cut from a hazel stool (MAT-01).</summary>
        public static readonly ItemDef HazelRod = new ItemDef("item.hazel_rod", ItemCategory.General);

        /// <summary>A willow basket made by the weaver (MAT-03).</summary>
        public static readonly ItemDef Basket = new ItemDef("item.basket", ItemCategory.General);

        /// <summary>A wattle panel made by the wattle yard (MAT-04).</summary>
        public static readonly ItemDef WattlePanel = new ItemDef("item.wattle_panel", ItemCategory.General);

        /// <summary>A sheaf of cut reed, cured then used for thatch (MAT-02).</summary>
        public static readonly ItemDef Thatch = new ItemDef("item.thatch", ItemCategory.General);

        /// <summary>All built-in items.</summary>
        public static IReadOnlyList<ItemDef> All { get; } = new[]
        {
            Log, Meat, Berries, Mushrooms, Plank, Beam, WillowRod, HazelRod, Basket, WattlePanel, Thatch,
        };

        /// <summary>Finds an item by id, or null.</summary>
        public static ItemDef Find(string id)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Id == id) return All[i];
            return null;
        }
    }
}
