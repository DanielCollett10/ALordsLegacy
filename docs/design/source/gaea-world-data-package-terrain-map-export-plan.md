# Gaea World Data Package — Terrain & Map Export Plan

Nov 15, 2026 · @Dan

## 1. Scope

Gaea delivers one package: tiled heightfields and data masks for the 50 × 50 km world, with a metadata file saying exactly how to read them. The Unity project imports, interprets and spawns from that package; nothing in Gaea depends on Unity code.

| Gaea delivers | The Unity project does |
| --- | --- |
| Terrain height, eroded and river-cut | Imports tiles as Unity Terrain and streams them |
| Geography maps: slope, flow, wear, deposits, rivers, wetness | Uses them for navigation cost, overlays and terrain painting |
| Suitability masks: lowland, midland, highland, forestry, rocky, later farmland, clay, iron | Scatters trees from the forestry mask; places deposits from suitability with a per-world seed |
| One tile grid, one naming scheme, one metadata file | Reads the metadata, never guesses ranges |

### How this fits the GDD and spec

- **Gaea is the right tool for this map.** Spec PLT-08 says maps of 10–20 km² or more must move to an external terrain generator with erosion plus soil and wetness maps. A 50 km world is 2,500 km².
- **A floating origin is required** beyond about 8 km across (PLT-05, PLT-06). That is Unity's job, but it is why the tile grid and world origin must be fixed now.
- **The 2 × 2 km test map still comes first.** MVP-0 to MVP-2 run on it (PLT-07). Proposal: cut it from this Gaea world as a single-tile build, so the test map already looks like the final land. That changes PLT-07 (which says sculpt it in Unity), so the owner and lead must agree.
- **The land should read as English countryside:** rolling hills, valleys, open land and forest (WORLD-01). Keep relief gentle; a few hundred metres from lowest valley to highest hill is plenty.
- **Rivers, marsh, groundwater and soil are later features** (GDD section 12), but the MVP map must leave room for them. Exporting flow, rivers and wetness now means they can be switched on later without re-authoring the terrain.
- **Resource deposits are random per world** (WORLD-04), and their placement rules are still OPEN (WORLD-05). So Gaea exports where clay or iron *could* be (suitability), and the game picks the actual deposits with a seed.

## 2. Graph layout

Keep your current chain (Perlin, Adjust, Warp, Erosion2, Rivers) and add three analysis groups, a resource mask group and one export section. The key change: every band and mask is taken from the **Rivers output**, the final height, while Flow, Wear and Deposits come from Erosion2's secondary ports.

&#91;embedded content: Gaea graph sections · generation, analysis, masks, exports\]

Nothing outside `WORLD_DATA_EXPORTS` is exported, so the Unity agent never receives stray files such as `Height_03.png`. Preview-only colour nodes (section 6) hang off the side and stay out of it.

## 3. Export contract

There are two kinds of map, and the contract keeps them apart: **heightfields** whose values mean metres, and **data maps and masks** whose values are 0–1 weights. Every file is linear data: no sRGB, no colour correction, no per-tile levelling.

### Contract v001 (from the real graph, 15 Nov 2026)

Every row is one end node inside `WORLD_DATA_EXPORTS`, marked for export, with the node name as the filename. Erosion2 and Rivers themselves are **not** marked.

