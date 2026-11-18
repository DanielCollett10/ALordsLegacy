using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Foraging
{
    /// <summary>
    /// Operations-phase system for foraging camps (FD-22..24, TSK-06). For every Working foraging operation it keeps one
    /// open <c>task.forage</c> per idle forager (age <c>ForagerMinAge</c> or more, light work only). Each task reserves the
    /// nearest free bush in the work region to the hut, plans to pick up to <c>HandCarryItems</c> baskets from it
    /// (one item per basket, <c>GatherMinutesPerBasket</c> each), and delivers them to the camp's own hut store only
    /// (decision 0044; never a granary, never the camp, FD-12). No hut store with room means no new tasks (Storage full). A sweep frees the bush and drops carried food of tasks that ended without delivering. Stateless.
    /// </summary>
    public sealed class ForagingSystem : ISimSystem
    {
        /// <summary>Id of the forage task definition.</summary>
        public const string ForageTaskId = "task.forage";
        /// <summary>Stage id of the picking step.</summary>
        public const string PickStageId = "stage.forage";
        private const int MaxBasketsPerTask = 20;

        /// <inheritdoc />
        public void Step(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            foreach (Operation op in new List<Operation>(world.Operations.All))
            {
                if (op.TypeId != OperationTypes.ForagingId) continue;
                Sweep(world, op);
                if (ForagingStatus.Of(world, op) != OperationState.Working) CancelOpen(world, op);
                else Refill(world, op);
            }
            ReleaseOrphanBushes(world);
        }

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result)
        {
        }

        private static void CancelOpen(SimWorld world, Operation op)
        {
            foreach (TaskRecord t in world.Tasks.ByOwner(op.Id))
                if (t.State == TaskState.Open && t.Def.Id == ForageTaskId) world.Tasks.Cancel(t.Id);
        }

        // A bush reserved by a task that ended (done or cancelled) is freed.
        private static void ReleaseOrphanBushes(SimWorld world)
        {
            foreach (BushRecord b in world.Bushes.All)
            {
                if (!b.ReservedBy.HasValue) continue;
                TaskRecord t = world.Tasks.Get(b.ReservedBy.Value);
                if (t != null && t.IsFinished) world.Bushes.Release(b.Id);
            }
        }

        // Food still on the carrier of an ended forage task (cancelled mid-carry) is put on the ground at the bush.
        private static void Sweep(SimWorld world, Operation op)
        {
            foreach (TaskRecord t in world.Tasks.ByOwner(op.Id))
            {
                if (!t.IsFinished || t.Def.Id != ForageTaskId || !t.CarrierId.HasValue) continue;
                LedgerLocation carrier = world.Inventory.Get(t.CarrierId.Value);
                if (carrier == null || carrier.Total == 0) continue;
                PointMm at = default;
                if (t.BushId.HasValue && world.Bushes.TryGet(t.BushId.Value, out BushRecord b)) at = PointMm.FromMetres(b.X, b.Z);
                else { Footprint hut = OperationStatus.Hut(world, op); if (hut != null) at = hut.Centre; }
                CarrierDrop.DropAll(world, carrier, at);
            }
        }

        private static void Refill(SimWorld world, Operation op)
        {
            Footprint hut = OperationStatus.Hut(world, op);
            TreeRegion region = OperationStatus.WorkRegion(world, op);
            if (hut == null || region == null) return;
            int open = 0;
            foreach (TaskRecord t in world.Tasks.ByOwner(op.Id))
                if (t.State == TaskState.Open && t.Def.Id == ForageTaskId) open++;
            int wanted = CountIdleForagers(world, op) - open;
            float ax = (float)hut.AccessPoint.XMetres, az = (float)hut.AccessPoint.ZMetres;
            for (int i = 0; i < wanted; i++)
            {
                BushRecord bush = world.Bushes.NearestAvailable(ax, az, region, BushKind.Berries, BushKind.Mushrooms);
                if (bush == null) return;
                LedgerLocation store = ForagingStores.HutStoreWithRoom(world, op, out int room);
                if (store == null) return;
                int n = Math.Min(Math.Min(bush.Baskets, Math.Max(1, world.Tuning.HandCarryItems)), Math.Min(room, MaxBasketsPerTask));
                Footprint storeFp = world.Footprints.Get(store.ModuleId);
                TaskRecord task = world.Tasks.Create(BuildDef(world, bush, storeFp, store, n), op.Id);
                if (!world.Bushes.Reserve(bush.Id, task.Id)) { world.Tasks.Cancel(task.Id); return; }
                world.Tasks.SetBush(task.Id, bush.Id);
            }
        }

        private static TaskDef BuildDef(SimWorld world, BushRecord bush, Footprint storeFp, LedgerLocation store, int baskets)
        {
            int minutes = Math.Max(1, world.Tuning.GatherMinutesPerBasket);
            var steps = new List<TaskStep> { TaskStep.WalkTo(new PathPoint(bush.X, bush.Z)) };
            for (int i = 0; i < baskets; i++) steps.Add(TaskStep.Work(minutes, false, PickStageId));
            steps.Add(TaskStep.WalkTo(new PathPoint(storeFp.AccessPoint.XMetres, storeFp.AccessPoint.ZMetres)));
            steps.Add(TaskStep.DepositForage(store.Id, baskets));
            return new TaskDef(ForageTaskId, steps, EligibilityPolicy.AssignedFamilyOnly);
        }

        private static int CountIdleForagers(SimWorld world, Operation op)
        {
            int n = 0;
            foreach (int fid in op.FamilyIds)
            {
                Family fam = world.People.FindFamily(fid);
                if (fam == null) continue;
                foreach (int cid in fam.MemberIds)
                {
                    Citizen c = world.People.FindCitizen(cid);
                    if (c == null || c.AgeYears(world.Tick, world.Tuning.AgeingRate) < world.Tuning.ForagerMinAge) continue;
                    if (DispatchSystem.IsIdle(world, c)) n++;
                }
            }
            return n;
        }
    }
}
