using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Markets
{
    /// <summary>One stall row of the market panel (MK-40).</summary>
    public sealed class StallLine
    {
        /// <summary>The stall footprint id.</summary>
        public int FootprintId { get; internal set; }
        /// <summary>Staffing family id, or 0 when unstaffed.</summary>
        public int FamilyId { get; internal set; }
        /// <summary>Family surname, or "Unstaffed".</summary>
        public string FamilyText { get; internal set; }
        /// <summary>True once the stall is built.</summary>
        public bool IsBuilt { get; internal set; }
        /// <summary>Food items on the stall.</summary>
        public int Items { get; internal set; }
        /// <summary>True when Deconstruct is offered now (MK-23b).</summary>
        public bool CanDeconstruct { get; internal set; }
        /// <summary>Why Deconstruct is refused; empty when it can be done.</summary>
        public string DeconstructReason { get; internal set; }
    }

    /// <summary>Everything the market panel shows, read from the Sim in one go (T-469, MK-40). Immutable.</summary>
    public sealed class MarketReport
    {
        /// <summary>The market id.</summary>
        public int MarketId { get; private set; }
        /// <summary>"Market id".</summary>
        public string Name { get; private set; }
        /// <summary>Food on built stalls by type: meat, berries, mushrooms (zeros included).</summary>
        public IReadOnlyList<KeyValuePair<string, int>> Stock { get; private set; }
        /// <summary>Food on built stalls in total.</summary>
        public int TotalFood { get; private set; }
        /// <summary>Stalls that are built.</summary>
        public int StallsBuilt { get; private set; }
        /// <summary>Stall slots the market area has.</summary>
        public int Slots { get; private set; }
        /// <summary>One line per stall (built or planned), in creation order.</summary>
        public IReadOnlyList<StallLine> Stalls { get; private set; }
        /// <summary>Footprint ids of the built granaries that supply this market.</summary>
        public IReadOnlyList<int> SupplyingGranaryIds { get; private set; }
        /// <summary>Households served at the last Sunday allotment.</summary>
        public int ServedLastSunday { get; private set; }
        /// <summary>Households left short at the last Sunday allotment.</summary>
        public int ShortLastSunday { get; private set; }

        private MarketReport() { }

        /// <summary>The report for a market, or null when the id is unknown.</summary>
        public static MarketReport Of(SimWorld world, int marketId)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            Market m = world.Markets.Get(marketId);
            if (m == null) return null;
            var r = new MarketReport { MarketId = marketId, Name = "Market " + marketId };

            var counts = new int[GranaryReport.FoodItems.Count];
            var lines = new List<StallLine>();
            foreach (int id in m.StallFootprintIds)
            {
                bool built = world.IsBuilt(id);
                LedgerLocation store = world.Inventory.FindFoodStore(id);
                int family = m.FamilyOf(id);
                Family f = family == 0 ? null : world.People.FindFamily(family);
                CommandResult can = new DeconstructStall(id).Validate(world);
                lines.Add(new StallLine
                {
                    FootprintId = id,
                    FamilyId = family,
                    FamilyText = family == 0 ? "Unstaffed" : (f != null ? f.Surname : "Family " + family),
                    IsBuilt = built,
                    Items = store == null ? 0 : store.Total,
                    CanDeconstruct = can.IsAccepted,
                    DeconstructReason = can.IsAccepted ? "" : can.Text,
                });
                if (!built || store == null) continue;
                r.StallsBuilt++;
                r.TotalFood += store.Total;
                for (int i = 0; i < counts.Length; i++) counts[i] += store.CountOf(GranaryReport.FoodItems[i].Id);
            }
            var stock = new List<KeyValuePair<string, int>>();
            for (int i = 0; i < counts.Length; i++) stock.Add(new KeyValuePair<string, int>(GranaryReport.FoodItems[i].Id, counts[i]));
            r.Stock = stock;
            r.Stalls = lines;

            Areas.AreaRecord area = world.Areas.Get(m.AreaId);
            r.Slots = area == null ? 0 : Geometry.StallGrid.Slots(area.Polygon, world.Tuning).Count;

            var granaries = new List<int>();
            foreach (Operation op in world.Operations.All)
            {
                if (op.TypeId != OperationTypes.GranaryId || !op.SuppliesMarket) continue;
                foreach (int mod in op.ModuleIds) if (world.IsBuilt(mod)) { granaries.Add(mod); break; }
            }
            r.SupplyingGranaryIds = granaries;
            r.ServedLastSunday = world.LastAllotmentServed;
            r.ShortLastSunday = world.LastAllotmentShort;
            return r;
        }

        /// <summary>Count of one food item in Stock (0 when not listed).</summary>
        public int StockOf(string itemId)
        {
            foreach (KeyValuePair<string, int> kv in Stock) if (kv.Key == itemId) return kv.Value;
            return 0;
        }
    }
}
