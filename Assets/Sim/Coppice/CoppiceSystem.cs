using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.Foraging;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Coppice
{
    /// <summary>
    /// Operations-phase system for coppice huts (MAT-10, MAT-11, MAT-13..18). For every Working coppice operation it keeps
    /// one open <c>task.coppice</c> per idle cutter (age <c>ForagerMinAge</c> or more, light work). Each task reserves the
    /// nearest free stool, in the work region, of the kind the hut holds least of (among kinds with room), plans
    /// <c>min(stool rods, HandCarryRods, store room)</c> rods (<c>CoppiceMinutesPerRod</c> each) and delivers them to
    /// the hut's own store. A sweep drops rods of a task that ended mid-carry as an operation pile. Stateless.
    /// </summary>
    public sealed class CoppiceSystem : ISimSystem
    {
        /// <summary>Id of the coppice task definition.</summary>
        public const string CoppiceTaskId = "task.coppice";
        /// <summary>Stage id of the cutting step.</summary>
        public const string CutStageId = "stage.coppice";

        /// <inheritdoc />
        public void Step(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            foreach (Operation op in new List<Operation>(world.Operations.All))
            {
                if (op.TypeId != OperationTypes.CoppiceId) continue;
                Sweep(world, op);
                if (CoppiceStatus.Of(world, op) != OperationState.Working) CancelOpen(world, op);
                else Refill(world, op);
            }
            ReleaseOrphanStools(world);
        }

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result)
        {
        }

        private static void CancelOpen(SimWorld world, Operation op)
        {
            foreach (TaskRecord t in world.Tasks.ByOwner(op.Id))
                if (t.State == TaskState.Open && t.Def.Id == CoppiceTaskId) world.Tasks.Cancel(t.Id);
        }

        // A stool reserved by a coppice task that ended is freed.
        private static void ReleaseOrphanStools(SimWorld world)
        {
            foreach (BushRecord b in world.Bushes.All)
            {
                if (!b.ReservedBy.HasValue || !BushKinds.IsStool(b.Kind)) continue;
                TaskRecord t = world.Tasks.Get(b.ReservedBy.Value);
                if (t != null && t.IsFinished) world.Bushes.Release(b.Id);
            }
        }

        private static void Sweep(SimWorld world, Operation op)
        {
            foreach (TaskRecord t in world.Tasks.ByOwner(op.Id))
                if (t.IsFinished && t.Def.Id == CoppiceTaskId) CoppiceDrop.DropCarried(world, t, null);
        }

        private static void Refill(SimWorld world, Operation op)
        {
            Footprint hut = OperationStatus.Hut(world, op);
            TreeRegion region = OperationStatus.WorkRegion(world, op);
            LedgerLocation store = CoppiceStores.HutStore(world, op);
            if (hut == null || region == null || store == null) return;
            int open = 0;
            foreach (TaskRecord t in world.Tasks.ByOwner(op.Id))
                if (t.State == TaskState.Open && t.Def.Id == CoppiceTaskId) open++;
            int wanted = CountIdleCutters(world, op) - open;
            float ax = (float)hut.AccessPoint.XMetres, az = (float)hut.AccessPoint.ZMetres;
            for (int i = 0; i < wanted; i++)
            {
                if (!PlanOne(world, op, hut, store, region, ax, az)) return;
            }
        }

        // MAT-16/17: the kind the hut is shortest of first; a kind with no room or no free stool is skipped.
        private static bool PlanOne(SimWorld world, Operation op, Footprint hut, LedgerLocation store, TreeRegion region, float ax, float az)
        {
            var kinds = new List<BushKind>(CoppiceStores.StoolKinds);
            var held = new Dictionary<BushKind, int>();
            foreach (BushKind k in kinds)
            {
                ItemDef rod = CoppiceStores.RodOf(k);
                held[k] = store.CountOf(rod.Id) + CoppiceStores.Pending(world, store.Id, rod);
            }
            kinds.Sort((a, b) => held[a] != held[b] ? held[a].CompareTo(held[b]) : ((int)a).CompareTo((int)b));
            foreach (BushKind k in kinds)
            {
                int room = CoppiceStores.Room(world, store, CoppiceStores.RodOf(k));
                if (room <= 0) continue;
                BushRecord stool = world.Bushes.NearestAvailable(ax, az, region, k);
                if (stool == null) continue;
                int n = Math.Min(Math.Min(stool.Baskets, Math.Max(1, world.Tuning.HandCarryRods)), room);
                TaskRecord task = world.Tasks.Create(BuildDef(world, stool, hut, store, n), op.Id);
                if (!world.Bushes.Reserve(stool.Id, task.Id)) { world.Tasks.Cancel(task.Id); return false; }
                world.Tasks.SetBush(task.Id, stool.Id);
                return true;
            }
            return false;
        }

        private static TaskDef BuildDef(SimWorld world, BushRecord stool, Footprint hut, LedgerLocation store, int rods)
        {
            int minutes = Math.Max(1, world.Tuning.CoppiceMinutesPerRod);
            var steps = new List<TaskStep> { TaskStep.WalkTo(new PathPoint(stool.X, stool.Z)) };
            for (int i = 0; i < rods; i++) steps.Add(TaskStep.Work(minutes, false, CutStageId));
            steps.Add(TaskStep.WalkTo(new PathPoint(hut.AccessPoint.XMetres, hut.AccessPoint.ZMetres)));
            steps.Add(TaskStep.DepositForage(store.Id, rods));
            return new TaskDef(CoppiceTaskId, steps, EligibilityPolicy.AssignedFamilyOnly);
        }

        private static int CountIdleCutters(SimWorld world, Operation op)
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
