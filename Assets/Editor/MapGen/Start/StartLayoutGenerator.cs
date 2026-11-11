using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LordsLegacy.Game;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Tuning;
using UnityEditor;
using UnityEngine;

namespace LordsLegacy.EditorTools
{
    /// <summary>
    /// Writes <c>Assets/Maps/TestMap/StartLayout.asset</c> (camp.md CMP-08): finds a flat spot (at most 5 degrees)
    /// just outside the forest edge with trees within 50 m, and a trunk road running past the camp that passes
    /// <see cref="RoadValidator"/>. Deterministic and idempotent: the same map gives the same asset.
    /// </summary>
    public static class StartLayoutGenerator
    {
        /// <summary>Output asset path.</summary>
        public const string AssetPath = ForestScatter.Folder + "/StartLayout.asset";
        /// <summary>Steepest slope allowed under the camp, degrees.</summary>
        public const double MaxCampSlopeDeg = 5.0;
        /// <summary>Radius in which trees must stand, metres.</summary>
        public const float TreeRadiusM = 50f;
        /// <summary>Fewest trees within <see cref="TreeRadiusM"/>.</summary>
        public const int MinTrees = 50;

        private const long TicksPerDay = 1440;
        private const long TicksPerAgedYear = 365 * 1440 / 2; // POP-27: 2x ageing, so one aged year = half a calendar year

        [MenuItem("LordsLegacy/Map/Generate Start Layout")]
        public static void GenerateMenu()
        {
            var cfg = Generate(Path.Combine(Application.streamingAssetsPath, "Maps", SimHost.DefaultMapFile), AssetPath);
            Debug.Log("Start layout written to " + AssetPath + " (camp at " + cfg.campCentreM + ").");
        }

        /// <summary>Finds the spot on the baked map at <paramref name="mapPath"/> and writes (or updates in place) the asset.</summary>
        public static StartLayoutConfig Generate(string mapPath, string assetPath)
        {
            MapData map = MapDataIO.Read(File.ReadAllBytes(mapPath));
            ForestRegion region = ForestScatter.ReadRegion(ForestScatter.RegionPath);
            var cfg = Build(map, region);

            var asset = AssetDatabase.LoadAssetAtPath<StartLayoutConfig>(assetPath);
            if (asset == null)
            {
                asset = cfg;
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            else
            {
                EditorUtility.CopySerialized(cfg, asset);
                UnityEngine.Object.DestroyImmediate(cfg);
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>Pure search: returns a new unsaved config for the map and forest region. Throws if no spot fits.</summary>
        public static StartLayoutConfig Build(MapData map, ForestRegion region)
        {
            SimWorld world = SimWorld.FromMapData(map, new SimTuning());
            float midZ = (region.minZ + region.maxZ) * 0.5f;
            var zs = new List<int>();
            for (int z = (int)Math.Ceiling(region.minZ) + 20; z <= region.maxZ - 20; z += 8) zs.Add(z);
            zs = zs.OrderBy(z => Math.Abs(z - midZ)).ThenBy(z => z).ToList();

            // West edge of the forest: camp centre d metres outside it. Camp half width 12 m plus road at +14 m.
            for (int d = 20; d <= 80; d += 4)
            {
                int x = (int)Math.Floor(region.minX) - d;
                foreach (int z in zs)
                {
                    if (!CampFlat(world, x, z)) continue;
                    if (world.Trees.QueryRadius(x, z, TreeRadiusM).Count < MinTrees) continue;
                    var cfg = Make(x, z);
                    var road = cfg.trunkRoadM.Select(p => new PointMm((long)(p.x * 1000), (long)(p.y * 1000))).ToList();
                    if (!RoadValidator.Validate(world, road, RoadTypes.DirtPathId).IsValid) continue;
                    // Final proof: the whole layout applies to a fresh world.
                    StartLayoutApplier.Apply(SimWorld.FromMapData(map, new SimTuning()), cfg.ToLayout());
                    return cfg;
                }
            }
            throw new InvalidOperationException("No flat spot near the forest edge fits the start layout.");
        }

        private static bool CampFlat(SimWorld world, int cx, int cz)
        {
            for (int dx = -13; dx <= 13; dx += 2)
                for (int dz = -9; dz <= 9; dz += 2)
                    if (!world.Terrain.Contains(cx + dx, cz + dz) || world.Terrain.SlopeDegAt(cx + dx, cz + dz) > MaxCampSlopeDeg)
                        return false;
            return true;
        }

        private static StartLayoutConfig Make(int cx, int cz)
        {
            var cfg = ScriptableObject.CreateInstance<StartLayoutConfig>();
            cfg.campCentreM = new Vector2(cx, cz);
            cfg.campRotationDegrees = 0f;
            cfg.fireM = Vector2.zero;
            cfg.stockpileM = new Vector2(6f, 0f);
            cfg.oxStandM = new Vector2(-6f, 0f);
            cfg.oxCount = 2;
            cfg.trunkRoadTypeId = RoadTypes.DirtPathId;
            // N-S road along the camp's forest side, just outside its 24 m wide footprint.
            cfg.trunkRoadM = new List<Vector2>
            {
                new Vector2(cx + 14, cz - 40), new Vector2(cx + 14, cz), new Vector2(cx + 14, cz + 40),
            };
            cfg.families = new List<StartLayoutConfig.FamilyConfig>
            {
                Family("Ashford", -9f, -5f, C("Edmund", Sex.Male, 34, 12), C("Mary", Sex.Female, 31, 200), C("Tom", Sex.Male, 15, 88), C("Anne", Sex.Female, 9, 301)),
                Family("Marsh", -9f, 5f, C("John", Sex.Male, 40, 45), C("Alice", Sex.Female, 36, 150), C("Will", Sex.Male, 17, 260)),
                Family("Thorne", 9f, 5f, C("Hugh", Sex.Male, 29, 99), C("Joan", Sex.Female, 27, 333), C("Kate", Sex.Female, 16, 17)),
            };
            return cfg;
        }

        private static StartLayoutConfig.FamilyConfig Family(string surname, float tx, float tz, params StartLayoutConfig.CitizenConfig[] members) =>
            new StartLayoutConfig.FamilyConfig { surname = surname, tentSpotM = new Vector2(tx, tz), citizens = members.ToList() };

        /// <summary>Citizen aged <paramref name="agedYears"/> (POP-27 years) plus <paramref name="extraDays"/> on the start date.</summary>
        private static StartLayoutConfig.CitizenConfig C(string name, Sex sex, int agedYears, int extraDays) =>
            new StartLayoutConfig.CitizenConfig { name = name, sex = sex, birthTick = -(agedYears * TicksPerAgedYear + extraDays * TicksPerDay) };
    }
}
