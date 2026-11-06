namespace LordsLegacy.Sim.Terrain
{
    /// <summary>Axis-aligned rectangle on the x/z ground plane, in world metres. Min and max are both inclusive.</summary>
    public readonly struct Bounds2
    {
        /// <summary>Empty bounds: contains nothing, zero size.</summary>
        public static readonly Bounds2 Empty = new Bounds2(0f, 0f, 0f, 0f, true);

        /// <summary>Smallest x.</summary>
        public readonly float MinX;
        /// <summary>Smallest z.</summary>
        public readonly float MinZ;
        /// <summary>Largest x.</summary>
        public readonly float MaxX;
        /// <summary>Largest z.</summary>
        public readonly float MaxZ;
        /// <summary>True for the empty rectangle (no area covered).</summary>
        public readonly bool IsEmpty;

        /// <summary>Creates a non-empty rectangle.</summary>
        public Bounds2(float minX, float minZ, float maxX, float maxZ) : this(minX, minZ, maxX, maxZ, false) { }

        private Bounds2(float minX, float minZ, float maxX, float maxZ, bool isEmpty)
        {
            MinX = minX; MinZ = minZ; MaxX = maxX; MaxZ = maxZ; IsEmpty = isEmpty;
        }

        /// <summary>Extent along x.</summary>
        public float SizeX => MaxX - MinX;
        /// <summary>Extent along z.</summary>
        public float SizeZ => MaxZ - MinZ;

        /// <summary>True if (x,z) is inside or on the edge. Always false when empty.</summary>
        public bool Contains(float x, float z) => !IsEmpty && x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;
    }
}
