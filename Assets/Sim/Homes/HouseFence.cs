using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Inventory;

namespace LordsLegacy.Sim.Homes
{
    /// <summary>State of a house's fence, derived from the link and the construction site (decision 0053).</summary>
    public enum FenceState { None = 0, Building = 1, Built = 2 }

    /// <summary>Helpers for a house's fence: its state, its plot-derived recipe and its removal (HL-10..13, decision 0053).</summary>
    public static class HouseFence
    {
        /// <summary>None with no link; Building while the fence footprint has a site; Built otherwise.</summary>
        public static FenceState StateOf(SimWorld world, HomeRecord home)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (home == null) throw new ArgumentNullException(nameof(home));
            if (home.FenceFootprintId == 0 || world.Footprints.Get(home.FenceFootprintId) == null) return FenceState.None;
            return world.IsBuilt(home.FenceFootprintId) ? FenceState.Built : FenceState.Building;
        }

        /// <summary>The fence recipe of the plot (the fence type's own recipe is empty).</summary>
        public static ConstructionRecipe RecipeFor(SimWorld world, HomeRecord home)
        {
            AreaRecord plot = world.Areas.Get(home.PlotAreaId);
            return plot == null ? ConstructionRecipe.Empty : FenceLine.Of(plot).Recipe(world.Tuning);
        }

        /// <summary>The recipe for a fence footprint, or null when the footprint is not a linked fence.</summary>
        public static ConstructionRecipe RecipeOf(SimWorld world, Footprint fp)
        {
            if (fp == null || fp.Type.Id != ModuleTypes.FenceId) return null;
            HomeRecord home = world.Homes.FindByFence(fp.Id);
            return home == null ? null : RecipeFor(world, home);
        }

        /// <summary>
        /// Removes a house's fence with the house (HL-13). An unbuilt fence has its site cancelled and materials dropped;
        /// a built one refunds its recipe items by <c>DeconstructRefundShare</c> (rounded down) as a ground pile at the
        /// marker. Does nothing when there is no fence.
        /// </summary>
        public static void Remove(SimWorld world, HomeRecord home)
        {
            if (home == null || home.FenceFootprintId == 0) return;
            int id = home.FenceFootprintId;
            Footprint fp = world.Footprints.Get(id);
            if (fp != null)
            {
                ConstructionSite site = world.Construction.Get(id);
                if (site != null) RemoveModule.ReleaseSite(world, site, fp);
                else
                {
                    ConstructionRecipe recipe = RecipeFor(world, home);
                    var refunds = new SortedDictionary<string, int>(StringComparer.Ordinal);
                    foreach (KeyValuePair<string, int> kv in recipe.Items)
                    {
                        int n = (int)Math.Floor(kv.Value * world.Tuning.DeconstructRefundShare + 1e-9);
                        if (n > 0) refunds[kv.Key] = n;
                    }
                    if (refunds.Count > 0)
                    {
                        LedgerLocation pile = world.Inventory.GetOrAddGroundPile(fp.Centre, null);
                        foreach (KeyValuePair<string, int> kv in refunds) world.Inventory.Produce(pile, Items.Find(kv.Key), kv.Value);
                    }
                }
                world.Footprints.Remove(id);
            }
            world.Homes.SetFence(home, 0);
        }
    }
}
