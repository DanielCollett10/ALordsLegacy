using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Markets
{
    /// <summary>
    /// Operations-phase system for store families (MK-24, decision 0044). For every built granary that supplies the
    /// market, each idle working member (age <c>ForagerMinAge</c> or more) of an assigned family that has a built stall
    /// gets one open <c>task.stock_stall</c> when the stall has room and the granary has food (MK-24 step 1).
    /// The task walks to the granary, loads for 10 minutes, collects up to <c>min(HandCarryItems, stall room)</c> items in
    /// proportion to the granary stock through ledger reservations (never food reserved for a household allotment,
    /// MK-27), walks to the stall and unloads for 10 minutes (MK-25). Where the food comes from is one method,
    /// <see cref="ChooseStockSource"/>, so later steps (forager huts, the camp) extend it. Planning promises are counted
    /// from the open tasks, so nothing is saved besides the tasks themselves. A sweep drops food left on the carrier of a
    /// task that ended early and frees its reservations.
    /// </summary>
    public sealed class StoreFamilySystem : ISimSystem
    {
        /// <summary>Id of the stock-the-stall task definition.</summary>
        public const string StockStallTaskId = "task.stock_stall";
        /// <summary>Stage id of the load step.</summary>
        public const string LoadStageId = "stage.stock_load";
        /// <summary>Stage id of the unload step.</summary>
        public const string UnloadStageId = "stage.stock_unload";
        /// <summary>Minutes to load or unload one trip (MK-25, PLACEHOLDER).</summary>
        public const int LoadMinutes = 10;

        /// <summary>Ledger reservation owner of a stock task: negative, so it never clashes with footprint or allotment owners.</summary>
        public static int OwnerFor(int taskId) => -1_000_000 - taskId;

        /// <inheritdoc />
        public void Step(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            Sweep(world);
            Market market = world.Markets.Current;
            if (!world.FoodHandedOver && SuppliesBuiltMarket(world, market)) world.FoodHandedOver = true;
            foreach (Operation op in new List<Operation>(world.Operations.All))
            {
                if (op.TypeId != OperationTypes.GranaryId) continue;
                bool supplies = market != null && op.SuppliesMarket;
                if (!supplies) CancelOpen(world, op, true);
                Refill(world, op, supplies ? market : null);
            }
            RemoveEmptyCampStore(world);
        }

        // CMP-20: a built granary that supplies the market triggers the handover at once, with no wait for stall stock.
        private static bool SuppliesBuiltMarket(SimWorld world, Market market)
        {
            if (market == null) return false;
            foreach (Operation op in world.Operations.All)
            {
                if (op.TypeId != OperationTypes.GranaryId || !op.SuppliesMarket) continue;
                foreach (int m in op.ModuleIds) if (world.IsWorking(m)) return true;
            }
            return false;
        }

        // CMP-21: once the camp food store is empty (nothing reserved, no trip still to load from it) it is removed.
        private static void RemoveEmptyCampStore(SimWorld world)
        {
            LedgerLocation camp = world.Camp?.FoodStoreLocation;
            if (!world.FoodHandedOver || camp == null || camp.Total != 0 || PendingCollect(world, camp.Id) != 0) return;
            if (world.Inventory.RemoveEmptyLocation(camp.Id)) world.Camp.FoodStoreLocation = null;
        }

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result)
        {
        }

        /// <summary>
        /// The one place that decides where a stall trip takes food from (MK-24 step 1): the granary's own food store when
        /// it is built and holds unreserved food beyond what open trips have promised. Null when there is none.
        /// <paramref name="available"/> is that spare food. Later tasks extend this for other sources.
        /// </summary>
        public static LedgerLocation ChooseStockSource(SimWorld world, Operation granary, out int available)
        {
            available = 0;
            foreach (int m in granary.ModuleIds)
            {
                if (!world.IsWorking(m)) continue;
                LedgerLocation store = world.Inventory.FindFoodStore(m);
                if (store == null) continue;
                int spare = FreeFood(store) - PendingCollect(world, store.Id);
                if (spare <= 0) continue;
                available = spare;
                return store;
            }
            return null;
        }

        /// <summary>Unreserved food in a store (never counts food reserved for an allotment, MK-27).</summary>
        public static int FreeFood(LedgerLocation store)
        {
            int n = 0;
            foreach (ItemDef item in Items.All) if (item.Category == ItemCategory.Food) n += Math.Max(0, store.AvailableOf(item.Id));
            return n;
        }

        /// <summary>Items open stock tasks have planned to take from a granary store but not yet loaded.</summary>
        public static int PendingCollect(SimWorld world, int granaryLocationId)
        {
            int n = 0;
            foreach (TaskRecord t in world.Tasks.All)
            {
                if (t.IsFinished || t.Def.Id != StockStallTaskId) continue;
                for (int i = t.CurrentStep; i < t.Def.Steps.Count; i++)
                {
                    TaskStep s = t.Def.Steps[i];
                    if (s.Kind == StepKind.CollectStock && s.LocationId == granaryLocationId) n += s.Count;
                }
            }
            return n;
        }

        /// <summary>Items unfinished stock tasks have promised to a stall store but not yet loaded (once loaded, the stall's
        /// room is held by a ledger capacity reservation and counted by <c>FreeSpace</c>).</summary>
        public static int PendingDeposit(SimWorld world, int stallLocationId)
        {
            int n = 0;
            foreach (TaskRecord t in world.Tasks.All)
            {
                if (t.IsFinished || t.Def.Id != StockStallTaskId) continue;
                bool loaded = false;
                for (int i = 0; i < t.CurrentStep && i < t.Def.Steps.Count; i++)
                    if (t.Def.Steps[i].Kind == StepKind.CollectStock) loaded = true;
                if (loaded) continue;
                foreach (TaskStep s in t.Def.Steps)
                    if (s.Kind == StepKind.DepositStock && s.LocationId == stallLocationId) n += s.Count;
            }
            return n;
        }

        /// <summary>
        /// Splits <paramref name="n"/> items over the unreserved food of a store in proportion to its stock (MKT-03d):
        /// each item gets the floor of its share, the rest goes to the largest remainders (ties: catalogue order).
        /// Never more than the store has of an item. Returns (item, count) pairs with count above zero.
        /// </summary>
        public static List<KeyValuePair<ItemDef, int>> Proportions(LedgerLocation store, int n)
        {
            var items = new List<ItemDef>();
            var have = new List<int>();
            int total = 0;
            foreach (ItemDef item in Items.All)
            {
                if (item.Category != ItemCategory.Food) continue;
                int a = Math.Max(0, store.AvailableOf(item.Id));
                if (a == 0) continue;
                items.Add(item); have.Add(a); total += a;
            }
            var result = new List<KeyValuePair<ItemDef, int>>();
            if (total == 0 || n <= 0) return result;
            n = Math.Min(n, total);
            var take = new int[items.Count];
            var rem = new long[items.Count];
            int given = 0;
            for (int i = 0; i < items.Count; i++)
            {
                take[i] = (int)((long)n * have[i] / total);
                rem[i] = (long)n * have[i] % total;
                given += take[i];
            }
            while (given < n)
            {
                int best = -1;
                for (int i = 0; i < items.Count; i++)
                    if (take[i] < have[i] && (best < 0 || rem[i] > rem[best])) best = i;
                take[best]++; rem[best] = -1; given++;
            }
            for (int i = 0; i < items.Count; i++) if (take[i] > 0) result.Add(new KeyValuePair<ItemDef, int>(items[i], take[i]));
            return result;
        }

        // Cancels the op's open stock tasks; with keepCollect, trips that end in the granary itself stay.
        private static void CancelOpen(SimWorld world, Operation op, bool keepCollect = false)
        {
            LedgerLocation own = keepCollect ? GranaryStore(world, op) : null;
            foreach (TaskRecord t in world.Tasks.ByOwner(op.Id))
            {
                if (t.State != TaskState.Open || t.Def.Id != StockStallTaskId) continue;
                if (own != null && IsCollectTrip(t, own.Id)) continue;
                world.Tasks.Cancel(t.Id);
            }
        }

        private static bool IsCollectTrip(TaskRecord t, int granaryLocationId)
        {
            foreach (TaskStep s in t.Def.Steps)
                if (s.Kind == StepKind.DepositStock) return s.LocationId == granaryLocationId;
            return false;
        }

        /// <summary>The first working food store of a granary operation, or null.</summary>
        public static LedgerLocation GranaryStore(SimWorld world, Operation granary)
        {
            foreach (int m in granary.ModuleIds)
            {
                if (!world.IsWorking(m)) continue;
                LedgerLocation store = world.Inventory.FindFoodStore(m);
                if (store != null) return store;
            }
            return null;
        }

        /// <summary>
        /// MK-24 step 2: the forager hut store with the most unreserved food beyond what open trips have promised
        /// (ties: nearest to the granary), at any distance. Null when no hut store has food. <paramref name="available"/>
        /// is that spare food.
        /// </summary>
        public static LedgerLocation ChooseForagerSource(SimWorld world, LedgerLocation granaryStore, out int available)
        {
            available = 0;
            LedgerLocation best = null;
            double bestDist = 0;
            Footprint gfp = world.Footprints.Get(granaryStore.ModuleId);
            foreach (Operation op in world.Operations.All)
            {
                if (op.TypeId != OperationTypes.ForagingId) continue;
                LedgerLocation hut = Foraging.ForagingStores.HutStore(world, op);
                if (hut == null) continue;
                int spare = FreeFood(hut) - PendingCollect(world, hut.Id);
                if (spare <= 0) continue;
                Footprint hfp = world.Footprints.Get(hut.ModuleId);
                double d = gfp == null || hfp == null ? 0 : gfp.Centre.DistanceTo(hfp.Centre);
                if (best == null || spare > available || (spare == available && d < bestDist))
                {
                    best = hut; available = spare; bestDist = d;
                }
            }
            LedgerLocation camp = world.FoodHandedOver ? world.Camp?.FoodStoreLocation : null;
            if (camp != null)
            {
                int spare = FreeFood(camp) - PendingCollect(world, camp.Id);
                // The camp competes like a hut: fullest first; on a tie the nearest wins.
                double d = gfp == null ? 0 : gfp.Centre.DistanceTo(camp.Position);
                if (spare > 0 && (best == null || spare > available || (spare == available && d < bestDist)))
                {
                    best = camp; available = spare;
                }
            }
            return best;
        }

        // A stock task that ended (done, or cancelled from outside) leaves nothing behind: reservations freed, carried food dropped.
        private static void Sweep(SimWorld world)
        {
            foreach (TaskRecord t in new List<TaskRecord>(world.Tasks.All))
            {
                if (!t.IsFinished || t.Def.Id != StockStallTaskId) continue;
                world.Inventory.CancelOwner(OwnerFor(t.Id));
                if (!t.CarrierId.HasValue) continue;
                LedgerLocation carrier = world.Inventory.Get(t.CarrierId.Value);
                if (carrier == null || carrier.Total == 0) continue;
                Footprint at = FirstStep(t, StepKind.DepositStock, world);
                PointMm p = at != null ? at.Centre : default;
                Foraging.ForagingSystem.DropAll(world, carrier, p);
            }
        }

        private static Footprint FirstStep(TaskRecord t, StepKind kind, SimWorld world)
        {
            foreach (TaskStep s in t.Def.Steps)
            {
                if (s.Kind != kind) continue;
                LedgerLocation l = world.Inventory.Get(s.LocationId);
                return l == null ? null : world.Footprints.Get(l.ModuleId);
            }
            return null;
        }

        private static void Refill(SimWorld world, Operation op, Market market)
        {
            bool built = false;
            foreach (int m in op.ModuleIds) if (world.IsWorking(m)) built = true;
            if (!built) { CancelOpen(world, op); return; }
            if (market != null) RefillStalls(world, op, market);
            CollectFromForagers(world, op);
        }

        // MK-24 step 2: idle workers still without a trip fetch food from the fullest forager hut store into the granary.
        private static void CollectFromForagers(SimWorld world, Operation op)
        {
            LedgerLocation granary = GranaryStore(world, op);
            if (granary == null) return;
            int wanted = -OpenTrips(world, op);
            foreach (int fid in op.FamilyIds)
            {
                Family fam = world.People.FindFamily(fid);
                if (fam != null && !fam.IsInPassage) wanted += CountIdleWorkers(world, fam);
            }
            for (int i = 0; i < wanted; i++)
            {
                int room = granary.FreeSpace - PendingDeposit(world, granary.Id);
                if (room <= 0) break;
                LedgerLocation source = ChooseForagerSource(world, granary, out int available);
                if (source == null) break;
                int n = Math.Min(Math.Min(Math.Max(1, world.Tuning.HandCarryItems), room), available);
                world.Tasks.Create(BuildDef(world, source, granary, n), op.Id);
            }
        }

        private static int OpenTrips(SimWorld world, Operation op)
        {
            int n = 0;
            foreach (TaskRecord t in world.Tasks.ByOwner(op.Id))
                if (t.State == TaskState.Open && t.Def.Id == StockStallTaskId) n++;
            return n;
        }

        private static void RefillStalls(SimWorld world, Operation op, Market market)
        {
            foreach (int fid in op.FamilyIds)
            {
                Family fam = world.People.FindFamily(fid);
                int stall = market.StallOf(fid);
                if (fam == null || fam.IsInPassage || stall == 0 || !world.IsWorking(stall)) continue;
                LedgerLocation stallStore = world.Inventory.FindFoodStore(stall);
                if (stallStore == null) continue;
                int wanted = CountIdleWorkers(world, fam) - CountOpen(world, op, fam);
                for (int i = 0; i < wanted; i++)
                {
                    int room = stallStore.FreeSpace - PendingDeposit(world, stallStore.Id);
                    if (room <= 0) break;
                    LedgerLocation source = ChooseStockSource(world, op, out int available);
                    if (source == null) break;
                    int n = Math.Min(Math.Min(Math.Max(1, world.Tuning.HandCarryItems), room), available);
                    world.Tasks.Create(BuildDef(world, source, stallStore, n), op.Id);
                }
            }
        }

        // Open stock tasks of this family's stall: one per idle worker, no more.
        private static int CountOpen(SimWorld world, Operation op, Family fam)
        {
            int stall = world.Markets.Current.StallOf(fam.Id);
            LedgerLocation store = stall == 0 ? null : world.Inventory.FindFoodStore(stall);
            if (store == null) return 0;
            int n = 0;
            foreach (TaskRecord t in world.Tasks.ByOwner(op.Id))
            {
                if (t.State != TaskState.Open || t.Def.Id != StockStallTaskId) continue;
                foreach (TaskStep s in t.Def.Steps)
                    if (s.Kind == StepKind.DepositStock && s.LocationId == store.Id) { n++; break; }
            }
            return n;
        }

        private static TaskDef BuildDef(SimWorld world, LedgerLocation source, LedgerLocation stallStore, int n)
        {
            Footprint granary = world.Footprints.Get(source.ModuleId);
            Footprint stall = world.Footprints.Get(stallStore.ModuleId);
            // The camp food store has no footprint: walk to its spot.
            PathPoint from = granary != null ? new PathPoint(granary.AccessPoint.XMetres, granary.AccessPoint.ZMetres)
                : new PathPoint(source.Position.XMetres, source.Position.ZMetres);
            var steps = new List<TaskStep>
            {
                TaskStep.WalkTo(from),
                TaskStep.Work(LoadMinutes, false, LoadStageId),
                TaskStep.CollectStock(source.Id, n),
                TaskStep.WalkTo(new PathPoint(stall.AccessPoint.XMetres, stall.AccessPoint.ZMetres)),
                TaskStep.Work(LoadMinutes, false, UnloadStageId),
                TaskStep.DepositStock(stallStore.Id, n)
            };
            return new TaskDef(StockStallTaskId, steps, EligibilityPolicy.AssignedFamilyOnly);
        }

        private static int CountIdleWorkers(SimWorld world, Family fam)
        {
            int n = 0;
            foreach (int cid in fam.MemberIds)
            {
                Citizen c = world.People.FindCitizen(cid);
                if (c == null || c.AgeYears(world.Tick, world.Tuning.AgeingRate) < world.Tuning.ForagerMinAge) continue;
                if (DispatchSystem.IsIdle(world, c)) n++;
            }
            return n;
        }
    }
}
