# M8: Woodcutters fell trees

**Shipped (player-visible):** the player draws a woodcutting area and a work area, places a hut and a log storage in the
area, assigns families to the hut, and the workers walk out and fell trees. Felled trees become stumps, and logs lie on the
ground at the tree. The operation panel shows status, modules, family slots, worker tasks and ground logs, with Pause and
Resume and Draw work area. The Woodcutter button on the build bar is enabled.

**Design source:** `docs/design/woodcutting.md` (WC-01..WC-37), `docs/design/areas.md` (AR-*), `docs/design/people.md` (PPL-*).

## Main technical changes by system

- **Areas:** `area.woodcutting` (Building, 150–5,000 m²) and `area.work_woodcutting` (Work). Work areas never trim or block roads.
  The area's operation is created on designate, and deleting the area removes its module footprints.
  See `docs/systems/areas.md`.
- **Operations (new):** `OperationTypeDef`, `OperationRegistry`, `OperationStatus` (computed, not stored), `WoodcuttingSystem`,
  `FellingOutcome`, `OperationTypes.Woodcutting`. Status is Working, Paused, MissingHut, MissingStorage, NoWorkers, NoTrees,
  StorageFull. See `docs/systems/operations.md`.
- **Modules:** `ModuleTypes` (hut 6×4 blocks, log storage 8×6 holds 20 logs, oxen stand 3×3 holds 1 ox). `ModulePlacement.Preview`
  is pure and `PlaceModule` / `RemoveModule` run through the commands. Placing clears trees under the footprint.
  See `docs/systems/footprints.md`.
- **Tasks:** `TaskBoard`, `DispatchSystem` (every 10 minutes), `StepRunnerSystem`, and `task.fell_tree`. Steps are MoveTo and the
  effective `ProcessDefs.Timber` stages (fell 90, delimb 30 off, buck 15 per log). See `docs/systems/tasks.md`.
- **Ledger:** `Ledger` with locations (ground pile, storage, stockpile), journals and `Audit()`. Logs enter only through `Produce`.
  See `docs/systems/inventory.md`.
- **Working day:** `WorkDay` plans shifts (work ends at sunset, Sunday meal, minimum sleep). See `docs/systems/people.md`.
- **Navigation:** the nav grid and A* pathfinder, with road speed. Movement orders re-plan when the nav grid changes.
  See `docs/systems/navigation.md`.
- **Unity:** `ModuleTool` (ghost, snap to area edge, Q/E rotate), `OperationPanelView`, `ModuleRenderer`, `LogPileRenderer`,
  `TreeRenderer` (stumps and felled trunks). Scene wired by `GameSceneGenerator.CreateAreaTools`, `CreatePeopleRenderers`,
  `CreateAgentSelection`. `GameSceneTests.GameSceneHasM8WoodcuttingWired` checks it.

## Decisions made

- 0030 move orders hand off between systems.
- 0031 operations create tasks from their state.
- 0032 inventory as located counts with a journal.

## Known gaps and debt

- No "previous handler" preference for hauling or felling (WC-22, WC-24). The code gives a task to the nearest eligible
  worker. See the note in `docs/design/woodcutting.md`.
- Felling stops at total log capacity (WC-25). The stop and restart rules are in the operation's `LogLoad`.
- `OperationTypes` and `ProcessDefs` are static lookups, not on the world catalogs (decision 0019 says they should be). Open for the human.
- Left in the codebase: `Assets/Editor/T151TempTestRunner.cs` and `TmpTestRun.cs` (temporary runners; see `docs/systems/editor-tools.md`).

## Follow-ups

- Hauling of felled logs to storage by ox is M9. Haul rules are in `docs/systems/operations.md`.
