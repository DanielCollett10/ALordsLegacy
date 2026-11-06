using System;

namespace LordsLegacy.Sim.Terrain
{
    /// <summary>
    /// Heights (metres) at the nodes of a regular grid. Node (ix,iz) sits at world
    /// (originX + ix*cellSize, originZ + iz*cellSize). Sampling is bilinear. Slope uses central
    /// differences of the sampled height one cell either side (one-sided at the edge).
    /// Queries outside <see cref="Bounds"/> are clamped to the nearest edge point.
    /// Memory: 4 bytes per node; a 6x6 km map at 2 m cells is 3001x3001 nodes = ~9.0M floats = ~36 MB.
    /// </summary>
    public sealed class HeightGrid : IHeightField
    {
        private readonly float[] _heights;
        private readonly float _originX;
        private readonly float _originZ;

        /// <summary>World x of node column 0.</summary>
        public float OriginX => _originX;
        /// <summary>World z of node row 0.</summary>
        public float OriginZ => _originZ;
        /// <summary>Distance between nodes in metres.</summary>
        public float CellSize { get; }
        /// <summary>Number of nodes along x.</summary>
        public int Width { get; }
        /// <summary>Number of nodes along z.</summary>
        public int Depth { get; }
        /// <inheritdoc />
        public Bounds2 Bounds { get; }

        /// <summary>Creates a grid. <paramref name="heights"/> is row-major (index = iz*width + ix) and is copied.</summary>
        public HeightGrid(float originX, float originZ, float cellSize, int width, int depth, float[] heights)
        {
            if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));
            if (width < 2) throw new ArgumentOutOfRangeException(nameof(width));
            if (depth < 2) throw new ArgumentOutOfRangeException(nameof(depth));
            if (heights == null) throw new ArgumentNullException(nameof(heights));
            if (heights.Length != width * depth) throw new ArgumentException("heights length must be width*depth", nameof(heights));
            _heights = (float[])heights.Clone();
            _originX = originX;
            _originZ = originZ;
            CellSize = cellSize;
            Width = width;
            Depth = depth;
            Bounds = new Bounds2(originX, originZ, originX + (width - 1) * cellSize, originZ + (depth - 1) * cellSize);
        }

        /// <summary>Height at a node.</summary>
        public float NodeHeight(int ix, int iz) => _heights[iz * Width + ix];

        /// <inheritdoc />
        public bool Contains(float x, float z) => Bounds.Contains(x, z);

        /// <inheritdoc />
        public float HeightAt(float x, float z)
        {
            float fx = (Clamp(x, Bounds.MinX, Bounds.MaxX) - _originX) / CellSize;
            float fz = (Clamp(z, Bounds.MinZ, Bounds.MaxZ) - _originZ) / CellSize;
            int ix = Math.Min((int)fx, Width - 2);
            int iz = Math.Min((int)fz, Depth - 2);
            float tx = fx - ix, tz = fz - iz;
            float h00 = _heights[iz * Width + ix], h10 = _heights[iz * Width + ix + 1];
            float h01 = _heights[(iz + 1) * Width + ix], h11 = _heights[(iz + 1) * Width + ix + 1];
            float a = h00 + (h10 - h00) * tx;
            float b = h01 + (h11 - h01) * tx;
            return a + (b - a) * tz;
        }

        /// <inheritdoc />
        public float SlopeDegAt(float x, float z)
        {
            x = Clamp(x, Bounds.MinX, Bounds.MaxX);
            z = Clamp(z, Bounds.MinZ, Bounds.MaxZ);
            float x0 = Math.Max(x - CellSize, Bounds.MinX), x1 = Math.Min(x + CellSize, Bounds.MaxX);
            float z0 = Math.Max(z - CellSize, Bounds.MinZ), z1 = Math.Min(z + CellSize, Bounds.MaxZ);
            float gx = (HeightAt(x1, z) - HeightAt(x0, z)) / (x1 - x0);
            float gz = (HeightAt(x, z1) - HeightAt(x, z0)) / (z1 - z0);
            double g = Math.Sqrt((double)gx * gx + (double)gz * gz);
            return (float)(Math.Atan(g) * 180.0 / Math.PI);
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