| Export name (node name) | Comes from | Kind | Value meaning | Format | Folder | Action in Gaea |
| --- | --- | --- | --- | --- | --- | --- |
| `WORLD_HEIGHT` | Rivers, main output → Clamp | Heightfield | 0–1 maps to `height_min_m`–`height_max_m` | PNG16 | 01\_Terrain | Rename from WORLD\_Height; EXR → PNG16 |
| `WORLD_HEIGHT_MASTER` | Rivers, main output → Clamp | Heightfield | As above, full precision | EXR 32-bit | 01\_Terrain | Add |
| `WORLD_FLOW` | Erosion2, Flow port → Clamp | Data map | Relative water flow, one scale for the world | EXR 32-bit | 02\_Geography | Add; unmark Erosion2 |
| `WORLD_WEAR` | Erosion2, Wear port → Clamp | Data map | Erosion intensity | EXR 32-bit | 02\_Geography | Add |
| `WORLD_DEPOSITS` | Erosion2, Deposits port → Clamp | Data map | Sediment build-up | EXR 32-bit | 02\_Geography | Add |
| `WORLD_RIVERS` | Rivers, rivers port → Clamp | Mask | 1 = river channel | PNG16 | 02\_Geography | Add; unmark Rivers |
| `WORLD_WETNESS` | Flow × flat × lowland | Data map | 0 = dry, 1 = waterlogged | PNG16 | 02\_Geography | EXR → PNG16 |
| `MASK_LOWLAND` | Height band, low | Mask | 1 = fully in band | PNG16 | 03\_Environmental\_Masks | Rename (capitals) |
| `MASK_MIDLAND` | Height band, middle | Mask | 1 = fully in band | PNG16 | 03\_Environmental\_Masks | Rename (capitals) |
| `MASK_HIGHLAND` | Height band, high | Mask | 1 = fully in band | PNG16 | 03\_Environmental\_Masks | Rename (capitals) |
| `MASK_SLOPE_FLAT` | Slope band, flat | Mask | 1 = fully in band | PNG16 | 03\_Environmental\_Masks | Rename; EXR → PNG16 |
| `MASK_SLOPE_GENTLE` | Slope band, gentle | Mask | 1 = fully in band | PNG16 | 03\_Environmental\_Masks | Rename; EXR → PNG16 |
| `MASK_SLOPE_STEEP` | Slope band, steep | Mask | 1 = fully in band | PNG16 | 03\_Environmental\_Masks | Rename; EXR → PNG16 |
| `MASK_FORESTRY` | Forest recipe (section 5) | Suitability | 0 = no trees, 1 = densest forest | PNG16 | 03\_Environmental\_Masks | Rename from MASK\_Forest\_Suitability; EXR → PNG16 |
| `MASK_ROCKY` | Steep + worn highland, minus deposits | Suitability | 1 = bare rock likely | PNG16 | 03\_Environmental\_Masks | Add (missing, essential) |
| `MASK_FARMLAND` | Lowland × flat/gentle × wetness | Suitability | 1 = best farmland | PNG16 | 05\_Resource\_Masks | EXR → PNG16 |
| `MASK_CLAY` | Deposits × flat × wetness | Suitability | 1 = clay most likely | PNG16 | 05\_Resource\_Masks | Rename (capitals); EXR → PNG16 |
| `MASK_IRON` | Designed geology layer | Suitability | 1 = iron most likely | PNG16 | 05\_Resource\_Masks | Later |

`WORLD_SLOPE` is dropped from v001: the slope bands cover the masks, and anything needing true degrees computes slope from `WORLD_HEIGHT` (rule 5 below).

### Build settings for test build v001

| Setting | Value |
| --- | --- |
| Build type | Normal (not tiled) |
| Resolution | 1024 (Community Edition export limit; about 49 m per pixel). Pipeline check only; final builds need a paid edition, which commercial use requires anyway |
| World size | 50,000 × 50,000 m |
| Terrain height | TO FILL: metres from the Build settings → `height_max_m` |
| Range | Normalized, if offered; confirm edges match in the neighbour tile test |
| Build log | On |
| Destination | `Builds/2026-11-15_test01/` |

### Rules the receiving agent can rely on

1. **Heights are metres via the metadata.** A 16-bit PNG stores 0–65535 and carries no metres; the Unity agent converts it to RAW 16 for the terrain importer, so `height_min_m` and `height_max_m` in the metadata are the only source.
2. **One scale for the world.** Any Autolevel, Equalize or Normalize runs once on the whole world, never per tile, so a value of 0.5 means the same thing on every tile.
3. **Masks are weights, not pictures.** 0–1, linear, single channel. Import with sRGB off.
4. **Every map uses the same tile grid** (section 4). Masks may be at a lower resolution than height, but cover exactly the same ground per tile.
5. **Exact slope comes from height.** `WORLD_SLOPE` is a convenience; anything that needs true degrees (navigation, GEO-08, ROAD-09) should compute slope from `WORLD_HEIGHT`.
6. **Suitability is not placement.** Clay, iron and later resources are chances; the game seeds the actual deposits (WORLD-04).

