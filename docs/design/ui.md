# UI: build bar, tool panel and tool input (M3 onward)

Implements tech spec section 17 (UI-01..08) and the mockups in `source/`. Layout and style are placeholders;
wireframes come later. UI Toolkit only. The UI reads Sim state and sends commands only (UI-07).

> Implementation note (code-built panels): `CitizenPanelView` and `OperationPanelView` build their whole UI in code.
> They have no UXML and no StyleSheet field, so CLAUDE.md's ".uxml/.uss" rule is not met for them. `TopBarController`,
> `ToolPanelView`, `GameMenuView` and `DebugOverlayView` take a UXML and StyleSheet when one is assigned, and build the
> same element names in code when none is (their own comments say so). Owner to decide whether the two code-only panels
> should move to UXML.

## Build bar (bottom of the screen)

| ID | Rule | Tag |
| --- | --- | --- |
| UIB-01 | A row of buttons centred along the bottom edge, one per placeable thing, built from a list of **tool entries** (id, label, enabled) rather than hard-coded in the UXML (UI-01) | DECIDED |
| UIB-02 | M3 entries: **Road** (enabled), **Test area** (enabled from M4), **Test work area** (enabled from M4). Future entries (Woodcutting, Storage yard, House, Granary) show greyed with a dashed border and the tooltip "Coming later" | DECIDED |

