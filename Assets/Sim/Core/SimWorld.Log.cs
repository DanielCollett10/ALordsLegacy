namespace LordsLegacy.Sim.Core
{
    /// <summary>The debug log part of <see cref="SimWorld"/> (T-628, decision 0058). Not saved.</summary>
    public sealed partial class SimWorld
    {
        private SimLog _log;

        /// <summary>The world's debug log. Never null, empty after a load, and never read by Sim rules.</summary>
        public SimLog Log => _log ?? (_log = new SimLog(() => Tick));
    }
}
