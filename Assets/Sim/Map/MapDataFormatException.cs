using System;

namespace LordsLegacy.Sim.Map
{
    /// <summary>Thrown when a map file is not valid or has an unsupported version.</summary>
    public sealed class MapDataFormatException : Exception
    {
        /// <summary>Creates the exception with a message.</summary>
        public MapDataFormatException(string message) : base(message) { }
    }
}
