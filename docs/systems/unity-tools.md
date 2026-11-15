# Unity tools, picking and camera

**Purpose:** turn mouse and keyboard input into previews and Sim commands. Tools hold draft state only. Rules come from the
Sim's pure validators. Picking and the camera only read the Sim (decision 0017 for snapping).

## Key types and files

`Assets/Game/Tools/`, `Assets/Game/Picking/`, `Assets/Game/Debugging/`, `Assets/Game/Camera/`.

| Type | Role |
|---|---|
| `ITool` | `Id`, `HasDraft`, `Activate`, `Deactivate`, `Cancel`, `HandleEscape`. |
| `ToolController` | Keeps exactly one active tool. `Register`, `Toggle(id)`, `HandleEscape()`, `IsPointerOverUi`. Sets `CameraRig.ToolActive`. |
| `RoadTool` (id `road`) | Draft points and node drag. Calls `RoadValidator`, `PlacementRules`, `SimHost.Execute` (`DrawRoad`, `DeleteRoadSegment`, `MoveRoadNode`). |
| `AreaTool` (ids `test-area`, `test-work-area`, woodcutter ids) | Draft points with edges. Click on an edge inserts a point (max 8). Drag a handle to bend it, double-click to straighten. Snaps when `Snapping` is on. |
| `ModuleTool` (id `module`) | Queue of module types for one operation. Ghost follows the cursor. Q/E rotate 15° (Shift 1°). Sends `PlaceModule` or `RemoveModule`. |
| `Shared/PressTracker` | Click, drag and double-click. 4 px drag threshold, 6 px double-click radius. |
| `Shared/SnapPicker` | Best candidate by kind order within a pixel limit: area vertex, road side, area edge. |
| `Shared/SnapMarkers`, `ToolHitTester`, `ModuleSnap` | Snap markers (AR-22), draft point hits, module side snap (3 mm inset). |
| `AgentPicker` | Left click (no tool, not over UI) selects a citizen or ox by screen distance, else a module, the building area, or a linked work area (`TryPickOperation`). |
| `GroundPicker` | Ray from the camera → x/z only. Height and slope from `World.Terrain`, never the collider. `ReadAt(x, z)` reads without a ray. |
| `GroundReadoutView` | One label of the ground sample each frame. |
| `CameraRig` | Pan, scroll zoom, middle-drag orbit (pitch 10–85°), held above terrain. Refuses left and right mouse while a tool is active. |
| `DebugSendController` | Editor and development builds only. Right click on ground with a citizen selected sends `SendCitizenTo`. |
| `DebugPathOverlay` | Drapes the selected citizen's remaining path with `TerrainDrape.Ribbon`. |
| `SnappingHotkey` | Input System button action (`<Keyboard>/alt`). Toggles `ViewSettings.Snapping`. |

## Rules

- A tool keeps view state (draft, ghost, drag) and asks the Sim for every rule. It never checks geometry itself.
- Preview and command use the same validator, so the preview cannot disagree with the result.
- Snapping and `ViewSettings` are read here and in views only. The Sim never reads them.
- Escape: the active tool first (`HandleEscape`), and it deactivates only if the tool returns false.
- Only one tool is active. Switching cancels the old tool's draft.
- Camera and tool keys read `Keyboard.current` directly. Game UI never takes focus (`unity-ui.md`).

## Extending

1. Implement `ITool`, keep the draft in the tool, and call the Sim validator for previews.
2. Register it in `GameSceneGenerator` (the tool object, its picker, `ToolPanel`, `TopBar` and `ToolController` references).
3. Add an entry to `ToolEntry.Defaults()` so it appears on the build bar.
4. Send the command through `SimHost.Execute` only.

## Gotchas

- `AreaTool.GhostEnabled` is false by default (the ghost is built but not shown).
- Right-click point removal in `AreaTool` has no minimum point check.
- `CameraRig` refuses left and right mouse with a tool active; middle, scroll and keys still work.

## Tests

`Assets/Tests/PlayMode/`: `ToolControllerTests`, `RoadToolTests`, `RoadToolCursorTests`, `AreaToolTests`, `AreaCursorGhostTests`,
`CameraRigTests`, `GroundPickerTests`, `CitizenPanelTests`, `DebugSendTests`, `CursorMarkerTests`. No ModuleTool test file is listed
here; check before relying on one.
EditMode: `ToolInputTests`.
