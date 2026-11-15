# 0039: Deconstruction marks a module, then a task finishes it

**Context.** M12 adds demolition with a 50% refund (CN-50..58). A built module may still be working, and its logs may be
reserved. Removing it at once would hide the work, skip the working-day rules, and leave no way to cancel.

**Decision.**
- Deconstruct on a built module adds a `DeconstructionEntry` to `world.Deconstruction` (`MarkForDeconstruction`). The module
  stops working at once: no storage capacity, no ox stand, and a marked hut means "Missing hut".
- Marking a hut unassigns its families in the same `Apply` (CN-56).
- Odd-jobbers take `task.deconstruct` (helpers only, heavy work, like Build). Progress lives on the entry, so
  `CancelDeconstruction` throws it away and the module works again.
- Finishing removes the footprint and the entry. The refund (`floor(recipe logs × DeconstructRefundShare)`) and any storage
  contents become one unowned ground pile at the site. Both shares are `SimTuning` values, saved in Core.
- Unbuilt sites and 0-minute recipes skip the task and are removed at once, as `RemoveModule` does.
- An operation area goes only when its last module is gone (pending-operation set). `DeleteArea` refuses an area with modules.

**Consequences.** Deconstruction state and pending operations are saved in the Structures section, so the save version bumps
(decision 0038). Builders and deconstructors work where they stand until the walk-to-site step exists. Changing a share affects
only later refunds, because each finished module computes its refund when it finishes.
