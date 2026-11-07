# UI: build bar, tool panel and tool input (M3 onward)

Implements tech spec section 17 (UI-01..08) and the mockups in `source/`. Layout and style are placeholders;
wireframes come later. UI Toolkit only. The UI reads Sim state and sends commands only (UI-07).

## Build bar (bottom of the screen)

| ID | Rule | Tag |
| --- | --- | --- |
| UIB-01 | A row of buttons centred along the bottom edge, one per placeable thing, built from a list of **tool entries** (id, label, enabled) rather than hard-coded in the UXML (UI-01) | DECIDED |
| UIB-02 | M3 entries: **Road** (enabled), **Test area** (enabled from M4), **Test work area** (enabled from M4). Future entries (Woodcutting, Storage yard, House, Granary) show greyed with a dashed border and the tooltip "Coming later" | DECIDED |
| UIB-03 | Clicking a button activates its tool and highlights the button. Clicking the active button again, or Escape with nothing in progress, deactivates it. Only one tool is active at a time | DECIDED |
| UIB-04 | Switching tools discards any unconfirmed draft | DECIDED |
| UIB-05 | Clicks on UI never pass through to the ground (no point placed under a button) | DECIDED |

## Tool panel (just above the build bar, while drawing)

Shown only while a tool has a draft, as in `source/drawing-an-area-snapped-to-road.png`:

- Line 1: `<Tool>: <facts> · <Valid | Invalid: reason>`.
  - Road: `Road · 3 points · 142 m · max grade 9% · Valid`
  - Area: `Test area · 6 points · 1,240 m² · Valid`
- Line 2: a hint for the active tool's controls (as in the mockup).
- **Cancel** and **Confirm** buttons. Confirm is disabled while invalid.
- Invalid reasons are also labelled in the world near the problem when the tool can tell where it is
  (e.g. the red stretch of a too-steep road, or the overlapped area).

## Top bar addition

- **Snapping: On/Off** toggle on the right (UI-02). It is a presentation setting (`ViewSettings`), not Sim state.
  Holding Alt inverts it while held.

## Input summary

| Input | Road tool | Area tools |
| --- | --- | --- |
| Left-click | add point (on a road: start a branch) | add point; on an edge: insert point; on point 1: close |
| Left-drag | move a placed point | move a point; drag the edge handle to bend |
| Double-click / Enter | finish and confirm | close / confirm |
| Right-click | cancel the draft; with no draft: delete the segment under the cursor | on a point: remove it; elsewhere: cancel the draft; with no draft: delete the area under the cursor |
| Backspace | remove the last point | remove the last point |
| Escape | cancel the draft, or leave the tool | same |
| Middle mouse, scroll, keys | camera (unchanged) | camera (unchanged) |

Game pause and speed are unchanged by tools. Drawing works while paused.
