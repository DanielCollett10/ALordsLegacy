using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Inventory;

namespace LordsLegacy.Sim.Operations
{
    /// <summary>
    /// ST-40, source 1: the camp General store. While it holds free General goods (no beams), the yard families empty it
    /// into the nearest yard with room. A full yard leaves the rest where it is.
    /// </summary>
    public sealed class YardCampStoreSource : IYardJobSource
    {
        /// <inheritdoc />
        public YardTrip NextTrip(SimWorld world, Operation yard)
        {
            if (world == null || yard == null) return null;
            LedgerLocation camp = world.Camp?.GeneralStoreLocation;
            return camp == null ? null : YardRoute.TripFrom(world, yard, camp);
        }
    }
}
