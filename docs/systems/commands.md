# Commands

**Purpose:** the only way the Unity layer (or a test) changes Sim state. Each command validates without mutating, then applies, then is logged (decision 0011).

## Key types and files

| Type | File | Role |
|---|---|---|
| `ISimCommand` | `Assets/Sim/Commands/ISimCommand.cs` | `Validate(world)` must not mutate. `Apply(world)` runs only after an accepted `Validate`. |
| `CommandResult` | `Commands/CommandResult.cs` | Immutable. Accepted (created ids, removed ids, `RemovedTreeIds`; decision 0015) or Rejected (`CommandRejectCode` + text). |
| `CommandRejectCode` | `Commands/CommandRejectCode.cs` | `InvalidArgument`, `NotFound`, `NotAllowed`, `Conflict`, … |
| `CommandLog`, `CommandLogEntry` | `Commands/` | Append-only `(tick, command, result)`. Not saved. |

Commands are in `Assets/Sim/Commands/`: `DrawRoad`, `DeleteRoadSegment`, `MoveRoadNode`, `DesignateArea`, `DeleteArea`,
`PlaceModule`, `RemoveModule`, `AssignFamily`, `UnassignFamily`, `AssignOx`, `UnassignOx`, `SetSitePriority`,
`SendCitizenTo`, plus `OperationSettings` (pause, resume, work area, use default area).

## Flow

`world.Execute(cmd)`: `Validate` → if accepted, `Apply` → append to `CommandLog` → notify every system's
`OnCommandApplied` (accepted only). A rejected command changes nothing and notifies no one. `Execute` works while
paused and never changes `Tick`. Unity calls it only through `SimHost.Execute`.

## Extending

1. Write the command: `Validate` returns a rejection for every bad input (no exceptions for bad player input).
2. `Apply` makes the change through the registries, so their `Version` counters bump.
3. Return created and removed ids in the `CommandResult`, so views can react.
4. Add tests: a rejected case that proves nothing changed, and an accepted case.
5. Preview the command in the tool with the same pure validator it uses, so the preview cannot disagree.

## Gotchas

- Preview queries (`RoadValidator`, `AreaValidator`, `ModulePlacement.Preview`) are pure. The command re-runs them, so
  the result is the same as the preview.
- `RemovedTreeIds` tells the view which trees went; trees do not return on `DeleteArea`.

## Tests

`Sim.Tests/Commands/CommandPipelineTests.cs`, `Commands/SendCitizenToTests.cs`, `Areas/AreaCommandTests.cs`,
`Roads/DrawRoadTests.cs`, `DeleteRoadSegmentTests.cs`, `MoveRoadNodeTests.cs`, `Operations/OperationCommandTests.cs`,
`Operations/OxCommandTests.cs`, `Operations/PlaceRemoveConstructionTests.cs`, `Operations/SitePriorityTests.cs`.
