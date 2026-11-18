using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Foraging;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Thatch
{
    /// <summary>
    /// Operations-phase system for thatcher's huts (MAT-34, MAT-37, MAT-39b, MAT-39c). For every Working thatching
    /// operation it keeps one open <c>task.cut_reed</c> per idle cutter (age 14 or more, heavy work). Each task reserves
    /// the nearest free reed bed in the work region and plans <c>min(bed sheaves, ReedSheavesPerCut)</c> bundles, each
    /// <c>ThatchCutMinutesPerBundle</c> minutes, times <c>ThatchOffSeasonFactor</c> outside Dec-Feb. The finished bundles
    /// stand at the bed as one curing stook (<see cref="CuringRegistry"/>). Families stay assigned all year (MAT-37).
    /// </summary>
    public sealed class ThatchingSystem : ISimSystem
    {
        /// <summary>Id of the reed cutting task definition.</summary>
        public const string CutReedTaskId = "task.cut_reed";
        /// <summary>Stage id of the cutting step.</summary>
        public const string CutStageId = "stage.cut_reed";

        /// <inheritdoc />
        public void Step(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            foreach (Operation op in new List<Operation>(world.Operations.All))
            {
                if (op.TypeId != OperationTypes.ThatchingId) continue;
                if (ThatchingStatus.Of(world, op) != OperationState.Working) CancelOpen(world, op);
                else Refill(world, op);
            }
            ReleaseOrphanBeds(world);
        }

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result)
        {
        }

        private static void CancelOpen(SimWorld world, Operation op)
        {
            foreach (TaskRecord t in world.Tasks.ByOwner(op.Id))
                if (t.State == TaskState.Open && t.Def.Id == CutReedTaskId) world.Tasks.Cancel(t.Id);
        }

        // A bed reserved by a cut_reed task that ended is freed.
        private static void ReleaseOrphanBeds(SimWorld world)
        {
            foreach (BushRecord b in world.Bushes.All)
            {
                if (!b.ReservedBy.HasValue || b.Kind != BushKind.ReedBed) continue;
                TaskRecord t = world.Tasks.Get(b.ReservedBy.Value);
                if (t != null && t.IsFinished) world.Bushes.Release(b.Id);
            }
        }

        private static void Refill(SimWorld world, Operation op)
        {
            Footprint hut = OperationStatus.Hut(world, op);
            TreeRegion region = OperationStatus.WorkRegion(world, op);
            if (hut == null || region == null) return;
            int open = 0;
            foreach (TaskRecord t in world.Tasks.ByOwner(op.Id))
                if (t.State == TaskState.Open && t.Def.Id == CutReedTaskId) open++;
            int wanted = CountIdleCutters(world, op) - open;
            float ax = (float)hut.AccessPoint.XMetres, az = (float)hut.AccessPoint.ZMetres;
            for (int i = 0; i < wanted; i++)
            {
                BushRecord bed = world.Bushes.NearestAvailable(ax, az, region, BushKind.ReedBed);
                if (bed == null) return;
                int n = Math.Min(bed.Baskets, Math.Max(1, world.Tuning.ReedSheavesPerCut));
                TaskRecord task = world.Tasks.Create(BuildDef(world, bed, n), op.Id);
                if (!world.Bushes.Reserve(bed.Id, task.Id)) { world.Tasks.Cancel(task.Id); return; }
                world.Tasks.SetBush(task.Id, bed.Id);
            }
        }

        /// <summary>Minutes one bundle takes now: the winter rate, times the off-season factor outside Dec-Feb (MAT-34).</summary>
        public static int MinutesPerBundle(SimWorld world)
        {
            int m = Math.Max(1, world.Tuning.ThatchCutMinutesPerBundle);
            return ThatchingStatus.IsWinter(world) ? m : m * Math.Max(1, world.Tuning.ThatchOffSeasonFactor);
        }

        private static TaskDef BuildDef(SimWorld world, BushRecord bed, int bundles)
        {
            int minutes = MinutesPerBundle(world);
            var steps = new List<TaskStep> { TaskStep.WalkTo(new PathPoint(bed.X, bed.Z)) };
            for (int i = 0; i < bundles; i++) steps.Add(TaskStep.Work(minutes, true, CutStageId));
            return new TaskDef(CutReedTaskId, steps, EligibilityPolicy.AssignedFamilyOnly);
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
                    if (c == null || c.AgeYears(world.Tick, world.Tuning.AgeingRate) < OperationStatus.WorkingAgeYears) continue;
                    if (DispatchSystem.IsIdle(world, c)) n++;
                }
            }
            return n;
        }
    }
}
