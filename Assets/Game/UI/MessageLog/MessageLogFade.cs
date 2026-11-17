namespace LordsLegacy.Game
{
    /// <summary>
    /// Fade timing for the message log (T-508). An entry is fully opaque for the first
    /// <see cref="FadeStartS"/> seconds of real time, fades out until <see cref="LifetimeS"/>, then is gone.
    /// Pure presentation maths: no Sim state, no rules.
    /// </summary>
    public static class MessageLogFade
    {
        /// <summary>Real seconds an entry stays on screen before it is gone.</summary>
        public const float LifetimeS = 30f;

        /// <summary>Real seconds after which the entry starts to fade.</summary>
        public const float FadeStartS = 25f;

        /// <summary>True once an entry of this age (real seconds) must no longer be shown.</summary>
        public static bool Expired(float ageS) => ageS >= LifetimeS;

        /// <summary>Opacity for an entry of this age (real seconds): 1 until the fade starts, then linear to 0.</summary>
        public static float Opacity(float ageS)
        {
            if (ageS <= FadeStartS) return 1f;
            if (ageS >= LifetimeS) return 0f;
            return 1f - (ageS - FadeStartS) / (LifetimeS - FadeStartS);
        }
    }
}
