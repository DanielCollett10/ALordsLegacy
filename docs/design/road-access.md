# Road access (M22b; movement rules built in M23a)

Owner direction 19/11/2026 (`docs/direction/m22-m24-direction.md`) and owner answers the same day. Amends homes.md HM-02
and HM-04, household-levels.md HL-10, HL-12 and HL-12c, and GDD HSE-01. All distances are PLACEHOLDER `SimTuning`
properties (follow the mirror rule).

## The network and the orb

| ID | Rule | Tag |
| --- | --- | --- |
| RA-01 | The **road network** is every road segment joined, through junctions, to the trunk road. A road that isn't joined to the trunk is not part of it. Derived whenever roads change; not saved | DECIDED |
| RA-02 | Every building has an **access orb**: a small marker (radius 2 m) just outside its access point. It is not snappable and it is not an obstacle. A house's orb is its **forecourt**: 5 m deep, centred on the front edge | DECIDED |
| RA-03 | A building **has road access** when its orb overlaps a network segment (counting the road's width) | DECIDED |
| RA-04 | **Alleys pass access on.** A house whose forecourt reaches an alley (HP-04) has access if the alley leads to a house or road that has access. Access spreads through chains of alleys | DECIDED (owner 19/11/2026; chaining PROPOSED) |
| RA-05 | **Construction sites are exempt.** A site is supplied and built whether or not it has access | DECIDED |

## What happens without access

| ID | Rule | Tag |
| --- | --- | --- |
| RA-10 | A **non-gatherer** (workshop, granary, storage yard, market, oxen post) without access does not work: it creates no tasks. Its assigned families stay assigned (their slots are kept) but do **odd jobs** until access returns, then go back to work (owner 19/11/2026). Status: *No road access* | DECIDED |
| RA-11 | A **gatherer** (woodcutter, forager, coppice hut, thatcher's hut) **always works** into its own store, access or not. Granary and storage yard families collect from it **only when both buildings' orbs touch the same connected road network**. Otherwise its store fills and it stops at *Storage full* as today (owner 19/11/2026) | DECIDED |
| RA-11b | "Same network" means the same connected group of road segments. Each building's network is derived along with RA-01 (not saved). Any collection or delivery between two buildings (yard, granary, workshop fetch) uses this same test | DECIDED (extension to workshop fetches PROPOSED) |
| RA-12 | A **house** without access is still built, but no new family moves in. A family already living there stays (no eviction). The house panel shows *No road access* | DECIDED |
| RA-13 | Access is re-checked whenever roads, plots or buildings change. Getting access back takes effect at once | DECIDED |

## Showing it

| ID | Rule | Tag |
| --- | --- | --- |
| RA-20 | The orb is drawn while placing and while the building is selected: green with access, red without | DECIDED |
| RA-21 | The panel's first line under the title reads *No road access* (warning colour), only when it applies | DECIDED |
| RA-22 | **Alert** (minimal FG-10): one alert per building without access. Alerts are derived from conditions, clear themselves and are not saved. A top-bar badge shows the count and an icon floats over each building | DECIDED |

## Movement on roads (built in M23a)

| ID | Rule | Tag |
| --- | --- | --- |
| RA-30 | **Everyone uses roads.** A trip from A to B is: walk off-road from A to the nearest network point, go along the network, then walk off-road from the network point nearest B. Someone who is off the road (left somewhere, just arrived) heads to the nearest road first | DECIDED (owner 19/11/2026) |
| RA-31 | **Gatherers** (felling, foraging, cutting rods, cutting reed) go along the road to the network point nearest their target, then leave the road freely to reach it, and come back the same way | DECIDED (owner 19/11/2026) |
| RA-32 | Household trips (home, larder, market) may use alleys and the forecourt. Goods carriers use the network only between orbs | DECIDED |
| RA-33 | If B has no network point within reach (no access), the trip walks off-road from the nearest network point, as RA-31 | PROPOSED |

## House plots (amends HM-02, HM-04, HL-10..12c)

| ID | Rule | Tag |
| --- | --- | --- |
| HP-01 | A plot **needs no road**. Plots and houses may touch each other. *A plot must face a road* is gone | DECIDED |
| HP-02 | The **first two nodes** the player places set the **front edge**. A *Rotate front* button (in the tool panel while drawing, and on the house panel while the plot is unbuilt) moves the front edge to the next edge | DECIDED |
| HP-03 | The house sits on the front edge as HM-04 describes (centred, set 1 m back), facing the forecourt | DECIDED |
| HP-04 | **Fence (replaces HL-10/HL-12c):** the fence runs round the **whole plot**, with a gate at the forecourt. Where the front edge meets a neighbouring house, that stretch is left open as an **alley**: household traffic only, not part of the road network (RA-32), but it passes access on (RA-04). The fence cost (HL-11) counts fenced length only | DECIDED (shape overhaul later) |
| HP-05 | Bug: a plot beside a road **and** another plot is refused. It must be accepted | DECIDED |
