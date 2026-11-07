using System;

namespace LordsLegacy.Prototypes.PerfSpike.Grid
{
    /// <summary>
    /// THROWAWAY (T-007). Builds a seeded synthetic cost grid: noise hills (sum of smooth bumps) give a
    /// height field; cost = 1 + SlopeWeight * gradient, so cost rises with slope; a few straight-ish
    /// polyline "roads" lower the cost to RoadCost. Same seed and size always give the same grid.
    /// Default: 6 km x 6 km at 2 m cells = 3000 x 3000 cells.
    /// </summary>
    public static class SpikeMapGenerator
    {
        /// <summary>Default map side in metres (6 km).</summary>
        public const float DefaultSideMetres = 6000f;
        /// <summary>Default cell size in metres.</summary>
        public const float DefaultCellSize = 2f;
        /// <summary>Default number of cells per side (3000).</summary>
        public const int DefaultCells = 3000;

        /// <summary>Number of hills laid down on a 6 km map.</summary>
        public const int HillCount = 60;
        /// <summary>Cost added per unit of gradient (rise over run). 0.5 gradient = cost 6 at weight 10.</summary>
        public const float SlopeWeight = 10f;
        /// <summary>Cost of a road cell (cheaper than flat ground, which costs 1).</summary>
        public const float RoadCost = 0.4f;
        /// <summary>Number of road polylines.</summary>
        public const int RoadCount = 4;

        /// <summary>Builds the default 3000 x 3000 map at 2 m cells.</summary>
        public static SpikeCostGrid Generate(ulong seed)
        {
            return Generate(seed, DefaultCells, DefaultCells, DefaultCellSize);
        }

        /// <summary>Builds a map of width x height cells of <paramref name="cellSize"/> metres, from <paramref name="seed"/>.</summary>
        public static SpikeCostGrid Generate(ulong seed, int width, int height, float cellSize)
        {
            if (width < 3) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 3) throw new ArgumentOutOfRangeException(nameof(height));
            if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));

            var rng = new SpikeRng(seed);
            float[] heights = BuildHeights(rng, width, height, cellSize);
            float[] costs = BuildCosts(heights, width, height, cellSize);
            AddRoads(rng, costs, width, height, cellSize);
            return new SpikeCostGrid(width, height, cellSize, costs);
        }

        /// <summary>Heights in metres from seeded smooth hills. Scales the radius with the map side.</summary>
        private static float[] BuildHeights(SpikeRng rng, int width, int height, float cellSize)
        {
            float sideX = width * cellSize;
            float sideZ = height * cellSize;
            float scale = Math.Max(sideX, sideZ) / DefaultSideMetres;
            var heights = new float[width * height];

            for (int h = 0; h < HillCount; h++)
            {
                float cx = rng.Range(0f, sideX);
                float cz = rng.Range(0f, sideZ);
                float radius = rng.Range(150f, 700f) * scale;
                float peak = rng.Range(10f, 120f);
                float r2 = radius * radius;

                int x0 = Math.Max(0, (int)Math.Floor((cx - radius) / cellSize));
                int x1 = Math.Min(width - 1, (int)Math.Ceiling((cx + radius) / cellSize));
                int z0 = Math.Max(0, (int)Math.Floor((cz - radius) / cellSize));
                int z1 = Math.Min(height - 1, (int)Math.Ceiling((cz + radius) / cellSize));

                for (int z = z0; z <= z1; z++)
                {
                    float dz = z * cellSize - cz;
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = x * cellSize - cx;
                        float d2 = dx * dx + dz * dz;
                        if (d2 >= r2) continue;
                        float t = 1f - d2 / r2;
                        heights[z * width + x] += peak * t * t;
                    }
                }
            }
            return heights;
        }

        /// <summary>Flat ground costs 1; cost rises with the central-difference gradient.</summary>
        private static float[] BuildCosts(float[] heights, int width, int height, float cellSize)
        {
            var costs = new float[width * height];
            float inv2 = 1f / (2f * cellSize);
            for (int z = 0; z < height; z++)
            {
                int zm = z == 0 ? 0 : z - 1;
                int zp = z == height - 1 ? z : z + 1;
                float zScale = (z == 0 || z == height - 1) ? 1f / (cellSize * (zp - zm)) : inv2;
                for (int x = 0; x < width; x++)
                {
                    int xm = x == 0 ? 0 : x - 1;
                    int xp = x == width - 1 ? x : x + 1;
                    float xScale = (x == 0 || x == width - 1) ? 1f / (cellSize * (xp - xm)) : inv2;
                    float gx = (heights[z * width + xp] - heights[z * width + xm]) * xScale;
                    float gz = (heights[zp * width + x] - heights[zm * width + x]) * zScale;
                    float grad = (float)Math.Sqrt(gx * gx + gz * gz);
                    costs[z * width + x] = 1f + SlopeWeight * grad;
                }
            }
            return costs;
        }

        /// <summary>Lays RoadCount polylines of 3 legs each; each cell they touch becomes RoadCost.</summary>
        private static void AddRoads(SpikeRng rng, float[] costs, int width, int height, float cellSize)
        {
            float sideX = width * cellSize;
            float sideZ = height * cellSize;
            float legLength = 0.15f * Math.Max(sideX, sideZ);

            for (int r = 0; r < RoadCount; r++)
            {
                float px = rng.Range(0f, sideX);
                float pz = rng.Range(0f, sideZ);
                float heading = rng.Range(0f, (float)(2.0 * Math.PI));
                for (int leg = 0; leg < 3; leg++)
                {
                    float len = legLength * rng.Range(0.6f, 1.4f);
                    float nx = px + (float)Math.Cos(heading) * len;
                    float nz = pz + (float)Math.Sin(heading) * len;
                    RasteriseLeg(costs, width, height, cellSize, px, pz, nx, nz);
                    px = Clamp(nx, 0f, sideX - cellSize * 0.5f);
                    pz = Clamp(nz, 0f, sideZ - cellSize * 0.5f);
                    heading += rng.Range(-0.8f, 0.8f);
                }
            }
        }

        /// <summary>Marks every cell a straight leg crosses. Samples at half-cell steps so the cells stay 8-connected.</summary>
        private static void RasteriseLeg(float[] costs, int width, int height, float cellSize, float ax, float az, float bx, float bz)
        {
            float dx = bx - ax, dz = bz - az;
            float length = (float)Math.Sqrt(dx * dx + dz * dz);
            int steps = Math.Max(1, (int)Math.Ceiling(length / (cellSize * 0.5f)));
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                int cx = (int)Math.Floor((ax + dx * t) / cellSize);
                int cz = (int)Math.Floor((az + dz * t) / cellSize);
                if (cx < 0 || cz < 0 || cx >= width || cz >= height) continue;
                int idx = cz * width + cx;
                if (costs[idx] > RoadCost) costs[idx] = RoadCost;
            }
        }

        private static float Clamp(float v, float lo, float hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }
    }
}
