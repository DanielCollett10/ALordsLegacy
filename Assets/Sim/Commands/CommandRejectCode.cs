namespace LordsLegacy.Sim.Commands
{
    /// <summary>Why the Sim refused a command. Stable numeric values are not promised yet.</summary>
    public enum CommandRejectCode
    {
        /// <summary>A command referred to something that does not exist.</summary>
        NotFound = 1,
        /// <summary>The command's arguments are malformed (e.g. too few points, bad type id).</summary>
        InvalidArgument = 2,
        /// <summary>The command clashes with existing state (e.g. overlaps another area).</summary>
        Conflict = 3,
        /// <summary>The rule forbids the action in the current state.</summary>
        NotAllowed = 4,
    }
}
