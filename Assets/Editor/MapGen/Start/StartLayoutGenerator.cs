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
            // The main object's name must match the file name, or Unity logs "Main Object Name ... does not match filename".
            cfg.name = Path.GetFileNameWithoutExtension(assetPath);

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
                    // GR-24b: the trunk road must reach the nearest reachable map edge (it is the incoming road).
                    if (!ExtendToEdge(world, cfg)) continue;
                    // Final proof: the whole layout applies to a fresh world.
                    StartLayoutApplier.Apply(SimWorld.FromMapData(map, new SimTuning()), cfg.ToLayout());
                    return cfg;
                }
            }
            throw new InvalidOperationException("No flat spot near the forest edge fits the start layout.");
        }

        /// <summary>Distance between control points while extending, metres.</summary>
        private const float ExtendStepM = 40f;
        /// <summary>The last control point sits this far inside the edge so it stays on the map and within the edge tolerance.</summary>
        private const float EdgeInsetM = 0.5f;
        private const int MaxSearchNodes = 20000;
        private static readonly float[] TurnDeg = { 0f, 15f, -15f, 30f, -30f, 45f, -45f, 60f, -60f, 75f, -75f };

        /// <summary>
        /// Extends the trunk road from the end nearer a map edge to that edge, bending around ground
        /// <see cref="RoadValidator"/> refuses. Tries the nearest edge first, then the next nearest. Deterministic.
        /// Returns false (config unchanged) when no edge can be reached.
        /// </summary>
        private static bool ExtendToEdge(SimWorld world, StartLayoutConfig cfg)
        {
            var b = world.Terrain.Bounds;
            float minX = b.MinX + EdgeInsetM, maxX = b.MaxX - EdgeInsetM, minZ = b.MinZ + EdgeInsetM, maxZ = b.MaxZ - EdgeInsetM;
            var road = cfg.trunkRoadM;
            // Candidates: (end index, edge direction as unit axis, distance). Each end only heads for edges it does not already run along.
            var cands = new List<(bool atEnd, int axis, int sign, float dist)>();
            for (int e = 0; e < 2; e++)
            {
                Vector2 p = e == 0 ? road[0] : road[road.Count - 1];
                cands.Add((e == 1, 0, -1, p.x - minX)); cands.Add((e == 1, 0, 1, maxX - p.x));
                cands.Add((e == 1, 1, -1, p.y - minZ)); cands.Add((e == 1, 1, 1, maxZ - p.y));
            }
            foreach (var c in cands.OrderBy(c => c.dist).ThenBy(c => c.atEnd).ThenBy(c => c.axis).ThenBy(c => c.sign))
            {
                Vector2 start = c.atEnd ? road[road.Count - 1] : road[0];
                float target = c.axis == 0 ? (c.sign < 0 ? minX : maxX) : (c.sign < 0 ? minZ : maxZ);
                var path = new List<Vector2>();
                int budget = MaxSearchNodes;
                if (!Search(world, road, c.atEnd, start, c.axis, c.sign, target, path, ref budget)) continue;
                if (c.atEnd) road.AddRange(path);
                else road.InsertRange(0, Enumerable.Reverse(path));
                return true;
            }
            return false;
        }

        // Depth-first search for control points leading from 'from' to the edge line. 'added' holds the points chosen so far.
        private static bool Search(SimWorld world, List<Vector2> road, bool atEnd, Vector2 from, int axis, int sign, float target,
            List<Vector2> added, ref int budget)
        {
            if (budget-- <= 0) return false;
            float remaining = Math.Abs((axis == 0 ? from.x : from.y) - target);
            var dir0 = axis == 0 ? new Vector2(sign, 0) : new Vector2(0, sign);
            foreach (float deg in TurnDeg)
            {
                float rad = deg * Mathf.Deg2Rad;
                var dir = new Vector2(dir0.x * Mathf.Cos(rad) - dir0.y * Mathf.Sin(rad), dir0.x * Mathf.Sin(rad) + dir0.y * Mathf.Cos(rad));
                float progress = axis == 0 ? dir.x * sign : dir.y * sign;
                Vector2 next;
                bool last = remaining <= ExtendStepM * progress;
                if (last)
                {
                    float t = remaining / progress;
                    next = from + dir * t;
                    // Snap the final point exactly onto the inset edge line.
                    if (axis == 0) next.x = target; else next.y = target;
                }
                else next = from + dir * ExtendStepM;
                next = new Vector2((float)Math.Round(next.x, 1), (float)Math.Round(next.y, 1));
                if (last) { if (axis == 0) next.x = target; else next.y = target; }

                added.Add(next);
                if (RoadValid(world, road, atEnd, added) && (last || Search(world, road, atEnd, next, axis, sign, target, added, ref budget)))
                    return true;
                added.RemoveAt(added.Count - 1);
            }
            return false;
        }

        private static bool RoadValid(SimWorld world, List<Vector2> road, bool atEnd, List<Vector2> added)
        {
            var pts = new List<Vector2>(road);
            if (atEnd) pts.AddRange(added); else pts.InsertRange(0, Enumerable.Reverse(added));
            var mm = pts.Select(p => new PointMm((long)Math.Round(p.x * 1000.0), (long)Math.Round(p.y * 1000.0))).ToList();
            return RoadValidator.Validate(world, mm, RoadTypes.DirtPathId).IsValid;
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
