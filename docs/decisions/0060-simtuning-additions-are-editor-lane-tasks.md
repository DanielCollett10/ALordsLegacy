# 0060: SimTuning additions are their own editor-lane tasks

**Context.** The SimTuning mirror rule (CLAUDE.md lessons) makes the same task update CoreSection, the SimConfig asset and
SimConfigTests, and quote both the dotnet and SimConfig EditMode totals. The code lane cannot run Unity, so code-lane tasks
that added tuning kept producing follow-up "run the SimConfig tests" tickets (T-597, T-619, T-620). Plan review, 19/11/2026.

**Decision.**
- New or changed `SimTuning` properties go in a small **editor-lane** task (usually 1 to 2 points): the property, its default,
  CoreSection read and write (older saves load the default), the save version bump if needed, the SimConfig asset and tests,
  `tuning.md`, and both totals quoted.
- The code-lane task that uses the values depends on it and adds no tuning of its own.
- One exception: a change that also needs save migration logic (for example appending 64 to a saved `Speeds` list) stays in
  the same editor task with its own Sim test.

**Consequences.** Code tasks stay pure dotnet. No "run the SimConfig tests" follow-ups should come from new tasks.
