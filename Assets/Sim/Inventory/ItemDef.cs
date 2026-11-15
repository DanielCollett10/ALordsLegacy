namespace LordsLegacy.Sim.Inventory
{
    /// <summary>Definition of one kind of item. Immutable.</summary>
    public sealed class ItemDef
    {
        /// <summary>Stable id, e.g. <c>item.log</c>.</summary>
        public string Id { get; }
        /// <summary>Item category.</summary>
        public ItemCategory Category { get; }
        /// <summary>Rations one item feeds; 0 for non-food (FD-01).</summary>
        public int RationsPerItem { get; }

        /// <summary>Creates an item definition.</summary>
        public ItemDef(string id, ItemCategory category, int rationsPerItem = 0)
        {
            Id = id; Category = category; RationsPerItem = rationsPerItem;
        }
    }
}
