# Direction: M22 to M24 and beyond (19/11/2026)

Owner decisions from the 19/11 direction session. Nothing here is a ticket; the planner turns it into milestones. Where this disagrees with a design doc, this wins until the planner amends that doc.

## Milestones

| Milestone | Theme | Essential | Cut line |
|---|---|---|---|
| M22a UI and tools | UI foundations and tool feel | `SimLog` and F5 log first. `ToolInfo` and one shared tooltip service. Category bar and build bars per `UI_Design_V2` (UIW-06/07, 20..25). Shared building panel and conversion of every panel (including thatcher, daub pit, house Upgrade). Top bar hover panels (UIW-10..13). 64x speed. Tool-input contract. Demolish road. Oxen post. Work areas open only from their owning building. Module undo and Modules Management. Module snapping to curves and corners. Roads: clear trees, treat footprints as obstacles, show static areas. UI consistency audit against the spec | Final polish after the audit |
| M22b Houses, access, camp | Placement and settlement rules | Road Access orb, status, panel line, minimal alert. Houses with no road requirement (front edge, forecourt, rotate, adjacency, alley, whole-plot fence). Camp handover and removal rework with grouped piles. Evict and relocate residents; workplace info; job removal and relocation | Fence shape overhaul |
| M23a Labour | Where people work | On-site presence for build and workshop progress; work spots outside the footprint; road-only carrier pathing; handler stickiness | Builders' yard and multi-person tasks are future |
| M23b Logistics | Haul board | Haul jobs with reservations on the ledger; target stock; hard tiers; waiting time and distance inside a tier; debug view | Weighted scoring only if play needs it |
| M24 Code check | Debt and tidy | Panel and tool consolidation; Unity hierarchy tidy; doc debt; `ModuleTypes` and `WorkshopDef` numbers read `SimTuning` (decision 0063; arch-1's ignored tests are the to-do list); unused `Weaver*`/`Wattle*`/`ThatcherHut*` tuning fields wired up; jscpd findings tagged "fix in M24" | |
| After M24 | Placeholder graphics pass, then World MVP (FG-01..03, FG-07, FG-09) | | |

**Split 19/11/2026 (planner, after plan review).** M22a becomes three milestones, each with its own play check: **M22a** SimLog, bars, tooltips, tool input, 64x. **M22b** shared panels and every panel conversion (gated on M21 closing, T-595). **M22c** roads respecting buildings, and the Oxen post. The old M22b (houses, access, camp) becomes **M22d**. M23a, M23b and M24 get tasks once M22d is planned in detail and their design docs exist.

Dependencies: `SimLog` before M23. Shared panel and `ToolInfo` before further panels; M20 and M21 panels convert in M22a. Road Access status and alert (M22b) before road-only carrier pathing (M23a). On-site presence before the haul board. M21's fence and gate rules (HL-10, HL-12, HL-12c) are built on road frontage; M22b reworks them.

## Tool-input contract (amends ui.md UIB-06, CAM-A5)

Applies to every tool. Right-click or Backspace, in order:
1. Pop the last step (road node, area point, module), or cancel the placement in progress.
2. Deselect the building or tool.
3. Close the open category.
Escape does the same.

**Deleting things:** right-click delete under the cursor is removed.
- **Plots** are removed from their own menu (house panel and the like).
- **Roads** use a **Demolish road** button shown just above the build bar while the road tool is active. Clicking it enters demolish mode; a left click on a road demolishes it. Right-click leaves demolish mode first, then follows the order above.

## UI spec decisions (`UI_Design_V2/MVPv2_UI_Wireframe_Spec.md`)

The spec wins over the current UI, except for the corrections below. Planner: correct the spec text.

- **Removal stays as built** (CN-50..58). An unbuilt site drops its logs with no refund. A built module drops 50% as an unowned pile on the ground. Nothing moves to storage automatically. Spec section 9 messages must say this, not "returned to the nearest storage".
- **Granary and storage yard keep 2 family slots** (spec UIW-60 says 1).
- **Storage yard shows General goods only** (no logs). Logs live at the woodcutter or as input items (spec UIW-60 lists Logs).
- **Message bar: newest at the top** (spec UIW-04). Current UI shows newest last.
- **Oxen:** keep the "Oxen: n" button on the General tab for now. **Oxen post** is also a standalone Logistics building (spec UIW-21), later upgradeable to a stable and then a large stable. Reading to confirm: the post is a shared stand for the pool, and the oxen stand module stays tied to its operation.
- **"Sawyers yard"** is the correct name. Rename "Sawpit" in UI text.
- **Top bar items** become hover panels.
- **64x speed is required** (UIW-02). Planner: check the pacer cap and a performance budget (it doubles the load of 32x) and update the CLK rules.
- **Modules Management** is the Modules tab plus Expand flow: placing modules with undo; removal of unbuilt modules happens there. Standalone buildings keep their panel Deconstruct. Operation panels hold modules only.
- **Not in the spec:** tool tooltips (cost, needs, produces, from `ToolInfo`), the alert badge, the "No road access" line, the Demolish road button, and the house, market, citizen, thatcher and daub-pit panels. The consistency ticket extends the template to them.

## Camp (amends camp.md CMP-20..24)

- Citizens eat from the camp until a market and a granary both exist (the handover).
- At the handover everyone eats from the market and the granary takes the camp's food.
- The camp stays while any family is homeless. When all are housed and the handover has happened, the camp is removed and its contents drop as grouped piles: food, General goods, construction goods.
- Ground piles of General goods become valid construction sources. Granary families collect the camp food pile.

## Houses and road access (amends homes.md HM-02, HM-04, household-levels.md HL-10..12c, GDD HSE-01)

- A plot needs no road; houses may touch. The first two polygon nodes define the front edge; a rotate button chooses which edge is the front.
- Each building shows a small non-snappable orb (the house's is the ~5 m forecourt). When it overlaps the road network the building has road access.
- Without access a non-gatherer building does not work; a gatherer works but cannot move stock to storage. The panel shows "No road access" at the top only when it applies. An alert tells the player.
- Goods carriers use paths to and from buildings. People may leave paths to gather.
- For now a plain fence goes around the whole plot. Where a front edge meets a neighbouring house, no fence closes it: an alley (household traffic only, no logistics network) is left. Fence shape overhaul comes later.
- Bug to fix now: a plot beside a road and another plot is wrongly refused.
- Defaults adopted unless the owner objects: the network means a road connected to the trunk; only goods carriers must use roads; construction sites are exempt from access; a house with no access is built but stays vacant (current residents stay, alert fires, no eviction); alleys do not extend access to a backyard house.
- Alert (minimal, FG-10): condition-based, derived, clears itself; a top-bar badge and an icon over the building.

## Labour and logistics (amends construction.md, people.md, tasks.md)

- Builders stay odd-jobbers (CN-25). They work on-site next to what they build, inside the area but not under the building. Progress only counts while present. Same for workshop work. A later builders' yard takes families who only do complex builds.
- A job is a citizen plus an ox. A citizen who leads an ox keeps leading it when the next job suits (handler stickiness).
- Ox load is 1 for logs and beams. Later, carts carry 2 beams; logs are only dragged by oxen.
- Haul board: reuse the ledger reservations (0035, 0055); derive jobs each planning tick, save only waiting-time start. Hard tiers: construction first, then output collection under storage pressure, then inputs against target stock, then stockpiling. Assigned oxen never leave their building. First pass covers ox jobs only; hand carries stay. Weights and thresholds are `SimTuning` properties with integer scores.
- Debug: categorised `SimLog` (F5), tick-stamped, ring buffer, no save section.

## Roads

Roads treat static footprints as obstacles and show static areas while drawing. Trees under a road vanish with no logs. A later ground-clearance stage before building is undecided.

## Rules to adopt

- New building panels use the shared template; an exception needs a decision record (CLAUDE.md lessons and ui.md edit, done by the planner).
- UI reads `*Report` read models only, so a known/reported state layer can be added later.
- Foundation principle for future systems: actual state, known state, reported state. The Sim knows everything; the player may not. Build none of it now.

## Parked (after M23, not scheduled)

- Baskets per household (carry double, one market trip a fortnight, or a penalty without one); free day each week; village policy on Sunday work.
- Citizen schedule and agency (needs per week, home chores, per-profession hours). Needs its own design doc. Cheap first step: shift windows as data in `WorkDay`.
- Multi-person tasks; builders' yard; carts; Oxen post upgrades (stable, large stable).
- Wattle fence variety; ground-clearance stage; alert when no oxen are free for building.
- More dynamic house placement on the plot (position, orientation and size varied) for a more appealing townscape (owner 19/11/2026). Needs its own design pass; HM-04 and HP-03 stay the rule until then.
- Post-World: regional map, imperfect information, military, regional transport and trade routes, administration.

## Open (resolved 19/11/2026)

1. Oxen post: confirmed. It is a building (not a module) and works as an oxen stand for the whole town. Oxen with no stand, post or workplace linger where they were left.
2. Road access: the defaults stand, with two changes. **Everyone** uses roads, not only goods carriers: someone off the road heads to the nearest road first. Gatherers go by road to the point nearest their target, then leave it freely. Alleys **do** give access to a backyard house. Rules are in `design/road-access.md`, and the M23a pathing covers everyone.
3. Test area tools go in a Debug category. Pinned panels are capped at 4. M22a stays one milestone.
