# Placeholder art register

Every placeholder object in the game, so the owner can swap in real models (Blender, MTree) one at a time.
Owner, 16/11/2026: **agents never generate or source final models.** They add placeholders through
`PlaceholderVisualGenerator` (decision 0010) and **add a row here in the same task**. Swapping art means editing the
`VisualDefinition` asset for that id (decision 0009); Sim data never changes.

Columns: **Id** = visual id (`Assets/Game/Visuals/Definitions/<id>.asset`). **Box** = placeholder size in metres
(x × y × z, `VisualSizes`). **Drawn by** = renderer. **Status**: Placeholder / Final.

## Registry-backed (swap by editing the definition)

| Id | What | Box (m) | Drawn by | Status |
| --- | --- | --- | --- | --- |
| `visual.tree.generic` | Standing tree (all species) | from trunk data | `TreeRenderer` | Placeholder |
| `visual.tree.stump` | Tree stump | 0.6 × 0.5 × 0.6 | `TreeRenderer` | Placeholder |
| `visual.tree.felled` | Felled trunk (stretched to length) | 0.6 × len × 0.6 | `TreeRenderer` | Placeholder |
| `visual.item.log` | One log (stretched to length) | 0.35 × len × 0.35 | `LogPileRenderer` | Placeholder |
| `visual.citizen.adult` | Adult citizen | 0.5 × 1.7 × 0.5 | `PeopleRenderer` | Placeholder |
| `visual.citizen.child` | Child (adult box × 0.7) | 0.5 × 1.7 × 0.5 | `PeopleRenderer` | Placeholder |
| `visual.ox` | Ox | 0.8 × 1.3 × 1.6 | `PeopleRenderer` | Placeholder |
| `visual.camp.tent` | Family tent at camp | 3 × 2 × 3 | `CampRenderer` | Placeholder |
| `visual.camp.fire` | Camp fire | 0.8 × 0.4 × 0.8 | `CampRenderer` | Placeholder |
| `visual.camp.logpile` | Camp log stockpile | 1.5 × 0.7 × 2.5 | `CampRenderer` | Placeholder |
| `visual.camp.tiepost` | Ox tie post | 0.2 × 1.5 × 0.2 | `CampRenderer` | Placeholder |
| `visual.module.woodcutting_hut` | Woodcutting hut **and forager hut** (shared) | 6 × 2.6 × 4 | `ModuleRenderer` | Placeholder |
| `visual.module.log_storage` | Log storage **and granary** (default fallback) | 8 × 1.2 × 6 | `ModuleRenderer` | Placeholder |
| `visual.module.oxen_stand` | Oxen stand | 3 × 1 × 3 | `ModuleRenderer` | Placeholder |
| `visual.module.house` | Tier 1 log house (HSE-23) | 6 × 4 × 8 | `ModuleRenderer` | Placeholder |
| `visual.module.market_stall` | Market stall (open table, MK-43) | 3 × 0.9 × 2 | `ModuleRenderer` | Placeholder |
| `visual.item.food_crate` | Food crate: 1-3 on a stall by fill, one held by a food carrier (MK-43) | 0.6 × 0.35 × 0.6 | `ModuleRenderer`, `PeopleRenderer` | Placeholder |
| `visual.module.hewing_station` | Hewing station (T-526) | 6 × 2.2 × 4 | `ModuleRenderer` | Placeholder |
| `visual.module.sawpit` | Sawpit (T-526) | 6 × 1.4 × 4 | `ModuleRenderer` | Placeholder |
| `visual.item.beam_stack` | Beams in a hewing station's store; height grows with fill | 1.2 × 0.5 × 0.6 | `ModuleRenderer` | Placeholder |
| `visual.item.plank_stack` | Planks in a sawpit's store; height grows with fill | 1.2 × 0.4 × 0.6 | `ModuleRenderer` | Placeholder |
| `visual.construction.scaffold` | Building site scaffold | footprint × 2.6 | `ConstructionRenderer` | Placeholder |
| `visual.road.dirt_path` | Dirt road surface | ribbon | `RoadRenderer` | Placeholder |
| `visual.fallback` | Shown for a missing id | 1 m cube | `VisualRegistry` | Placeholder |

## Not yet in the registry (hard-coded; need an id before they can be swapped)

| Thing | Where | Today |
| --- | --- | --- |
| Forage bush, full / empty (berry and mushroom alike) | `BushRenderer` | Built-in sphere, green / brown material |
| Forager hut (own model) | `ModuleRenderer.VisualIdOf` | Reuses woodcutting hut |
| Granary (own model) | `ModuleRenderer.VisualIdOf` | Falls back to log storage |
| Food items (meat, berries, mushrooms) on carriers and in stores | — | Not drawn |
| Terrain ground layers: grass, dirt, forest floor | `Assets/Maps/TestMap/*.terrainlayer` | Placeholder textures |
| Animations (walk, chop, lead ox, hammer, carry) | `WalkBob` | Bob only, no rig |

## Coming with planned milestones (add the row when the task lands)

M15: market area fill (stall and crate rows landed in T-478). M17: wattle, daub and
thatch house tiers, their sites and materials.
