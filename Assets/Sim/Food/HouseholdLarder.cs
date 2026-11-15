using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Homes;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Food
{
    /// <summary>
    /// Read-only larder numbers for one household, for the house panel (FD-10, FD-16, HM-21). The larder is the house
    /// larder when the family is housed, else its camp larder. Queries never create a larder and never change state.
    /// </summary>
    public static class HouseholdLarder
    {
        /// <summary>
        /// The larder the household eats from now, or null when it has none yet. Pure: unlike <see cref="Larders"/>
        /// it does not create a location.
        /// </summary>
        public static LedgerLocation LarderOf(SimWorld world, Family family)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (family == null) throw new ArgumentNullException(nameof(family));
            HomeRecord home = family.HomeHouseId.HasValue ? world.Homes.Get(family.HomeHouseId.Value) : null;
            int id = home != null ? home.LarderLocationId : family.LarderLocationId;
            return id == 0 ? null : world.Inventory.Get(id);
        }

        /// <summary>Items held in the household's larder per food type, in catalog order. Zero counts are included.</summary>
        public static IReadOnlyList<FoodStock> CountsOf(SimWorld world, Family family)
        {
            var list = new List<FoodStock>();
            LedgerLocation larder = LarderOf(world, family);
            foreach (ItemDef item in Items.All)
                if (item.Category == ItemCategory.Food)
                    list.Add(new FoodStock(item, larder == null ? 0 : larder.CountOf(item.Id)));
            return list;
        }

        /// <summary>Rations the household holds: larder food × rations per item, plus the part of an item already opened.</summary>
        public static double Rations(SimWorld world, Family family)
        {
            double t = family.CarriedRations;
            foreach (FoodStock s in CountsOf(world, family)) t += s.Rations;
            return t;
        }

        /// <summary>
        /// Days the larder lasts at the household's current need: <see cref="Rations"/> ÷ <see cref="EatingSystem.DailyNeed"/>.
        /// Infinity when nobody in the household eats.
        /// </summary>
        public static double DaysLeft(SimWorld world, Family family)
        {
            double need = EatingSystem.DailyNeed(world, family);
            return need <= 0 ? double.PositiveInfinity : Rations(world, family) / need;
        }
    }
}
