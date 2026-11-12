# 0031: Operations create their tasks from their state each tick

**Context.** M8 has woodcutting operations with workers, trees and storage that change at any time (a worker
goes home, a tree is cut elsewhere, storage fills, the player pauses). Keeping a stored job queue on the operation
means every one of those changes must update that queue.

**Decision.**
- An operation holds no job list. `WoodcuttingSystem` (operations phase) reads the operation every tick.
- While the operation is Working it opens one `task.fell_tree` per idle worker of its families, and only while logs
  on hand plus the yield of open tasks stay under storage capacity (WC-25). Each new task reserves the nearest free
  standing tree at creation, using the task id as owner.
- When the operation is not Working, its still-open fell tasks are cancelled. Tasks already under way are not
  cancelled; they pause when the worker goes off shift (PPL-24, decision 0030 for the walk).
- A task is a `TaskRecord` built from a `TaskDef` (decision 0005). Dispatch and step running are shared systems, not
  per-operation code.

**Consequences.** Status, job count and reservations are derived from world state, so saving needs only the registries
and the tick. A new operation type adds one operations-phase system that follows this pattern, not a queue. The cost is
a scan of each operation every tick; that is fine at MVP size. The timber steps are built per tree when its task is
opened, so a tree's log count is fixed at that point.
