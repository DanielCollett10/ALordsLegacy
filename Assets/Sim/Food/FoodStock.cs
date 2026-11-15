using LordsLegacy.Sim.Inventory;

namespace LordsLegacy.Sim.Food
{
    /// <summary>One food type's raw count across every location, for the top bar tooltip (FD-04). Immutable.</summary>
    public sealed class FoodStock
    {
        /// <summary>Creates a stock line.</summary>
        public FoodStock(ItemDef item, int count)
        {
            Item = item; Count = count;
        }

        /// <summary>The food item.</summary>
        public ItemDef Item { get; }

        /// <summary>Items held in all locations.</summary>
        public int Count { get; }

        /// <summary>Rations those items feed (count × rations per item).</summary>
        public int Rations => Count * Item.RationsPerItem;
    }
}
