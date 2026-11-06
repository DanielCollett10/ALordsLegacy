namespace LordsLegacy.Sim.Calendar
{
    /// <summary>Sunrise and sunset for one day, as hours from midnight (0-24).</summary>
    public readonly struct SunTimes
    {
        /// <summary>Sunrise, hours after midnight.</summary>
        public double Sunrise { get; }

        /// <summary>Sunset, hours after midnight. Always greater than <see cref="Sunrise"/>.</summary>
        public double Sunset { get; }

        /// <summary>Length of the day between sunrise and sunset, in hours.</summary>
        public double DaylightHours => Sunset - Sunrise;

        /// <summary>Creates a sunrise/sunset pair.</summary>
        public SunTimes(double sunrise, double sunset)
        {
            Sunrise = sunrise;
            Sunset = sunset;
        }
    }
}
