using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Inventory;

namespace LordsLegacy.Sim.Kit
{
    /// <summary>
    /// Kit stores (decision 0052, ST-20): a forager hut or granary gets a <see cref="LocationKind.GoodsStore"/> with
    /// <c>ModuleId</c> = the building and a per-item slot count from tuning (MAT-53) when it is built. Never a
    /// construction, workshop or yard source; village totals still count it. The ledger section saves it.
    /// </summary>
    public static class KitStores
    {
        /// <summary>Basket slots of a building type (0 when it does not use kit).</summary>
        public static int BasketSlots(SimWorld world, FootprintTypeDef type)
        {
            if (type == null) return 0;
            if (type.Id == ModuleTypes.ForagerHutId) return world.Tuning.ForagerHutBasketSlots;
            if (type.Id == ModuleTypes.GranaryId) return world.Tuning.GranaryBasketSlots;
            return 0;
        }

        /// <summary>True for a goods store that is a building's kit store.</summary>
        public static bool IsKitStore(SimWorld world, LedgerLocation l)
        {
            if (l == null || l.Kind != LocationKind.GoodsStore || l.ModuleId == 0) return false;
            Footprint f = world.Footprints.Get(l.ModuleId);
            return f != null && (f.Type.Id == ModuleTypes.ForagerHutId || f.Type.Id == ModuleTypes.GranaryId);
        }

        /// <summary>The building's kit store, or null.</summary>
        public static LedgerLocation Find(SimWorld world, int buildingId)
        {
            LedgerLocation l = world.Inventory.FindGoodsStore(buildingId);
            return IsKitStore(world, l) ? l : null;
        }

        /// <summary>Creates the kit store of a just-built kit-using building; no-op for other types, zero slots or if it exists.</summary>
        public static void OnBuilt(SimWorld world, Footprint fp)
        {
            if (fp == null) return;
            int slots = BasketSlots(world, fp.Type);
            if (slots <= 0 || world.Inventory.FindGoodsStore(fp.Id) != null) return;
            LedgerLocation s = world.Inventory.AddGoodsStore(slots, fp.Id);
            world.Inventory.SetItemCap(s, Items.Basket, slots);
        }
    }
}
