# Game design

_The human owns this folder. Agents read it but don't make design decisions._

**A Lord's Legacy** is a grounded medieval settlement sim: grow a 10–50 person camp into
a network of villages that funds a town with a keep and castle. Physical economy (every
log exists), real time and distance, freeform roads and plots, labour by family.

## Sources

- `source/gdd-medieval-settlement-regional-economy-simulation.md`: the owner's GDD v0.4
  (rule IDs such as `ROAD-04`, tags DECIDED / PROPOSED / PLACEHOLDER / OPEN).
- `source/technical-spec-medieval-settlement-regional-economy-simulati.md`: tech spec v0.2.
- `source/*.png`: UI mockups (top bar, build bar, popups, area drawing).

Where the tech spec conflicts with `CLAUDE.md` or `docs/decisions/`, the decisions win.

## Per-system docs

One file per system is being split out of the GDD (see the planning conversation).
Tasks cite rule IDs; keep the IDs when moving rules between files.
