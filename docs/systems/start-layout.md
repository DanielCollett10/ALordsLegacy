# Start layout

**Purpose:** the first camp. Pure data describes the camp, its trunk road, families and oxen, and an applier builds them
through the normal commands, so starting state follows the same rules as play (decision 0027).

## Key types and files

`Assets/Sim/Start/` (`LordsLegacy.Sim.Start`) and `Assets/Game/Config/StartLayoutConfig.cs` (Unity side).

| Type | Role |
|---|---|
| `StartLayout` | Pure data. Camp centre and rotation, fire, stockpile, ox stand and tent spots (relative to the camp, rotated with it), trunk road control points, families with citizens, `OxCount`, `Stock` (`StartStock` list, default 20 `item.log`). |
| `StartLayoutApplier` | `Apply(world, layout)`. Adds the camp footprint (clears trees under it), draws the trunk road with `Execute(DrawRoad)`, adds families (home = tent spot), citizens and oxen, and produces the start stock into the stockpile. Throws `InvalidOperationException` with the refusal text if a command is refused. |
| `CampInfo` | `SimWorld.Camp`: footprint id, world-space fire, stockpile and stand points, `StockpileLocation`. Null until applied. |
| `StartLayoutConfig` (Unity) | ScriptableObject. `ToLayout()` converts it to `StartLayout`. Asset `Assets/Maps/TestMap/StartLayout.asset`. |

## Flow

`SimHost.Initialize` builds the world from the map, then applies `StartLayoutConfig.ToLayout()` through the applier. A null
layout means no camp. Starting oxen come from `OxCount`, not from `SimTuning.StartingOxen`.

When a game is loaded from a save, the layout is not applied (`ForRestore`). The camp and stockpile come back through the save.

Menu `LordsLegacy/Map/Generate Start Layout` (`StartLayoutGenerator`) writes the asset. It picks a flat spot at most 5° west of
the forest edge, with 50+ trees within 50 m and a north-south trunk road past the camp. It is idempotent.

## Extending

Change the layout by changing the asset or the generator's rules. Starting items go in `Stock`. Do not add a special case to
the commands for the camp.

## Gotchas

- `CampInfo.StockpileLocationId` keeps the ledger id. The ledger section re-attaches `CampInfo.StockpileLocation` on load (`save.md`).
- A stockpile is not a storage module, so no operation pool counts it.

## Tests

`Sim.Tests/Start/StartLayoutTests.cs`, `Sim.Tests/Scenarios/BuildItFirstScenarioTests.cs`. Unity:
`Assets/Tests/EditMode/StartLayoutConfigTests.cs`, `Assets/Tests/PlayMode/HutFromStartTests.cs`.
