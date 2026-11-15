# M10: Build it first

**Shipped (player-visible):** the player places a hut, an oxen stand or log storage that needs materials. It appears as a
scaffold. Oxen bring logs from the camp stockpile or from ground piles to the site, and workers build it, rising with progress.
Built modules work as before. The operation panel shows each module's state (Delivering n / m logs, Building n%, Built), with a
High priority toggle. The Assign button is disabled until the hut is built. The citizen panel shows the current construction task.
This completes MVP-1.

**Design source:** `docs/design/construction.md` (CN-*).

## Main technical changes by system

- **Records:** `FootprintTypeDef.ConstructionRecipe` (hut 8 logs and 960 worker-minutes; oxen stand 1 and 60; log storage and
  camp empty = built at once). `ConstructionRegistry` holds one `ConstructionSite` per unbuilt footprint. No record = built.
  See `docs/systems/construction.md`.
- **Sources and reservation:** `ConstructionSources` covers the stockpile, built log storage and unowned piles. `PlaceModule`
  reserves nearest-first in the same `Apply` as the footprint. All or nothing.
- **System:** `ConstructionSystem` (operations phase, after haul) opens `task.deliver_materials` (ox, up to 4 logs per trip) and
  `task.build` (up to `MaxBuildersPerSite` = 4). Build progress is capped by the recipe minutes times delivered over needed.
  Logs are consumed as the progress passes each share (`Ledger.Consume`).
- **Priority:** `SetSitePriority` command sets the site's and its open tasks' priority. Dispatch orders by priority, then travel.
- **Wind-down:** a deliver still under way at the end of work is dropped as a site-owned pile where the ox stands, with its
  reservation moved to that pile. Build does not wind down.
- **Working day:** odd-jobbers with construction work follow `WorkDay`. An unfinished build keeps its progress overnight.
- **Unity:** `ConstructionRenderer` (scaffold height from `ConstructionProgress.Fraction`), `CitizenTaskText` for the panel line,
  Delivering/Building/Built text in the operation panel, High toggle, and the Assign gate.
  Wired in `GameSceneGenerator.CreatePeopleRenderers`. No new UI objects.

## Decisions made

- 0033 construction sites are footprint records (not a separate building object). Sites are keyed by footprint id.

## Known gaps and debt

- Not built: the 50% removal refund (CN-33); artisan build steps (PPL-42); a walk-to-site step (builders work where they stand;
  dispatch breaks ties by distance to the site).
- No scene test for M10 (`GameSceneTests` has none for construction). Only `ConstructionVisualsTests` covers the visuals.
- The footprint and operation share the ledger owner field. A site's pile is owned by the footprint id. `RemoveModule` guards the clash.
- Recipe and builder numbers are tuning values; see `docs/design/tuning.md`.

## Follow-ups

- Walk-to-site step and the removal refund (CN-33) are the next construction items.
- Artisan build steps (PPL-42) are not built.
- A scene test for construction wiring, like the M7–M9 ones.
