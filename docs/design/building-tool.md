# Building tool: draft, confirm, expand (M12)

How the player places an operation (woodcutter now, forager in M14) and adds modules later. Replaces the M8–M10
flow where the area is confirmed first and each module is a separate command. Placement rules (WC-02..08), costs
(CN-02, CN-10..12) and construction are unchanged. Owner notes 15/11/2026. Technical shape: decision 0037.

Tags: DECIDED = owner agreed. PROPOSED = lead's default, owner may change. OPEN = not decided.

## Can I start it?

| ID | Rule | Tag |
| --- | --- | --- |
| BT-01 | An operation's **minimum cost** = the summed recipes of its mandatory modules at their minimum count. Woodcutter: hut 8 + one log storage 0 = **8 logs** | DECIDED |
| BT-02 | The build bar entry is **disabled** while available logs (CN-11) < minimum cost. Tooltip: *Needs 8 logs (have 5)*. It re-checks live, so it lights up as soon as logs arrive | DECIDED |
| BT-03 | The same gate applies to every buildable entry with a cost (house plot M13, granary M14) | PROPOSED |

## The draft

| ID | Rule | Tag |
| --- | --- | --- |
| BT-10 | Clicking the entry starts a **draft**. Nothing reaches the Sim until Confirm | DECIDED |
| BT-11 | **Area stage:** draw the area as today (points, bends, snapping). Closing a valid shape (Enter, double-click or the *Next: modules* button) locks it and moves to the module stage. To change the shape, Cancel and start again | DECIDED (owner 15/11/2026) |
| BT-12 | **Module stage:** the tool panel lists the type's modules (Hut, Log storage, Oxen stand) with their cost. Pick one, it follows the cursor (Q/E, snapping as WC-04), click to place a **ghost** module. Ghosts are checked against the draft area, each other, existing footprints and roads, exactly as WC-03 | DECIDED |
| BT-13 | **Cost while drafting:** each ghost adds its recipe to a running total shown in the panel (*Cost 9 logs, have 12*). A ghost that would take the total over available logs is refused: *Not enough logs (have 12, need 13)* | DECIDED |
| BT-14 | **Right-click on a ghost module removes just that ghost** (not an undo of the last one). Right-click on empty ground does nothing in the module stage | DECIDED |
| BT-15 | **Confirm** is enabled only when: the area is valid, every mandatory module is placed at its minimum count (hut 1, log storage 1), and the total is affordable. While disabled it says what is missing (*Place a Hut*) | DECIDED |
| BT-16 | Confirm places the area and all ghost modules in **one command**, all or nothing. Each module becomes a construction site as in M10, reserved nearest-source-first in placement order | DECIDED |
| BT-17 | **Cancel** (button, or Escape with no module in hand) throws away the area and every ghost. Escape with a module in hand only drops that module | DECIDED (Escape detail PROPOSED) |
| BT-18 | Max counts still apply per type: hut exactly 1, log storage and oxen stand any number (WC-05) | DECIDED |

## Expand modules

| ID | Rule | Tag |
| --- | --- | --- |
| BT-20 | The operation panel has **Expand modules**. It opens the module stage on the existing area (existing modules shown solid, new ones as ghosts) | DECIDED |
| BT-21 | Same rules as BT-12..14. Confirm needs at least one ghost and an affordable total, and places them in one command. Cancel discards the ghosts | DECIDED |
| BT-22 | Modules being built **never stop built ones working**: the operation keeps producing; only the new module is unusable until Built (CN-04). The operation itself stays unusable until its mandatory modules are Built, as now | DECIDED |

## Implementation notes

> Implementation note (BT-03): only the woodcutter has a cost gate. The house and granary entries do not exist yet, so
> nothing else is gated (`ToolEntry.CostLogs` is ready for them).
> Implementation note (BT-12, BT-20): Expand offers no hut when one already stands in the area. The hut count is capped at
> `HutCount` by the same rule as BT-18, and the Confirm text for Expand is "Place at least one module" (BT-21).

## Not in M12

Reshaping a confirmed area (FG-05), moving modules (BLD-12), multi-select removal.
