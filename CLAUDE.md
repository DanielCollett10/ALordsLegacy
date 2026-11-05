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
- Namespace `NetSim.Sim` (and sub-namespaces) for Sim code, `NetSim.Game` for Unity code.
- Sim code: no static mutable state, no `DateTime.Now`, no `System.Random` without a seed.
  Time only advances through the Sim tick.
- Every new Sim behaviour gets an NUnit test in `Sim.Tests/`.
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
