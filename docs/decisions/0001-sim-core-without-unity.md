# 0001: Keep the simulation in pure C# without UnityEngine

**Context.** Most code is written by AI agents in parallel git worktrees. Unity is slow
to open, needs a per-checkout Library import, and can't run in batchmode on a project
that is already open.

**Decision.** Game rules live in `Assets/Sim` with `noEngineReferences: true`. A separate
`Sim.Tests` project compiles the same files and runs NUnit tests with `dotnet test`.

**Consequences.** Agents can build and verify most work in seconds without Unity. The
Unity layer stays thin. The 2D view can later be replaced with 3D without touching rules.
Sim code can't use Unity types (Vector3, Mathf); use our own small math types.
