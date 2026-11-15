# 0037: An operation and its modules are placed by one command

**Context.** Until M11 the woodcutter tool sent `DesignateArea` on Confirm and then one `PlaceModule` per module. A
player could end up with an area and no hut, and Cancel couldn't undo what was already sent. M12 (`building-tool.md`)
wants a draft that is only committed when the mandatory modules are placed and affordable.

**Decision.**
- The draft (area points and ghost modules) lives only in the Unity tool. The Sim gives it pure queries:
  `BuildCost` (minimum cost of a type, cost of a module list, can afford) and a draft module preview that checks a
  module against a polygon and a list of other draft rectangles that aren't in the world yet.
- `DesignateOperation` (area + ordered module list) validates everything and applies everything in one `Apply`:
  area, operation, footprints, construction sites and reservations. Any failure rejects the whole command.
- `PlaceModules` (operation id + module list) does the same for Expand modules.
- Preview and command use the same validator code (decision 0028).
- `DesignateArea` stays for non-operation areas and tests; `PlaceModule` stays as the single-module case.

**Consequences.** No half-placed operations exist, so no "area without hut" state needs handling. The command log has
one entry per confirm. Ghost checks must include the other ghosts, not just world footprints.
