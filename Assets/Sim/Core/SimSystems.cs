using System.Collections.Generic;

namespace LordsLegacy.Sim.Core
{
    /// <summary>
    /// The one ordered list of systems that <see cref="SimWorld"/> runs. Order is explicit here and
    /// fixed (CLK-07); there is no reflection and no discovery. Every tick, each system's
    /// <see cref="ISimSystem.Step"/> runs in list order. An accepted command notifies every system's
    /// <see cref="ISimSystem.OnCommandApplied"/> in list order.
    /// </summary>
    /// <remarks>
    /// Phase order, earliest first. A system goes in the phase that matches its job:
    /// <list type="number">
    /// <item><description>sync: copy or refresh derived data from the registries (e.g. road and area indexes).</description></item>
    /// <item><description>routine: daily and scheduled needs and states (eating, resting, growth).</description></item>
    /// <item><description>dispatch: choose new jobs and targets for idle people and oxen.</description></item>
    /// <item><description>steps: advance the current job by one tick (work progress, harvesting, building).</description></item>
    /// <item><description>movement: move citizens and oxen along their paths.</description></item>
    /// <item><description>operations: settle the tick (stock totals, camp summaries, cleanup of finished work).</description></item>
    /// </list>
    /// Within a phase, add new systems at the end of that phase's block so existing relative order does not change.
    /// No systems exist yet, so the list is empty.
    /// </remarks>
    public static class SimSystems
    {
        /// <summary>
        /// Creates the production system list for a new world, in the phase order above. Returns a new list each
        /// call, so no system instance is shared between worlds.
        /// </summary>
        /// <returns>The systems in run order. Never null; empty while no subsystem has registered one.</returns>
        public static IReadOnlyList<ISimSystem> CreateDefault()
        {
            return new ISimSystem[]
            {
                // sync
                new LordsLegacy.Sim.Navigation.NavSyncSystem(),
                // routine
                // dispatch
                // steps
                // movement
                new LordsLegacy.Sim.Navigation.MovementSystem(),
                // operations
            };
        }
    }
}
