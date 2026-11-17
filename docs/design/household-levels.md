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
| HL-10 | A built house's panel has **Build fence**. The fence runs along every plot edge **except road frontage** | PROPOSED |
| HL-11 | It costs **1 wattle panel per 2 m** of fenced edge (rounded up) and 20 worker-minutes per panel, built by odd-jobbers as a normal site | PROPOSED |
| HL-12 | A fence does not block walking (yet) and is not needed to live in the house. It **is** a level 2 need (HL-21) | PROPOSED |
| HL-13 | Deconstructing the house takes the fence with it (refund share as CN-5x) | PROPOSED |

## Household levels

| ID | Rule | Tag |
| --- | --- | --- |
| HL-20 | The level is stored on the **household** (the family). It starts at 1 and never drops (HSE-10). Moving house keeps it | PROPOSED |
| HL-21 | Levels and needs are data (HSE-09). **Level 2 needs:** *Diverse food*: the family's last Sunday allotment held **2 or more food types** and it is not Hungry (HSE-12 made per household). *Fenced plot* (HL-12). *Water* and *Church*: counted as met (HSE-19) | PROPOSED |
| HL-22 | Needs are checked once a day. When every level 2 need is met, the household **reaches level 2 at once** and the message log says so. The house panel shows each need ticked or not | PROPOSED |
| HL-23 | **Renovation (HSE-20) and house tier 2 are deferred.** In M21 a level 2 household still lives in a tier 1 house. Renovation recipes (10 beams + 6 planks + 10 daub was the draft) come with the house upgrade milestone | DECIDED (owner 17/11/2026: upgrades after M21) |
| HL-25 | Level 2 **gives nothing yet**. Artisan backyards and house upgrades come after M21 | DECIDED (owner 17/11/2026) |
| HL-26 | The top bar tooltip shows households by level | PROPOSED |

> Note (HL-22 vs HSE-20): the GDD wants a renovation before a level applies. With renovation deferred, M21 grants the
> level when the needs are met. When upgrades arrive, the owner decides whether existing level 2 households must renovate.
