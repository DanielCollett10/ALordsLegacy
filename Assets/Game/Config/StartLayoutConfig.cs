using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Start;
using UnityEngine;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Authoring asset for a new world's start layout (camp.md CMP-01..08). Converts to the Sim's
    /// <see cref="StartLayout"/>; <see cref="SimHost"/> applies it after building the world.
    /// Positions are metres; points marked "relative" are in the camp's own frame and rotate with it.
    /// </summary>
    [CreateAssetMenu(fileName = "StartLayout", menuName = "Lords Legacy/Start Layout")]
    public sealed class StartLayoutConfig : ScriptableObject
    {
        /// <summary>One citizen.</summary>
        [Serializable]
        public sealed class CitizenConfig
        {
            /// <summary>Given name.</summary>
            public string name;
            /// <summary>Sex.</summary>
            public Sex sex;
            [Tooltip("Birth tick; negative = born before the game started (one tick = one game minute)")]
            public long birthTick;
        }

        /// <summary>One family.</summary>
        [Serializable]
        public sealed class FamilyConfig
        {
            /// <summary>Family name.</summary>
            public string surname;
            [Tooltip("Tent spot relative to the camp centre, metres")] public Vector2 tentSpotM;
            /// <summary>Members.</summary>
            public List<CitizenConfig> citizens = new List<CitizenConfig>();
        }

        [Tooltip("Camp centre in world metres")] public Vector2 campCentreM;
        [Tooltip("Camp rotation, degrees counter-clockwise")] public float campRotationDegrees;
        [Tooltip("Fire, relative to the camp centre, metres")] public Vector2 fireM;
        [Tooltip("Stockpile spot, relative, metres")] public Vector2 stockpileM = new Vector2(6f, 0f);
        [Tooltip("Ox stand, relative, metres")] public Vector2 oxStandM = new Vector2(-6f, 0f);
        [Tooltip("Trunk road control points, world metres")] public List<Vector2> trunkRoadM = new List<Vector2>();
        [Tooltip("Trunk road type id")] public string trunkRoadTypeId = RoadTypes.DirtPathId;
        /// <summary>Families.</summary>
        public List<FamilyConfig> families = new List<FamilyConfig>();
        [Tooltip("Oxen at start")] [Range(0, 50)] public int oxCount = 2;

        /// <summary>Builds the Sim start layout.</summary>
        public StartLayout ToLayout()
        {
            var road = new List<PointMm>();
            if (trunkRoadM != null) foreach (Vector2 p in trunkRoadM) road.Add(Mm(p));
            var fams = new List<StartFamily>();
            if (families != null)
                foreach (FamilyConfig f in families)
                {
                    if (f == null) continue;
                    var cits = new List<StartCitizen>();
                    if (f.citizens != null)
                        foreach (CitizenConfig c in f.citizens)
                            if (c != null) cits.Add(new StartCitizen(c.name, c.sex, c.birthTick));
                    fams.Add(new StartFamily(f.surname, Mm(f.tentSpotM), cits));
                }
            return new StartLayout(Mm(campCentreM), campRotationDegrees, Mm(fireM), Mm(stockpileM), Mm(oxStandM),
                road, fams, oxCount, string.IsNullOrEmpty(trunkRoadTypeId) ? RoadTypes.DirtPathId : trunkRoadTypeId);
        }

        private static PointMm Mm(Vector2 m) =>
            new PointMm((long)Math.Round((double)(decimal)m.x * 1000.0), (long)Math.Round((double)(decimal)m.y * 1000.0));
    }
}