`WORLD_RIVERS` and `WORLD_WETNESS` are additions to the original list: the spec asks for wetness maps (PLT-08) and the GDD plans a groundwater overlay (HSE-13).

## 4. Resolution and tiling

Proposed grid: **25 × 25 tiles of 2 km each, height at 1025 px per tile (about 2 m per pixel), masks at 513 px per tile (about 4 m per pixel)**, built with a Hybrid build and shared edge pixels. One 2 km tile is exactly the size of the MVP test map, so the test map can be any single tile.

### Why 2 m per pixel for height

| Metres per pixel | Whole world (px) | One 16-bit map | Verdict |
| --- | --- | --- | --- |
| 1 m | 50,000 × 50,000 | about 5 GB | Too heavy for this hardware class |
| 2 m | 25,000 × 25,000 | about 1.25 GB | **Final height**; matches the 2 m navigation grid (NAV-07) |
| 4 m | 12,500 × 12,500 | about 310 MB | **Masks and data maps**: they drive density, not shape |
| 8 m | 6,250 × 6,250 | about 80 MB | **Early whole-world tests** while the graph is changing |

Unity terrain heightmaps must be a power of two plus one (513, 1025, 2049, 4097), which is why tiles are 1025 and 513, not 1024 and 512.

### Tile rules

1. **Hybrid build, not a plain Tiled build.** Gaea's Hybrid build processes the world in buckets that fit in RAM, without the small-tile problems of a Tiled build such as poorly formed erosion. Erosion2 and Rivers need to see the whole landscape.
2. **Shared edge pixels on.** Neighbouring tiles repeat their border row and column, so Unity terrains meet with no seam. Gaea supports overlapping edge pixels for tiled and split builds.
3. **Global first, tiles last.** Generate, erode and normalise the whole world, then cut it into tiles at export. Nothing is levelled per tile.
4. **Same grid for every map.** A mask tile `x07_y12` covers exactly the same 2 km square as the height tile `x07_y12`.
5. **World centred on the origin** (PLT-05): the world runs from −25,000 m to +25,000 m on both axes. Tile `x00_y00` is the corner given in the metadata; confirm which corner in the neighbour test (section 7).

### Naming

`<EXPORT_NAME>_y<YY>_x<XX>.<ext>`, two-digit zero-padded indices, for example `WORLD_HEIGHT_y00_x00.png`, `MASK_FORESTRY_y12_x07.png`. Gaea 2's default tile pattern is `_y%Y%_x%X%`, so set the filename to the export name and let Gaea add the suffix.

### Metadata file

One file, `06_Tile_Metadata/world_metadata.json`, written by hand after each build. The receiving agent reads this, never the image headers.

```json
{
  "world_name": "Settlement_World_01",
  "gaea_version": "2.x.x",
  "graph_file": "WorldGen_v003.terrain",
  "build_date": "2026-11-15",
  "world_size_m": [50000, 50000],
  "world_origin": "centre at 0,0",
  "tiles": [25, 25],
  "tile_size_m": 2000,
  "tile_origin_corner": "TO CONFIRM: south-west or north-west",
  "shared_edge_pixels": true,
  "height_min_m": 0,
  "height_max_m": 400,
  "maps": {
    "WORLD_HEIGHT": {"kind": "heightfield", "px_per_tile": 1025, "format": "png16"},
    "WORLD_FLOW":   {"kind": "data", "px_per_tile": 513, "format": "exr32", "range": [0, 1]},
    "MASK_FORESTRY":{"kind": "suitability", "px_per_tile": 513, "format": "png16", "range": [0, 1]}
  }
}
```

