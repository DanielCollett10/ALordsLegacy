namespace LordsLegacy.Sim.Core
{
    /// <summary>The debug log part of <see cref="SimWorld"/> (T-628, decision 0058). Not saved.</summary>
    public sealed partial class SimWorld
    {
        private SimLog _log;

        /// <summary>The world's debug log. Never null, empty after a load, and never read by Sim rules.</summary>
        public SimLog Log => _log ?? (_log = new SimLog(() => Tick));

        private void LogRefused(Commands.ISimCommand command, Commands.CommandResult result) =>
            Log.Write(LogCategory.Commands, command.GetType().Name + " refused: " + result.Text);
    }
}
