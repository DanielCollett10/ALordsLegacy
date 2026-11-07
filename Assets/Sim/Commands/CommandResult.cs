using System;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// Outcome of <see cref="ISimCommand.Validate"/>, <see cref="ISimCommand.Apply"/> or
    /// <see cref="Core.SimWorld.Execute"/>. Immutable. Accepted results list the ids the command
    /// created and removed; rejected results carry a reason code and a human-readable text.
    /// </summary>
    public sealed class CommandResult
    {
        private static readonly long[] NoIds = Array.Empty<long>();

        /// <summary>True if the command was applied.</summary>
        public bool IsAccepted { get; }

        /// <summary>True if the command was refused and changed nothing.</summary>
        public bool IsRejected => !IsAccepted;

        /// <summary>Why the command was refused. Meaningful only when <see cref="IsRejected"/>.</summary>
        public CommandRejectCode Reason { get; }

        /// <summary>Human-readable explanation of the rejection. Empty when accepted.</summary>
        public string Text { get; }

        /// <summary>Ids created by the command. Empty when rejected.</summary>
        public long[] Created { get; }

        /// <summary>Ids removed by the command. Empty when rejected.</summary>
        public long[] Removed { get; }

        private CommandResult(bool accepted, CommandRejectCode reason, string text, long[] created, long[] removed)
        {
            IsAccepted = accepted;
            Reason = reason;
            Text = text ?? string.Empty;
            Created = created;
            Removed = removed;
        }

        /// <summary>An accepted result, with optional created and removed ids (copied).</summary>
        public static CommandResult Accepted(long[] created = null, long[] removed = null) =>
            new CommandResult(true, default, string.Empty,
                created == null || created.Length == 0 ? NoIds : (long[])created.Clone(),
                removed == null || removed.Length == 0 ? NoIds : (long[])removed.Clone());

        /// <summary>A rejected result with a reason code and text. Ids are always empty.</summary>
        public static CommandResult Rejected(CommandRejectCode reason, string text) =>
            new CommandResult(false, reason, text, NoIds, NoIds);

        /// <summary>Short form for logs and debug views.</summary>
        public override string ToString() =>
            IsAccepted ? "Accepted" : "Rejected(" + Reason + "): " + Text;
    }
}
