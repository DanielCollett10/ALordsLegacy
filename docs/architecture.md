# Architecture

_Living document. Agents update it when they change how parts fit together._

## Layers

| Layer | Folder | Depends on | Tested by |
|---|---|---|---|
| Simulation core | `Assets/Sim/` (asmdef `NetSim.Sim`, no UnityEngine) | nothing | `Sim.Tests/` via `dotnet test` |
| Unity layer | `Assets/Game/` | `NetSim.Sim`, UnityEngine | Unity EditMode/PlayMode tests |
| Editor tooling | `Assets/Editor/` | UnityEditor | by hand / editor lane |

## Simulation core

- `SimWorld` owns the tick clock. `Step(n)` advances time; nothing else does.
- (Add devices, links, packets, economy here as they are built.)

## Unity layer

- (Describe the view: how Sim state becomes something on screen.)

## Data flow

Player input → Unity layer → Sim command → `SimWorld.Step` → Sim state → Unity layer renders.
