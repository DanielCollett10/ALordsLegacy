# 0062: Duplicate-code scans use jscpd

**Context.** Agents copied existing code instead of extending the shared pieces in `docs/shared-infrastructure.md`
(for example the hard-coded recipes in M19, M20 and M21). Reviews only catch a copy if the reviewer remembers the
original. The owner approved jscpd on 19/11/2026. (First written as 0061, which M21 had already used.)

**Decision.**
- Tool: **jscpd 4.x**, run through `npx`, pinned to one exact version in `Tools/dup-scan.ps1`. It needs Node.js LTS (20 or later) on the machine.
- Settings live in `.jscpd.json` at the repo root. Format: `csharp`. Paths: `Assets/Sim`, `Assets/Game`, `Assets/Editor`.
  Ignore: `**/ThirdParty/**`, `**/*.meta`, `Assets/Tests/**`, `Sim.Tests/**`. Thresholds: `minLines` 15, `minTokens` 100.
  Reporters: `console`, `markdown`, `json`. Output: `Logs/jscpd/` (never committed).
- One command: `pwsh Tools/dup-scan.ps1`. It never fails the build. It prints the totals and the report path.
- Each scan is written up in `docs/test-runs/<milestone>-duplication.md`: command, versions, totals, then one row per
  clone with the shared piece it should use and a tag, either `fix in M24` or `fixed here`. Fix here only if the fix is
  under 20 lines and inside one file. Each write-up compares with the previous one.

**Consequences.** Duplication becomes a number that M24 can drive down. Scans stay report-only until a baseline exists. A
later decision may turn the threshold into a failing check.
