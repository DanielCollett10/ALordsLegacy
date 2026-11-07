# Future goals (deferred, not scheduled)

Ideas the owner raised that are bigger than one milestone. Nothing here gets built until it's moved into a
system doc with rules. Source: T-119 notes (M3/M4 play), triaged 07/11/2026.

| ID | Goal | Why it's deferred | Touches first |
| --- | --- | --- | --- |
| FG-01 | **Larger map, 25-50 km across** | Needs FG-02 first; the 2 km test map is a single 1025² heightmap and a ~36 MB HeightGrid at 6 km | Map pipeline, `HeightGrid` memory, camera far plane |
| FG-02 | **World chunking and streaming** (terrain, trees and Sim data loaded by region) | An architecture change to the map pipeline (decision 0003) and to the tree renderer cells (decision 0008). Needs its own decision | `MapData` format version, `TreeRegistry` grid, `TreeRenderer` |
| FG-03 | **Map-authoring pipeline** (real maps instead of the generated test map: hand-sculpted or imported heightmaps, forest painting, rivers) | Only worth building once map size (FG-01) is fixed | `Assets/Editor/MapGen/`, bake |
| FG-04 | **Enemy AI faction and orchestrator** (a rival lord who plans and acts) | Needs people, movement, economy and military first (GDD MVP-1 and later) | New Sim system |

Not deferred, already working: "branch from a road creates a junction" (checked on replay; see roads.md).
