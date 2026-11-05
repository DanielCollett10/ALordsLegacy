# 0005: Task types and who may take them are data

**Context.** Helper work will keep growing (leading oxen between a log bay and a sawmill,
carrying for builders, later repairs). The owner wants it easy to define, replace and extend.
The tech spec already has tasks, a dispatcher and steps (TSK-01..14).

**Decision.** Each task type is a definition, not a class: its ID, its step list (MoveTo,
Work, PickUp, Drop, AttachOx...), whether each step is light or heavy (POP-A3), any required
resource (an ox), and an **eligibility policy**: `AssignedFamilyOnly`, `AssignedThenHelpers`
or `HelpersOnly`. The dispatcher reads the policy. It never has special cases per task type.

**Consequences.** A new helper job is a new task definition plus the owner (building) that
creates it; no dispatcher change. Step kinds are a small fixed set in code; adding a new
step kind is a code change and should be rare. Tests can build task definitions in code.
