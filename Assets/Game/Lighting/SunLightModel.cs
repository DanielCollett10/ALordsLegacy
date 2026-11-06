using System;
using LordsLegacy.Sim.Calendar;
using UnityEngine;

namespace LordsLegacy.Game
{
    /// <summary>Result of <see cref="SunLightModel.Evaluate"/>.</summary>
    public readonly struct SunLightState
    {
        /// <summary>Sun elevation in degrees. Below 0 means the sun is under the horizon (night).</summary>
        public readonly float ElevationDeg;
        /// <summary>Compass azimuth in degrees (0 = north, 90 = east, 180 = south).</summary>
        public readonly float AzimuthDeg;
        /// <summary>Light colour.</summary>
        public readonly Color Color;
        /// <summary>Directional light intensity.</summary>
        public readonly float Intensity;
        /// <summary>Ambient light intensity.</summary>
        public readonly float AmbientIntensity;

        public SunLightState(float elevationDeg, float azimuthDeg, Color color, float intensity, float ambientIntensity)
        {
            ElevationDeg = elevationDeg;
            AzimuthDeg = azimuthDeg;
            Color = color;
            Intensity = intensity;
            AmbientIntensity = ambientIntensity;
        }
    }

    /// <summary>Pure maths turning Sim time and seasonal daylight into sun and sky values (TIME-A5, TIME-A6).</summary>
    public static class SunLightModel
    {
        /// <summary>Noon elevation on the shortest day.</summary>
        public const float WinterPeakDeg = 20f;
        /// <summary>Noon elevation on the longest day.</summary>
        public const float SummerPeakDeg = 60f;
        /// <summary>Lowest the sun dips below the horizon at midnight.</summary>
        public const float MidnightDipDeg = 30f;
        /// <summary>Fixed elevation used by steady light.</summary>
        public const float SteadyElevationDeg = 45f;
        /// <summary>Fixed azimuth used by steady light.</summary>
        public const float SteadyAzimuthDeg = 180f;

        private const float NightIntensity = 0.08f;
        private const float DayIntensity = 1.1f;
        private const float NightAmbient = 0.25f;
        private const float DayAmbient = 1f;
        private static readonly Color NightColor = new Color(0.45f, 0.55f, 0.9f);
        private static readonly Color HorizonColor = new Color(1f, 0.6f, 0.35f);
        private static readonly Color NoonColor = new Color(1f, 0.96f, 0.88f);

        /// <summary>Computes sun and sky for a game time. With <paramref name="steadyLight"/> the result ignores time.</summary>
        /// <param name="tickFraction">Fraction (0-1) of the current tick elapsed, for smoothing between ticks.</param>
        public static SunLightState Evaluate(GameTime now, double tickFraction, Daylight daylight, bool steadyLight)
        {
            if (daylight == null) throw new ArgumentNullException(nameof(daylight));
            if (steadyLight)
                return new SunLightState(SteadyElevationDeg, SteadyAzimuthDeg, NoonColor, DayIntensity, DayAmbient);

            SunTimes sun = daylight.For(now.DayOfYear);
            double dayLen = sun.Sunset - sun.Sunrise;
            double hour = now.Hour + (now.Minute + tickFraction) / 60.0;

            double range = daylight.MaxDaylightHours - daylight.MinDaylightHours;
            double season = range <= 0 ? 0.5 : (dayLen - daylight.MinDaylightHours) / range;
            double peak = WinterPeakDeg + season * (SummerPeakDeg - WinterPeakDeg);

            double elevation, azimuth;
            if (hour >= sun.Sunrise && hour < sun.Sunset)
            {
                double p = (hour - sun.Sunrise) / dayLen;
                elevation = peak * Math.Sin(Math.PI * p);
                azimuth = 90.0 + 180.0 * p;
            }
            else
            {
                double nightLen = 24.0 - dayLen;
                double since = hour >= sun.Sunset ? hour - sun.Sunset : hour + 24.0 - sun.Sunset;
                double q = since / nightLen;
                elevation = -MidnightDipDeg * Math.Sin(Math.PI * q);
                azimuth = (270.0 + 180.0 * q) % 360.0;
            }

            // Light strength rises from the night level across the first 15 degrees of elevation.
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((float)elevation / 15f));
            float high = Mathf.Clamp01((float)elevation / 45f);
            Color dayColor = Color.Lerp(HorizonColor, NoonColor, high);
            Color color = Color.Lerp(NightColor, dayColor, k);
            float intensity = Mathf.Lerp(NightIntensity, DayIntensity, k);
            float ambient = Mathf.Lerp(NightAmbient, DayAmbient, k);
            return new SunLightState((float)elevation, (float)azimuth, color, intensity, ambient);
        }
    }
}
