using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Navigation;

namespace LordsLegacy.Sim.Markets
{
    /// <summary>
    /// The market's stalls as a food market (MK-31, MK-32, MK-34). Stock is the food on every built stall; granary stock
    /// never counts. Range is <c>SimTuning.MarketRangeM</c> walking from the area's centre point.
    /// </summary>
    public sealed class StallMarket : IFoodMarket
    {
        private readonly SimWorld _world;
        private readonly Market _market;

        /// <summary>Creates the stall market view of a market.</summary>
        public StallMarket(SimWorld world, Market market) { _world = world; _market = market; }

        /// <inheritdoc />
        public IReadOnlyList<LedgerLocation> Stocks
        {
            get
            {
                var list = new List<LedgerLocation>();
                foreach (int id in _market.StallFootprintIds)
                {
                    if (!_world.IsBuilt(id)) continue;
                    LedgerLocation l = _world.Inventory.FindFoodStore(id);
                    if (l != null) list.Add(l);
                }
                return list;
            }
        }

        /// <inheritdoc />
        public PathPoint Origin
        {
            get
            {
                var c = MarketRange.CentreOf(_world, _market);
                return new PathPoint(c.XMetres, c.ZMetres);
            }
        }

        /// <inheritdoc />
        public double RangeM => _world.Tuning.MarketRangeM;
    }
}
