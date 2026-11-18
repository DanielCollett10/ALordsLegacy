using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;

namespace LordsLegacy.Sim.Operations
{
    /// <summary>
    /// Rules the yard job sources share (ST-09, ST-40, ST-41): which yard a load goes to, and which General item a trip
    /// carries. Beams are never carried by hand (ST-43, st-5). Reserved stock is never offered (ST-40, ST-41).
    /// </summary>
    internal static class YardRoute
    {
        /// <summary>The yard's General store (ST-03), or null when the yard has no working module.</summary>
        public static LedgerLocation StoreOf(SimWorld world, Operation yard)
        {
            foreach (int m in yard.ModuleIds)
                if (world.IsWorking(m)) return world.Inventory.FindGoodsStore(m);
            return null;
        }

        /// <summary>
        /// The nearest working yard that has room for at least one more item (ST-09), measured from <paramref name="from"/>;
        /// ties go to the lowest operation id. Null when every yard is full or none is working.
        /// </summary>
        public static Operation NearestYardWithRoom(SimWorld world, PointMm from)
        {
            Operation best = null;
            double bestD = double.MaxValue;
            foreach (Operation op in world.Operations.All)
            {
                if (op.TypeId != OperationTypes.StorageYardOpId) continue;
                LedgerLocation store = StoreOf(world, op);
                if (store == null || store.FreeSpace <= 0) continue;
                double d = ConstructionSources.StorePosition(world, store).DistanceTo(from);
                if (d < bestD) { best = op; bestD = d; }
            }
            return best;
        }

        /// <summary>
        /// The trip that empties <paramref name="from"/> into <paramref name="yard"/>, or null when this yard is not the
        /// nearest one with room, the yard has no store, or nothing hand-carriable is free at the source.
        /// </summary>
        public static YardTrip TripFrom(SimWorld world, Operation yard, LedgerLocation from)
        {
            LedgerLocation dest = StoreOf(world, yard);
            if (dest == null || from == null) return null;
            Operation nearest = NearestYardWithRoom(world, ConstructionSources.StorePosition(world, from));
            if (nearest == null || nearest.Id != yard.Id) return null;
            ItemDef item = FirstHandCarriable(world, from);
            return item == null ? null : new YardTrip(from, dest, item, from.AvailableOf(item.Id));
        }

        /// <summary>
        /// The first General item, in catalogue order, that is free (unreserved) at a location and has a hand load.
        /// Beams have none, so they are skipped (st-5). Null when nothing qualifies.
        /// </summary>
        public static ItemDef FirstHandCarriable(SimWorld world, LedgerLocation at)
        {
            foreach (ItemDef item in Items.All)
            {
                if (item.Id == Items.Beam.Id) continue;
                if (HandLoads.For(item, world.Tuning) == null || at.AvailableOf(item.Id) <= 0) continue;
                return item;
            }
            return null;
        }
    }
}
