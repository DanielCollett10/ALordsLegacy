using System;
using System.Collections.Generic;

namespace LordsLegacy.Sim.Core
{
    /// <summary>
    /// A short, saved list of what happened in the settlement (arrivals, departures), newest last (decision 0045).
    /// Keeps the last <see cref="Capacity"/> entries. Plain tick and text only: it is the future feed for player alerts,
    /// and nothing reads it yet. <see cref="Version"/> moves on every <see cref="Add"/>.
    /// </summary>
    public sealed class SettlementLog
    {
        /// <summary>How many entries are kept. Older ones are dropped.</summary>
        public const int Capacity = 20;

        private readonly List<SettlementEntry> _entries = new List<SettlementEntry>();

        /// <summary>Bumped on every change, so views can rebuild only when it moves.</summary>
        public int Version { get; private set; }

        /// <summary>The kept entries, oldest first. Never null.</summary>
        public IReadOnlyList<SettlementEntry> Entries => _entries;

        /// <summary>Appends an entry. When the log is full, the oldest entry is dropped. Null text is stored as empty.</summary>
        /// <param name="tick">The tick the event happened on.</param>
        /// <param name="text">What happened, in plain words.</param>
        public void Add(long tick, string text)
        {
            _entries.Add(new SettlementEntry(tick, text ?? string.Empty));
            if (_entries.Count > Capacity) _entries.RemoveRange(0, _entries.Count - Capacity);
            Version++;
        }

        /// <summary>Load only: replaces the entries with the saved ones, oldest first. Keeps the last <see cref="Capacity"/>.</summary>
        internal void Restore(IEnumerable<SettlementEntry> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            _entries.Clear();
            foreach (SettlementEntry e in entries) Add(e.Tick, e.Text);
        }
    }

    /// <summary>One line of the settlement log: the tick it happened on and its text.</summary>
    public readonly struct SettlementEntry
    {
        /// <summary>Creates an entry.</summary>
        public SettlementEntry(long tick, string text)
        {
            Tick = tick;
            Text = text ?? string.Empty;
        }

        /// <summary>The tick the event happened on.</summary>
        public long Tick { get; }

        /// <summary>What happened. Never null.</summary>
        public string Text { get; }
    }
}
