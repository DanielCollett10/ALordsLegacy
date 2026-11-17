using System;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;

namespace LordsLegacy.Sim.Inventory
{
    /// <summary>
    /// Village timber totals for the top bar and panels (T-525, decisions 0046, 0047). Pure queries. Totals include reserved
    /// stock. Logs are what construction can draw on (camp stockpile, built log storage, unowned ground piles, as
    /// <see cref="ConstructionSources"/> counts them); beams and planks are everything in goods stores. A workshop's input
    /// stock, an ox's load, a site store and an operation's own pile are not counted.
    /// </summary>
    public static class GoodsStats
    {
        /// <summary>Logs in construction sources, reserved ones included.</summary>
        public static int Logs(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            long t = 0;
            foreach (var s in ConstructionSources.Sources(world, Items.Log)) t += s.Location.CountOf(Items.Log.Id);
            return (int)Math.Min(t, int.MaxValue);
        }

        /// <summary>Beams in every goods store, reserved ones included.</summary>
        public static int Beams(SimWorld world) => InGoodsStores(world, Items.Beam);

        /// <summary>Planks in every goods store, reserved ones included.</summary>
        public static int Planks(SimWorld world) => InGoodsStores(world, Items.Plank);

        private static int InGoodsStores(SimWorld world, ItemDef item)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            long t = 0;
            foreach (LedgerLocation l in world.Inventory.Locations)
                if (l.Kind == LocationKind.GoodsStore) t += l.CountOf(item.Id);
            return (int)Math.Min(t, int.MaxValue);
        }
    }
}
