# 0016: Roads are drawn as one mesh per segment, rebuilt on network change

**Context.** Trees are thousands of identical instances, so they are batch-drawn (decision 0008). Roads are few.
Each segment has its own width, ends and terrain-following strip, and the network changes only through commands.

**Decision.**
- `RoadRenderer` (`Assets/Game/Visuals/`) keeps one child GameObject with a mesh per `RoadSegment`, keyed by segment id.
- Each frame it compares `RoadNetwork.Version` with the last value it built. Only on a change does it add meshes for
  new segment ids and destroy meshes for removed ones. Segments are immutable, so an existing mesh is never rebuilt.
- The draft road is one separate mesh, rebuilt on each preview change.
- Roads have no colliders. Picking uses the Sim (`RoadNetwork.FindNearest`).

**Consequences.** Simple, and deleting a segment is one object destroyed. Cost grows with the number of segments
(one GameObject each). If road counts reach the thousands, or the 10,000-citizen stress test shows it, move to
batched or chunked meshes. That is a new decision, not a quiet change.
