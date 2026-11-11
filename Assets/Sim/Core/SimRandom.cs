using System;

namespace LordsLegacy.Sim.Core
{
    /// <summary>
    /// The Sim's one deterministic random source (CLK-07, decision 0022 "seeded random"). A PCG-XSH-RR 64/32
    /// generator written here, so the output does not depend on the .NET runtime.
    /// <para>
    /// The whole state is one <see cref="ulong"/>, read and written through <see cref="State"/>. A future save
    /// stores that number and restores it with <see cref="Restore"/>; the sequence then continues exactly.
    /// The increment is fixed, so the state is the only thing that changes. No static state.
    /// </para>
    /// <para>
    /// Not thread-safe. Each <see cref="SimWorld"/> owns one instance, and only Sim code that runs in tick order
    /// draws from it.
    /// </para>
    /// </summary>
    public sealed class SimRandom
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private const ulong Increment = 1442695040888963407UL; // odd, so the LCG has full period

        /// <summary>
        /// The generator's entire state. Store it in a save and pass it to <see cref="Restore"/> to resume the
        /// same sequence. Any value is valid.
        /// </summary>
        public ulong State { get; private set; }

        /// <summary>
        /// Creates a generator whose sequence depends only on <paramref name="seed"/>. Uses PCG's standard seeding
        /// (advance, add the seed, advance) so that small seeds such as 1 and 2 give unrelated sequences.
        /// </summary>
        /// <param name="seed">Any 64-bit value. The same seed always gives the same sequence.</param>
        public SimRandom(ulong seed)
        {
            State = 0;
            NextUInt();
            State = unchecked(State + seed);
            NextUInt();
        }

        /// <summary>
        /// Replaces the state with a value saved from <see cref="State"/>, so the sequence resumes where it was saved.
        /// </summary>
        /// <param name="state">A value previously read from <see cref="State"/>.</param>
        public void Restore(ulong state)
        {
            State = state;
        }

        /// <summary>Next 32 random bits, unsigned. Advances the state once.</summary>
        public uint NextUInt()
        {
            ulong old = State;
            State = unchecked(old * Multiplier + Increment);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return unchecked((xorShifted >> rot) | (xorShifted << ((-rot) & 31)));
        }

        /// <summary>
        /// A random whole number in [<paramref name="min"/>, <paramref name="max"/>): <paramref name="min"/>
        /// inclusive, <paramref name="max"/> exclusive (the same convention as <c>Random.Range</c> for ints).
        /// Unbiased: values that would skew the result are redrawn. Advances the state at least once.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="max"/> is not greater than <paramref name="min"/>.</exception>
        public int NextInt(int min, int max)
        {
            if (max <= min) throw new ArgumentOutOfRangeException(nameof(max), "max must be greater than min.");
            uint range = unchecked((uint)((long)max - min)); // 1 .. 2^32-1, fits uint
            uint threshold = unchecked((0u - range) % range); // 2^32 mod range, the biased tail to redraw
            while (true)
            {
                uint x = NextUInt();
                if (x >= threshold) return (int)((long)min + (long)(x % range));
            }
        }

        /// <summary>
        /// A random number in [0, 1). Advances the state once. Resolution is 2^-32.
        /// </summary>
        public double NextDouble()
        {
            return NextUInt() * (1.0 / 4294967296.0);
        }
    }
}
