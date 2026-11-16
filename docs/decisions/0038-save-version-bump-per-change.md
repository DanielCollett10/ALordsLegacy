# 0038: Every change to saved bytes bumps the save format version

**Context.** M12–M14 add Sim state in many tasks: deconstruction, new tuning fields (stored in the Core section,
SV-14), homes, larders, eating, bushes and the allotment. Decision 0034 has one int32 format version and the reader refuses
any other version. Without a rule, a task could change a section's layout but leave the version alone. An old save would
then fail half-way through a section with a confusing error, or load wrongly.

**Decision.**
- Any task that changes what `SimSave.Write` produces bumps `SaveWriter.CurrentVersion` by one **in the same task**. That
  covers a new section, a new field in an existing section, or a new `SimTuning` field saved in Core. Tasks that only add
  code paths and no saved state don't bump.
- Each bump comes with a test: bytes with the previous version are refused with `SaveFormatException` and the SV-33 text
  (*This save was made with an older version of the game*). The round-trip test (0034) still passes.
- No migrations before the first player build (SV-33). Old dev saves are throwaway (SV-32).
- The Load list must show a refused save as a disabled row with the reason, so the player can still delete it (SV-23).
- Because these tasks all touch `Assets/Sim/Save/SaveWriter.cs`, they run one at a time. That is accepted.
- Plan tasks never name the target number. A task bumps by one **from the value on main when it starts**; if a
  merge conflicts on the number, the later merge takes the next number and fixes its refusal test (lead, 16/11/2026).
- A new section gets its own file in `Assets/Sim/Save/Sections/`, is registered in `SaveSections.cs`, and goes at the
  end of the 0034 read order unless it must come earlier. The task notes the position in 0034.

**Consequences.** Version numbers climb quickly during development, and that's fine. Plan tasks state "bumps save version"
or "no saved-state change" explicitly.
