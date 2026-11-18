using System;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;

namespace LordsLegacy.Sim.Foraging
{
    /// <summary>
    /// New-game scatter of forage bushes (FD-20). Uses its own <see cref="SimRandom"/> seeded from
    /// <c>WorldSeed</c>, so it never moves the world's main random stream and the map is not re-baked (decision 0025).
    /// A spot is accepted when it is on walkable ground, clear of tree trunks, and either at a forest edge (a tree
    /// within <c>BushEdgeM</c>) or, with chance <c>BushClearingChance</c>, in a clearing.
    /// </summary>
    public static class BushScatter
    {
        private const ulong SeedSalt = 0xB05B05B05UL;

        /// <summary>Fills <see cref="SimWorld.Bushes"/> from the tuning and the world's terrain and trees.</summary>
        public static void Run(SimWorld world)
        {
            SimTuning t = world.Tuning;
            Bounds2 b = world.Terrain.Bounds;
            if (b.IsEmpty || t.BushCount <= 0) return;
            var rng = new SimRandom(unchecked((ulong)(long)t.WorldSeed) ^ SeedSalt);
            int attempts = t.BushCount * 40;
            for (int i = 0; i < attempts && world.Bushes.Count < t.BushCount; i++)
            {
                float x = b.MinX + (float)(rng.NextDouble() * (b.MaxX - b.MinX));
                float z = b.MinZ + (float)(rng.NextDouble() * (b.MaxZ - b.MinZ));
                double roll = rng.NextDouble();
                int kindRoll = rng.NextInt(0, 2);
                if (world.Terrain.SlopeDegAt(x, z) > t.MaxWalkSlopeDeg) continue;
                if (world.Trees.QueryRadius(x, z, (float)t.BushTreeClearM).Count > 0) continue;
                bool edge = world.Trees.QueryRadius(x, z, (float)t.BushEdgeM).Count > 0;
                if (!edge && roll >= t.BushClearingChance) continue;
                world.Bushes.Add(x, z, (BushKind)kindRoll, t.BushBaskets);
            }
            ScatterStools(world, rng, b);
        }

        /// <summary>
        /// Hazel stools at the forest edge, willow stools on <see cref="LowGround"/> (decision 0050, MAT-13/15/16).
        /// Runs after the forage pass on the same random stream, so forage bushes keep their positions.
        /// </summary>
        private static void ScatterStools(SimWorld world, SimRandom rng, Bounds2 b)
        {
            SimTuning t = world.Tuning;
            LowGround low = null;
            Place(world, rng, b, BushKind.HazelStool, t.HazelStoolCount, null);
            if (t.WillowStoolCount > 0) low = new LowGround(world.Terrain);
            Place(world, rng, b, BushKind.WillowStool, t.WillowStoolCount, low);
        }

        private static void Place(SimWorld world, SimRandom rng, Bounds2 b, BushKind kind, int count, LowGround low)
        {
            SimTuning t = world.Tuning;
            int placed = 0;
            int attempts = count * 40;
            for (int i = 0; i < attempts && placed < count; i++)
            {
                float x = b.MinX + (float)(rng.NextDouble() * (b.MaxX - b.MinX));
                float z = b.MinZ + (float)(rng.NextDouble() * (b.MaxZ - b.MinZ));
                if (world.Terrain.SlopeDegAt(x, z) > t.MaxWalkSlopeDeg) continue;
                if (world.Trees.QueryRadius(x, z, (float)t.BushTreeClearM).Count > 0) continue;
                if (low != null) { if (!low.Contains(x, z)) continue; }
                else if (world.Trees.QueryRadius(x, z, (float)t.BushEdgeM).Count == 0) continue;
                world.Bushes.Add(x, z, kind, BushKinds.FullUnits(kind, t));
                placed++;
            }
        }
    }
}
