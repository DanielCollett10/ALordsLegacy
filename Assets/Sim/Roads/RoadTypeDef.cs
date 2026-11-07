namespace LordsLegacy.Sim.Roads
{
    /// <summary>Definition of one road type (RD-04). Immutable.</summary>
    public sealed class RoadTypeDef
    {
        /// <summary>Stable id, e.g. <c>road.dirt_path</c>.</summary>
        public string Id { get; }
        /// <summary>Full road width in metres.</summary>
        public double WidthM { get; }
        /// <summary>Maximum grade as a fraction (0.15 = 15%, rise over run per 1 m step).</summary>
        public double MaxGrade { get; }
        /// <summary>Walking speed multiplier on this road.</summary>
        public double SpeedMultiplier { get; }
        /// <summary>Id the view uses to pick the look of this road.</summary>
        public string VisualId { get; }

        /// <summary>Creates a road type definition.</summary>
        public RoadTypeDef(string id, double widthM, double maxGrade, double speedMultiplier, string visualId)
        {
            Id = id; WidthM = widthM; MaxGrade = maxGrade; SpeedMultiplier = speedMultiplier; VisualId = visualId;
        }
    }
}
