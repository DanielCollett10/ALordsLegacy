# MVPv2 UI Wireframe Spec

Source: MVPv2 UI Wireframe Plan (design canvas): https://claude.ai/artifact/DDTEknJGEfyEtW83CtmHqS
Status: placeholder layout and behaviour for MVPv2. Final art, layout and style come later. Implements Tech Spec section 17 (UI-01 to UI-08).

Tags: DECIDED = build as written. PROPOSED = build, easy to change. OPEN = do not build; ask the owner.

## 1. Look (placeholder style)

| Token | Value | Use |
| --- | --- | --- |
| Bar background | `#1c1b18` | Top bar, build bars, panel title bars |
| Panel background | `#2a2824` | Popup panels, prompts |
| Button background | `#36332d` | Buttons, list cells, inactive tabs |
| Border | `#57524a` | Panel and button borders |
| Divider | `#3d3a34` | Section dividers |
| Text | `#ece6d8` | Main text |
| Muted text | `#bdb5a5` | Labels, hints |
| Accent (active) | `#d29a3a`, text `#1c1b18` | Active speed, selected category, active tab, progress bars |
| Warning text | `#e8b968` | Status such as "Missing hut", "Full", "In construction" |

Font: Inter (fallback system sans), 12-14 px. Mouse-driven PC UI; buttons about 30 px tall.

## 2. Game screen (HUD)

| ID | Element | Rule | Tag |
| --- | --- | --- | --- |
| UIW-01 | Top bar | Full width, 44 px, left to right: date/season/year/time (e.g. "Mon 1 Mar · Spring · Year 1 · 06:14"), Citizens, Food (days), Construction (Logs · Beams · Planks), Goods, then speed buttons, then Light and Snapping toggles | DECIDED |
| UIW-02 | Speed buttons | Pause, 1x, 2x, 4x, 8x, 16x, 32x, 64x; the active speed is highlighted | DECIDED (64x needs owner confirmation against Tech Spec CLK rules) |
| UIW-03 | Light / Snapping | Two toggle buttons at the far right: "Steady light", "Snapping: On/Off" | DECIDED |
| UIW-04 | Message bar | Top left under the top bar, about 360 px wide. Newest message at the top. Each message shows for 30 seconds then fades. Never blocks clicks on the world | DECIDED |
| UIW-05 | Cursor readout | Bottom left: x, z, height (m), slope (degrees) | DECIDED |
| UIW-06 | Category bar | Bottom centre, always visible: Road, House, Processing, Logistics, Gathering, Agriculture. The selected category is highlighted | DECIDED |
| UIW-07 | Building bar | Opens directly above the category bar when a category is clicked; only one shows at a time | DECIDED |

## 3. Top bar hover panels

Hovering a top bar item opens its detail panel directly underneath; moving the mouse away closes it. Each panel lists two columns: item on the left, amount on the right.

| ID | Panel | Rows | Tag |
| --- | --- | --- | --- |
| UIW-10 | Citizens | Families, Arriving, Leaving, Next family (e.g. "No empty house") | DECIDED |
| UIW-11 | Food | One row per food type that exists (Berries, Mushrooms, Meat), plus "Lasts: N days" | DECIDED |
| UIW-12 | Construction | Logs, Beams, Planks; new building materials are added automatically from definitions | DECIDED |
| UIW-13 | Goods | One row per good that exists (example list: Wool, Linen, Boots, Clothes, Spears; none exist yet) | DECIDED |

## 4. Build bars

| ID | Bar | Buttons | Tag |
| --- | --- | --- | --- |
| UIW-20 | Agriculture | "Nothing implemented yet" (disabled) | DECIDED |
| UIW-21 | Logistics | Oxen post, Granary, Storage yard, Market | DECIDED |
| UIW-22 | Processing | Sawyers yard, Hewing station, Wattle yard | DECIDED |
| UIW-23 | Gathering | Woodcutter, Forager, Coppice hut, Daub pit, Clay pit | DECIDED |
| UIW-24 | Road, House | Direct tools; no building bar | DECIDED |
| UIW-25 | Buttons generated from definitions | Each bar's buttons come from building definitions, not hard-coded | DECIDED |

