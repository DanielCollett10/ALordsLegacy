# Unity UI

**Purpose:** the panels a player reads and clicks: top bar, build bar, tool panel, operation and citizen panels, debug overlay,
and the Esc menu with Save and Load. All UI Toolkit, and none of it holds rules (rules: `docs/design/ui.md`).

## Key types and files

`Assets/Game/UI/` and subfolders. Most panels take a UXML and StyleSheet when assigned and build their elements in code when not. `CitizenPanelView` and `OperationPanelView` are code-only (no UXML). See the note in `design/ui.md`.

| Type | Folder | Role |
|---|---|---|
| `UiFocusRules` | `UI/` | `Apply(root)`: `focusable = false` and stops navigation events at the panel root. Every game panel calls it. |
| `ViewSettings` | `UI/` | Presentation settings: `Snapping`, `SnapDistancePx` (12), `RoadSnapDistancePx` (16), `SteadyLight`, `WalkStepsPerMetre`. |
| `TopBarController`, `FoodText` | `UI/TopBar/` | Date, time, pause, speed buttons from `Pacer.Speeds`, steady light, "running slower" notice, snapping toggle. `FoodText` gives "Food: N days" and a tooltip with counts per food type (FD-04). |
| `BuildBarView`, `ToolEntry` | `UI/BuildBar/` | Non-focusable buttons from `ToolEntry.Defaults()`. Road, Test area, Test work area, Woodcutter, Forager, House plot, Granary and Market are enabled (Market greys out with "Only one market per town"); Storage yard is greyed ("Coming later"). Raises `ToolClicked`. A `ToolEntry` may carry `CostLogs` (a `Func<int>`, for example `BuildCost.MinimumFor`): the bar re-checks it every frame against available logs and greys the button with the tooltip "Needs N logs (have M)" (BT-02). |
| `BuildEntryState` | `UI/BuildBar/` | Plain C#: `Of(costLogs, availableLogs)` → `(Enabled, Tooltip)`. Equal cost counts as affordable. |
| `ToolPanelView` | `UI/ToolPanel/` | Status line, hint line, Cancel and Confirm. `Show(status, hint, isValid)`, `Hide()`, events `Cancelled` and `Confirmed`. Also action buttons (`SetActions`). The draft tool writes the status ("Cost N logs, have M") and passes `CanConfirm` as `isValid`. |
| `OperationPanelView`, `OxText` | `UI/OperationPanel/` | Opened by selecting an operation. Status cause, modules with state, Deconstruct per module (and cancel while under way), Deconstruct operation, Expand modules (opens the draft tool), 2 family slots, worker tasks, ground logs, tree count, Pause/Resume, Draw work area, Use default, Oxen tab, stock line, High priority toggle. A foraging camp shows "Forager camp <id>" and "Hut store: n / cap" (FD-23b). |
| `CitizenPanelView` | `UI/CitizenPanel/` | Read-only name, age, family, activity, position, the home line ("Home: <family> house" or "Homeless (camp)", HM-22), the task line from `CitizenTaskText`, and "Hungry" while the family is short (FD-16). Shows refusals of debug sends. |
| `HousePanelView` | `UI/HousePanel/` | Opened when `AgentPicker` selects a house or its plot (HM-21). State (Delivering, Building n%, Built, Deconstructing n%), household and members or Empty, the larder by food type with days left, "Hungry" when short, Market access line ("Yes", "No", MK-44, from `MarketRange.AccessText`), High priority while unbuilt, Deconstruct (or Cancel house while unbuilt). Uses `ConfirmDialog`. See `homes.md` and `food.md`. |
| `GranaryPanelView` | `UI/GranaryPanel/` | Opened when `AgentPicker.TryPickGranary` selects a granary (T-449; checked after houses, before operations). Reads `GranaryReport.Of`: state line, food lines, "Stored: n / cap". "Supplies market" toggle (MK-10; asks first before the handover). Two family slots with Assign and Unassign (FD-32). Deconstruct (Cancel granary while unbuilt) via `ConfirmDialog`. |
| `MarketPanelView` | `UI/MarketPanel/` | Opened by `AgentPicker.TryPickMarket`. Reads `MarketReport.Of`: stock by food type, stalls with family or "Unstaffed" and stock, supplying granaries, households served and short last Sunday. Per-stall Deconstruct (MK-23b) and Remove market. Tints houses in range (`MarketRange`). |
| `DebugOverlayView` | `UI/DebugOverlay/` | F3 toggles. FPS, tree and road counts, nav cell, area under the cursor, `SimStats`. Read only. |
| `GameMenuView`, `GameMenuState` | `UI/GameMenu/` | Esc menu: Resume, Save, Load, Options, Quit (menu rules SV-01..04 in `docs/design/saving.md`). |
| `SavePanelController`, `LoadPanelController` | `UI/GameMenu/` | Save with a name. Load lists saves newest first; each row has a bin (`Delete` through `ConfirmDialog`). A save this build cannot read is greyed with its reason and Load disabled. Load checks the map. See `save.md`. |
| `ConfirmDialog`, `ConfirmDialogHost` | `UI/ConfirmDialog/` | Shared modal: `Show(message, confirmText, onConfirm)`, Cancel and Escape cancel, Confirm runs the action once. Built in code on its own UIDocument (created on first use). While open, `GameMenuState.IsOpen` is true, so tool, camera and picking input are blocked. Buttons are non-focusable (UIK-01). Used by deconstruction and save delete. |
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
`GameMenuTests`, `LoadPanelTests`, `SaveLoadScreenMatchTests`, `SnappingToggleTests`, `ConfirmDialogTests`,
`BuildBarAffordabilityTests`, `HousePanelTests`, `GranaryPanelTests`, `MarketPanelTests`, `ForagerUiTests`, `CitizenPanelTests`, `InputFocusSetUpFixture`, `TestPanelSettings`. EditMode: `BuildEntryStateTests`.
