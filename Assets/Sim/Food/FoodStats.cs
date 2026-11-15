using System;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Food
{
    /// <summary>Village food numbers for the top bar (FD-04). Pure queries; no state.</summary>
    public static class FoodStats
    {
        /// <summary>Rations held in every location, food items × their rations per item.</summary>
        public static int TotalRations(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            long t = 0;
            foreach (ItemDef item in Items.All)
                if (item.Category == ItemCategory.Food) t += (long)world.Inventory.TotalOf(item) * item.RationsPerItem;
            return (int)Math.Min(t, int.MaxValue);
        }

        /// <summary>Raw counts per food type across every location, in catalog order (FD-04 tooltip). Includes zero counts.</summary>
        public static System.Collections.Generic.IReadOnlyList<FoodStock> CountsByType(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var list = new System.Collections.Generic.List<FoodStock>();
            foreach (ItemDef item in Items.All)
                if (item.Category == ItemCategory.Food) list.Add(new FoodStock(item, world.Inventory.TotalOf(item)));
            return list;
        }

        /// <summary>Rations the village eats per day: adults (14+) AdultRations each, children ChildRations each.</summary>
        public static double DailyNeed(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            double need = 0;
            foreach (Citizen c in world.People.Citizens)
                need += c.AgeYears(world.Tick, world.Tuning.AgeingRate) >= OperationStatus.WorkingAgeYears
                    ? world.Tuning.AdultRations : world.Tuning.ChildRations;
            return need;
        }

        /// <summary>Days of food left: <see cref="TotalRations"/> ÷ <see cref="DailyNeed"/>. Infinity when nobody eats.</summary>
        public static double DaysLeft(SimWorld world)
        {
            double need = DailyNeed(world);
            return need <= 0 ? double.PositiveInfinity : TotalRations(world) / need;
        }
    }
}
