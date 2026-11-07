# 0017: Snapping is a Unity-side presentation setting; the Sim never snaps

**Context.** Players want drawn points to land on road ends and centre lines (and later area edges). Snapping must not
change what is valid, and the Sim must give the same answer for the same points in tests and in play.

**Decision.**
- `ViewSettings.Snapping` (default On) and `SnapDistancePx` (12) live in `Assets/Game/UI/`. The top bar button and the
  Alt action toggle them. The Sim never reads them.
- Tools choose the point to send. `RoadTool` uses `RoadNetwork.FindNearest` as the query and `SnapPicker` as the choice.
- Commands carry exact integer-mm points. `DrawRoad` validates whatever it gets and never snaps.
- Junctions are made whatever the snapping setting (RD-07). Snapping only helps the cursor land on them.

**Consequences.** Tests and replays are unaffected by the toggle, and a snapped point and a typed point are the same
command. Future snapping (areas) reuses the same toggle and the same Sim queries, and the tool stays in charge of
which point is chosen.
