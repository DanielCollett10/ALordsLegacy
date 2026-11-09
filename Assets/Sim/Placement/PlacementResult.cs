namespace LordsLegacy.Sim.Placement
{
    /// <summary>Why a point cannot be placed (docs/design/placement.md).</summary>
    public enum PlacementRefusal
    {
        /// <summary>Not refused.</summary>
        None = 0,
        /// <summary>Inside a building area (PL-01, PL-03).</summary>
        InsideArea = 1,
        /// <summary>Inside a road corridor (PL-01).</summary>
        OnRoad = 2,
        /// <summary>Outside the map (PL-04).</summary>
        OutsideMap = 3
    }

    /// <summary>Answer of a <see cref="PlacementRules"/> query: Ok, or a reason. Immutable.</summary>
    public sealed class PlacementResult
    {
        /// <summary>The shared Ok result.</summary>
        public static readonly PlacementResult Allowed = new PlacementResult(PlacementRefusal.None, null);

        /// <summary>True if the point may be placed.</summary>
        public bool Ok => Reason == PlacementRefusal.None;
        /// <summary>Why not, or <see cref="PlacementRefusal.None"/>.</summary>
        public PlacementRefusal Reason { get; }
        /// <summary>Name of the blocking area for <see cref="PlacementRefusal.InsideArea"/>, else null.</summary>
        public string AreaName { get; }

        private PlacementResult(PlacementRefusal reason, string areaName) { Reason = reason; AreaName = areaName; }

        /// <summary>A refusal without an area name.</summary>
        public static PlacementResult Refused(PlacementRefusal reason) => new PlacementResult(reason, null);

        /// <summary>A refusal for being inside the named area.</summary>
        public static PlacementResult InsideArea(string areaName) => new PlacementResult(PlacementRefusal.InsideArea, areaName);

        /// <summary>Short text for the UI, e.g. "inside Test area 1"; empty when Ok.</summary>
        public string Text
        {
            get
            {
                switch (Reason)
                {
                    case PlacementRefusal.InsideArea: return "inside " + AreaName;
                    case PlacementRefusal.OnRoad: return "on a road";
                    case PlacementRefusal.OutsideMap: return "outside the map";
                    default: return "";
                }
            }
        }
    }
}
