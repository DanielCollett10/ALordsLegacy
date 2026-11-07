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
| UIB-06 | **Escape** goes to the active tool first. If the tool has a draft, it cancels the draft and Escape stops there. Only when the tool reports it is idle does the ToolController deactivate the tool | DECIDED |
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

> Implementation note (panel visibility): the road tool and both area tools show the panel whenever they are active, with an idle hint
> ("Road · no points yet"). That matches `roads.md` ("the tool panel shows while the tool is active") but not the
> sentence above ("shown only while a tool has a draft"). Owner to pick one.
>
> Implementation note (invalid label): when invalid, line 1 shows the bare reason (e.g. `too steep`), without the
> `Invalid:` prefix shown above.

## Top bar addition

- **Snapping: On/Off** toggle on the right (UI-02). It is a presentation setting (`ViewSettings`), not Sim state.
  Default On. Pressing **Alt** toggles it, exactly like clicking the button (not a hold).

## Input summary

| Input | Road tool | Area tools |
| --- | --- | --- |
| Left-click | add point (on a road: start a branch) | add point; on an edge: insert point; on point 1: close |
| Left-drag | move a placed point | move a point; drag the edge handle to bend |
| Double-click / Enter | finish and confirm | close / confirm |
| Right-click | cancel the draft; with no draft: delete the segment under the cursor | on a point: remove it; elsewhere: cancel the draft; with no draft: delete the area under the cursor |
| Backspace | remove the last point | remove the last point |
| Escape | cancel the draft, or leave the tool | same |
| Alt (press) | toggle snapping on/off | same |
| Middle mouse, scroll, keys | camera (unchanged) | camera (unchanged) |
| Middle-drag | camera orbit: turn and tilt | same |

Game pause and speed are unchanged by tools. Drawing works while paused.

**What a click hits** (all drawing tools, in this order, within 12 screen pixels): a draft **vertex**, then an
edge's **bend handle** (area tools), then a draft **edge**, then a confirmed object of the tool's kind (road
segment or area: used for right-click delete and road branching), then **ground**. A press that moves more than
4 pixels before release is a drag; otherwise it is a click.

**Camera vs tools.** The camera keeps middle mouse (rotate), scroll (zoom), keys and screen-edge pan. While a tool
is active, left and right mouse buttons belong to the tool; the camera never pans or rotates from them. Edge
pan keeps working while a point is being dragged.

**Right mouse is click-only for tools.** Tools act on a right **click** (release without moving more than 4 px),
never on a right press or drag. Right-drag is kept free for a possible later use.

## Keyboard and UI focus

| ID | Rule | Tag |
| --- | --- | --- |
| UIK-01 | Game UI buttons (top bar, build bar, tool panel) never take keyboard focus. Clicking them must not leave a focused element | DECIDED |
| UIK-02 | Keyboard UI navigation (WASD, arrows, Tab) is off in game. Camera and tool keys must never change UI selection (bug: A/D cycled the speed buttons) | DECIDED |

## Camera controls (milestone M5, in code)

| ID | Rule | Tag |
| --- | --- | --- |
| CAM-A1 | **Middle-drag orbits** the camera around the ground point under the screen centre, at the current zoom distance: horizontal drag turns (yaw), vertical drag tilts (pitch) down towards the ground or up towards the horizon, within pitch limits. Middle-drag turns and tilts (in code) | DECIDED |
| CAM-A2 | **Scroll** zooms as now: in = down towards the ground, out = up | DECIDED |
| CAM-A3 | The camera never goes below the ground (Sim terrain height + clearance) or outside the map, while orbiting, tilting, zooming or panning. If a tilt would push it into a hill, it is held above the ground | DECIDED |
| CAM-A4 | **Q/E rotation is removed.** Q and E are left free (no binding) for now | DECIDED |
| CAM-A5 | **Right-click** (no drag) cancels: a tool draft first; later, it closes the topmost unpinned popup (UI-03/04). In road or area mode with no draft, it deletes the object under the cursor (roads.md, areas.md). Right-drag does nothing for now (kept free) | DECIDED |
