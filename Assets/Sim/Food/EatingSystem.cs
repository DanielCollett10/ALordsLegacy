using System;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Food
{
    /// <summary>
    /// Routine-phase system (FD-15, FD-16, FOOD-A9): at each midnight every household eats its need from its larder
    /// (the house larder when housed, else the family's camp larder). Whole items are used up first; the unused part of
    /// the last item is carried to the next day. A household that cannot cover its need is Hungry for the day. Hungry
    /// changes nothing else: no system reads it for work.
    /// </summary>
    public sealed class EatingSystem : ISimSystem
    {
        /// <inheritdoc />
        public void Step(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (world.Tick <= 0 || world.Now.Hour != 0 || world.Now.Minute != 0) return;
            foreach (Family f in world.People.Families) Eat(world, f);
        }

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result) { }

        /// <summary>Rations the family eats per day: adults (14+) AdultRations, children ChildRations. Pure.</summary>
        public static double DailyNeed(SimWorld world, Family family)
        {
            double need = 0;
            foreach (int id in family.MemberIds)
            {
                Citizen c = world.People.FindCitizen(id);
                if (c == null) continue;
                need += c.AgeYears(world.Tick, world.Tuning.AgeingRate) >= OperationStatus.WorkingAgeYears
                    ? world.Tuning.AdultRations : world.Tuning.ChildRations;
            }
            return need;
        }

        private static void Eat(SimWorld world, Family f)
        {
            double need = DailyNeed(world, f);
            if (need <= 0) { f.IsHungry = false; f.HungryDays = 0; return; }
            Homes.HomeRecord home = f.HomeHouseId.HasValue ? world.Homes.Get(f.HomeHouseId.Value) : null;
            LedgerLocation larder = home != null ? Larders.ForHome(world, home) : Larders.ForFamily(world, f);
            double carried = f.CarriedRations;
            double left = need;
            const double eps = 1e-9;
            while (left > eps)
            {
                if (carried > eps)
                {
                    double take = Math.Min(carried, left);
                    carried -= take; left -= take;
                    continue;
                }
                ItemDef next = null;
                foreach (ItemDef item in Items.All)
                    if (item.Category == ItemCategory.Food && item.RationsPerItem > 0 && larder.AvailableOf(item.Id) > 0) { next = item; break; }
                if (next == null || !world.Inventory.Consume(larder, next, 1)) break;
                carried = next.RationsPerItem;
            }
            if (carried < eps) carried = 0;
            f.CarriedRations = carried;
            f.IsHungry = left > eps;
            f.HungryDays = f.IsHungry ? f.HungryDays + 1 : 0;
            if (f.IsHungry) world.LastHungryTick = world.Tick;
        }
    }
}
