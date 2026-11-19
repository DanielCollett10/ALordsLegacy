# Tier 1 houses: daub, fences and household levels 1–2 (M21)

Completes the tier 1 house and adds the first household level step. Implements GDD HSE-09..12, HSE-19, CON-16 in a small
form. Lead's proposal 17/11/2026, owner answers the same day. Builds on `homes.md`, `materials.md`, `planks.md`.

Tags: DECIDED = owner agreed. PROPOSED = lead's default, owner may change. OPEN = not decided.

## Daub

| ID | Rule | Tag |
| --- | --- | --- |
| HL-01 | **Daub** is a General item (light, carried by hand 2 a trip). Made at a **daub pit**: a workshop (decision 0047) with **no input item** (local earth and water), placed like the granary, 4 × 4 m, 4 logs, 240 min, 1 family slot, light work 8+ | PROPOSED |
| HL-02 | A daub pit family makes **10 daub per working day**; the pit stores 20. Sites fetch daub from the pit directly; yards do not collect it (ST-42b) | PROPOSED |
| HL-03 | Straw-tempered and clay daub (CON-16, CON-19) come with farming and clay pits, later | DECIDED (GDD) |
| HL-04 | A new **tier 1 house** costs **10 logs + 10 thatch + 10 panels + 10 daub**. The camp starts with **90 daub** too (9 houses, like MAT-36) | DECIDED (owner 17/11/2026) |

## Fences

| ID | Rule | Tag |
| --- | --- | --- |
| HL-10 | **M22d: the fence line is replaced by road-access.md HP-04 (whole plot, gate at the forecourt, alleys); HL-12 and HL-12c follow it.** A built house's panel has **Build fence**. The fence runs along every plot edge **except road frontage** | PROPOSED |
| HL-11 | It costs **1 wattle panel per 2 m** of fenced edge (rounded up) and 20 worker-minutes per panel, built by odd-jobbers as a normal site | PROPOSED |
| HL-12 | A built fence **blocks walking** (it is solid). The unfenced road frontage is the way in and out, drawn as an **open gate** at the house's access point. People inside a fenced plot walk round to the frontage to leave, and nobody cuts across a fenced plot. A fence under construction does not block yet | DECIDED (owner 18/11/2026) |
| HL-12b | A fence is not needed to live in the house. It **is** a level 2 need (HL-21) | PROPOSED |
| HL-12c | A fence never closes a plot completely: the road frontage is always left open, so the house can always be reached | PROPOSED |
| HL-13 | Deconstructing the house takes the fence with it (refund share as CN-5x). How it is built: decision 0053 | PROPOSED |

## Household levels

| ID | Rule | Tag |
| --- | --- | --- |
| HL-20 | The level is stored on the **household** (the family). It starts at 1 and never drops (HSE-10). Moving house keeps it | PROPOSED |
| HL-21 | Levels and needs are data (HSE-09). **Level 2 needs:** *Diverse food*: the family's last Sunday allotment held **2 or more food types** and it is not Hungry (HSE-12 made per household). *Fenced plot* (HL-12). *Water* and *Church*: counted as met (HSE-19) | PROPOSED |
| HL-22 | **Owner 19/11/2026: needs are derived whenever they are read (panel, `UpgradeHousehold`), not checked once a day; this replaces the daily check below.** Was: Needs are checked once a day. The house panel shows each need ticked or not. When every level 2 need is met the household is **eligible** and the panel shows an **Upgrade** button. The player presses it and the household becomes level 2; the message log says so. Nothing is automatic (HSE-20) | DECIDED (owner 17/11/2026) |
| HL-23 | In M21 the Upgrade button **costs nothing and is instant**. Renovation materials, labour and the tier 2 house look are added to the same button later (the draft was 10 beams + 6 planks + 10 daub) | DECIDED (owner 17/11/2026: upgrade costs after M21) |
| HL-25 | Level 2 **gives nothing yet**. Artisan backyards and house upgrades come after M21 | DECIDED (owner 17/11/2026) |
| HL-26 | The top bar tooltip shows households by level | PROPOSED |

> Note (HL-22, HL-23): the player always orders the upgrade (HSE-20). M21 has the button without a cost; the
> renovation cost joins it later, so no level 2 household ever skips the button.

> Implementation note (HL-22): `HouseholdNeedsReport` derives needs on each read, for the panel and for `UpgradeHousehold`.
> Owner kept this as the rule on 19/11/2026.

> Owner 19/11/2026 (HL-01): the daub pit's yard is **5 m**, like the granary. Task dp-1 changes it from the 3 m below.
> Implementation note (HL-01): the daub pit's yard is now 5 m, like the granary (T-627; was 3 m, like the workshops).
> Its recipe (4 logs, 240 min), size and yard are hard-coded in `ModuleTypes.DaubPit`, not read from `SimTuning`. Output (HL-02) is tuned.