The heights and seed values above are placeholders; fill them from the Build settings each time. Keep one entry per exported map.

## 5. Masks for forests and resources

Yes, your height masks are the right start for forests, but on their own they would put trees evenly over every lowland and midland slope. Combine them with slope, water and a large-scale patchiness noise, so the forestry mask gives the woods-and-open-land pattern of the English countryside (WORLD-01, WORLD-03).

### Build the bands from the final height

Your graph takes the height masks straight off Erosion2. Take them from the **Rivers node output** instead: Rivers is the last node that changes the ground, so masks taken earlier will not line up with the river valleys.

- **Height bands:** three Height nodes on the final height, each with a Min, Max and a soft Falloff, so bands blend rather than step. Overlap them slightly so no ground falls between bands.
- **Slope bands:** three Slope nodes: Flat, Gentle, Steep. Steep should start around the building slope limit the GDD will set (WORLD-07), so the same mask can preview where buildings will fail.

### MASK\_FORESTRY recipe

1. **Where trees can grow:** Combine `MASK_LOWLAND` and `MASK_MIDLAND` with Max. Add `MASK_HIGHLAND` at about 30% for thinner upland woods.
2. **Not on cliffs:** Combine with the inverse of `MASK_STEEP` using Multiply.
3. **Not in rivers or on rock:** Subtract `WORLD_RIVERS` (channel only, so riverside woods stay) and `MASK_ROCKY`.
4. **Woods, not a carpet:** Multiply by a large Perlin noise (features 1–3 km across) put through a Threshold or Curve. This decides which areas are woodland and which are open land.
5. **Feathered edges:** a small Blur so forest edges thin out instead of stopping at a hard line.
6. **Clamp to 0–1** and send it to `WORLD_DATA_EXPORTS`.
7. **Optional override:** a Draw node to force or remove forest by hand, for example timber near the start (WORLD-06), once the start location is decided (WORLD-10).

### Other masks

| Mask | Recipe in Gaea | Notes |
| --- | --- | --- |
| `MASK_ROCKY` | Max of `MASK_STEEP` and (`MASK_HIGHLAND` × high Wear), minus Deposits | Exposed rock: steep or eroded, with little sediment |
| `WORLD_WETNESS` | Flow × `MASK_FLAT` × `MASK_LOWLAND`, blurred | Valley floors that collect water; basis for marsh and wells later |
| `MASK_FARMLAND` | `MASK_LOWLAND` × (Flat + Gentle) × moderate wetness, minus rivers and rocky | Later: crops and soil fertility (FOOD-11) |
| `MASK_CLAY` | Deposits × `MASK_FLAT` × wetness | Clay sits in river sediment and valley floors |
| `MASK_IRON` | A designed geology layer: painted bands, an imported rock map, or a separate noise in `04_Geology` | Not taken from rocky ground alone, as the agent's note says |

### How the game will use them (for the Unity agent)

- **Trees:** scatter density follows `MASK_FORESTRY`; species can follow the other masks (oak in lowland and midland, birch at forest edges where the mask is 0.2–0.5, pine on dry highland). Each tree then becomes an entity with an ID and a seeded trunk length (PLT-09).
- **Deposits:** the game picks clay, stone and iron sites from the suitability masks with a per-world seed, spread out so some are far from the start (WORLD-04).
- **Overlays:** wetness and farmland feed the groundwater and crop overlays the GDD plans (HSE-13).

## 6. Why the map has no colour (and whether it matters)

A grey map is normal: Gaea shows a heightfield as plain shading until you add colour nodes. You do not need Gaea colour for the game, because Unity paints the ground with real tiling materials blended by your masks. Add colour in Gaea only as a preview for your own eyes.

### How colour works in Gaea

All Gaea colour comes from running a colour gradient through a black-and-white mask: the low end of the gradient lands on the low end of the mask. The quickest option is a **SatMap** node, which picks from hundreds of colour maps taken from satellite data; **CLUTer** does the same with a gradient you design.

