# 0003: Terrain and trees are baked into Sim map data

**Context.** Slope checks (AREA, GEO-08), road gradients (NAV-03), placement and pathfinding
all need terrain height and slope inside the Sim. The Sim cannot reference UnityEngine
(0001), so it cannot read Unity Terrain. The terrain source will also change later
(external generator or real elevation data, PLT-08).

**Decision.** The Sim owns a `HeightGrid` (heights in metres on a regular grid, bilinear
sampling, slope in degrees) and a tree list (stable ID, x/z position, usable trunk length).
An editor "map bake" step exports whatever the current terrain source is into one versioned
Sim map data file. The Unity layer draws the terrain mesh; the Sim never asks Unity anything.

**Consequences.** This is permanent, not a stop-gap: swapping Unity Terrain for another
source only means writing a new bake step. Terrain is read-only at runtime in the MVPs.
Re-baking the map changes tree IDs only if trees change; saves reference the map version.
Unity's built-in terrain trees are authoring-only (PLT-09).
Files: tree authoring data is a `TreeAuthoringData` ScriptableObject (tree id, x, z, usable
trunk length) next to the terrain in `Assets/Maps/<MapId>/`. The bake writes
`Assets/StreamingAssets/Maps/<MapId>.lsmap`; at runtime the Sim host reads those bytes
from `Application.streamingAssetsPath` and passes them to the Sim's MapData reader.
The baked file is committed as a plain git binary (`*.lsmap binary` in `.gitattributes`);
the repo does not use Git LFS yet. The 2 km test map is a few MB. Move map files and art
to LFS (tech spec DEV-01) before maps reach 6 km or the owner's models arrive.
The world target is 25×25 km (WORLD-A1): one uniform 2 m grid would be ~156M cells, so
Sim code queries terrain through a height-field interface. The single `HeightGrid` serves
the MVP maps; a tiled version with coarser cells where nobody builds replaces it later.
