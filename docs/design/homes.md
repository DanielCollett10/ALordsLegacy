# Homes: plots and houses (M13)

Families stop living in tents. Implements GDD MVP-2-01 and the base of section 5 burgages (HSE-01..03, HSE-23).
Backyards, expansions, extra houses and household levels are not in M13. Construction: `construction.md`.
Building tool and cost gate: `building-tool.md`.

Tags: DECIDED = owner agreed. PROPOSED = lead's default, owner may change. OPEN = not decided.

## Drawing a plot

| ID | Rule | Tag |
| --- | --- | --- |
| HM-01 | Build bar **House plot**: draws a plot with the area tool (4–8 points, bends, snapping). Disabled while logs < the house recipe (BT-02) | PROPOSED |
| HM-02 | A plot needs **road frontage**: at least one edge snapped flush to a road (HSE-01, AREA-05). Otherwise refused: *A plot must face a road* | DECIDED |
| HM-03 | Plot area **60–400 m²** (HSE plot sizes, PLACEHOLDER). Too small: *Too small for a house*. In M13 extra space is just garden, with no use yet | PROPOSED |
| HM-04 | The **house is placed for the player** on the frontage edge: a 6 × 8 m footprint (PLACEHOLDER), centred on the longest frontage edge, its long side along the road, set 1 m back. While drawing, the house shows as a ghost; if it can't fit, *No room for the house* | PROPOSED |
| HM-05 | Confirm creates the plot and the house construction site in one command (like BT-16). Trees under the plot are cleared (BLD-10-A) | PROPOSED |
| HM-06 | House recipe: **10 logs, 720** worker-minutes (CON-A1 720). Tier 1, logs only (HSE-23) | DECIDED (owner 15/11/2026; values PLACEHOLDER) |

> Implementation note (HM-01, HM-06): the recipe is now 10 logs, 10 thatch and 10 wattle panels, 720 min (MAT-40). The build bar
> and the plot gate check the whole recipe, not only logs. An unbuilt site placed before M20 keeps the logs-only recipe (MAT-40b).

## Moving in

| ID | Rule | Tag |
| --- | --- | --- |
| HM-10 | A house holds **one family** (HSE-02, level 1) | DECIDED |
| HM-11 | When a house is **Built**, a Homeless family moves in **automatically**. Homeless families **with a workplace** come first, nearest workplace to the house (straight-line) first. If no homeless family has a workplace, any homeless family moves in (lowest family id). Ties by lowest family id. Player control over who lives where comes later | DECIDED (owner 15/11/2026) |
| HM-12 | Moving in changes the family's **home** (PPL-11): its members sleep there, mill about near it (PPL-30) and commute from it (PPL-25). Their tent at the camp is removed. Someone mid-walk carries on and uses the new home from their next routine change | PROPOSED |
| HM-13 | A family with a house is no longer **Homeless** (POP-A1) | DECIDED |
| HM-14 | Deconstructing a house (CN-50..57) makes its family Homeless again, with a tent back at the camp | PROPOSED |
| HM-14b | The plot goes with its house: when the house's deconstruction finishes (or an unbuilt house site is cancelled), the plot area is removed too. CN-58's refusal is for **operation** areas only. Right-click delete of a plot that still has a house is refused: *Deconstruct the house first* | PROPOSED |

> Implementation note (HM-14): the family goes back to the camp fire as its home point, not to a tent spot. The tent
> spot is not saved, so the evicted family's tent is drawn at the fire. Owner to decide whether the tent should return to
> its old spot (needs a saved spot) or the fire is fine.
> Implementation note (HM-11): a family with no members never moves in (not in the design). Owner to confirm.

## What the player sees

| ID | Rule | Tag |
| --- | --- | --- |
| HM-20 | A house site shows the scaffold (CN-42); a built house shows a placeholder house model | DECIDED |
| HM-21 | Clicking a house or its plot opens the **house panel**: state (*Delivering*, *Building n%*, *Built*), the household name and members, or *Empty*, plus High priority while unbuilt and Deconstruct | PROPOSED |
| HM-22 | The citizen panel shows *Home: <family> house* or *Homeless (camp)* | PROPOSED |

> Implementation note (HM-21): an unbuilt house's button reads "Cancel house" (not Deconstruct). After a confirm
> ("Cancel this unbuilt house? The site and its plot are removed at once.") it removes the site and its plot. The panel
> also shows *Deconstructing n%*.

## Not in M13

Backyards and expansions (HSE-04..05), household levels (HSE-09..), choosing which family moves in (later: finer
player control over family placement), larders (M14).
