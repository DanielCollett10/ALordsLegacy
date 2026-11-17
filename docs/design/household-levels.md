# Tier 1 houses: daub, fences and household levels 1–2 (M21)

Completes the tier 1 house and adds the first step up. Implements GDD HSE-09..12, HSE-19..22, CON-16 in a small form.
Lead's proposal 17/11/2026, for the owner to review. Builds on `homes.md`, `materials.md`, `planks.md`.

Tags: DECIDED = owner agreed. PROPOSED = lead's default, owner may change. OPEN = not decided.

## Daub

| ID | Rule | Tag |
| --- | --- | --- |
| HL-01 | **Daub** is a General item (light, carried by hand 2 a trip). Made at a **daub pit**: a workshop (decision 0047) with **no input item** (local earth and water), placed like the granary, 4 × 4 m, 4 logs, 240 min, 1 family slot, light work 8+ | PROPOSED |
| HL-02 | A daub pit family makes **10 daub per working day**; the pit stores 20 | PROPOSED |
| HL-03 | Straw-tempered and clay daub (CON-16, CON-19) come with farming and clay pits, later | DECIDED (GDD) |
| HL-04 | A new **tier 1 house** costs **10 logs + 10 thatch + 10 panels + 10 daub** (MAT-40 plus daub). The camp starts with **90 daub** too (9 houses, like MAT-36) | PROPOSED |

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
| HL-20 | The level is **the house's tier** (GDD: household level = house tier). It is stored on the house. A family that moves into a tier 2 house is level 2. Levels never drop (HSE-10) | PROPOSED |
| HL-21 | Levels and needs are data (HSE-09). **Level 2 needs:** *Diverse food*: the family's last Sunday allotment held **2 or more food types** and it is not Hungry (HSE-12 made per household). *Fenced plot* (HL-12). *Water* and *Church*: counted as met (HSE-19) | PROPOSED |
| HL-22 | Meeting every need makes the house **eligible**. Its panel shows each need ticked or not, and **Renovate to tier 2** when eligible (HSE-20) | PROPOSED |
| HL-23 | **Renovation to tier 2** is a construction site on the existing house: **10 beams + 6 planks + 10 daub, 720 min** (HSE-21 for tier 2; PL-11b). The family stays living there. When it finishes the house is tier 2 with a new look | PROPOSED |
| HL-24 | Eligibility is checked once a day; if a need lapses during a renovation, the renovation still finishes | PROPOSED |
| HL-25 | What level 2 **gives** the player (GDD: artisan backyards, not built yet). See open question below | OPEN |
| HL-26 | The top bar tooltip shows households by level | PROPOSED |

## Open

- HL-25: options are (a) nothing yet beyond the look and the count, (b) level 2 families work faster, (c) arrivals
  need a share of level 2 households to keep coming. Lead leans (a) for M21 and makes artisans the payoff later.
