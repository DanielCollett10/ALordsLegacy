namespace LordsLegacy.Sim.Save
{
    /// <summary>
    /// The save header (decision 0034): read alone, without the sections, to list saves and find their map.
    /// </summary>
    public sealed class SaveHeader
    {
        /// <summary>Player-visible save name.</summary>
        public string Name { get; }
        /// <summary>Real time the save was made, as an int64 (e.g. Unix seconds) supplied by the caller. The Sim never reads a clock.</summary>
        public long CreatedRealTime { get; }
        /// <summary>Game tick at the moment of saving.</summary>
        public long GameTick { get; }
        /// <summary>Id of the map the save was made on.</summary>
        public string MapId { get; }
        /// <summary>FNV-1a content hash of the map (<c>MapData.ContentHash</c>).</summary>
        public ulong MapContentHash { get; }

        /// <summary>Creates a header.</summary>
        public SaveHeader(string name, long createdRealTime, long gameTick, string mapId, ulong mapContentHash)
        {
            Name = name ?? throw new System.ArgumentNullException(nameof(name));
            CreatedRealTime = createdRealTime;
            GameTick = gameTick;
            MapId = mapId ?? throw new System.ArgumentNullException(nameof(mapId));
            MapContentHash = mapContentHash;
        }
    }
}