### Quick preview setup

1. Add a SatMap node after the Rivers node (or after a mask, to colour that mask).
2. Change its Index slider until the colours look like green English farmland; try the Random button within a library.
3. For more control, blend two SatMaps with a Mixer node, or feed `MASK_FORESTRY` into one SatMap so woods show darker green.
4. Keep this branch **outside** `WORLD_DATA_EXPORTS`. It is for checking the land looks right, not for the game.

### What the game uses instead

- Unity terrain layers use the tiling ground materials from the Blender art plan (grass, dirt, forest floor, later rock and mud), sourced from Poly Haven and ambientCG.
- Your masks decide where each layer shows: forestry for forest floor, rocky for rock, wetness for mud, the rest grass. That painting is the Unity agent's job.
- If the owner later wants a far-distance colour map, export one SatMap result as `WORLD_COLOUR_REF` (8-bit colour, sRGB). It would be the one colour image in the package, and the only one that is not linear data.

## 7. Step-by-step exercises

Do these in order; each one ends with something you can check before moving on. Nothing here needs a full-resolution 50 km build.

### Exercise 1 — Organise the graph and mark exports

- [ ] Group the graph into the sections in section 2 (select nodes, then add a frame or comment box with the section name).
- [ ] Re-wire the Height (and later Slope) nodes to take the **Rivers output**, not Erosion2.
- [ ] Rename each output node to its export name (`MASK_LOWLAND`, and so on). Gaea uses the node name as the filename by default.
- [ ] Right-click Erosion2 > **Mark for Export** (or F3). In the Build tab, open the dropdown next to its name and keep only the ports you want: Flow, Wear, Deposits. Export the height from the Rivers node, not from Erosion2's Out.
- [ ] Mark Rivers, the three height masks, Forestry and Rocky for export the same way.
- [ ] Set formats per section 3: PNG 16-bit for height (plus an EXR master), PNG 16-bit for masks, EXR for flow, wear and deposits.

**Check:** the Build tab lists only the maps in the contract, each with the right name and format.

### Exercise 2 — First whole-world test build

- [ ] Build settings: normal (non-tiled) build at 1024 px for the whole 50 km (the Community Edition limit, about 49 m per pixel). Tick **Generate Build Log**.
- [ ] Build to a dated folder, e.g. `Builds/2026-11-15_test01/`.
- [ ] Open each mask in an image viewer: black where expected, white where expected, smooth band edges.
- [ ] Write down the world height in metres from the Build settings; that becomes `height_max_m`.

**Check:** every map lines up with the height when laid over it, and the forestry mask shows patchy woods, not an even carpet.

### Exercise 3 — Neighbour tile test

- [ ] Before building, add a temporary marker you can recognise: a Draw node bump near one corner of the world.
- [ ] Switch to a Hybrid build with the section 4 grid (25 × 25 tiles, 1025 px), shared edge pixels on, and build only a 2 × 2 block of tiles if your Gaea version offers selective tile builds; otherwise build the full grid at a low resolution.
- [ ] Open two neighbouring height tiles: the shared edge row must have identical values.
- [ ] Find the marker's tile to confirm which corner is `x00_y00`, and record it in the metadata.
- [ ] Ask the Unity agent to import the four tiles and confirm no seams, and that flow and forestry values match across edges.
- [ ] Remove the marker.

### Exercise 4 — Metadata and versioning

- [ ] Fill `world_metadata.json` (section 4) from this build.
- [ ] Save the graph as a numbered version (`WorldGen_v001.terrain`) next to the build, and never edit a version that has been handed over.
- [ ] Write a short `07_Documentation/README.md`: what changed since the last version, and anything the Unity agent should know.

### Exercise 5 — Pick the MVP test tile (needs owner sign-off)

- [ ] Choose one 2 km tile with gentle hills, open plains and a forest (MVP-0-01), near where the trunk road would run.
- [ ] Export just that tile at full resolution as the candidate MVP test map, and offer it to the lead in place of a hand-sculpted map (PLT-07).

