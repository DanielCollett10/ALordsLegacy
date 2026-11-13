# Future goals (deferred, not scheduled)

Ideas the owner raised that are bigger than one milestone. Nothing here gets built until it's moved into a
system doc with rules. Source: T-119 notes (M3/M4 play), triaged 07/11/2026.

| ID | Goal | Why it's deferred | Touches first |
| --- | --- | --- | --- |
| FG-01 | **Larger map, 25-50 km across** | Needs FG-02 first; the 2 km test map is a single 1025² heightmap and a ~36 MB HeightGrid at 6 km | Map pipeline, `HeightGrid` memory, camera far plane |
| FG-02 | **World chunking and streaming** (terrain, trees and Sim data loaded by region) | An architecture change to the map pipeline (decision 0003) and to the tree renderer cells (decision 0008). Needs its own decision | `MapData` format version, `TreeRegistry` grid, `TreeRenderer` |
| FG-03 | **Map-authoring pipeline** (real maps instead of the generated test map: hand-sculpted or imported heightmaps, forest painting, rivers) | Only worth building once map size (FG-01) is fixed | `Assets/Editor/MapGen/`, bake |
| FG-04 | **Enemy AI faction and orchestrator** (a rival lord who plans and acts) | Needs people, movement, economy and military first (GDD MVP-1 and later) | New Sim system |
| FG-05 | **Reshaping confirmed areas and roads** (owner, M6 planning). A confirmed building area must not be freely reshaped. It waits for the build / "work in progress" mechanic: while a building is still a WIP it can be changed from the WIP building panel at the bottom of the screen. A **work area** is reshaped from a button on the UI panel of the building it belongs to (e.g. the woodcutting work area lives in the woodcutter's panel) | Needs construction (WIP state) and building panels first (MVP-1) | Building panel UI, `AreaRegistry` reshape command (AREA-11) |

## Milestone order (owner, M6 planning)

After M6: **M7 People walk the land** (GDD MVP-1 foundations: families, citizens and movement over terrain and roads),
then **woodcutting** (MVP-1 loop), which needs walking. Reshaping (FG-05) and save/load come later, once those exist.
Update 11/11/2026: M7 People walk the land, then **M8 Woodcutters fell trees**, **M9 Oxen haul logs** and **M10
Build it first** (which completes MVP-1). See `people.md`, `movement.md`, `camp.md` and `woodcutting.md`.

Update 13/11/2026 (owner, after M9): **M10 Build it first** (`construction.md`), then **M11 Save and come back**
(save/load before more systems are added), then MVP-2 split into **M12 Homes** (plots and houses first: small, and
the schedule needs homes), **M13 Food on the table** (foraging, granary, larders, eating, hunger; the camp-site is
the only market, FOOD-A10b), and **M14 Markets** (stall families, MKT-03..03f).
**Markets wait until food has been played**: the full stall-family system is not built before M13 is signed off.

Not deferred, already working: "branch from a road creates a junction" (checked on replay; see roads.md).
