# 0025: One seeded random source owned by SimWorld

**Context.** Routine choices (for example where a citizen mills, PPL-31) must be reproducible from a seed (CLK-07).
`System.Random` without a seed, or the Unity random, would break replays and saves. Decision numbers 0022 and 0023 are
already taken by navigation and the pace, so this one takes the next free number.

**Decision.**
- `SimRandom` (`LordsLegacy.Sim.Core`) is a PCG-XSH-RR 64/32 generator written in Sim code. It is not `System.Random`.
- `SimWorld.Random` is the only random source the Sim uses. It is never null and is seeded from `SimTuning.WorldSeed`
  (default 12345).
- Its whole state is one `ulong` (`State`), read to save and written back with `Restore`. The increment is fixed.
- `NextInt(min, max)` is max-exclusive and unbiased. `NextDouble()` is in [0, 1).

**Consequences.** Same seed, same sequence, on any machine. Nothing else in the Sim may create its own random source.
Adding a new kind of random choice means drawing from `world.Random` in an `ISimSystem`, in tick order. Changing the
algorithm changes every saved game, so the pinned sequence test in `SimRandomTests` must be updated only with a decision.
