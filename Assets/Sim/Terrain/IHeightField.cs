namespace LordsLegacy.Sim.Terrain
{
    /// <summary>
    /// Read-only ground shape, queried in world metres (decision 0003: Sim x/z = Unity x/z, height = Unity y).
    /// Sim code must depend on this interface, never on a concrete class, so a tiled version can replace HeightGrid.
    /// </summary>
    public interface IHeightField
    {
        /// <summary>Area with real data. Empty for a field with no map.</summary>
        Bounds2 Bounds { get; }

        /// <summary>True if (x,z) lies inside the field's data area.</summary>
        bool Contains(float x, float z);

        /// <summary>Ground height in metres. Points outside <see cref="Bounds"/> are clamped to the nearest edge.</summary>
        float HeightAt(float x, float z);

        /// <summary>Steepest-direction slope in degrees (0 flat, below 90). Points outside <see cref="Bounds"/> are clamped.</summary>
        float SlopeDegAt(float x, float z);
    }
}
