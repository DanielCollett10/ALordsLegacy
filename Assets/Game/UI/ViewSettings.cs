namespace LordsLegacy.Game
{
    /// <summary>Presentation-only settings. Never read by the Sim; never changes Sim state.</summary>
    public sealed class ViewSettings
    {
        /// <summary>When true the view uses fixed, steady lighting instead of the seasonal daylight cycle.</summary>
        public bool SteadyLight { get; set; }
    }
}
