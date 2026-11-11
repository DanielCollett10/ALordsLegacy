# 0027: The start layout is data, applied through the normal commands

**Context.** The game starts with a camp, three families, oxen and a trunk road (camp.md CMP-01..07). The start must
be the same every time and must obey the same rules the player does.

**Decision.**
- `StartLayout` (`Assets/Sim/Start/`) is plain data: camp centre and rotation, camp-relative points, trunk road control
  points, families with citizens, and the ox count. On the Unity side `StartLayoutConfig` (ScriptableObject) holds it and
  converts with `ToLayout()`.
- `StartLayoutApplier.Apply(world, layout)` builds the start through public APIs: it adds the camp footprint (clearing
  trees), sends `DrawRoad` through `world.Execute` for the trunk road, then adds families, citizens and oxen. It can run
  once per world.
- A refused trunk road throws `InvalidOperationException` with the refusal text, so a bad layout fails loudly.
- The layout asset is written by an editor script (CMP-08, decision 0010). The Sim does not search for the camp site.

**Consequences.** The starting road is checked by the same validator as any road, and it appears in the `CommandLog`.
The oxen count comes from the layout, so `SimTuning.StartingOxen` is not read (see `tuning.md`). A new start can be made
without code changes.
