using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Operations.Workshops
{
    /// <summary>
    /// Operations-phase system shared by every workshop (T-523, decision 0047, HW-03, HW-06). For each built, unpaused
    /// workshop with a working-age family member, whenever its input stock has room for an ox load and a source holds
    /// that much free input (<see cref="InputSources"/>), it takes a free ox from the <see cref="OxPool"/> and opens a
    /// <see cref="FetchTaskId"/> task: FetchOx, LeadTo source, LoadOx, LeadTo workshop, UnloadOx into its stock, ReturnOx.
    /// The task reserves the source stock and the stock room, so nothing is double-booked. It runs after
    /// <see cref="ConstructionSystem"/>, so construction deliveries claim free oxen first (HW-06). Sunset wind-down is the
    /// step runner's: an unfinished fetch leaves its load as a workshop-owned pile where the ox stands.
    /// </summary>
    public sealed class WorkshopSystem : ISimSystem
    {
        /// <summary>Id of the fetch-input task definition.</summary>
        public const string FetchTaskId = "task.fetch_input";

        private readonly InputSources _sources = InputSources.CreateDefault();

        /// <inheritdoc />
        public void Step(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            Sweep(world);
            foreach (Operation op in new List<Operation>(world.Operations.All))
            {
                WorkshopDef def = OperationTypes.WorkshopOf(op.TypeId, world.Tuning);
                if (def == null) continue;
                Footprint fp = HutOf(world, op);
                if (fp == null || op.Paused || !world.IsWorking(fp.Id) || !HasWorker(world, op)) continue;
                List<LedgerLocation> stocks = StocksOf(world, fp, def);
                if (stocks == null) continue;
                // A def with no inputs never fetches: it only opens work while the store has room (decision 0051).
                if (def.Inputs.Count > 0) AttachOxen(world, op, fp);
                OpenWork(world, op, def, fp, stocks);
                if (def.Inputs.Count > 0) OpenTasks(world, op, def, fp, stocks);
                if (def.Inputs.Count > 0) OpenHandTasks(world, op, def, fp, stocks);
            }
        }

        // Hand-carried inputs (decision 0051, MAT-23): the workshop's own family fetches up to the item's hand load with the
        // shared task.haul_by_hand, one open trip per working family. The reserve counts the trip's n items as taken.
        private void OpenHandTasks(SimWorld world, Operation op, WorkshopDef def, Footprint fp, IList<LedgerLocation> stocks)
        {
            int open = 0;
            foreach (TaskRecord t in world.Tasks.ByOwner(op.Id))
                if (t.Def.Id == HandHaulTaskDefs.HaulId && !t.IsFinished) open++;
            int families = WorkingFamilies(world, op);
            for (int i = 0; i < def.Inputs.Count && open < families; i++)
            {
                RecipeInput input = def.Inputs[i];
                if (input.Carry != CarryClass.Hand) continue;
                int? hand = HandLoads.For(input.Item, world.Tuning);
                if (!hand.HasValue) continue;
                LedgerLocation stock = stocks[i];
                while (open < families)
                {
                    LedgerLocation src = _sources.Find(world, input.Item, fp.AccessPoint);
                    if (src == null) break;
                    int n = Math.Min(Math.Min(Math.Max(1, hand.Value), src.AvailableOf(input.Item.Id)), stock.FreeSpace);
                    if (n <= 0 || !InputReserve.Allows(world, op, input.Item, n)) break;
                    TaskRecord task = world.Tasks.Create(HandHaulTaskDefs.Build(world, src, stock, input.Item, n), op.Id);
                    int owner = HandHaulTaskDefs.OwnerFor(task.Id);
                    if (world.Inventory.ReserveStock(src, input.Item, n, owner) == 0 ||
                        world.Inventory.ReserveCapacity(stock, input.Item, n, owner) == 0)
                    {
                        world.Inventory.CancelOwner(owner);
                        world.Tasks.Cancel(task.Id);
                        break;
                    }
                    open++;
                }
            }
        }

        /// <summary>The input stocks of a workshop in recipe order, or null when any is missing.</summary>
        internal static List<LedgerLocation> StocksOf(SimWorld world, Footprint fp, WorkshopDef def)
        {
            var list = new List<LedgerLocation>(def.Inputs.Count);
            foreach (RecipeInput input in def.Inputs)
            {
                LedgerLocation s = world.Inventory.FindWorkshopStock(fp.Id, input.Item.Id);
                if (s == null) return null;
                list.Add(s);
            }
            return list;
        }

        /// <summary>True when every input has one batch available (unreserved) in its stock.</summary>
        internal static bool HasBatch(WorkshopDef def, IList<LedgerLocation> stocks)
        {
            for (int i = 0; i < def.Inputs.Count; i++)
                if (stocks[i].AvailableOf(def.Inputs[i].Item.Id) < def.Inputs[i].Count) return false;
            return true;
        }

        /// <summary>Id of the work task definition (T-524, HW-02, PL-05).</summary>
        public const string WorkTaskId = "task.workshop_work";

        /// <summary>Unfinished work tasks of the operation.</summary>
        internal static int OpenWorkCount(SimWorld world, Operation op)
        {
            int n = 0;
            foreach (TaskRecord t in world.Tasks.ByOwner(op.Id))
                if (t.Def.Id == WorkTaskId && !t.IsFinished) n++;
            return n;
        }

        /// <summary>
        /// Minutes in a workshop shift (HW-02): <c>WorkshopShiftMinutes</c> when set above 0, otherwise (the default) the
        /// length of today's daylight work window, so "per day" means a real working day in every season.
        /// </summary>
        public static int ShiftMinutes(SimWorld world)
        {
            if (world.Tuning.WorkshopShiftMinutes > 0) return world.Tuning.WorkshopShiftMinutes;
            WorkDay.Window win = world.WorkDay.WindowOf(world, WorkDay.DayStart(world));
            return (int)Math.Max(1, win.End - win.Sunrise);
        }

        // Families of the operation with a working-age member: each may run one work task at a time.
        private static int WorkingFamilies(SimWorld world, Operation op)
        {
            int n = 0;
            foreach (int fid in op.FamilyIds)
            {
                Family fam = world.People.FindFamily(fid);
                if (fam == null) continue;
                foreach (int cid in fam.MemberIds)
                {
                    Citizen c = world.People.FindCitizen(cid);
                    if (c != null && c.AgeYears(world.Tick, world.Tuning.AgeingRate) >= OperationStatus.WorkingAgeYears) { n++; break; }
                }
            }
            return n;
        }

        // One work task per assigned family while the stock holds an input and the store has room for its outputs.
        // Work minutes per input = shift minutes / inputs per day (HW-02). Progress lives in the task, like a build's.
        private static void OpenWork(SimWorld world, Operation op, WorkshopDef def, Footprint fp, IList<LedgerLocation> stocks)
        {
            LedgerLocation store = world.Inventory.FindGoodsStore(fp.Id);
            if (store == null) return;
            int minutes = Math.Max(1, ShiftMinutes(world) / def.BatchesPerFamilyDay);
            int open = OpenWorkCount(world, op);
            int families = WorkingFamilies(world, op);
            while (open < families && HasBatch(def, stocks) && store.FreeSpace >= def.OutputsPerBatch)
            {
                var taskDef = new TaskDef(WorkTaskId, new[] { TaskStep.Work(minutes, true) },
                    EligibilityPolicy.AssignedFamilyOnly, RequiredResource.None);
                TaskRecord task = world.Tasks.Create(taskDef, op.Id, null, TaskPriority.Normal);
                bool ok = true;
                for (int i = 0; ok && i < def.Inputs.Count; i++)
                    ok = world.Inventory.ReserveStock(stocks[i], def.Inputs[i].Item, def.Inputs[i].Count, task.Id) != 0;
                foreach (RecipeOutput o in def.Outputs)
                    if (ok) ok = world.Inventory.ReserveCapacity(store, o.Item, o.Count, task.Id) != 0;
                if (!ok)
                {
                    world.Inventory.CancelOwner(task.Id);
                    world.Tasks.Cancel(task.Id);
                    return;
                }
                open++;
            }
        }

        /// <summary>A work task has finished: its reservations end, one input is consumed and the outputs are produced.</summary>
        internal static void OnWorkDone(SimWorld world, TaskRecord t)
        {
            world.Inventory.CancelOwner(t.Id);
            Operation op = world.Operations.Get(t.OwnerOperationId);
            WorkshopDef def = op == null ? null : OperationTypes.WorkshopOf(op.TypeId, world.Tuning);
            Footprint fp = def == null ? null : HutOf(world, op);
            if (fp == null) return;
            List<LedgerLocation> stocks = StocksOf(world, fp, def);
            LedgerLocation store = world.Inventory.FindGoodsStore(fp.Id);
            if (stocks == null || store == null || !HasBatch(def, stocks)) return;
            for (int i = 0; i < def.Inputs.Count; i++)
                world.Inventory.Consume(stocks[i], def.Inputs[i].Item, def.Inputs[i].Count);
            foreach (RecipeOutput o in def.Outputs) world.Inventory.Produce(store, o.Item, o.Count);
        }

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result)
        {
        }

        internal static Footprint HutOf(SimWorld world, Operation op)
        {
            foreach (int id in op.ModuleIds)
            {
                Footprint f = world.Footprints.Get(id);
                if (f != null && WorkshopBuildings.IsWorkshop(f.Type)) return f;
            }
            return null;
        }

        internal static bool HasWorker(SimWorld world, Operation op)
        {
            foreach (int fid in op.FamilyIds)
            {
                Family fam = world.People.FindFamily(fid);
                if (fam == null) continue;
                foreach (int cid in fam.MemberIds)
                {
                    Citizen c = world.People.FindCitizen(cid);
                    if (c != null && c.AgeYears(world.Tick, world.Tuning.AgeingRate) >= OperationStatus.WorkingAgeYears) return true;
                }
            }
            return false;
        }

        // Frees the reservations and oxen of fetch tasks that finished or were cancelled outside the step runner.
        private static void Sweep(SimWorld world)
        {
            var owners = new SortedSet<int>();
            foreach (Reservation r in world.Inventory.Reservations)
            {
                TaskRecord t = world.Tasks.Get(r.Owner);
                if (t != null && t.IsFinished && (t.Def.Id == FetchTaskId || t.Def.Id == WorkTaskId)) owners.Add(r.Owner);
            }
            foreach (int owner in owners) world.Inventory.CancelOwner(owner);
            foreach (Ox ox in world.People.Oxen)
            {
                if (ox.State.Kind != OxStateKind.Reserved) continue;
                TaskRecord t = world.Tasks.Get(ox.State.TaskId);
                if (t == null || t.IsFinished) ox.State = OxState.Free;
            }
        }

        // Open fetch tasks of this workshop without an ox ask the pool, in task id order.
        private static void AttachOxen(SimWorld world, Operation op, Footprint fp)
        {
            var pool = new OxPool(world);
            foreach (TaskRecord t in world.Tasks.InState(TaskState.Open))
            {
                if (t.OwnerOperationId != op.Id || t.Def.Id != FetchTaskId || t.OxId.HasValue) continue;
                Ox ox = pool.FindFor(op.Id, fp.AccessPoint);
                if (ox == null) return;
                Attach(world, t, ox);
            }
        }

        private static void Attach(SimWorld world, TaskRecord t, Ox ox)
        {
            bool assigned = world.Operations.FindOxOwner(ox.Id) != null;
            world.Tasks.SetOx(t.Id, ox.Id, assigned);
            ox.State = OxState.Reserved(t.Id);
        }

        // One fetch per free ox. Each round picks the ox-carried input with the lowest share of its stock capacity (PL-30,
        // decision 0051), skipping inputs with no room for a load, with no source holding a load, or blocked by the reserve.
        private void OpenTasks(SimWorld world, Operation op, WorkshopDef def, Footprint fp, IList<LedgerLocation> stocks)
        {
            var pool = new OxPool(world);
            while (true)
            {
                int best = -1;
                LedgerLocation source = null;
                int load = Math.Max(1, def.OxLoadForInput);
                for (int i = 0; i < def.Inputs.Count; i++)
                {
                    RecipeInput cand = def.Inputs[i];
                    LedgerLocation s = stocks[i];
                    if (cand.Carry != CarryClass.Ox || s.FreeSpace < load) continue;
                    if (!InputReserve.Allows(world, op, cand.Item, load)) continue;
                    LedgerLocation src = _sources.Find(world, cand.Item, fp.AccessPoint);
                    if (src == null || src.AvailableOf(cand.Item.Id) < load) continue;
                    // Lower share first: held/capacity compared by cross-multiplication; ties keep recipe order.
                    if (best >= 0 && !Shorter(stocks[i], stocks[best])) continue;
                    best = i;
                    source = src;
                }
                if (best < 0) return;
                RecipeInput input = def.Inputs[best];
                ItemDef inItem = input.Item;
                LedgerLocation stock = stocks[best];
                Ox ox = pool.FindFor(op.Id, PointOf(world, source));
                if (ox == null) return;

                PathPoint pick = PathOf(PointOf(world, source));
                PathPoint drop = new PathPoint(fp.AccessPoint.XMetres, fp.AccessPoint.ZMetres);
                string item = inItem.Id;
                var steps = new[]
                {
                    TaskStep.FetchOx(),
                    TaskStep.LeadTo(pick),
                    TaskStep.LoadOx(source.Id, load, Math.Max(1, world.Tuning.OxLoadMinutes), false, item),
                    TaskStep.LeadTo(drop),
                    TaskStep.UnloadOx(stock.Id, load, Math.Max(1, world.Tuning.OxUnloadMinutes), false, item),
                    TaskStep.ReturnOx(),
                };
                var taskDef = new TaskDef(FetchTaskId, steps, EligibilityPolicy.AssignedFamilyOnly, RequiredResource.Ox);
                TaskRecord task = world.Tasks.Create(taskDef, op.Id, null, TaskPriority.Normal);
                if (world.Inventory.ReserveStock(source, inItem, load, task.Id) == 0 ||
                    world.Inventory.ReserveCapacity(stock, inItem, load, task.Id) == 0)
                {
                    world.Inventory.CancelOwner(task.Id);
                    world.Tasks.Cancel(task.Id);
                    return;
                }
                Attach(world, task, ox);
            }
        }

        // True when a holds a strictly lower share of its capacity than b (held plus incoming, so open fetches count).
        private static bool Shorter(LedgerLocation a, LedgerLocation b)
        {
            long ua = a.Capacity - a.FreeSpace, ub = b.Capacity - b.FreeSpace;
            return ua * b.Capacity < ub * a.Capacity;
        }

        // Where to lead the ox to reach a source: a pile lies on the ground, a building is reached at its access point.
        private static PointMm PointOf(SimWorld world, LedgerLocation l)
        {
            if (l.Kind == LocationKind.GroundPile) return l.Position;
            Footprint f = world.Footprints.Get(l.ModuleId);
            return f != null ? f.AccessPoint : l.Position;
        }

        private static PathPoint PathOf(PointMm p) => new PathPoint(p.XMetres, p.ZMetres);
    }
}