**Right-click order (UIW-26, DECIDED):**
1. While placing a building, right-click cancels the placement.
2. With a building tool selected and nothing being placed, right-click deselects that building (e.g. Sawyers yard).
3. The next right-click closes the category (e.g. Processing).

## 5. Building panels: shared template

| ID | Rule | Tag |
| --- | --- | --- |
| UIW-30 | Clicking a building or area opens its panel: a movable box with a title bar showing the building name (numbered per type, e.g. "Woodcutter 1") and a status line, with Pin and Close (x) buttons at the top right | DECIDED |
| UIW-31 | Panels can be dragged anywhere. An unpinned panel is replaced when the player selects something else; a pinned panel stays until closed | DECIDED |
| UIW-32 | Panels read state through read-only views and act only by issuing commands (Tech Spec CMD-07, ARC-03) | DECIDED |
| UIW-33 | Panel width about 360 px; sections separated by dividers, each with a small upper-case heading | DECIDED |

## 6. Production / gathering building panel (built)

Two tabs under the title bar: **General** and **Modules**. Clicking a tab switches the panel body.

### General tab (UIW-40)

| Section | Content | Tag |
| --- | --- | --- |
| Families (n / slots) | One row per family slot: filled slot shows family and worker count with a Remove button; empty slot shows "empty" with an Assign family button | DECIDED |
| Work area | Workers, logs waiting at tree sites, standing trees in work area, work area shape and size; buttons: Draw work area, Use default | DECIDED |
| Storage capacity | Item and total, e.g. "Logs 0 / 40", with a progress bar. The total is the sum of all the building's storage modules | DECIDED |
| Operation | Buttons: Pause operation, Expand operation, Deconstruct operation, Oxen (n) | DECIDED (Oxen button behaviour OPEN) |

### Modules tab (UIW-41)

| Section | Content | Tag |
| --- | --- | --- |
| Modules (n) | One card per module: name and status (Built / In construction n%); a detail line (e.g. "Core module · 2 family slots", "Holds 20 logs · 0 / 20", "Logs delivered 0 / 1"); buttons: High priority: On/Off, and Deconstruct (Cancel while the module is still in construction) | DECIDED |

## 7. Building in construction (UIW-50)

Shown instead of the tabs while a building is not yet built.

| Section | Content | Tag |
| --- | --- | --- |
| Progress | "In progress n%" with a progress bar; hint: building starts once enough materials are on site | DECIDED |
| Items required | Two-column grid, delivered / needed per item, e.g. Logs 5 / 10, Thatch 2 / 10, Planks 2 / 5 | DECIDED |
| Buttons | High priority: On/Off, Cancel construction (opens a removal prompt, section 9) | PROPOSED |

## 8. Storage building panels (granary, storage yard) (UIW-60)

Same template, with no Modules tab, no work area and no expansion.

| Section | Content | Tag |
| --- | --- | --- |
| Families (n / slots) | Hauler family slot(s), with Assign / Remove; hint line explaining what they haul | DECIDED (1 slot each PROPOSED) |
| Stored | Two-column grid of item and amount; only item types that exist in the game are listed (granary: Berries, Mushrooms, Meat; storage yard: Logs, Beams, Planks) | DECIDED |
| Capacity | Total / reserved / available of capacity, with a progress bar | DECIDED |
| Building | High priority: On/Off, Pause, Deconstruct (granary / storage yard) | DECIDED |

## 9. Removal prompts (UIW-70)

Modal box with a message and Yes / No buttons (Yes highlighted).

| Case | Message | Tag |
| --- | --- | --- |
| Unbuilt, no resources used | Cancel this unbuilt "X"? The site will be removed at once. | DECIDED |
| Under construction, resources used | Cancel building "X"? The site will be removed and half the resources used returned. | DECIDED |
| Built | Deconstruct "X"? Half its building materials return to the nearest storage. | PROPOSED (matches GDD BLD-11) |

## 10. Open questions

- [ ] Oxen (n) button on production panels: confirm it shows oxen assigned to the building, and what clicking it does.
- [ ] 64x speed: confirm, and update Tech Spec CLK and P5 prototype targets (64x doubles the simulation load of 32x).
- [ ] Should granary and storage yard panels show a single "General" tab header for consistency?
