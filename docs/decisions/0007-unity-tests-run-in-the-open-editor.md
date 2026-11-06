# 0007: Unity tests run in the owner's open Editor, not in batch-mode CI

**Context.** Tech spec DEV-03/DEV-04 assumed Unity runs headless in CI with a licence on the
agents' servers. In practice the owner has Unity Personal (free; activated by signing in to
Unity Hub) on their own PC, and agents work in checkouts on that PC. A project that is open
in the Editor can't also be opened by a batch-mode Unity (decision 0001).

**Decision.**
- Sim code stays tested with `dotnet test` (no Unity needed). This is most of the work.
- Unity EditMode/PlayMode tests run **inside the open Editor** on the owner's PC: by an
  editor-lane agent through the `unity` MCP server (docs/unity-mcp.md), or, without MCP, by
  the owner in Window → General → Test Runner, pasting the result into the task.
- Editor-lane acceptance criteria say "passes in the Unity Test Runner", not "in batch mode".
  Batch-mode CI (DEV-03) is postponed until there is a build machine.
- Screenshots and builds (e.g. the perf benchmark) are triggered the same way: menu item via
  MCP, or by the owner.

**Status.** The owner chose the Unity MCP route (agents run Unity tests themselves).

**Consequences.** If the MCP is ever disconnected, without it every editor
task ends with a short human step (run the tests, report). Editor-lane tasks run one at a
time against the open Editor.
