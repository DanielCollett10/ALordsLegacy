# Unity tests

Sim logic is tested with `dotnet test Sim.Tests/Sim.Tests.csproj`. This folder holds the
Unity-side tests: `EditMode/` (Editor only) and `PlayMode/`.

## Agents: run through the unity MCP (decision 0007)
Use the `unity` MCP tools against the owner's open Editor (see `docs/unity-mcp.md`): run the
EditMode and PlayMode suites (`test` commands), then read the console for failures.
Wait for compilation first.

## Fallback if MCP is down
Open the project in Unity, then Window → General → Test Runner. Pick the EditMode or PlayMode
tab, click Run All, and paste the result into the task.

## Rules
- Screenshot steps must skip with a warning (`Assert.Ignore` / `Debug.LogWarning`) when
  `SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null`, so logic asserts still gate.
- Tests never depend on real frame time. Advance the game with `SimHost.Tick` (decision 0006).
- All UIDocuments use the single `Assets/Game/UI/PanelSettings.asset`; never create another.
