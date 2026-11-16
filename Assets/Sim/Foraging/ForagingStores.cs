using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Foraging
{
    /// <summary>
    /// Where a forager may deliver (decision 0044, FD-23b, FD-24): only the operation's own forager hut store. Granaries
    /// and the camp food store (FD-12) are never targets; store families move the food on later. Pure queries; nothing
    /// is stored. Room is the store's free space minus what unfinished forage tasks have already promised it.
    /// </summary>
    public static class ForagingStores
    {
        /// <summary>The one food store a forager of the operation may deliver to: its built hut's store, or null.</summary>
        public static LedgerLocation HutStore(SimWorld world, Operation op)
        {
            Footprint hut = OperationStatus.Hut(world, op);
            return hut == null ? null : world.Inventory.FindFoodStore(hut.Id);
        }

        /// <summary>Items that unfinished forage tasks have promised to a store but not yet put in it.</summary>
        public static int Pending(SimWorld world, int storeLocationId)
        {
            int n = 0;
            foreach (TaskRecord t in world.Tasks.All)
            {
                if (t.IsFinished || t.Def.Id != ForagingSystem.ForageTaskId) continue;
                foreach (TaskStep s in t.Def.Steps)
                    if (s.Kind == StepKind.DepositForage && s.LocationId == storeLocationId) n += s.Count;
            }
            return n;
        }

        /// <summary>True when the hut store is physically full (FD-23b: Storage full). False when there is no hut store.</summary>
        public static bool AllFull(SimWorld world, Operation op)
        {
            LedgerLocation store = HutStore(world, op);
            return store != null && store.FreeSpace <= 0;
        }

        /// <summary>
        /// The hut store if it has room left after pending deliveries; null when it has none or there is no hut store.
        /// <paramref name="room"/> is that room, or 0 when null.
        /// </summary>
        public static LedgerLocation HutStoreWithRoom(SimWorld world, Operation op, out int room)
        {
            room = 0;
            LedgerLocation store = HutStore(world, op);
            if (store == null) return null;
            int r = store.FreeSpace - Pending(world, store.Id);
            if (r <= 0) return null;
            room = r;
            return store;
        }
    }
}
