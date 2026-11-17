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
                LedgerLocation stock = world.Inventory.FindWorkshopStock(fp.Id);
                if (stock == null) continue;
                AttachOxen(world, op, fp);
                OpenWork(world, op, def, fp, stock);
                OpenTasks(world, op, def, fp, stock);
            }
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
        private static void OpenWork(SimWorld world, Operation op, WorkshopDef def, Footprint fp, LedgerLocation stock)
        {
            LedgerLocation store = world.Inventory.FindGoodsStore(fp.Id);
            if (store == null) return;
            int minutes = Math.Max(1, world.Tuning.WorkshopShiftMinutes / def.InputsPerDayPerFamily);
            int open = OpenWorkCount(world, op);
            int families = WorkingFamilies(world, op);
            while (open < families && stock.AvailableOf(def.Input.Id) >= 1 && store.FreeSpace >= def.OutputsPerInput)
            {
                var taskDef = new TaskDef(WorkTaskId, new[] { TaskStep.Work(minutes, true) },
                    EligibilityPolicy.AssignedFamilyOnly, RequiredResource.None);
                TaskRecord task = world.Tasks.Create(taskDef, op.Id, null, TaskPriority.Normal);
                if (world.Inventory.ReserveStock(stock, def.Input, 1, task.Id) == 0 ||
                    world.Inventory.ReserveCapacity(store, def.Output, def.OutputsPerInput, task.Id) == 0)
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
            LedgerLocation stock = world.Inventory.FindWorkshopStock(fp.Id);
            LedgerLocation store = world.Inventory.FindGoodsStore(fp.Id);
            if (stock == null || store == null || !world.Inventory.Consume(stock, def.Input, 1)) return;
            world.Inventory.Produce(store, def.Output, def.OutputsPerInput);
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

        // One fetch per free ox while the stock has room for a whole load and a source has that much free input.
        private void OpenTasks(SimWorld world, Operation op, WorkshopDef def, Footprint fp, LedgerLocation stock)
        {
            int load = Math.Max(1, def.OxLoadForInput);
            var pool = new OxPool(world);
            while (stock.FreeSpace >= load)
            {
                LedgerLocation source = _sources.Find(world, def.Input, fp.AccessPoint);
                if (source == null || source.AvailableOf(def.Input.Id) < load) return;
                Ox ox = pool.FindFor(op.Id, PointOf(world, source));
                if (ox == null) return;

                PathPoint pick = PathOf(PointOf(world, source));
                PathPoint drop = new PathPoint(fp.AccessPoint.XMetres, fp.AccessPoint.ZMetres);
                string item = def.Input.Id;
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
                if (world.Inventory.ReserveStock(source, def.Input, load, task.Id) == 0 ||
                    world.Inventory.ReserveCapacity(stock, def.Input, load, task.Id) == 0)
                {
                    world.Inventory.CancelOwner(task.Id);
                    world.Tasks.Cancel(task.Id);
                    return;
                }
                Attach(world, task, ox);
            }
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
