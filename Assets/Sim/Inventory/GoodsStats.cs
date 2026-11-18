using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;

namespace LordsLegacy.Sim.Inventory
{
    /// <summary>
    /// Village timber totals for the top bar and panels (T-525, decisions 0046, 0047). Pure queries. Totals include reserved
    /// stock. Logs are what construction can draw on (camp stockpile, built log storage, unowned ground piles, as
    /// <see cref="ConstructionSources"/> counts them); beams and planks are everything in goods stores. A workshop's input
    /// stock, an ox's load, a site store and an operation's own pile are not counted. Storage yards are goods stores, so
    /// their beams and planks are in the totals (T-541, ST-07).
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

        /// <summary>Beams in every goods store (workshop and storage yard), reserved ones included.</summary>
        public static int Beams(SimWorld world) => OfItem(world, Items.Beam);

        /// <summary>Planks in every goods store, reserved ones included.</summary>
        public static int Planks(SimWorld world) => OfItem(world, Items.Plank);

        /// <summary>Hazel and willow rods in every goods store (coppice huts, yards), reserved ones included (T-566).</summary>
        public static int Rods(SimWorld world) => OfItem(world, Items.HazelRod) + OfItem(world, Items.WillowRod);

        /// <summary>Baskets in every goods store, kit stores of forager huts and granaries included, reserved ones included (T-566).</summary>
        public static int Baskets(SimWorld world) => OfItem(world, Items.Basket);

        /// <summary>Wattle panels in every goods store, reserved ones included (T-566).</summary>
        public static int Panels(SimWorld world) => OfItem(world, Items.WattlePanel);

        /// <summary>One General item in every goods store (workshop and storage yard), reserved ones included (T-547).</summary>
        public static int OfItem(SimWorld world, ItemDef item)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (item == null) throw new ArgumentNullException(nameof(item));
            long t = 0;
            foreach (LedgerLocation l in world.Inventory.Locations)
                if (l.Kind == LocationKind.GoodsStore) t += l.CountOf(item.Id);
            return (int)Math.Min(t, int.MaxValue);
        }

        /// <summary>Every General item in catalogue order with its village total (zeros included), as (item id, count) pairs (T-547).</summary>
        public static IReadOnlyList<KeyValuePair<string, int>> GeneralByItem(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var list = new List<KeyValuePair<string, int>>();
            foreach (ItemDef item in Items.All)
                if (item.Category == ItemCategory.General) list.Add(new KeyValuePair<string, int>(item.Id, OfItem(world, item)));
            return list;
        }
    }
}
