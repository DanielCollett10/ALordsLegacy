# M6 playtest checklist ("Owner: play M6 and sign off")

Each line matches one owner issue from M5 play (screenshots in `docs/design/source/`).

## 1. Drawn on the ground (decision 0020, AR-06, RD-19)
- [ ] Draw a long road over a hill. Zoom right out: the road stays visible along its whole length, with no gaps where it sinks into the terrain (screenshot 1)
- [ ] Draw a large area across a dip or rise: every edge follows the ground up and down and stays straight seen from above (screenshot 4)
- [ ] Same for a draft, a ghost and a dashed work-area outline
- [ ] Bent and road-following edges keep their curve and still follow the ground
- [ ] FPS on the test map is still comparable to M5 (look at the F3 overlay)

## 2-4. Edges join the points you placed (AR-30..33)
- [ ] Points 1 and 2 on a road, point 3 away from it: the edges run 2→3 and 3→1 straight to the points, never to a spot on the road (screenshot 2)
- [ ] Drag point 3 left, past point 2: edges still run 2→3 and 3→1, so the shape follows your points (expected-behaviour image)
- [ ] Screenshot 6: points 1 and 4 on the road, 2 and 3 away from it. Edge 3→4 is one straight line to point 4, not down to the road and back along it
- [ ] Click straight-edged points in a crossing order: they're still tidied into a clean shape (auto-reorder kept)
- [ ] A crossing draft the tidy-up can't fix (with a bent edge): red outline with crosses where it crosses, no fill, and Confirm is disabled
- [ ] A valid 4-point area next to a road: the fill exactly matches the outline, including along the road
- [ ] Screenshot 7: draw a plot whose edge cuts through the corner of a neighbouring test area (one that follows a road). The outline wraps around the neighbour's boundary, the panel says "fitted around …", and it confirms (AR-34)
- [ ] Draw a plot that would be split in two by a neighbour: still refused, "overlaps …"
- [ ] Draw a plot whose edge cuts across the corner of a road: trimmed back to the road edge, "fitted around road", and it confirms
- [ ] Draw a plot with a road running right through it: refused, "crosses road" (no split plots)
- [ ] A test work area over a test area is not trimmed

## 5. Can't place where it can't go (placement.md)
- [ ] Test area tool: move the cursor over an existing test area or onto a road. The sphere disappears and clicking does nothing (screenshot 5)
- [ ] Test area tool: snap to the edge or corner of an existing area or a road side. The sphere shows, and the point can be placed
- [ ] Test work area tool: points can go inside test areas and on roads
- [ ] Road tool: the ring disappears over a test area, and you can't click there; on a road you can still start a branch
- [ ] Drag an area point or a road node towards a test area: it stops at the edge and follows again once the cursor leaves
