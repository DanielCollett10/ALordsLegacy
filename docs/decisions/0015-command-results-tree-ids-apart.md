# 0015: Tree removals are reported apart from segment removals

**Context.** A drawn road now clears trees (RD-11) and a delete command removes segments (RD-12). Both report
what they changed through `CommandResult`, which had one `Created` and one `Removed` list of segment ids. Putting
tree ids into `Removed` would mix two kinds of id in one list, and a view could not tell which is which.

**Decision.**
- `CommandResult.RemovedTreeIds` (long[], ascending) holds trees a command removed. `Removed` keeps only segment ids.
- `Accepted(created, removed, removedTreeIds)` takes the tree list as an optional third argument.
- Tree removal goes through `TreeClearing.RemoveNearPolyline` with half the road width + `RoadTreeClearanceM`,
  measured against each new segment's centre line. Trees never return (BLD-10-D).
- Deleting a segment is `DeleteRoadSegment(segmentId)`: unknown id → `NotFound`; nodes left empty are removed by
  `RoadNetwork.RemoveSegment`; no tree changes.

**Consequences.**
- Views that show "what changed" read `RemovedTreeIds` for trees and `Removed` for segments. Future commands with
  tree removals (buildings, BLD-10-A) should use the same field.
- Ids are still plain longs; the field names say what they are, so no typed id wrapper is needed yet.