## 8. Folder structure

The agent's folder plan works as it stands; this fills in what goes where. Each handover is one dated, versioned copy of the whole tree.

```
WORLD_GENERATION/
  v001_2026-11-15/
    01_Terrain/               WORLD_HEIGHT_y00_x00.png ... (+ EXR masters)
    02_Geography/             WORLD_SLOPE, WORLD_FLOW, WORLD_WEAR, WORLD_DEPOSITS, WORLD_RIVERS, WORLD_WETNESS
    03_Environmental_Masks/   MASK_LOWLAND, MASK_MIDLAND, MASK_HIGHLAND, MASK_FLAT/GENTLE/STEEP, MASK_FORESTRY, MASK_ROCKY
    04_Geology/               GEO_ROCKTYPE (later), the designed iron and stone layers
    05_Resource_Masks/        MASK_FARMLAND, MASK_CLAY, MASK_IRON
    06_Tile_Metadata/         world_metadata.json, Gaea build log
    07_Documentation/         README.md, WorldGen_v001.terrain, preview screenshots
```

- Never overwrite a handed-over version; make `v002` instead.
- Keep Gaea's working builds elsewhere (e.g. `Documents/Gaea/Builds`) and copy only checked results into `WORLD_GENERATION`.
- Store the package in Git LFS with the rest of the art (DEV-01), or in shared storage the agents can reach.

## 9. Open questions for the owner

- [ ] Is 50 × 50 km the final world size? It sets the tile grid, so change it before the neighbour test, not after.
- [ ] Should the MVP test map be cut from this Gaea world instead of sculpted in Unity (changes PLT-07)?
- [ ] Height range: what relief do you want from lowest valley to highest hill (for example 300 or 400 m)?
- [ ] Is the start location fixed or chosen, and where does the trunk road run (WORLD-10)? Both affect where the forestry mask must guarantee timber (WORLD-06).
- [ ] Clay and iron: suitability maps with game-seeded deposits (WORLD-04), or fixed designed deposits? The agent's note suggests designed iron, which would change WORLD-04.
- [ ] Scale check for the Unity side: the performance target is 50,000 trees on the 2 × 2 km test map (PRF-02). Forest at that density over 50 km would mean millions of trees, so the lead needs a plan for simulating and streaming far regions.

## Sources

- GDD v0.4 and Technical Spec v0.2, 5 Nov 2026 (rule IDs cited throughout); the Gaea agent's World Data Package note.
- [Gaea — Build features (Hybrid, Network builds)](https://quadspinner.com/Gaea/Build)
- [Gaea docs — Building and Mark for Export](https://docs.quadspinner.com/Guide/Using-Gaea/Building.html)
- [Gaea docs — Save Strategies (choosing exported ports)](https://docs.quadspinner.com/Guide/Using-Gaea/Save-Strategies.html)
- [Gaea docs — File Formats](https://docs.quadspinner.com/Guide/Using-Gaea/FileFormats.html)
- [Gaea docs — Tiled Build](https://docs.quadspinner.com/Guide/Build/Tiled.html)
- [Gaea blog — 1.3.1.0: shared edge pixels, tile limits](https://blog.quadspinner.com/gaea-1-3-1-0-bleeding-edge/)
- [Gaea 2 change log](https://eadocs.quadspinner.com/change-log)
- [Gaea docs — Color Production](https://docs.quadspinner.com/Guide/Using-Gaea/Color-Production.html)
- [Gaea docs — SatMaps](https://docs.quadspinner.com/Reference/Color/SatMaps.html)
- [UNIGINE — Importing terrain from Gaea 2 (tile filename pattern)](https://developer.unigine.com/docs/future/objects/objects/terrain/landscape_terrain/heightmap_import/heightmap_gaea)

Several QuadSpinner doc pages are written for Gaea 1.3; check menu names against your Gaea 2 version.
