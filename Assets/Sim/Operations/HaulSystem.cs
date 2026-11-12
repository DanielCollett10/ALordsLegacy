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

namespace LordsLegacy.Sim.Operations
{
    /// <summary>
    /// Operations-phase system that opens Haul tasks (T-290, WC-21..24, WC-26, WC-33, WCO-07, WCO-08, LED-05, TSK-09).
    /// Logs reach storage only by ox. For each woodcutting operation that is not Paused and has a hut, storage and a
    /// worker, every own ground pile with at least <c>OxLoadLogs</c> unreserved logs gets a Haul task (fewer when no
    /// standing tree and no unfinished fell task is left to add to the pile). The task reserves, at creation, the
    /// pile's logs and room in the storage with free space nearest the pile; then it asks <see cref="OxPool.FindFor"/>
    /// for an ox every tick. A task without an ox stays Open and shows as Waiting for ox; the dispatcher will not hand
    /// it out, so felling comes first. Steps: FetchOx, LeadTo pile, LoadOx, LeadTo storage, UnloadOx, ReturnOx.
    /// Also releases the reservations and ox of haul tasks that were cancelled from outside the step runner.
    /// </summary>
    public sealed partial class HaulSystem : ISimSystem
    {
        /// <summary>Id of the Haul task definition.</summary>
        public const string HaulTaskId = "task.haul_logs";

        /// <summary>Haul tasks outrank Fell tasks once they have an ox.</summary>
        public const int HaulPriority = 1;

        /// <inheritdoc />
        public void Step(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            Sweep(world);
            foreach (Operation op in new List<Operation>(world.Operations.All))
            {
                if (op.TypeId != OperationTypes.WoodcuttingId) continue;
                if (!CanHaul(world, op)) continue;
                ResolveAwaiting(world, op);
                OpenEarly(world, op);
                OpenTasks(world, op);
                AttachOxen(world, op);
            }
        }

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result)
        {
        }

