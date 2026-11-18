using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations.Workshops;

namespace LordsLegacy.Sim.Operations
{
    /// <summary>
    /// ST-42, source 3: workshop output stores at or above <c>YardCollectThreshold</c> full (0 by default, so any free
    /// output item counts), fullest first (ties to the lowest operation id). Only workshops with an input and an output
    /// count (ST-42b), so gathering huts are never collected. Only the output store is read, never the input stock
    /// (PL-30d). Light items go by hand; <see cref="YardRoute.TripFrom"/> returns null with no free item.
    /// </summary>
    public sealed class YardWorkshopSource : IYardJobSource
    {
        /// <inheritdoc />
        public YardTrip NextTrip(SimWorld world, Operation yard)
        {
            if (world == null || yard == null) return null;
            YardTrip best = null;
            double bestFill = -1;
            foreach (Operation op in world.Operations.All)
            {
                if (!WorkshopBuildings.IsWorkshop(OperationTypes.WorkshopOf(op.TypeId, world.Tuning))) continue;
                foreach (int m in op.ModuleIds)
                {
                    if (!world.IsWorking(m)) continue;
                    LedgerLocation store = world.Inventory.FindGoodsStore(m);
                    if (store == null || store.Capacity <= 0) continue;
                    double fill = (double)store.Total / store.Capacity;
                    if (fill < world.Tuning.YardCollectThreshold || fill <= bestFill) continue;
                    YardTrip trip = YardRoute.TripFrom(world, yard, store);
                    if (trip == null) continue;
                    best = trip; bestFill = fill;
                }
            }
            return best;
        }
    }
}
