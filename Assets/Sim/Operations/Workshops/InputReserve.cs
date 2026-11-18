using System;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Operations.Workshops
{
    /// <summary>
    /// The workshop input reserve (T-537, PL-30..PL-33): a workshop opens a fetch only while the total of its input in
    /// storage, minus every fetch of that item already on the way, minus the load, stays at or above the reserve.
    /// Storage means (PL-31) logs in <see cref="LocationKind.Storage"/> and <see cref="LocationKind.Stockpile"/> and beams
    /// in workshop and storage-yard <see cref="LocationKind.GoodsStore"/>s, reserved or not. Ground piles, workshop input
    /// stock, sites and carried items are never counted (the input stock is committed, PL-30d).
    /// </summary>
    public static class InputReserve
    {
        /// <summary>The reserve of a workshop's input; 0 means none. The player's setting (PL-30b), else the tuning default.</summary>
        public static int ReserveOf(SimWorld world, Operation op)
        {
            WorkshopDef def = OperationTypes.WorkshopOf(op.TypeId, world.Tuning);
            return def == null ? Math.Max(0, world.Tuning.WorkshopDefaultReserve) : ReserveOf(world, op, def.Input);
        }

        /// <summary>The reserve of <paramref name="op"/> for one input item.</summary>
        public static int ReserveOf(SimWorld world, Operation op, ItemDef item)
        {
            int v;
            return Math.Max(0, op.Reserves.TryGetValue(item.Id, out v) ? v : world.Tuning.WorkshopDefaultReserve);
        }

        /// <summary>Items of <paramref name="item"/> in counted storage (PL-31).</summary>
        public static int InStorage(SimWorld world, ItemDef item)
        {
            int total = 0;
            foreach (LedgerLocation l in world.Inventory.Locations)
                if (Counts(world, l, item)) total += l.CountOf(item.Id);
            return total;
        }

        /// <summary>Items of <paramref name="item"/> that unfinished fetch tasks have reserved in counted storage (PL-30c).</summary>
        public static int OnTheWay(SimWorld world, ItemDef item)
        {
            int n = 0;
            foreach (Reservation r in world.Inventory.Reservations)
            {
                if (r.Kind != ReservationKind.Stock || r.ItemId != item.Id) continue;
                TaskRecord t = world.Tasks.Get(r.Owner);
                if (t == null || t.IsFinished || t.Def.Id != WorkshopSystem.FetchTaskId) continue;
                LedgerLocation l = world.Inventory.Get(r.LocationId);
                if (l != null && Counts(world, l, item)) n += r.Count;
            }
            return n;
        }

        /// <summary>Storage total minus the fetches on the way: what is left above the reserve is what may still be fetched.</summary>
        public static int Free(SimWorld world, ItemDef item) => InStorage(world, item) - OnTheWay(world, item);

        /// <summary>True when one more fetch of <paramref name="load"/> items keeps storage at or above the reserve.</summary>
        public static bool Allows(SimWorld world, Operation op, ItemDef item, int load)
        {
            int reserve = ReserveOf(world, op);
            if (reserve <= 0) return true;
            return Free(world, item) - Math.Max(1, load) >= reserve;
        }

        private static bool Counts(SimWorld world, LedgerLocation l, ItemDef item)
        {
            if (item.Id == Items.Log.Id) return l.Kind == LocationKind.Storage || l.Kind == LocationKind.Stockpile;
            if (item.Id == Items.Beam.Id)
            {
                if (l.Kind != LocationKind.GoodsStore) return false;
                Footprint f = world.Footprints.Get(l.ModuleId);
                return f != null && WorkshopBuildings.HoldsGoods(f.Type);
            }
            return false;
        }
    }
}
