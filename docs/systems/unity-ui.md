# Unity UI

**Purpose:** the panels a player reads and clicks: top bar, build bar, tool panel, operation and citizen panels, debug overlay,
and the Esc menu with Save and Load. All UI Toolkit, and none of it holds rules (rules: `docs/design/ui.md`).

## Key types and files

`Assets/Game/UI/` and subfolders. Most panels take a UXML and StyleSheet when assigned and build their elements in code when not. `CitizenPanelView` and `OperationPanelView` are code-only (no UXML). See the note in `design/ui.md`.

| Type | Folder | Role |
|---|---|---|
| `UiFocusRules` | `UI/` | `Apply(root)`: `focusable = false` and stops navigation events at the panel root. Every game panel calls it. |
| `ViewSettings` | `UI/` | Presentation settings: `Snapping`, `SnapDistancePx` (12), `RoadSnapDistancePx` (16), `SteadyLight`, `WalkStepsPerMetre`. |
| `TopBarController` | `UI/TopBar/` | Date, time, pause, speed buttons from `Pacer.Speeds`, steady light, "running slower" notice, snapping toggle. |
| `BuildBarView`, `ToolEntry` | `UI/BuildBar/` | Non-focusable buttons from `ToolEntry.Defaults()`. Road, Test area, Test work area and Woodcutter are enabled; the rest greyed ("Coming later"). Raises `ToolClicked`. A `ToolEntry` may carry `CostLogs` (a `Func<int>`, for example `BuildCost.MinimumFor`): the bar re-checks it every frame against available logs and greys the button with the tooltip "Needs N logs (have M)" (BT-02). |
| `BuildEntryState` | `UI/BuildBar/` | Plain C#: `Of(costLogs, availableLogs)` → `(Enabled, Tooltip)`. Equal cost counts as affordable. |
| `ToolPanelView` | `UI/ToolPanel/` | Status line, hint line, Cancel and Confirm. `Show(status, hint, isValid)`, `Hide()`, events `Cancelled` and `Confirmed`. Also action buttons (`SetActions`). |
| `OperationPanelView`, `OxText` | `UI/OperationPanel/` | Opened by selecting an operation. Status cause, modules with state, 2 family slots, worker tasks, ground logs, tree count, Pause/Resume, Draw work area, Use default, Oxen tab, stock line, High priority toggle. |
| `CitizenPanelView` | `UI/CitizenPanel/` | Read-only name, age, family, activity, position, and the task line from `CitizenTaskText`. Shows refusals of debug sends. |
| `DebugOverlayView` | `UI/DebugOverlay/` | F3 toggles. FPS, tree and road counts, nav cell, area under the cursor, `SimStats`. Read only. |
| `GameMenuView`, `GameMenuState` | `UI/GameMenu/` | Esc menu: Resume, Save, Load, Options, Quit (menu rules SV-01..04 in `docs/design/saving.md`). |
| `SavePanelController`, `LoadPanelController` | `UI/GameMenu/` | Save with a name, list saves newest first, load with the map check. See `save.md`. |
| `SnappingHotkey` | `UI/TopBar/` | See `unity-tools.md`. |

## Rules

- Call `UiFocusRules.Apply` on every new game panel. Game UI never takes keyboard focus.
- UI Toolkit only. Use `.uxml` and `.uss` where the layout is static, or build in code where it is not.
- Panels send commands through `SimHost.Execute`. A refusal shows the `CommandResult` text.
- Panels show Sim state; they do not compute it. Use `OperationStatus.Reported` for the operation status line.
- Labels are named (for example `debug-area-name`, `citizen-message`), and tests look them up by name.

## Extending

1. New panel: a UIDocument in `GameSceneGenerator`, a view class, `UiFocusRules.Apply(root)` on its root.
2. New command button: call `SimHost.Execute` and show the refusal text.
3. Add a named label for each value so PlayMode tests can check it.

## Gotchas

- Use `PanelSettings` (`UI/PanelSettings.asset`) on every UIDocument.
- `OperationPanelView.OnEnable` adds the work area overlay, so there is no scene object for it.
- The operation panel's Assign button is disabled with the text "Hut not built yet" while the hut is unbuilt (CN-04b).

## Tests

`Assets/Tests/PlayMode/`: `TopBarTests`, `ToolPanelTests`, `OperationPanelTests`, `CitizenPanelTests`, `DebugOverlayTests`,
`GameMenuTests`, `LoadPanelTests`, `SaveLoadScreenMatchTests`, `SnappingToggleTests`, `InputFocusSetUpFixture`, `TestPanelSettings`.
