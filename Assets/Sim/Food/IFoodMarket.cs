using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Navigation;

namespace LordsLegacy.Sim.Food
{
    /// <summary>
    /// A place households draw their weekly food from (FD-11, MKT-05). In M14 only the camp implements it; M15 stall
    /// markets plug in by implementing this and being returned from <see cref="AllotmentSystem"/>'s source.
    /// </summary>
    public interface IFoodMarket
    {
        /// <summary>The ledger locations the allotment is reserved on (the stalls, MK-32). Empty when the market has none.</summary>
        IReadOnlyList<LedgerLocation> Stocks { get; }

        /// <summary>Where walking distance is measured from, in metres.</summary>
        PathPoint Origin { get; }

        /// <summary>Walking range in metres; households further away get nothing (MKT-06).</summary>
        double RangeM { get; }
    }

    /// <summary>The camp as a food market (FD-11, FD-12): only the camp food store feeds it, never granaries or hut stores.</summary>
    public sealed class CampFoodMarket : IFoodMarket
    {
        private readonly SimWorld _world;

        /// <summary>Creates the camp market for a world.</summary>
        public CampFoodMarket(SimWorld world) { _world = world; }

        /// <inheritdoc />
        public IReadOnlyList<LedgerLocation> Stocks =>
            _world.Camp?.FoodStoreLocation == null ? new LedgerLocation[0] : new[] { _world.Camp.FoodStoreLocation };

        /// <inheritdoc />
        public PathPoint Origin => _world.Camp == null ? new PathPoint(0, 0) : new PathPoint(_world.Camp.Fire.XMetres, _world.Camp.Fire.ZMetres);

        /// <inheritdoc />
        public double RangeM => _world.Tuning.MarketRangeM;
    }
}
