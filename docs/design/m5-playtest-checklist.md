# M5 playtest checklist (for T-120 "Owner: play M5 and sign off")

Play the Game scene and tick each line. Any line that fails becomes a fix task. The rule IDs point to the doc
that says what "right" means.

## Camera (T-118, already built)
- [ ] Middle-drag turns and tilts around the screen centre at the current zoom; the camera never dips into a hill (CAM-A1, A3)
- [ ] Q/E do nothing (CAM-A4)

## Roads
- [ ] Start a road on a dead end: the new road carries straight on with no kink, and the two surfaces meet flush with no gap or overlap (RD-16)
- [ ] Start a road on the middle of a road: an ordinary T-junction, no forced direction (RD-16 exemption)
- [ ] With no draft, hover a road: node handles show at its ends and junctions (RD-17)
- [ ] Drag a road end: the road reshapes live and stays put on release (RD-17)
- [ ] Drag a junction: every road attached to it follows (RD-17)
- [ ] Drag a node somewhere too steep or across another road: the preview goes red with a reason, and on release the node goes back (RD-17)
- [ ] Escape or right-click while dragging a node puts it back (ui.md)
- [ ] Trees in the moved road's new path disappear (RD-11)
- [ ] Clicking (not dragging) a node still starts a branch (roads.md step 6)
- [ ] A ring the width of the road follows the cursor, and it jumps and changes colour when it would snap (RD-18)
- [ ] Snapping onto a road feels a little easier than in M3 (16 px, RD-18)

## Areas
- [ ] Click points in a crossing order (hourglass): red, "edges cross", Confirm disabled (AR-11)
- [ ] Make an hourglass by dragging a point, inserting a point and bending an edge: each is refused the same way (AR-11)
- [ ] A sphere sits under the cursor and changes colour when snapped (AR-20)
- [ ] Ghost edges show the shape the next click would make (AR-21)
- [ ] Near a road, dots every 2.5 m show along its sides within about 25 m, and points snap to them (AR-22)
- [ ] Snap one point to a road side with the previous point near (≤ 10 m) the same side: that point is pulled onto the road and the edge follows the road (AR-23)

## Tuning in the Inspector
- [ ] Select the `SimConfig` asset: road max grade, road width, area limits and the `SimTuning` values are there, with rule IDs in the tooltips (decision 0019)
- [ ] Change max grade to 30%, press Play: a slope that was red before is now valid. Set it back afterwards

## Not in M5 (see future-goals.md)
Larger map, chunking and streaming, map authoring, enemy AI.
