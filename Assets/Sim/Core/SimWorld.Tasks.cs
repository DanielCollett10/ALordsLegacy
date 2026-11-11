using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Core
{
    /// <summary>The task board part of <see cref="SimWorld"/> (T-274).</summary>
    public sealed partial class SimWorld
    {
        private TaskBoard _tasks;

        /// <summary>All tasks. Never null; empty at start. Reserves trees in <see cref="Trees"/>.</summary>
        public TaskBoard Tasks => _tasks ?? (_tasks = new TaskBoard(Trees));
    }
}
