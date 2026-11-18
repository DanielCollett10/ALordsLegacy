using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Foraging;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Thatch;

namespace LordsLegacy.Sim.Tasks
{
    /// <summary>
    /// Reed cutting steps (MAT-39c): WalkTo the bed, then one heavy Work step per bundle. Each finished step takes one
    /// sheaf from the bed; the last one stands all the bundles at the bed as one curing stook. A failed task stands the
    /// bundles already cut as a stook, so no work is lost (<see cref="FailCutReed"/>).
    /// </summary>
    public sealed partial class StepRunnerSystem
    {
        private void StepCutReed(SimWorld world, TaskRecord t, Citizen c)
        {
            TaskStep step = t.Def.Steps[t.CurrentStep];
            if (step.Kind == StepKind.WalkTo)
            {
                int r = Walk(c, t, step.X, step.Z);
                if (r == 2) Fail(world, t, c);
                else if (r == 1) Advance(world, t, c);
                return;
            }
            if (step.Kind != StepKind.Work || !t.BushId.HasValue || !world.Bushes.TryGet(t.BushId.Value, out BushRecord bed))
            {
                Fail(world, t, c);
                return;
            }
            if (_returning.Contains(t.Id))
            {
                int r = Walk(c, t, bed.X, bed.Z);
                if (r == 2) Fail(world, t, c);
                else if (r == 1) _returning.Remove(t.Id);
                return;
            }
            c.Activity = Activity.Working;
            world.Tasks.AddProgress(t.Id, 1);
            if (t.StepProgress < step.DurationTicks) return;
            if (!world.Bushes.TakeBasket(bed.Id, t.Id, world.Tick)) { Fail(world, t, c); return; }
            if (t.CurrentStep == t.Def.Steps.Count - 1) StandStook(world, t, bed, t.Def.Steps.Count - 1);
            Advance(world, t, c);
        }

        private static void StandStook(SimWorld world, TaskRecord t, BushRecord bed, int bundles)
        {
            if (bundles <= 0) return;
            long ready = world.Tick + (long)System.Math.Max(1, world.Tuning.ThatchCureDays) * 1440;
            world.Curing.Add(PointMm.FromMetres(bed.X, bed.Z), t.OwnerOperationId, bundles, ready);
        }

        // A task that ends early stands the bundles already cut (the bed has lost them) as a stook.
        private static void FailCutReed(SimWorld world, TaskRecord t)
        {
            if (!t.BushId.HasValue || !world.Bushes.TryGet(t.BushId.Value, out BushRecord bed)) return;
            StandStook(world, t, bed, System.Math.Max(0, t.CurrentStep - 1));
            world.Bushes.Release(bed.Id);
        }
    }
}
