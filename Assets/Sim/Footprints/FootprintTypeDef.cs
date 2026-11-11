namespace LordsLegacy.Sim.Footprints
{
    /// <summary>Definition of one kind of fixed-rectangle thing (camp now; hut and modules later).</summary>
    public sealed class FootprintTypeDef
    {
        /// <summary>Id of the camp footprint type.</summary>
        public const string CampId = "footprint.camp";

        /// <summary>Camp type: 24 x 16 m, walkable, clears trees (CMP-02, CMP-04).</summary>
        public static readonly FootprintTypeDef Camp = new FootprintTypeDef(CampId, 24000, 16000, false, true);

        /// <summary>Unique type id.</summary>
        public string Id { get; }
        /// <summary>Size along the local X axis, in mm.</summary>
        public long WidthMm { get; }
        /// <summary>Size along the local Z axis, in mm.</summary>
        public long DepthMm { get; }
        /// <summary>True if movers cannot walk through it (MOV-03).</summary>
        public bool BlocksMovement { get; }
        /// <summary>True if trees under it are cleared.</summary>
        public bool ClearsTrees { get; }

        /// <summary>Creates a type definition.</summary>
        public FootprintTypeDef(string id, long widthMm, long depthMm, bool blocksMovement, bool clearsTrees)
        {
            Id = id; WidthMm = widthMm; DepthMm = depthMm; BlocksMovement = blocksMovement; ClearsTrees = clearsTrees;
        }
    }
}
