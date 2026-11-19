using System;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Construction
{
    /// <summary>
    /// The recipe an unbuilt footprint is built to (PL-13). A market stall placed before the plank recipe keeps its log cost:
    /// its site uses <see cref="ModuleTypes.MarketStallLogRecipe"/> while it holds, reserves or is delivering any log. This
    /// is derived from the saved site, so no save state is added. Every other footprint uses its type's recipe.
    /// </summary>
    public static class SiteRecipe
    {
        /// <summary>The recipe to build <paramref name="fp"/> to, from its site's current state.</summary>
        /// <exception cref="ArgumentNullException">The world or the footprint is null.</exception>
        public static ConstructionRecipe Of(SimWorld world, Footprint fp)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (fp == null) throw new ArgumentNullException(nameof(fp));
            bool stall = fp.Type.Id == ModuleTypes.MarketStallId;
            bool house = fp.Type.Id == ModuleTypes.HouseId;
            if (!stall && !house) return fp.Type.ConstructionRecipe;
            ConstructionSite site = world.Construction.Get(fp.Id);
            if (site == null) return fp.Type.ConstructionRecipe;
            if (stall) return HoldsLogs(world, site) ? ModuleTypes.MarketStallLogRecipe : fp.Type.ConstructionRecipe;
            // A new house reserves all four items at placement, and every item stays held (reserved, carried, on site or
            // used up) until the build ends. So a house that holds no daub was placed before daub (HL-04) and keeps the
            // older recipe, derived from the saved site; nothing new is saved.
            if (!HoldsLogs(world, site) || Holds(world, site, Items.Daub.Id)) return fp.Type.ConstructionRecipe;
            // T-577, MAT-40b: before thatch and panels a house held logs only; after them, logs, thatch and panels.
            bool thatchEra = Holds(world, site, Items.Thatch.Id) || Holds(world, site, Items.WattlePanel.Id);
            return thatchEra ? ModuleTypes.HouseNoDaubRecipe : ModuleTypes.HouseLogRecipe;
        }

        // Logs on the site, used up by the build, reserved for it, or on their way to it by a live delivery task.
        private static bool HoldsLogs(SimWorld world, ConstructionSite site)
        {
            if (Holds(world, site, Items.Log.Id)) return true;
            foreach (TaskRecord t in world.Tasks.All)
                if (!t.IsFinished && t.Def.Id == ConstructionTaskDefs.DeliverId && t.SiteFootprintId == site.FootprintId) return true;
            return false;
        }

        // The item is on the site, used up by the build, reserved for it, or carried to it by a live hand-carry task.
        private static bool Holds(SimWorld world, ConstructionSite site, string itemId)
        {
            if (site.ConsumedOf(itemId) > 0) return true;
            LedgerLocation loc = world.Inventory.Get(site.LedgerLocationId);
            if (loc != null && loc.CountOf(itemId) > 0) return true;
            foreach (int id in site.ReservationIds)
            {
                Reservation r = world.Inventory.GetReservation(id);
                if (r != null && r.ItemId == itemId) return true;
            }
            if (itemId == Items.Log.Id) return false;
            foreach (TaskRecord t in world.Tasks.All)
            {
                if (t.IsFinished || t.Def.Id != ConstructionTaskDefs.CarryId || t.SiteFootprintId != site.FootprintId) continue;
                foreach (TaskStep s in t.Def.Steps)
                    if (s.Kind == StepKind.DepositItems && s.ItemId == itemId) return true;
            }
            return false;
        }
    }
}
