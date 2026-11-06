using System.Globalization;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Ground readout for one x/z point: the position, plus height and slope taken from the Sim
    /// (<c>SimWorld.Terrain</c>), never from the Unity collider. <see cref="HasValue"/> is false when nothing was hit.
    /// </summary>
    public readonly struct GroundSample
    {
        /// <summary>Empty sample: nothing under the cursor.</summary>
        public static readonly GroundSample None = new GroundSample(0f, 0f, 0f, 0f, false);

        /// <summary>World x in metres (Sim x = Unity x).</summary>
        public readonly float X;
        /// <summary>World z in metres (Sim z = Unity z).</summary>
        public readonly float Z;
        /// <summary>Ground height in metres from the Sim.</summary>
        public readonly float HeightM;
        /// <summary>Slope in degrees from the Sim.</summary>
        public readonly float SlopeDeg;
        /// <summary>True when the sample holds a real point.</summary>
        public readonly bool HasValue;

        /// <summary>Creates a sample.</summary>
        public GroundSample(float x, float z, float heightM, float slopeDeg, bool hasValue)
        {
            X = x; Z = z; HeightM = heightM; SlopeDeg = slopeDeg; HasValue = hasValue;
        }

        /// <summary>Label text, e.g. "x 100.0  z 100.0  height 12.3 m  slope 4.5 deg". Dashes when empty.</summary>
        public string Format()
        {
            if (!HasValue) return "x --  z --  height -- m  slope --";
            var c = CultureInfo.InvariantCulture;
            return "x " + X.ToString("F1", c) + "  z " + Z.ToString("F1", c)
                 + "  height " + HeightM.ToString("F1", c) + " m  slope " + SlopeDeg.ToString("F1", c) + " deg";
        }
    }
}