        private static bool CanHaul(SimWorld world, Operation op)
        {
            if (op.Paused || OperationStatus.Hut(world, op) == null) return false;
            if (OperationStatus.StorageModules(world, op).Count == 0) return false;
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

        // Release stock/capacity reservations and held oxen of finished haul tasks.
        private static void Sweep(SimWorld world)
        {
            var owners = new SortedSet<int>();
            foreach (Reservation r in world.Inventory.Reservations)
            {
                TaskRecord t = world.Tasks.Get(r.Owner);
                if (t != null && t.IsFinished && t.Def.Id == HaulTaskId) owners.Add(r.Owner);
            }
            foreach (int owner in owners) world.Inventory.CancelOwner(owner);
            foreach (Ox ox in world.People.Oxen)
            {
                if (ox.State.Kind != OxStateKind.Reserved) continue;
                TaskRecord t = world.Tasks.Get(ox.State.TaskId);
                if (t == null || t.IsFinished) ox.State = OxState.Free;
            }
        }

        private static void OpenTasks(SimWorld world, Operation op)
        {
            int load = Math.Max(1, world.Tuning.OxLoadLogs);
            bool moreComing = MoreLogsComing(world, op);
            var piles = new List<LedgerLocation>();
            foreach (LedgerLocation l in world.Inventory.Locations)
                if (l.Kind == LocationKind.GroundPile && l.OwnerOperationId == op.Id) piles.Add(l);

            foreach (LedgerLocation pile in piles)
            {
                while (true)
                {
                    int available = pile.AvailableOf(Items.Log.Id);
                    if (available < load && (moreComing || available < 1)) break;
                    int n = Math.Min(load, available);
                    if (!TryCreate(world, op, pile, n)) break;
                }
            }
        }

        // True while a standing tree remains in the work region or a fell task is unfinished (more logs will arrive).
        private static bool MoreLogsComing(SimWorld world, Operation op)
        {
            foreach (TaskRecord t in world.Tasks.ByOwner(op.Id))
                if (!t.IsFinished && t.Def.Id == WoodcuttingSystem.FellTaskId) return true;
            Forestry.TreeRegion region = OperationStatus.WorkRegion(world, op);
            return region != null && world.Trees.AnyStandingIn(region);
        }

        // awaiting: id of the Fell task still producing this pile's logs (early fetch); stock is reserved once it is Done.
        private static bool TryCreate(SimWorld world, Operation op, LedgerLocation pile, int n) =>
            TryCreate(world, op, pile, n, null, out _);

        private static bool TryCreate(SimWorld world, Operation op, LedgerLocation pile, int n, int? awaiting,
            out TaskRecord created)
        {
            created = null;
            Footprint store = ChooseStorage(world, op, pile, n, out int count);
            if (store == null) return false;
            LedgerLocation dest = world.Inventory.GetOrAddStorage(store.Id, store.Type);

            var def = new TaskDef(HaulTaskId, new[]
            {
                TaskStep.FetchOx(),
                TaskStep.LeadTo(new PathPoint(pile.Position.XMetres, pile.Position.ZMetres)),
                TaskStep.LoadOx(pile.Id, count, Math.Max(1, world.Tuning.OxLoadMinutes)),
                TaskStep.LeadTo(new PathPoint(store.AccessPoint.XMetres, store.AccessPoint.ZMetres)),
                TaskStep.UnloadOx(dest.Id, count, Math.Max(1, world.Tuning.OxUnloadMinutes)),
                TaskStep.ReturnOx(),
            }, EligibilityPolicy.AssignedFamilyOnly, RequiredResource.Ox);

            TaskRecord task = world.Tasks.Create(def, op.Id, null, HaulPriority);
            if ((!awaiting.HasValue && world.Inventory.ReserveStock(pile, Items.Log, count, task.Id) == 0) ||
                world.Inventory.ReserveCapacity(dest, Items.Log, count, task.Id) == 0)
            {
                world.Inventory.CancelOwner(task.Id);
                world.Tasks.Cancel(task.Id);
                return false;
            }
            if (awaiting.HasValue) world.Tasks.SetAwaiting(task.Id, awaiting);
            created = task;
            return true;
        }

        // The storage with room nearest the pile: one that takes the whole load, else the nearest with any room.
        private static Footprint ChooseStorage(SimWorld world, Operation op, LedgerLocation pile, int n, out int count)
        {
            Footprint whole = null, partial = null;
            double wholeD = double.MaxValue, partialD = double.MaxValue;
            int partialFree = 0;
            foreach (Footprint f in OperationStatus.StorageModules(world, op))
            {
                LedgerLocation l = world.Inventory.GetOrAddStorage(f.Id, f.Type);
                int free = l.FreeSpace;
                if (free < 1) continue;
                double dx = f.AccessPoint.X - pile.Position.X, dz = f.AccessPoint.Z - pile.Position.Z;
                double d = dx * dx + dz * dz;
                if (free >= n) { if (d < wholeD) { wholeD = d; whole = f; } }
                else if (d < partialD) { partialD = d; partial = f; partialFree = free; }
            }
            count = whole != null ? n : partialFree;
            return whole ?? partial;
        }

        // Open haul tasks without an ox ask the pool, in task id order.
        private static void AttachOxen(SimWorld world, Operation op)
        {
            var pool = new OxPool(world);
            foreach (TaskRecord t in world.Tasks.InState(TaskState.Open))
            {
                if (t.OwnerOperationId != op.Id || t.Def.Id != HaulTaskId || t.OxId.HasValue) continue;
                LedgerLocation pile = PileOf(world, t);
                if (pile == null) { world.Tasks.Cancel(t.Id); continue; }
                Ox ox = pool.FindFor(op.Id, pile.Position);
                if (ox == null) continue;
                bool assigned = world.Operations.FindOxOwner(ox.Id) != null;
                world.Tasks.SetOx(t.Id, ox.Id, assigned);
                ox.State = OxState.Reserved(t.Id);
            }
        }

        private static LedgerLocation PileOf(SimWorld world, TaskRecord t)
        {
            foreach (TaskStep s in t.Def.Steps)
                if (s.Kind == StepKind.LoadOx) return world.Inventory.Get(s.LocationId);
            return null;
        }
    }
}
