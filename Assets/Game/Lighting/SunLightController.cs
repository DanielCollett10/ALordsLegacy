using LordsLegacy.Sim.Calendar;
using UnityEngine;

namespace LordsLegacy.Game
{
    /// <summary>Applies <see cref="SunLightModel"/> to a directional light every frame. Reads Sim state only.</summary>
    public sealed class SunLightController : MonoBehaviour
    {
        [SerializeField] private SimHost _simHost;
        [SerializeField] private Light _sun;
        [SerializeField] private TopBarController _topBar;

        private ViewSettings _settings;
        private Daylight _daylight;

        /// <summary>Overrides where the steady-light setting is read from (otherwise the top bar's).</summary>
        public void SetViewSettings(ViewSettings settings) => _settings = settings;

        /// <summary>Sets the host and light from code (tests, generators).</summary>
        public void Configure(SimHost host, Light sun)
        {
            _simHost = host;
            _sun = sun;
            _daylight = null;
        }

        private void LateUpdate() => Apply();

        /// <summary>Evaluates the model for the current Sim time and writes it to the light and ambient.</summary>
        public void Apply()
        {
            if (_simHost == null || _sun == null || !_simHost.IsInitialized) return;
            if (_daylight == null)
                _daylight = new Daylight(_simHost.World.Tuning.MinDaylightHours, _simHost.World.Tuning.MaxDaylightHours);

            ViewSettings settings = _settings ?? (_topBar != null ? _topBar.ViewSettings : null);
            bool steady = settings != null && settings.SteadyLight;
            SunLightState s = SunLightModel.Evaluate(_simHost.World.Now, _simHost.TickFraction, _daylight, steady);

            // Keep the light shining downward at night so the world stays readable.
            float elev = Mathf.Max(s.ElevationDeg, 8f) * Mathf.Deg2Rad;
            float az = s.AzimuthDeg * Mathf.Deg2Rad;
            var toSun = new Vector3(Mathf.Sin(az) * Mathf.Cos(elev), Mathf.Sin(elev), Mathf.Cos(az) * Mathf.Cos(elev));
            _sun.transform.rotation = Quaternion.LookRotation(-toSun);
            _sun.color = s.Color;
            _sun.intensity = s.Intensity;
            RenderSettings.ambientIntensity = s.AmbientIntensity;
        }
    }
}
