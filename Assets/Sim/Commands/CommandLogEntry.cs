namespace LordsLegacy.Sim.Commands
{
    /// <summary>One executed command and the tick it ran at.</summary>
    public readonly struct CommandLogEntry
    {
        /// <summary>The tick at which <see cref="Core.SimWorld.Execute"/> was called.</summary>
        public readonly long Tick;

        /// <summary>The command that was executed (accepted or rejected).</summary>
        public readonly ISimCommand Command;

        /// <summary>Creates an entry.</summary>
        public CommandLogEntry(long tick, ISimCommand command)
        {
            Tick = tick;
            Command = command;
        }
    }
}
