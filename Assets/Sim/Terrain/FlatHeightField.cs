namespace LordsLegacy.Sim.Terrain
{
    /// <summary>Height 0 and slope 0 everywhere; used by a world with no map. Bounds are empty, but Contains is always true.</summary>
    public sealed class FlatHeightField : IHeightField
    {
        /// <inheritdoc />
        public Bounds2 Bounds => Bounds2.Empty;
        /// <inheritdoc />
        public bool Contains(float x, float z) => true;
        /// <inheritdoc />
        public float HeightAt(float x, float z) => 0f;
        /// <inheritdoc />
        public float SlopeDegAt(float x, float z) => 0f;
    }
}
