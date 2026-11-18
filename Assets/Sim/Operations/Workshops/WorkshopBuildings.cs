using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Inventory;

namespace LordsLegacy.Sim.Operations.Workshops
{
    /// <summary>
    /// Stock and store of a built workshop (HW-01, PL-01, decision 0047): when a hewing station or sawpit finishes
    /// building it gets its input <see cref="LocationKind.WorkshopStock"/> and its output
    /// <see cref="LocationKind.GoodsStore"/>, sized from the def. Both live in the ledger, so the ledger save section covers them.
    /// </summary>
    public static class WorkshopBuildings
    {
        /// <summary>True for a hewing station or sawpit footprint type.</summary>
        public static bool IsWorkshop(FootprintTypeDef type) =>
            type != null && OperationTypes.WorkshopOperationFor(type.Id) != null;

        /// <summary>True only for a def with at least one input and one output (ST-42b). A null def, or one with no input
        /// (a daub pit) or no output (a gathering hut), is not a workshop for yard collection.</summary>
        public static bool IsWorkshop(WorkshopDef def) => def != null && def.Inputs.Count > 0 && def.Outputs.Count > 0;

        /// <summary>True for a building whose built footprint holds a <see cref="LocationKind.GoodsStore"/>: a workshop or a storage yard (ST-07).</summary>
        public static bool HoldsGoods(FootprintTypeDef type) =>
            IsWorkshop(type) || (type != null && (type.Id == ModuleTypes.StorageYardId || type.Id == ModuleTypes.CoppiceHutId));

        /// <summary>Creates the stock and store of a just-built workshop footprint; does nothing for other types or if they exist.</summary>
        public static void OnBuilt(SimWorld world, Footprint fp)
        {
            if (fp == null) return;
            if (fp.Type.Id == ModuleTypes.StorageYardId)
            {
                // ST-03: the yard's one General store, capacity from tuning, created when it is built.
                if (world.Inventory.FindGoodsStore(fp.Id) == null) world.Inventory.AddGoodsStore(world.Tuning.StorageYardCapacity, fp.Id);
                return;
            }
            if (fp.Type.Id == ModuleTypes.CoppiceHutId)
            {
                // MAT-14: the hut's own rod store, capped per kind. The yard never collects from it (ST-42b).
                if (world.Inventory.FindGoodsStore(fp.Id) == null)
                {
                    int per = System.Math.Max(1, world.Tuning.CoppiceHutStorePerKind);
                    LedgerLocation s = world.Inventory.AddGoodsStore(per * 2, fp.Id);
                    world.Inventory.SetItemCap(s, Items.HazelRod, per);
                    world.Inventory.SetItemCap(s, Items.WillowRod, per);
                }
                return;
            }
            if (!IsWorkshop(fp.Type)) return;
            WorkshopDef def = OperationTypes.WorkshopOf(OperationTypes.WorkshopOperationFor(fp.Type.Id), world.Tuning);
            Ledger inv = world.Inventory;
            foreach (RecipeInput input in def.Inputs) // one stock per input item, each with its own capacity (decision 0051)
                if (inv.FindWorkshopStock(fp.Id, input.Item.Id) == null) inv.AddWorkshopStock(fp.Id, input.Item, def.StockCapacityOf(input));
            if (inv.FindGoodsStore(fp.Id) == null) inv.AddGoodsStore(def.OutputCapacity, fp.Id);
        }
    }
}
