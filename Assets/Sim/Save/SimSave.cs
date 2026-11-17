using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Save.Sections;

namespace LordsLegacy.Sim.Save
{
    /// <summary>
    /// Writes and reads a whole save (decision 0034): header, Core section, then every <see cref="SaveSections"/>
    /// entry in order. Reading builds the world with <c>SimWorld.ForRestore</c>, reads the sections, then runs a full nav sync.
    /// </summary>
    public static class SimSave
    {
        /// <summary>Writes <paramref name="world"/> to bytes. The map id and hash identify the map the world was made on.</summary>
        public static byte[] Write(SimWorld world, string name, long createdRealTime, string mapId, ulong mapContentHash)
        {
            return Write(world, name, createdRealTime, mapId, mapContentHash, SaveSections.CreateDefault());
        }

        internal static byte[] Write(SimWorld world, string name, long createdRealTime, string mapId, ulong mapContentHash,
            IReadOnlyList<ISaveSection> sections)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var w = new SaveWriter(new SaveHeader(name, createdRealTime, world.Tick, mapId, mapContentHash));
            CoreSection.Write(w, new CoreState
            {
                Tick = world.Tick,
                Paused = world.Paused,
                RandomState = world.Random.State,
                Tuning = world.Tuning,
                RoadTypes = world.RoadTypes,
                AreaTypes = world.AreaTypes,
                EntryPoint = world.EntryPoint,
                LastArrivalTick = world.LastArrivalTick,
                SettlementEntries = new List<SettlementEntry>(world.SettlementLog.Entries)
            });
            for (int i = 0; i < sections.Count; i++)
            {
                w.BeginSection(sections[i].Id);
                sections[i].Write(world, w);
                w.EndSection();
            }
            return w.ToArray();
        }

        /// <summary>Reads a save back into a new world built on <paramref name="map"/>. Throws <see cref="SaveFormatException"/> on bad data.</summary>
        public static SimWorld Read(byte[] bytes, MapData map)
        {
            return Read(bytes, map, SaveSections.CreateDefault());
        }

        internal static SimWorld Read(byte[] bytes, MapData map, IReadOnlyList<ISaveSection> sections)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            var r = SaveReader.Open(bytes, out SaveHeader header);
            if (header.MapId != map.MapId || header.MapContentHash != map.ContentHash)
                throw new SaveMapMismatchException(header.MapId, header.MapContentHash, map.MapId, map.ContentHash);
            if (!r.NextSection(out int coreId, out SaveReader coreBody) || coreId != CoreSection.Id)
                throw new SaveFormatException("Save has no Core section.");
            var world = SimWorld.ForRestore(map, CoreSection.Read(coreBody));
            for (int i = 0; i < sections.Count; i++)
            {
                if (!r.NextSection(out int id, out SaveReader body) || id != sections[i].Id)
                    throw new SaveFormatException("Expected section " + sections[i].Id + " (number " + (i + 1) + " after Core) in save.");
                sections[i].Read(world, body);
            }
            world.Nav.Sync(world);
            return world;
        }
    }
}
