# Connecting agents to the Unity Editor (editor lane)

Editor-lane agents run in the editor checkout (the folder you keep open in Unity)
and can drive the Editor through an MCP server: create GameObjects, refresh assets,
read the console, run tests.

Agent NOC expects the server to be named **`unity`** in this repo's `.mcp.json`, so its
tools are called `mcp__unity__...` (already allowed in `.claude/settings.json`).

Options as of late 2026:
- **Unity's own MCP**, now provided through the Unity CLI (`unity mcp`). Unity deprecated
  the older in-Editor MCP server from the AI Assistant package.
- **MCP for Unity** (CoplayDev, community, open source).

Follow the chosen tool's setup guide, then put its server entry in `.mcp.json` under the
name `unity`. Check it works by opening a terminal in the editor checkout, running
`claude`, then `/mcp`.

Without MCP the editor lane still works: agents write scripts, UI Toolkit files and
Editor scripts as text, and list anything that needs the GUI as human steps.
