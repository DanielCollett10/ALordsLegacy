namespace LordsLegacy.Game
{
    /// <summary>Presentation-only settings. Never read by the Sim; never changes Sim state.</summary>
    public sealed class ViewSettings
    {
        /// <summary>When true the view uses fixed, steady lighting instead of the seasonal daylight cycle.</summary>
        public bool SteadyLight { get; set; }

        /// <summary>
        /// One snapping setting shared by road and area tools (roads.md Snapping). Default On. Set by the top bar
        /// button and by the Alt key (ToggleSnapping action). Tools read it; the Sim never does.
        /// </summary>
        public bool Snapping { get; set; } = true;

        /// <summary>Snap distance in screen pixels, used by the drawing tools (ui.md, roads.md). Presentation-only.</summary>
        public float SnapDistancePx { get; set; } = 12f;

        /// <summary>Spacing in metres of the snap dots along road sides and area edges (areas.md AR-22).</summary>
        public float SnapDotSpacingM { get; set; } = 2.5f;

        /// <summary>Snap markers show within this many metres of the cursor (areas.md AR-22).</summary>
        public float SnapMarkerRadiusM { get; set; } = 25f;

        /// <summary>Snap distance in screen pixels for the road tool (roads.md RD-18, from M5). Area tools keep <see cref="SnapDistancePx"/>.</summary>
        public float RoadSnapDistancePx { get; set; } = 16f;
    }
}
