using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Calendar;

namespace LordsLegacy.Sim.Food
{
    /// <summary>
    /// Routine-phase system (FD-11, FD-13, FOOD-A6, A8): at Sunday sunrise each household within the market's walking
    /// range is topped up to 7 days of need, nearest first, with food types mixed in proportion to the stock. The food
    /// is only reserved where it lies (on the market's stock, owner <see cref="OwnerFor"/>); the Sunday trip (FD-14)
    /// moves it. Last week's unclaimed reservations are released first. Shortage leaves the far households short.
    /// </summary>
    public sealed class AllotmentSystem : ISimSystem
    {
        /// <summary>Days of need a household is topped up to (FD-13).</summary>
        public const int Days = 7;

        private readonly Func<SimWorld, IEnumerable<IFoodMarket>> _markets;
        private Daylight _daylight;

        /// <summary>Creates the system: the camp until the handover (CMP-20), then only the market stalls (MK-30, MK-31).</summary>
        public AllotmentSystem() : this(Default) { }

        /// <summary>The markets the Sunday allotment draws from: the camp before the handover, the stall markets after.</summary>
        public static IEnumerable<IFoodMarket> Default(SimWorld w)
        {
            var list = new List<IFoodMarket>();
            if (!w.FoodHandedOver) list.Add(new CampFoodMarket(w));
            else foreach (Markets.Market m in w.Markets.All) list.Add(new Markets.StallMarket(w, m));
            return list;
        }

        /// <summary>Creates the system with a custom market source (M15).</summary>
        public AllotmentSystem(Func<SimWorld, IEnumerable<IFoodMarket>> markets) { _markets = markets; }

        /// <summary>Ledger reservation owner for a family's allotment: negative, so it never clashes with footprint or task ids.</summary>
        public static int OwnerFor(int familyId) => -1000 - familyId;

        /// <inheritdoc />
        public void Step(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (!world.Now.IsSunday) return;
            if (_daylight == null) _daylight = new Daylight(world.Tuning.MinDaylightHours, world.Tuning.MaxDaylightHours);
            int dawn = (int)Math.Ceiling(_daylight.For(world.Now.DayOfYear).Sunrise * 60.0);
            if (world.Now.Hour * 60 + world.Now.Minute != dawn) return;
            Run(world, _markets(world));
        }

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result) { }

