using System;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Homes;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Food
{
    /// <summary>
    /// Larders (FD-10, FOOD-A5): every house and every family has a food-only ledger location. A family's own larder
    /// is its camp larder while Homeless; moving into a house moves the food into the house larder, and an
    /// eviction moves it back. Larders are created on first need, so a family added by any route gets one.
    /// </summary>
    public static class Larders
    {
        /// <summary>The family's larder, creating it on first call.</summary>
        public static LedgerLocation ForFamily(SimWorld world, Family family)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (family == null) throw new ArgumentNullException(nameof(family));
            LedgerLocation l = world.Inventory.Get(family.LarderLocationId);
            if (l != null) return l;
            l = world.Inventory.AddFoodStore();
            family.LarderLocationId = l.Id;
            return l;
        }

        /// <summary>The house's larder, creating it on first call.</summary>
        public static LedgerLocation ForHome(SimWorld world, HomeRecord home)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (home == null) throw new ArgumentNullException(nameof(home));
            LedgerLocation l = world.Inventory.Get(home.LarderLocationId);
            if (l != null) return l;
            l = world.Inventory.AddFoodStore();
            home.LarderLocationId = l.Id;
            return l;
        }

        /// <summary>A family moved into a house: its camp larder food goes to the house larder.</summary>
        public static void OnMoveIn(SimWorld world, HomeRecord home, Family family) =>
            MoveAll(world, ForFamily(world, family), ForHome(world, home));

        /// <summary>A family left a house: the house larder food goes back to the family's camp larder.</summary>
        public static void OnEvict(SimWorld world, HomeRecord home, Family family) =>
            MoveAll(world, ForHome(world, home), ForFamily(world, family));

        private static void MoveAll(SimWorld world, LedgerLocation from, LedgerLocation to)
        {
            foreach (ItemDef item in Items.All)
            {
                if (item.Category != ItemCategory.Food) continue;
                int n = from.AvailableOf(item.Id);
                if (n > 0) world.Inventory.Transfer(from, to, item, n);
            }
        }
    }
}
