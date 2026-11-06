# 0008: Trees are Sim data, drawn by Unity without GameObjects

**Context.** Tech spec PRF-02 targets 50,000 trees on the test map, and tree state (felling, logs) belongs to
the Sim. One GameObject per tree costs memory and time, and a collider per tree makes every tree clickable.

**Decision.** Trees live only in the Sim's `TreeRegistry`. The Unity layer draws them with `TreeRenderer`:
instanced meshes in 64 m cells, frustum-culled, drawn with `Graphics.RenderMeshInstanced`. No per-tree
GameObjects and no colliders. Cursor picking hits the terrain, and tree lookups use a Sim radius query
(`TreeRegistry.QueryRadius`).

**Consequences.** Trees cannot be clicked directly; selection goes through the ground or a radius query.
When felling exists, the renderer must rebuild the affected cells (`TreeRenderer.Invalidate()`). Variety
between trees comes from the id (yaw), not from separate objects. The 50,000 figure is a target; the perf
spike has to measure it.
