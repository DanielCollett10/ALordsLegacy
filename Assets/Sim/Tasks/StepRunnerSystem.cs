using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Tasks
{
    /// <summary>
    /// Steps-phase system (TSK-12, TSK-13). Runs the current step of every Active task, in task id order.
    /// <list type="bullet">
    /// <item><description>MoveTo: issues a <see cref="MoveOrder"/> to the target tree and waits for it to finish
    /// (activity Walking). <c>Done</c> completes the step; <c>Failed</c> cancels the task.</description></item>
    /// <item><description>Work: activity Working; one tick of progress per tick; the step completes when the
    /// progress reaches its duration.</description></item>
    /// </list>
    /// A step fails when the assignee or target tree is gone, the task has no target to walk to, or the walk fails
    /// (No route). A failed step cancels the task, which releases its reservations, and frees the worker. Holds the
    /// orders it issued, so each world needs its own instance.
    /// </summary>
    public sealed partial class StepRunnerSystem : ISimSystem
    {
        private readonly Dictionary<int, MoveOrder> _orders = new Dictionary<int, MoveOrder>();
        // Tasks paused mid-Work: the worker must walk back to the tree before work continues.
        private readonly HashSet<int> _returning = new HashSet<int>();

        /// <inheritdoc />
        public void Step(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            foreach (TaskRecord t in world.Tasks.InState(TaskState.Active)) StepTask(world, t);
        }

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result)
        {
        }

        private void StepTask(SimWorld world, TaskRecord t)
        {
            Citizen c = t.AssigneeId.HasValue ? world.People.FindCitizen(t.AssigneeId.Value) : null;
            if (c == null) { Fail(world, t, null); return; }
            Ox ox = null;
            if (t.OxId.HasValue && !CheckOx(world, t, c, out ox)) { Fail(world, t, c); return; }
            if (!world.WorkDay.OnShift(world, c))
            {
                // Night, or not yet time to leave (PPL-24): keep progress and reservations; the routine walks the worker home.
                _orders.Remove(t.Id);
                if (t.Def.Steps[t.CurrentStep].Kind == StepKind.Work) _returning.Add(t.Id);
                return;
            }
            if (t.Target.HasValue)
            {
                if (!world.Trees.TryGet(t.Target.Value, out TreeRecord tree) || tree.State == TreeState.Stump)
                {
                    Fail(world, t, c);
                    return;
                }
            }

            TaskStep step = t.Def.Steps[t.CurrentStep];
            switch (step.Kind)
            {
                case StepKind.MoveTo: StepMove(world, t, c, true); break;
                case StepKind.FetchOx:
                case StepKind.LeadTo:
                case StepKind.ReturnOx: StepOxWalk(world, t, c, ox, step); break;
                default:
                    if (_returning.Contains(t.Id)) StepMove(world, t, c, false); // back to the tree after a night
                    else StepWork(world, t, c, ox, step);
                    break;
            }
        }

        private void StepMove(SimWorld world, TaskRecord t, Citizen c, bool advance)
        {
            if (!t.Target.HasValue || !world.Trees.TryGet(t.Target.Value, out TreeRecord tree))
            {
                Fail(world, t, c);
                return;
            }
            c.Activity = Activity.Walking;
            if (!_orders.TryGetValue(t.Id, out MoveOrder order) || c.Move != order)
            {
                order = new MoveOrder(tree.X, tree.Z);
                _orders[t.Id] = order;
                c.Move = order;
                return;
            }
            if (order.Status == MoveStatus.Failed) { Fail(world, t, c); return; }
            if (order.Status == MoveStatus.Done)
            {
                c.Move = null;
                _orders.Remove(t.Id);
                if (advance) Advance(world, t, c);
                else _returning.Remove(t.Id);
            }
        }

        private void StepWork(SimWorld world, TaskRecord t, Citizen c, Ox ox, TaskStep step)
        {
            c.Activity = Activity.Working;
            world.Tasks.AddProgress(t.Id, 1);
            if (t.StepProgress < step.DurationTicks) return;
            if ((step.Kind == StepKind.LoadOx || step.Kind == StepKind.UnloadOx) && !TransferLogs(world, t, ox, step))
            {
                Fail(world, t, c);
                return;
            }
            Advance(world, t, c);
        }

        private void Advance(SimWorld world, TaskRecord t, Citizen c)
        {
            TaskStep done = t.Def.Steps[t.CurrentStep];
            world.Tasks.CompleteStep(t.Id);
            FellingOutcome.OnStepCompleted(world, t, done);
            if (t.State == TaskState.Done)
            {
                c.Activity = Activity.MillingAbout;
                _oxTrack.Remove(t.Id);
                _rest.Remove(t.Id);
            }
        }

        private void Fail(SimWorld world, TaskRecord t, Citizen c)
        {
            _orders.Remove(t.Id);
            _returning.Remove(t.Id);
            ReleaseOx(world, t, c);
            world.Tasks.Cancel(t.Id);
            if (c != null)
            {
                c.Move = null;
                c.Activity = Activity.MillingAbout;
            }
        }
    }
}
