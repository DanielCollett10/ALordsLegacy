using System;
using System.Collections.Generic;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// In-memory, append-only list of every command executed, in order, as (tick, command) entries.
    /// Used for debugging and future replay (decision 0011). Owned by <see cref="Core.SimWorld"/>.
    /// </summary>
    public sealed class CommandLog
    {
        private readonly List<CommandLogEntry> _entries = new List<CommandLogEntry>();

        /// <summary>Entries in execution order. Read-only view.</summary>
        public IReadOnlyList<CommandLogEntry> Entries => _entries;

        /// <summary>Number of entries.</summary>
        public int Count => _entries.Count;

        /// <summary>Appends one entry. Called by <see cref="Core.SimWorld.Execute"/>.</summary>
        public void Append(long tick, ISimCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            _entries.Add(new CommandLogEntry(tick, command));
        }
    }
}
