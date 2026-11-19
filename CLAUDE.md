# Project rules for every agent

This is a Unity game built mostly by AI agents, coordinated by Agent NOC.
A human approves merges. Read this file, `docs/architecture.md` and the relevant
parts of `docs/design/` before changing anything.

## Architecture in one paragraph

All game rules live in **`Assets/Sim/`**: plain C# with **no `UnityEngine` references**
(enforced by `Sim.asmdef` → `noEngineReferences: true`). It is deterministic and
tick-based, and it is the source of truth for game state. Everything under
`Assets/Game/` is the Unity layer: it reads Sim state and draws it, and turns
player input into Sim commands. The Unity layer never holds game rules.
This split is what lets us swap the 2D view for a 3D one later without
touching the simulation.

## How to build and test

- Sim tests (fast, no Unity needed): `dotnet test Sim.Tests/Sim.Tests.csproj`
- `Sim.Tests.csproj` compiles the files in `Assets/Sim/**` directly, so a new
  `.cs` file there is picked up automatically.
- Unity EditMode/PlayMode tests run in the Unity Editor (editor lane only).

## Code rules

- C# 9 language level (Unity's compiler). No `record struct`, no file-scoped namespaces,
  no global usings.
- Namespace `LordsLegacy.Sim` (and sub-namespaces) for Sim code, `LordsLegacy.Game` for Unity code.
- Sim code: no static mutable state, no `DateTime.Now`, no `System.Random` without a seed.
  Time only advances through the Sim tick.
- Every new Sim behaviour gets an NUnit test in `Sim.Tests/`.
- New Sim state must be written in a save section and covered by the round-trip test in the same task
  (decision 0034). A task that adds state without a save section is not done.
- Prefer small files and small classes. Public API gets XML doc comments.
- UI uses **UI Toolkit** (`.uxml` + `.uss`), not uGUI, so it can be written as text.
- Prefabs and scenes: prefer an Editor script that generates them over hand-editing YAML.

## Git rules

- Work only on the branch you were given. Never push, merge, rebase, switch branches,
  or rewrite history.
- Commit messages start with the task ID, e.g. `T-012: add MAC table learning`.
- Never commit `Library/`, `Temp/`, `Logs/`, `obj/`, `bin/`, `UserSettings/`.

## Docs

- When you change how things fit together, update `docs/architecture.md`.
- Record a decision that future agents should not re-argue as
  `docs/decisions/NNNN-short-title.md` (context, decision, consequences; 10–20 lines).

## Lessons learned

Rules added after reviews kept catching the same mistakes. Follow them like any other rule.

- Before adding a type, check docs/shared-infrastructure.md. If a listed piece covers the need, extend it; don't write a parallel one. A task that duplicates a listed piece fails review.
- Sim code must not hard-code gameplay numbers (durations, yields, costs, ratios, thresholds) in a system or recipe. Add each one as a SimTuning property with a default, then follow the SimTuning mirror rule (CoreSection, SimConfig asset and tests). Do not copy an earlier system that hard-coded its numbers. If you find one, move it to SimTuning in the same task or list it in follow-ups. _(learned from T-583)_
- Milestone docs (docs/milestones/*.md) and per-system docs (docs/systems/*.md) must each stay at 60 lines or fewer. Before reporting a task that edits a doc, run a line count on every changed doc and trim it if it is over. Keep decisions, gaps and follow-ups. Fold test lists and technical bullets into short lines. _(learned from T-439)_
- When you add or change a SimTuning property or a code default such as a module recipe, grep for its name and sibling properties in Assets/Sim/Save/Sections/CoreSection.cs, Sim.Tests/Save/CoreSectionTests.cs, Assets/Tests/EditMode/SimConfigTests.cs and the SimConfig asset. In the same task, write and read it in CoreSection (older saves load the default), update the writable-property count, and update the SimConfig asset and tests. Then run `dotnet test Sim.Tests/Sim.Tests.csproj` and the SimConfig EditMode tests, and quote both totals. _(learned from T-537, T-528)_

