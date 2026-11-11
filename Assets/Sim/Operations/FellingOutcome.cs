using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Processes;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Operations
{
    /// <summary>
    /// What a finished timber step does to the world (TMB-05, TMB-07, WC-12). Called by the step runner after a step
    /// completes. After the fell stage the tree is Felled. When the task's last step completes the tree becomes a Stump
    /// and floor(trunk ÷ StandardLogLengthM) logs appear in a ground pile at the tree, owned by the operation.
    /// Steps without a stage id, and tasks without a tree target, are ignored.
    /// </summary>
    public static class FellingOutcome
    {
        /// <summary>Applies the effect of <paramref name="step"/>, which has just completed on <paramref name="task"/>.</summary>
        public static void OnStepCompleted(SimWorld world, TaskRecord task, TaskStep step)
        {
            if (step.StageId == null || !task.Target.HasValue) return;
            if (!world.Trees.TryGet(task.Target.Value, out TreeRecord tree)) return;
            if (step.StageId == ProcessDefs.FellStageId && tree.State == TreeState.Standing)
                world.Trees.SetState(tree.Id, TreeState.Felled);
            if (task.State != TaskState.Done || tree.State == TreeState.Stump) return;
            world.Trees.SetState(tree.Id, TreeState.Stump);
            int logs = tree.LogYield(world.Tuning);
            if (logs > 0)
                world.Inventory.ProduceOnGround(PointMm.FromMetres(tree.X, tree.Z), task.OwnerOperationId, Items.Log, logs);
        }
    }
}
