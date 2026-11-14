using System;

namespace LordsLegacy.Sim.Save
{
    public class SaveFormatException : Exception
    {
        public SaveFormatException(string message) : base(message) { }
    }
}