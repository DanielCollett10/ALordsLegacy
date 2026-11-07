using System;

namespace LordsLegacy.Prototypes.PerfSpike.Grid
{
    /// <summary>
    /// THROWAWAY (T-007). Tiny seeded PRNG (SplitMix64). Used instead of System.Random because
    /// System.Random's sequence is not guaranteed to match between .NET and Unity's Mono runtime.
    /// </summary>
    public sealed class SpikeRng
    {
        private ulong _state;

        /// <summary>Creates a generator. Same seed gives the same sequence on every runtime.</summary>
        public SpikeRng(ulong seed)
        {
            _state = seed;
        }

        /// <summary>Next raw 64-bit value.</summary>
        public ulong NextUInt64()
        {
            _state += 0x9E3779B97F4A7C15UL;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat()
        {
            return (float)(NextUInt64() >> 40) / (float)(1UL << 24);
        }

        /// <summary>Uniform float in [min, max).</summary>
        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }

        /// <summary>Uniform int in [0, maxExclusive).</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return (int)(NextUInt64() % (ulong)maxExclusive);
        }
    }
}
