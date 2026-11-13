# Saving and loading (live)

M11 (Save and come back). Implements GDD SAVE-01/02 and tech spec SAV-01..06. Technical shape: decision 0034.
Wins over the tech spec where they disagree.

Tags: DECIDED = owner agreed. PROPOSED = lead's default, owner may change. OPEN = not decided.

## The Esc menu

| ID | Rule | Tag |
| --- | --- | --- |
| SV-01 | **Esc** opens the game menu as simple placeholder buttons on screen (UI Toolkit; art direction later): **Resume, Save, Load, Options, Quit** | DECIDED (owner 13/11/2026) |
| SV-02 | While the menu is open the Sim is paused and tool, camera and picking input are blocked. Resume (or Esc again) closes it and puts the pause state back as it was before (a game paused before the menu stays paused) | DECIDED (restore rule PROPOSED) |
| SV-03 | **Options** and **Quit** are shown but disabled until the main screen and bootstrapper exist | DECIDED |
| SV-04 | If a tool is in the middle of a draft, Esc keeps its current meaning (cancel the draft). Only Esc with no active draft opens the menu | PROPOSED |

## Saving

| ID | Rule | Tag |
| --- | --- | --- |
| SV-10 | **Save** asks for a name, pre-filled with the game date (e.g. `Sun 14 Mar Year 1 08:40`). The player may change it. Confirm writes a new save; names don't have to be unique, and saving never overwrites another save | DECIDED (pre-fill PROPOSED) |
| SV-11 | **Autosave every 30 real minutes** of play, counted while the Game scene runs and the menu is closed (paused time counts). The timer restarts after any save. There is **one autosave slot**, named `Autosave`, and each autosave overwrites it. It appears in the Load list like any other save, dated by its last write | DECIDED (owner 13/11/2026) |
| SV-14 | **A save keeps the tuning it was started with** (SimConfig values and type catalogs), so a load continues exactly as it was saved. Editing SimConfig affects new games only. Possible later dev-only option: "load with current tuning", for balancing an existing save. Not in M11 | DECIDED (owner 13/11/2026, on lead advice) |
| SV-12 | A save is taken between ticks, never mid-tick (SAV-02). The player sees a short "Saved" note | DECIDED |
| SV-13 | Saves are files in the user's save folder (`Application.persistentDataPath/Saves/`). Each has a small header (name, real date and time created, game date, map id) that the list reads without loading the whole file | PROPOSED |

## Loading

| ID | Rule | Tag |
| --- | --- | --- |
| SV-20 | **Load** lists every save, **newest first**, showing name, real date and time created, and game date. Pick one and press Load | DECIDED (owner 13/11/2026) |
| SV-21 | Loading replaces the current game with no "are you sure" in M11. Unsaved progress is lost | PROPOSED |
| SV-22 | After a load everything on screen matches the saved Sim (SAVE-02): people mid-walk, oxen mid-haul with their logs, half-built sites, the clock, pause and speed default (speed is not saved, decision 0006) | DECIDED |
| SV-23 | Deleting or renaming saves: not in M11 | PROPOSED |

## Saves and map changes

| ID | Rule | Tag |
| --- | --- | --- |
| SV-30 | A save records the map id and a **content hash** of the map file it was played on. A save whose map hash doesn't match any installed map **is refused** with a clear reason ("This save was made on a different version of the test map"), never loaded with a warning. A half-matching world (trees with wrong ids, people inside new hills) is worse than no load | DECIDED (lead recommendation; owner asked which is best) |
| SV-31 | **How the problem is avoided in real builds:** once a map ships in a build it is **never re-baked in place**. A changed map gets a new map version (a new file, e.g. `testmap.v2.lsmap`), and the build keeps the old files, so every old save still finds its map. Only new games use the newest map | PROPOSED (applies from the first player build) |
| SV-32 | **During development**, re-baking the test map breaks old saves, and that is accepted: dev saves are throwaway. SV-30's message makes it obvious why | DECIDED |
| SV-33 | Save format changes (new Sim state) use a format version with a migration step per version (SAV-03). Before the first player build, a format change may simply refuse old saves with a message | PROPOSED |
