# 0050: Coppice stools and reed beds are bush kinds

**Context.** M19 adds hazel and willow stools, M20 adds reed beds (`design/materials.md` MAT-13, MAT-15, MAT-30,
MAT-39a). Each is a point on the map that a worker reserves, cuts a number of units from, and that regrows after a
number of days. `BushRegistry` (`systems/foraging.md`) already does exactly this for forage bushes: records, seeded
scatter, reservation by task id, regrow, save section and an instanced renderer.

**Decision.**
- New `BushKind` values `HazelStool`, `WillowStool` (M19) and `ReedBed` (M20) in the same `BushRegistry`.
- Units per cut and regrow days come **per kind** from tuning (`BushBaskets`/`BushRegrowDays` stay the forage values).
- `BushScatter` places each kind with its own count and ground rule (forest edge, low flat ground) from the same seeded random.
- Every "nearest available" query takes a **kind filter**. Foraging asks only for Berries and Mushrooms, the coppice hut only
  for stools, the thatcher only for reed beds. Adding the filter to foraging comes first, so nothing else changes.
- `BushesSection` accepts the new kind values; the save version bumps once when the first new kind is saved.

**Consequences.** No new registry, renderer or save section; the bush renderer picks art by kind. If gatherables later
need state bushes do not have (growth stages, fields), split them out then.
