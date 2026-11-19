using System;
using System.Collections.Generic;

namespace LordsLegacy.Sim.Core
{
    /// <summary>What part of the Sim a <see cref="SimLog"/> entry is about.</summary>
    public enum LogCategory
    {
        /// <summary>Task board and task choices.</summary>
        Tasks,
        /// <summary>Hauling and the haul board.</summary>
        Haul,
        /// <summary>Construction sites and supply.</summary>
        Construction,
        /// <summary>Food and eating.</summary>
        Food,
        /// <summary>Settlement growth and arrivals.</summary>
        Growth,
        /// <summary>Placement checks and refusals.</summary>
        Placement,
        /// <summary>Player commands.</summary>
        Commands,
        /// <summary>People decisions.</summary>
        People,
    }

    /// <summary>One <see cref="SimLog"/> entry.</summary>
    public readonly struct LogEntry
    {
        /// <summary>The Sim tick the entry was written at.</summary>
        public readonly long Tick;
        /// <summary>The entry category.</summary>
        public readonly LogCategory Category;
        /// <summary>The message.</summary>
        public readonly string Text;

        /// <summary>Creates an entry.</summary>
        public LogEntry(long tick, LogCategory category, string text)
        {
            Tick = tick;
            Category = category;
            Text = text;
        }
    }

    /// <summary>
    /// Debug ring buffer of what the Sim decided and refused (decision 0058). Not saved, never read by rules.
    /// Separate from <see cref="SettlementLog"/> (player messages). Instance per world; no static state.
    /// </summary>
    public sealed class SimLog
    {
        /// <summary>Debug capacity: the newest this many entries are kept. Not a gameplay number.</summary>
        public const int Capacity = 2000;

        private readonly Func<long> _tick;
        private readonly LogEntry[] _buffer = new LogEntry[Capacity];
        private readonly bool[] _enabled;
        private int _start;
        private int _count;

        /// <summary>Creates a log that stamps entries with <paramref name="tick"/>. All categories start enabled.</summary>
        public SimLog(Func<long> tick)
        {
            _tick = tick ?? throw new ArgumentNullException(nameof(tick));
            _enabled = new bool[Enum.GetValues(typeof(LogCategory)).Length];
            for (int i = 0; i < _enabled.Length; i++) _enabled[i] = true;
        }

        /// <summary>Number of entries held, at most <see cref="Capacity"/>.</summary>
        public int Count => _count;

        /// <summary>True when writes to <paramref name="category"/> are stored.</summary>
        public bool IsEnabled(LogCategory category) => _enabled[(int)category];

        /// <summary>Turns one category on or off. Existing entries are kept.</summary>
        public void SetEnabled(LogCategory category, bool enabled) => _enabled[(int)category] = enabled;

        /// <summary>Turns every category on or off.</summary>
        public void SetAllEnabled(bool enabled)
        {
            for (int i = 0; i < _enabled.Length; i++) _enabled[i] = enabled;
        }

        /// <summary>Stores an entry stamped with the current tick; drops the oldest when full. Does nothing if the category is off.</summary>
        public void Write(LogCategory category, string text)
        {
            if (!_enabled[(int)category]) return;
            var entry = new LogEntry(_tick(), category, text);
            if (_count < Capacity)
            {
                _buffer[(_start + _count) % Capacity] = entry;
                _count++;
            }
            else
            {
                _buffer[_start] = entry;
                _start = (_start + 1) % Capacity;
            }
        }

        /// <summary>The entry at <paramref name="index"/>, 0 being the oldest.</summary>
        public LogEntry this[int index]
        {
            get
            {
                if (index < 0 || index >= _count) throw new ArgumentOutOfRangeException(nameof(index));
                return _buffer[(_start + index) % Capacity];
            }
        }

        /// <summary>Copies entries oldest first, optionally only those of <paramref name="only"/>.</summary>
        public List<LogEntry> Snapshot(LogCategory? only = null)
        {
            var list = new List<LogEntry>(_count);
            for (int i = 0; i < _count; i++)
            {
                LogEntry e = this[i];
                if (only == null || e.Category == only.Value) list.Add(e);
            }
            return list;
        }

        /// <summary>Removes every entry.</summary>
        public void Clear()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _start = 0;
            _count = 0;
        }
    }
}
