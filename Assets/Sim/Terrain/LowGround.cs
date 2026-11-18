using System;

namespace LordsLegacy.Sim.Terrain
{
    /// <summary>
    /// Low flat ground (MAT-39a): below a height percentile of the map and gently sloped. Shared by willow stools
    /// and, later, reed beds. Build once per terrain; the threshold comes from a fixed sample grid, so it is deterministic.
    /// </summary>
    public sealed class LowGround
    {
        private const int Samples = 64;
        /// <summary>Height percentile limit (MAT-39a).</summary>
        public const double Percentile = 0.25;
        /// <summary>Slope limit in degrees (MAT-39a).</summary>
        public const double MaxSlopeDeg = 3;
        private readonly IHeightField _terrain;
        private readonly double _maxSlopeDeg;

        /// <summary>Low ground with the MAT-39a defaults (25th percentile, 3 degrees).</summary>
        public LowGround(IHeightField terrain) : this(terrain, Percentile, MaxSlopeDeg) { }

        /// <summary>Height below which ground counts as low. Unit: metres.</summary>
        public float HeightLimit { get; }

        /// <summary>Samples the terrain to find the percentile height (0 to 1, e.g. 0.25).</summary>
        public LowGround(IHeightField terrain, double percentile, double maxSlopeDeg)
        {
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            _maxSlopeDeg = maxSlopeDeg;
            Bounds2 b = terrain.Bounds;
            if (b.IsEmpty) { HeightLimit = float.NegativeInfinity; return; }
            var h = new float[Samples * Samples];
            for (int iz = 0; iz < Samples; iz++)
                for (int ix = 0; ix < Samples; ix++)
                    h[iz * Samples + ix] = terrain.HeightAt(
                        b.MinX + (b.MaxX - b.MinX) * ix / (Samples - 1),
                        b.MinZ + (b.MaxZ - b.MinZ) * iz / (Samples - 1));
            Array.Sort(h);
            double p = Math.Max(0.0, Math.Min(1.0, percentile));
            HeightLimit = h[(int)(p * (h.Length - 1))];
        }

        /// <summary>True when (x, z) is at or below the height limit and no steeper than the slope limit.</summary>
        public bool Contains(float x, float z) =>
            _terrain.HeightAt(x, z) <= HeightLimit && _terrain.SlopeDegAt(x, z) <= _maxSlopeDeg;
    }
}
