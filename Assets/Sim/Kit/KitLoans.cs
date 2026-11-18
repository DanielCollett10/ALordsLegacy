using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Kit
{
    /// <summary>
    /// A carry trip borrows a basket from its building's kit store (decision 0052, MAT-50..54, ST-25): a stock
    /// reservation of one basket owned by the trip's task. The basket never moves in the ledger; the reservation is
    /// released when the task ends, whatever the outcome. State is only the ledger reservation (saved with the ledger).
    /// </summary>
    public static class KitLoans
    {
        private const int OwnerBase = -3_000_000;

        /// <summary>Ledger reservation owner of a task's basket loan (negative, clear of footprint, allotment and stock owners).</summary>
        public static int OwnerFor(int taskId) => OwnerBase - taskId;

        /// <summary>Load multiplier a basket gives (MAT-54).</summary>
        public const int Factor = 2;

        /// <summary>
        /// Reserves one free basket in the kit store of <paramref name="buildingId"/> for the task <paramref name="taskId"/>
        /// (plan time: pass the id the task is about to get). False when the building has no kit store or no free basket.
        /// </summary>
        public static bool TryBorrow(SimWorld world, int buildingId, int taskId)
        {
            LedgerLocation kit = KitStores.Find(world, buildingId);
            if (kit == null) return false;
            return world.Inventory.ReserveStock(kit, Items.Basket, 1, OwnerFor(taskId)) != 0;
        }

        /// <summary>True while the task holds a basket loan.</summary>
        public static bool Holds(SimWorld world, int taskId)
        {
            int owner = OwnerFor(taskId);
            foreach (Reservation r in world.Inventory.Reservations)
                if (r.Owner == owner) return true;
            return false;
        }

        /// <summary>Releases the basket loans of tasks that ended (done, cancelled, failed) or no longer exist.</summary>
        public static void ReleaseFinished(SimWorld world)
        {
            List<int> owners = null;
            foreach (Reservation r in world.Inventory.Reservations)
            {
                if (r.Owner > OwnerBase - 1 || r.Owner <= OwnerBase - 1_000_000) continue;
                TaskRecord t = world.Tasks.Get(OwnerBase - r.Owner);
                if (t != null && !t.IsFinished) continue;
                if (owners == null) owners = new List<int>();
                if (!owners.Contains(r.Owner)) owners.Add(r.Owner);
            }
            if (owners == null) return;
            foreach (int o in owners) world.Inventory.CancelOwner(o);
        }
    }
}
