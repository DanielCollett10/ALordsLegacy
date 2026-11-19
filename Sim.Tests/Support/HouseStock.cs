using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;

namespace LordsLegacy.Sim.Tests.Support
{
    /// <summary>Test helper: stock for houses now that a house needs logs, thatch and panels (T-577, MAT-40).</summary>
    public static class HouseStock
    {
        /// <summary>Puts <paramref name="logs"/> logs plus as much thatch and as many panels in a new stockpile at <paramref name="at"/>.</summary>
        public static LedgerLocation Add(SimWorld w, PointMm at, int logs)
        {
            LedgerLocation pile = w.Inventory.AddStockpile(at);
            w.Inventory.Produce(pile, Items.Log, logs);
            LedgerLocation goods = w.Inventory.AddGoodsStore(at); // General items come only from goods stores
            w.Inventory.Produce(goods, Items.Thatch, logs);
            w.Inventory.Produce(goods, Items.WattlePanel, logs);
            return pile;
        }
    }
}