        /// <summary>Runs one allotment now (what Sunday dawn does). Public for tests and the later trip system.</summary>
        public static void Run(SimWorld world, IEnumerable<IFoodMarket> markets)
        {
            var families = new List<Family>(world.People.Families);
            foreach (Family f in families) { world.Inventory.CancelOwner(OwnerFor(f.Id)); f.ClearAllotment(); }
            var served = new HashSet<int>();
            var full = new HashSet<int>();
            foreach (IFoodMarket m in markets)
            {
                if (m.Stocks == null || m.Stocks.Count == 0) continue;
                var queue = new List<KeyValuePair<double, Family>>();
                foreach (Family f in families)
                {
                    if (served.Contains(f.Id) || EatingSystem.DailyNeed(world, f) <= 0) continue;
                    double d = WalkingDistanceM(world, m.Origin, new PathPoint(f.HomeX, f.HomeZ));
                    if (d >= 0 && d <= m.RangeM) queue.Add(new KeyValuePair<double, Family>(d, f));
                }
                queue.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : a.Value.Id.CompareTo(b.Value.Id));
                foreach (var e in queue)
                {
                    served.Add(e.Value.Id);
                    double deficit = Days * EatingSystem.DailyNeed(world, e.Value) - Held(world, e.Value);
                    double left = deficit;
                    foreach (LedgerLocation stock in FullestFirst(m.Stocks))
                    {
                        if (left <= 1e-9) break;
                        left -= Allot(world, stock, e.Value, left);
                    }
                    if (left <= 1e-9) full.Add(e.Value.Id);
                }
            }
            int ok = 0, short_ = 0;
            foreach (Family f in families)
            {
                if (EatingSystem.DailyNeed(world, f) <= 0) continue;
                if (full.Contains(f.Id)) ok++; else short_++;
            }
            world.LastAllotmentServed = ok;
            world.LastAllotmentShort = short_;
        }

        // FOOD-A8: the stall holding the most rations is drawn on first (ties keep the market's order).
        private static List<LedgerLocation> FullestFirst(IReadOnlyList<LedgerLocation> stocks)
        {
            var keyed = new List<KeyValuePair<double, LedgerLocation>>();
            foreach (LedgerLocation l in stocks)
            {
                if (l == null) continue;
                double r = 0;
                foreach (ItemDef item in Items.All)
                    if (item.Category == ItemCategory.Food) r += l.AvailableOf(item.Id) * item.RationsPerItem;
                keyed.Add(new KeyValuePair<double, LedgerLocation>(r, l));
            }
            var order = new List<int>();
            for (int i = 0; i < keyed.Count; i++) order.Add(i);
            order.Sort((x, y) => keyed[x].Key != keyed[y].Key ? keyed[y].Key.CompareTo(keyed[x].Key) : x.CompareTo(y));
            var result = new List<LedgerLocation>();
            foreach (int i in order) result.Add(keyed[i].Value);
            return result;
        }

        /// <summary>Walking distance in metres along the path, or -1 when there is no route.</summary>
        public static double WalkingDistanceM(SimWorld world, PathPoint from, PathPoint to)
        {
            PathResult r = GridPathfinder.Find(world.Nav, from, to);
            if (!r.Ok) return -1;
            double len = 0;
            for (int i = 1; i < r.Points.Count; i++)
            {
                double dx = r.Points[i].X - r.Points[i - 1].X, dz = r.Points[i].Z - r.Points[i - 1].Z;
                len += Math.Sqrt(dx * dx + dz * dz);
            }
            return len;
        }

        private static double Held(SimWorld world, Family f)
        {
            Homes.HomeRecord home = f.HomeHouseId.HasValue ? world.Homes.Get(f.HomeHouseId.Value) : null;
            LedgerLocation larder = home != null ? Larders.ForHome(world, home) : Larders.ForFamily(world, f);
            double r = f.CarriedRations;
            foreach (ItemDef item in Items.All)
                if (item.Category == ItemCategory.Food) r += larder.CountOf(item.Id) * item.RationsPerItem;
            return r;
        }

        private static double Allot(SimWorld world, LedgerLocation stock, Family f, double deficit)
        {
            var foods = new List<ItemDef>();
            foreach (ItemDef item in Items.All)
                if (item.Category == ItemCategory.Food && item.RationsPerItem > 0 && stock.AvailableOf(item.Id) > 0) foods.Add(item);
            double total = 0;
            foreach (ItemDef i in foods) total += stock.AvailableOf(i.Id) * i.RationsPerItem;
            if (total <= 0) return 0;
            double want = Math.Min(deficit, total);
            var take = new int[foods.Count];
            double got = 0;
            for (int i = 0; i < foods.Count; i++)
            {
                take[i] = (int)Math.Floor(want * stock.AvailableOf(foods[i].Id) / total + 1e-9);
                got += take[i] * foods[i].RationsPerItem;
            }
            while (got < want - 1e-9)
            {
                int best = -1; double bestGap = double.MinValue;
                for (int i = 0; i < foods.Count; i++)
                {
                    if (take[i] >= stock.AvailableOf(foods[i].Id)) continue;
                    double gap = want * stock.AvailableOf(foods[i].Id) * foods[i].RationsPerItem / total - take[i] * foods[i].RationsPerItem;
                    if (gap > bestGap) { bestGap = gap; best = i; }
                }
                if (best < 0) break;
                take[best]++; got += foods[best].RationsPerItem;
            }
            double reserved = 0;
            for (int i = 0; i < foods.Count; i++)
            {
                if (take[i] <= 0) continue;
                int id = world.Inventory.ReserveStock(stock, foods[i], take[i], OwnerFor(f.Id));
                if (id != 0) { f.AddAllotment(id); reserved += take[i] * foods[i].RationsPerItem; }
            }
            return reserved;
        }
    }
}
