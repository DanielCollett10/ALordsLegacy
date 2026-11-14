using System;

namespace LordsLegacy.Sim.Save
{
    /// <summary>Thrown when save bytes are not a readable save: bad magic, unknown version, truncated or corrupt data.</summary>
    public sealed class SaveFormatException : Exception
    {
        /// <summary>Creates the exception with a message.</summary>
        public SaveFormatException(string message) : base(message) { }
    }
}
