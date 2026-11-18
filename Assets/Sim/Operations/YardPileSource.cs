using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;

namespace LordsLegacy.Sim.Operations
{
    /// <summary>
    /// ST-41, source 2: unowned General ground piles (dropped kit, camp leftovers, deconstruction refunds). Among the piles
    /// this yard can take, the one nearest the yard goes first; ties go to the lowest ledger id.
    /// </summary>
    public sealed class YardPileSource : IYardJobSource
    {
        /// <inheritdoc />
        public YardTrip NextTrip(SimWorld world, Operation yard)
        {
            if (world == null || yard == null) return null;
            LedgerLocation dest = YardRoute.StoreOf(world, yard);
            if (dest == null) return null;
            PointMm yardAt = ConstructionSources.StorePosition(world, dest);
            YardTrip best = null;
            double bestD = double.MaxValue;
            foreach (LedgerLocation pile in world.Inventory.Locations)
            {
                if (pile.Kind != LocationKind.GroundPile || pile.OwnerOperationId != null) continue;
                YardTrip trip = YardRoute.TripFrom(world, yard, pile);
                if (trip == null) continue;
                double d = ConstructionSources.StorePosition(world, pile).DistanceTo(yardAt);
                if (d < bestD) { best = trip; bestD = d; }
            }
            return best;
        }
    }
}
