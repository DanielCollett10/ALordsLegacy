# 0019: Road and area type definitions belong to the world, configured from one Unity asset

**Context.** The owner wants to tune values such as road max grade in the Inspector (T-119). Road and area types
are static, code-built lists (`RoadTypes`, `AreaTypes`), and Sim code calls `RoadTypes.Find(id)` statically, so
nothing can change them without editing code. `SimTuning` is already an instance passed to `SimWorld`.

**Decision.**
- `SimWorld` owns its type catalogs: `World.RoadTypes` and `World.AreaTypes` (never null), built from definitions
  passed in when the world is built. With no definitions passed, the world uses today's defaults.
- All Sim code looks types up through the world, never statically. The static lists stay only as the **default
  definitions**, so tests and old callers keep working until they're migrated.
- Unity side: one ScriptableObject, `SimConfig` (`Assets/Game/Config/`), holds `SimTuning` values plus the road and
  area type values. `SimHost` builds `SimTuning` and the catalogs from it when the world is built. Read once at start;
  there's no live re-tuning (that would change rules in the middle of a running world).
- `SimConfig` is presentation-free data. Snap distances and other view values stay in `ViewSettings`.

**Consequences.** Type values are data (DEF-03), and a later save file can store them with the world. Unity code
must stop calling `RoadTypes.Find` and `AreaTypes.Find` statically and read `World.RoadTypes`/`World.AreaTypes`.
Adding a new road type means adding a definition, not code.