> Implementation note (UIB-02, M8): **Woodcutter** is enabled (`ToolEntry.Defaults`, tooltip "Draw the woodcutter's area, then
> place its modules"). Storage yard, House and Granary are still greyed "Coming later".
> Implementation note (UIB-02, M13): **House plot** is enabled (T-408), gated on the house's log cost. Storage yard and Granary
> are still greyed. This note replaces the M8 line above for House.
| UIB-03 | Clicking a button activates its tool and highlights the button. Clicking the active button again, or Escape with nothing in progress, deactivates it. Only one tool is active at a time | DECIDED |
| UIB-04 | Switching tools discards any unconfirmed draft | DECIDED |
| UIB-06 | **Escape** goes to the active tool first. If the tool has a draft, it cancels the draft and Escape stops there. Only when the tool reports it is idle does the ToolController deactivate the tool. **Amended M22a:** see the tool-input contract (UIT-01..05) below | DECIDED |
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

| ID | Rule | Tag |
| --- | --- | --- |
| UIP-01 | **Owner decision (M5): the panel shows only once something has started**, i.e. a draft has at least one point, or a road node is being dragged (RD-17), or a refused node move's reason is still showing. With a tool selected but nothing started, the panel is hidden to cut clutter. This replaces the current "always shown, 'no points yet'" behaviour | DECIDED |
| UIP-02 | With the panel hidden, the **cursor marker still shows** where the first point would go: the road ring (RD-18) and the area sphere (AR-20), including the snapped position and colour | DECIDED |
| UIP-03 | Cancelling a draft (Escape, right-click, Cancel button), confirming it, or removing its last point with Backspace hides the panel again | DECIDED |
>
> Implementation note (invalid label): when invalid, line 1 shows the bare reason (e.g. `too steep`), without the
> `Invalid:` prefix shown above.

## Top bar addition

- **Resources** (owner 17/11/2026, DECIDED): the top bar shows each resource after the date: *Food: N days*, *Logs: N*
  (camp stockpile, log storages and unowned ground piles), *Planks: N* (all General goods stores). Each new resource
  gets its own label for now. Later (direction DECIDED, not scheduled): compress into three hover menus, **Food** (every
  food type), **Construction goods** (logs, planks, thatch, panels…) and **General** (everything else), to be refined
  after it is built. **M22a:** built as the spec's hover panels UIW-10..13 (Citizens, Food, Construction, Goods); rows come
  from item definitions, so a new item appears without UI work.
- **Snapping: On/Off** toggle on the right (UI-02). It is a presentation setting (`ViewSettings`), not Sim state.
  Default On. Pressing **Alt** toggles it, exactly like clicking the button (not a hold).

## Input summary

| Input | Road tool | Area tools |
| --- | --- | --- |
| Left-click | add point (on a road: start a branch) | add point; on an edge: insert point; on point 1: close |
| Left-drag | move a placed point; with no draft, on a confirmed road node: move the node (RD-17) | move a point; drag the edge handle to bend |
| Hover | cursor ring (RD-18); with no draft, node handles show on the road under the cursor | cursor sphere, ghost edges, road snap dots (AR-20..22) |
| Double-click / Enter | finish and confirm | close / confirm |
| Right-click | UIT-01 (from M22a; no delete) | UIT-01 (from M22a; no delete) |
| Backspace | UIT-01 | UIT-01 |
| Escape | cancel a node drag (node goes back), else UIT-01 | UIT-01 |
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
| CAM-A5 | **Right-click** (no drag) cancels: a tool draft first; later, it closes the topmost unpinned popup (UI-03/04). ~~In road or area mode with no draft, it deletes the object under the cursor.~~ **Amended M22a:** right-click never deletes; it follows UIT-01. Right-drag does nothing for now (kept free) | DECIDED |

## Tool-input contract (M22a, direction 19/11/2026; amends UIB-06, CAM-A5 and the input summary)

| ID | Rule | Tag |
| --- | --- | --- |
| UIT-01 | **Right-click, Backspace and Escape** all do the first of these that applies, one step per press: (1) pop the last step of the placement in progress (road node, area point, module ghost); with no steps left, cancel the placement; (2) deselect the building or tool; (3) close the open category | DECIDED |
| UIT-02 | Escape with nothing to undo, no tool and no category open goes on to the next owner as today (close the topmost unpinned panel, then the Esc menu) | DECIDED |
| UIT-03 | **No right-click delete.** Right-click never removes a confirmed road, area or building | DECIDED |
| UIT-04 | **Demolish road**: while the road tool is active a "Demolish road" button shows just above the build bar. Clicking it enters demolish mode (cursor highlights the segment under it in red); a left click demolishes that segment. Right-click / Backspace / Escape leaves demolish mode first, then follows UIT-01 | DECIDED |
| UIT-05 | **Plots and areas** are removed from their own panel (house panel: Remove plot; operation panel: Deconstruct operation). Unbuilt modules are removed in Modules Management (UIM-01) | DECIDED |

Every `ITool` implements the contract through one shared step (`ToolController.Back()`), so a new tool cannot get it wrong.

## Modules Management (M22b; module undo in M22a)

| ID | Rule | Tag |
| --- | --- | --- |
| UIM-01 | Modules Management is the operation panel's **Modules** tab plus the Expand flow. Placing modules supports undo (UIT-01 pops the last ghost). Unbuilt modules are cancelled from their module card there | DECIDED |
| UIM-02 | Standalone buildings (granary, yard, workshops, house) keep Deconstruct on their own panel. Operation panels list modules only; the hut's Deconstruct operation stays in the General tab | DECIDED |
| UIM-03 | Module ghosts snap to curved area edges and into corners (presentation snap, decision 0017); the Sim still validates | DECIDED |
| UIM-04 | A work area opens only from its owning building's panel (Draw work area / select from the panel). Clicking a work area on the ground selects nothing | DECIDED |

## Building panels: shared template (M22b; decision 0057)

| ID | Rule | Tag |
| --- | --- | --- |
| UIS-01 | **Every building panel uses the shared template** (`MVPv2_UI_Wireframe_Spec.md` section 5, UIW-30..33): movable box, title "Name n", status line, Pin and Close, sections with upper-case headings, General / Modules tabs where the building has modules, the construction view (UIW-50) while unbuilt. A panel that cannot fit the template needs a decision record before it is built | DECIDED |
| UIS-02 | Panels read `*Report` read models only (never `SimWorld` collections directly), so a known/reported state layer can be added later | DECIDED |
| UIS-03 | The template also covers panels the spec does not draw: house, market, citizen, thatcher, daub pit. The consistency audit (M22b) lists each panel against the spec | DECIDED |
| UIS-04 | One unpinned panel at a time: selecting something else replaces it. Pinned panels stay until closed. At most 4 pinned panels; pinning a 5th unpins the oldest | DECIDED (owner 19/11/2026) |
| UIS-06 | **Debug category**: Test area and Test work area sit in a Debug category on the category bar, shown only in editor and development builds | DECIDED (owner 19/11/2026) |
| UIS-05 | Every tool button and building has a tooltip built from `ToolInfo` (cost, needs, produces), shown through one shared tooltip service | DECIDED |
