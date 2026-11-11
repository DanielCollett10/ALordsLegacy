using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Core
{
    /// <summary>The working-day part of <see cref="SimWorld"/> (T-277).</summary>
    public sealed partial class SimWorld
    {
        private WorkDay _workDay;

        /// <summary>Shift planning for workers of assigned families. Never null.</summary>
        public WorkDay WorkDay => _workDay ?? (_workDay = new WorkDay());
    }
}
