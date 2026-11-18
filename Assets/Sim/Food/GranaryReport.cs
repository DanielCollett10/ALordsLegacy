using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Inventory;

namespace LordsLegacy.Sim.Food
{
    /// <summary>Where a granary is in its life.</summary>
    public enum GranaryState { Delivering = 0, Building = 1, Built = 2, Deconstructing = 3 }

    /// <summary>Everything the granary panel shows, read from the Sim in one go (T-448). Immutable.</summary>
    public sealed class GranaryReport
    {
        /// <summary>The food items listed in Stock, in this fixed order.</summary>
        public static readonly IReadOnlyList<ItemDef> FoodItems = new[] { Items.Meat, Items.Berries, Items.Mushrooms };

        /// <summary>The granary footprint id.</summary>
        public int FootprintId { get; private set; }
        /// <summary>Delivering, Building, Built or Deconstructing.</summary>
        public GranaryState State { get; private set; }
        /// <summary>True once the site is built (also while being deconstructed).</summary>
        public bool IsBuilt { get; private set; }
        /// <summary>Logs delivered to the site (the full recipe once built).</summary>
        public int LogsDelivered { get; private set; }
        /// <summary>Logs the recipe needs.</summary>
        public int LogsNeeded { get; private set; }
        /// <summary>Build progress 0..100 (100 once built).</summary>
        public int BuildPercent { get; private set; }
        /// <summary>Deconstruction progress 0..100 (0 unless Deconstructing).</summary>
        public int DeconstructPercent { get; private set; }
        /// <summary>Count of every food item (including zeros) in a fixed order: meat, berries, mushrooms.</summary>
        public IReadOnlyList<KeyValuePair<string, int>> Stock { get; private set; }
        /// <summary>Items in the food store.</summary>
        public int Total { get; private set; }
        /// <summary>Items in the food store that are reserved.</summary>
        public int Reserved { get; private set; }
        /// <summary>Food store capacity.</summary>
        public int Capacity { get; private set; }
        /// <summary>Logs a deconstruction returns.</summary>
        public int RefundLogs { get; private set; }
        /// <summary>Food items that would become a ground pile on deconstruction (CN-55).</summary>
        public int DropsItems { get; private set; }

        /// <summary>True when the granary's operation supplies the market (MK-14).</summary>
        public bool SuppliesMarket { get; private set; }
        /// <summary>"Supplies market: Yes" or "Supplies market: No".</summary>
        public string SuppliesMarketText => "Supplies market: " + (SuppliesMarket ? "Yes" : "No");

        /// <summary>Baskets held in the granary's kit store (T-566).</summary>
        public int BasketsHeld { get; private set; }
        /// <summary>Basket slots of a granary (tuning), the "out of" of <see cref="BasketsHeld"/>.</summary>
        public int BasketSlots { get; private set; }
        /// <summary>"Baskets: 1 / 2".</summary>
        public string BasketsText => "Baskets: " + BasketsHeld + " / " + BasketSlots;

        private GranaryReport() { }

        /// <summary>The report for a granary footprint, or null when the id is not a granary.</summary>
        public static GranaryReport Of(SimWorld world, int footprintId)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            Footprint fp = world.Footprints.Get(footprintId);
            if (fp == null || fp.Type.Id != ModuleTypes.GranaryId) return null;

            var r = new GranaryReport { FootprintId = footprintId };
            ConstructionProgress cp = ConstructionProgress.Of(world, footprintId);
            DeconstructionEntry de = world.Deconstruction.Get(footprintId);
            r.IsBuilt = world.IsBuilt(footprintId);
            r.LogsNeeded = fp.Type.ConstructionRecipe.Logs;
            if (cp != null)
            {
                r.State = cp.State == ConstructionState.Delivering ? GranaryState.Delivering : GranaryState.Building;
                r.LogsDelivered = cp.LogsDelivered;
                r.BuildPercent = (int)(cp.Fraction * 100 + 1e-9);
            }
            else
            {
                r.State = GranaryState.Built;
                r.LogsDelivered = r.LogsNeeded;
                r.BuildPercent = 100;
            }
            if (de != null && r.IsBuilt)
            {
                r.State = GranaryState.Deconstructing;
                int total = DeconstructionWork.TotalMinutes(world, fp);
                r.DeconstructPercent = total <= 0 ? 100 : Math.Min(100, de.ProgressMinutes * 100 / total);
            }

            LedgerLocation store = world.Inventory.FindFoodStore(footprintId);
            var stock = new List<KeyValuePair<string, int>>();
            foreach (ItemDef item in FoodItems)
                stock.Add(new KeyValuePair<string, int>(item.Id, store == null ? 0 : store.CountOf(item.Id)));
            r.Stock = stock;
            if (store != null)
            {
                r.Total = store.Total;
                r.Capacity = store.Capacity;
                foreach (KeyValuePair<string, int> kv in store.ReservedCounts) r.Reserved += kv.Value;
            }
            r.BasketsHeld = Kit.KitStores.BasketsHeld(world, footprintId);
            r.BasketSlots = Kit.KitStores.BasketSlots(world, fp.Type);
            r.RefundLogs = DeconstructionWork.RefundLogs(world, fp);
            r.DropsItems = r.Total;
            Operations.Operation granaryOp = world.Operations.FindByModule(footprintId);
            r.SuppliesMarket = granaryOp != null && granaryOp.SuppliesMarket;
            return r;
        }

        /// <summary>Count of one food item in Stock (0 when not listed).</summary>
        public int StockOf(string itemId)
        {
            foreach (KeyValuePair<string, int> kv in Stock) if (kv.Key == itemId) return kv.Value;
            return 0;
        }

        /// <summary>The confirm-dialog text for removing this granary, or null when the id is not a granary.</summary>
        public static string ConfirmText(SimWorld world, int footprintId)
        {
            GranaryReport r = Of(world, footprintId);
            if (r == null) return null;
            if (!r.IsBuilt) return "Cancel this unbuilt granary? The site is removed at once.";
            string text = "Deconstruct this granary?\nReturns " + r.RefundLogs + " logs.";
            if (r.DropsItems > 0) text += "\n" + r.DropsItems + " food items are left on the ground.";
            return text;
        }
    }
}
