namespace LordsLegacy.Sim.Inventory
{
    /// <summary>Broad class of an item (INV-A1).</summary>
    public enum ItemCategory
    {
        /// <summary>Felled timber.</summary>
        Log = 0,
        /// <summary>Something people eat (FD-01).</summary>
        Food = 1,
        /// <summary>Other goods such as planks and beams (decision 0046); kept in a goods store.</summary>
        General = 2,
    }
}
