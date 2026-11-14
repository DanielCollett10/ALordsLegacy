using System;

namespace LordsLegacy.Sim.Save
{
    /// <summary>
    /// Thrown when a save was made on a different map (id) or a different version of the map (content hash), so it is
    /// refused rather than loaded with a warning (saving.md SV-30).
    /// </summary>
    public sealed class SaveMapMismatchException : Exception
    {
        /// <summary>The player-facing reason text from saving.md SV-30.</summary>
        public const string Reason = "This save was made on a different version of the test map";

        /// <summary>Map id stored in the save.</summary>
        public string SavedMapId { get; }

        /// <summary>Map content hash stored in the save.</summary>
        public ulong SavedMapHash { get; }

        /// <summary>Creates the exception for a save whose map id or hash does not match the map offered.</summary>
        public SaveMapMismatchException(string savedMapId, ulong savedMapHash, string mapId, ulong mapHash)
            : base(Reason + " (save: '" + savedMapId + "' #" + savedMapHash.ToString("x16") + ", map: '" + mapId + "' #" + mapHash.ToString("x16") + ").")
        {
            SavedMapId = savedMapId;
            SavedMapHash = savedMapHash;
        }
    }
}
