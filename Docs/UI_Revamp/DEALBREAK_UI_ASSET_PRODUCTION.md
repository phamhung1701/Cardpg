# Dealbreak UI Asset Production Plan — Character-Art Aligned

**Purpose:** Refine the UI asset plan around the newly supplied Dealbreak character art. This is a production-planning document only; it does not import, assign, alter, or replace project art and does not implement UI.

## 1. Character-art-driven style summary

### Reviewed canon

The new images are in `Assets/Art/Enemies/` and `Assets/Art/Headliner/`: nine enemy files and twelve Headliner files. All reviewed files are 1792×2400 portrait PNGs. Representative pieces across both directories include `Scrapper.png`, `Riot Guard.png`, `The Charmer.png`, `The Chairman.png`, `The Handler.png`, and `The Broker.png`.

### Visual observations

- **Shape language:** forceful silhouettes, angular hair/clothing/armor, sharp diagonal folds, broken geometric frames, cut-paper panels, arrows/chevrons, diamonds, shards, circles and occasional chunky symbols. Forms feel hand-cut and editorial, not ornamental.
- **Color tendencies:** warm paper/ivory, slate and blue-gray, near-black ink, and one or two character-specific accents (examples visible include muted brick red, ochre/yellow, cyan/teal, lime, or magenta). The accent varies by character; do not turn the whole HUD into a multicolor neon system.
- **Shadow and contour:** very heavy black outer contours and deep block shadows; hard-edged cel-shading and bold planes. UI should echo the clear ink edge and offset shadow, but keep control outlines lighter/simpler than the illustrated figures so gameplay remains the focus.
- **Texture:** halftone/stipple shadow fields, dry-brush/scuffed ink, paper grain, slight print wear/over-registration. Texture is visible in large illustration areas but must be reduced or removed behind small UI text and card values.
- **Silhouette:** readable at a glance, expressive and characterful, with strong pose/prop cues. The character is the hero; HUD ornament should not compete with the face or pose.
- **Graphic motifs:** slate-blue collage rectangles, diagonal bars, torn/rough poster edges, thin misregistered accent outlines, heavy ink shapes, cut diamonds, bold circles, sparse labels/stripes. Treat these as a limited motif vocabulary rather than a collection of unrelated decorations.
- **Framing:** use rectangular poster plates with clipped or irregular corners, a bold ink keyline, one offset slate/paper shadow, and occasional short accent rule. Allow the art’s own geometric backplate to remain visible in larger enemy/Headliner displays.
- **Typography tone:** condensed or grotesk editorial display for short headings; sturdy, neutral, highly legible body type for effects/stats. Use compact all-caps only for tiny category labels, not paragraphs. No faux-stencil overload or illegible distressed type.

### Art-to-UI relationship

The PNG previews show complete poster-like compositions with warm-paper margins and geometric fields behind characters. Although the files are RGBA PNGs, that does not prove their background is transparent. Inspect actual alpha/import settings before masking or cropping. Prefer using the existing composition intact in a Headliner presentation or expanded enemy detail. In compact combat cards, fit/crop conservatively around face and identifying prop; never erase/replace a supplied character or generate a substitute. Keep text on a separate solid UI plate instead of laying small copy over busy poster details.

## 2. Refined visual direction

The previous redesign plan’s “neo-punk editorial” direction remains appropriate but should now be governed by the supplied art’s specific print language. Shift from generic dark-blue panels with arbitrary bright accent colors toward **ink + paper + slate**, using one local character accent at a time.

### Proposed starting palette (validate against art and contrast)

| Token | Suggested starting color | Use |
|---|---|---|
| Ink | `#111315` | Contours, strongest text on light panels, offset shadow |
| Deep ink background | `#151A1F` | Negative space around HUD/panels |
| Slate blue-gray | `#718394` | Large field/surface, art-derived secondary panel |
| Dark slate | `#34424F` | Raised compact panel/card |
| Warm paper | `#F1EDE3` | Readable plate, type surface, bright contrast |
| Mid paper gray | `#C7C6BC` | Secondary text/quiet divider |
| Brick red | `#A84E4C` | Damage, danger, destructive confirmation |
| Ochre | `#C5A05A` | Credits/reward cue, not a general highlight |
| Accent rotation | muted cyan, magenta, or acid-lime | Character-specific highlight only; one accent family at a time |

Use semantic colors consistently and add a label, shape, or symbol to every meaningful state. Keep the combat viewport mostly dark/slate so paper cards and supplied art stand forward; use warm paper plates for highly legible text, warnings, selection tabs, and title/detail surfaces. Do not copy the screenshots’ saturated blue/orange styling wholesale.

### Component treatment

- **Panel/frame:** Unity-built angular plates with a solid interior, 2–4 px equivalent ink keyline at 1080p, one offset black/slate shadow, and optional clipped corner/torn-print detail. Prefer Sprite Sliced for stable artwork or vector-like Unity geometry for the majority of scalable surfaces.
- **Button:** hard-edged rectangular/cut-corner plate; clear label; ink outline; light/deep slate or paper fills; concise color stripe for semantic class. Hover/focus = slight lift or brighter paper edge, selected = tab/double keyline, pressed = short compression, disabled = reduced contrast but still readable. No soft capsules or glow.
- **Tooltip/detail:** warm-paper or light slate insert with a bold ink title strip and one small halftone/print accent outside the text column. Keep descriptions clean, aligned, and scrollable; do not put distress beneath body copy.
- **Shop/inventory slots:** rectangular, hardware/tag-inspired plates echoing pockets, labels, card sleeves, or clipped poster tabs. Keep icon/portrait/amount zones clear. Reuse the current slot textures as prototypes, then replace their style via scalable construction if needed.
- **Combat HUD:** restrained framing, deliberate alignment, clear bands and stats; reserve the highest artwork contrast for enemies and selected hand cards. Make current threat/action state stronger than decorative noise.
- **Targeting/selection:** thick but bounded ink/paper outline, one colored corner tab or directional notch, and a text/icon label such as “VALID TARGET.” Invalid targets dim with reason; never signal legality by color alone. Avoid pulsing neon outlines.
- **Map nodes:** small single-weight black pictograms with angular silhouettes, simple cut-corner backing plate, and clear state treatments. At 48–64 px node scale, use simple icon shapes—not mini-illustrations.
- **Background texture:** restrained paper fiber or sparse halftone at low opacity on static plates only. Do not tile the character poster images as a general screen background, and do not texture card indices, small text, health bars, or high-frequency interaction states.

Typography details, screen guidance, mechanics-preservation constraints, and broader roadmap remain in `Docs/UI_Revamp/DEALBREAK_UI_REDESIGN_PLAN.md`; this document adds concrete art alignment and production choices.

## 3. Refined asset classification

### A. Existing reusable assets

| Existing asset | Reuse plan | Notes / risks |
|---|---|---|
| `Assets/Art/Enemies/*.png` (9 files) | Use as current enemy visual canon; assign only to matching supported runtime definitions after content/ID review | Full 1792×2400 poster compositions; confirm import/index state and actual alpha before cropping. Filenames include labels that may correspond to Draft/renamed workbook terminology; do not use filename alone to rename runtime data. |
| `Assets/Art/Headliner/*.png` (12 files) | Use as current Headliner visual canon for enemy panels, preview/detail states, and Headliner presentation | Preserve the actual character art. Headliner source-card face identity path exists; decide per use whether card identity or authored portrait gets priority without changing mechanics. |
| 52 card faces + card backs under `Assets/Art/` | Keep as functional playing-card imagery | Retain clear suit/rank and card-back recognition; add UI frame treatment around them rather than repainting. |
| `itemSlot_default.png`, `itemSlot1_gray.png`, `itemSlotSelected1_gray.png`, `EmptyItemSlot10_darkTheme.png`, `SquareButton3_ultramarine.png` | Reuse temporarily as sizing/state references; selectively recolor/reframe if they support the canon | Do not assume art is sliced/scalable or that slot semantics are self-evident. |
| `Assets/Prefabs/Card.prefab`, `EnemyView.prefab`, `PathNodeButton.prefab`, `ShopItemButton.prefab` | Preserve data binding, sizing, interaction, and serialized references; restyle incrementally | `EnemyTypeData.portraitSprite` is an existing authored image hook; `EnemyDisplayUI` also presents Headliner source-card art. |
| `ActionButtonsUI`, `PathScreenUI`, `ShopUI`, `RunStatusUI`, `EnhancementTargetUI`, `GameplayMenuCoordinator` | Keep as behavior/integration foundations | Plan visual component reuse separately from rule authority and modal/input gating. |
| TMP Liberation Sans and its SDF assets | Use as baseline for prototype and fallback while typeface decision is open | Do not alter the shared TMP asset during this planning task. Verify contrast and glyph coverage before production. |

### B. Build in Unity with scalable UI techniques

These are functional or repeated UI surfaces; do not image-generate them as flattened text-bearing images.

| UI asset/component | Unity construction | Practical Unity notes | Priority |
|---|---|---|---|
| Shared panel/frame family | `Image` with Sprite Sliced 9-slice or layered `Image`/`RectTransform` plates; separate shadow and corner accents | Keep central text zone flat. Test wide/tall/compact forms; corners must not distort. | P0 |
| Button visual kit | Reusable prefab variants + `Selectable` transition states; base sliced plate plus state edge/tab | Preserve Button navigation, disabled reasons, hit size, `KeyButtonBorderStyle` behavior or replace coherently. | P0 |
| Tooltip/detail frame | Reusable detail prefab with flat text-bearing body and optional art/paper header strip | Clamp to safe viewport; scroll long content; maintain focus and pointer behavior. | P0 |
| Shop/inventory slot family | Prefab family with plate, icon/art viewport, cost/count/charge labels, selected/empty/unavailable treatment | Separate artwork viewport from labels; retain existing inventory/backpack and sale logic. | P0 |
| Selection/highlight/target indicators | Outline/tab/icon plus TMP text; shapes can be UI geometry | Separate hover, chosen, valid target, invalid target, and focus. Support keyboard/controller. | P0 |
| Combat banners/HUD decorations | Unity-built top/header rails, intent banner, HP/attack badges, section rules; use supplied art panels where appropriate | Do not bake names, values, numbers, or intent text into raster art. | P0 |
| Map node icon family | Vector-like UI geometry or simple single-color sprites, consistent outline/weight; status rings/tabs built in Unity | Test legibility at actual node size and grayscale/low-contrast states. | P0 |
| Texture accents and graphic frame details | Optional repeated texture material or small non-texture overlays | Keep opacity low; avoid full-screen moving/grain overlays. | P1 |

### C. Externally image-generated support art

**Do not generate character portraits.** The supplied 21 character compositions are the approved canon. Image-generation is appropriate only for optional non-semantic print texture and abstract decorative overlays that Unity geometry cannot produce as economically. All labels, buttons, icons that encode mechanics, borders that must resize, and interaction states remain Unity-built.

The prompt pack (`Docs/UI_Revamp/DEALBREAK_UI_ASSET_PROMPTS.md`) contains complete prompts for this bounded group:

| Generated asset | Use | Proposed export | Priority / gate |
|---|---|---|---|
| Paper + halftone microtexture swatch | Subtle texture mask for occasional static UI plates | 1024×1024 seamless grayscale/alpha texture; tileable; verify repeat seams | P1; only if Unity shader/noise is insufficient |
| Slate poster-field texture swatch | Low-opacity texture for a few large art-adjacent fields, visually linked to supplied poster backplates | 1024×1024 seamless grayscale/alpha mask (texture color supplied by Unity material) | P2; use sparingly |
| Irregular dry-ink corner/edge accent sheet | A few non-repeating poster-frame accents, used as separate decoration behind controls | 2048×1024 transparent PNG atlas of separated corner/edge marks; import as multiple sprites | P2; only if procedural/vector marks feel too sterile |

These are not required for the Combat-first slice. Prototype the Unity-built controls first, and reject generated overlays if their distress competes with text or character artwork.

### D. Missing reference — defer rather than invent

- **Player/protagonist identity and player-side illustration:** none was supplied in the inspected character set. Do not invent a protagonist portrait or commission it as a hidden assumption; defer until the art owner confirms the player-character concept.
- **Gear/Trick item-specific icon meanings:** current GameData definitions establish names/effects but do not provide an approved visual icon set. Start with consistent Unity-built category glyphs and defer item-specific authored pictograms until content owner/art direction review.
- **District/background environment art and menu key art:** character poster fields are not automatically environment assets. Defer any city/interior scene illustration until a reference is supplied; do not reuse character posters as a tiled scene backdrop.
- **Brand logo lockup and font family selection:** use current typography and text wordmark for prototype; defer custom logo art and licensed font acquisition until branding/type review.
- **Iconography for content still marked Draft, deferred, or acceptance-pending in the workbook:** do not produce until content status/names are approved.

## 4. First production batch — Combat-first vertical slice

### Batch order

1. **P0-A — Surface tokens and panel/frame kit:** compact, medium, large panel; flat text body; offset ink shadow; optional corner cuts; no baked text. Build in Unity. Provide light-paper and dark-slate surface variants.
2. **P0-B — Button visual kit:** primary, secondary, destructive, quiet/icon button; focus, hover, pressed, selected, disabled, busy states. Build in Unity.
3. **P0-C — Tooltip/detail frame:** reusable title/description/effect/cost/status anatomy. Build in Unity; place one detail panel in Combat.
4. **P0-D — Slot family:** player Gear rail, Tricks/backpack, targetable card/item slot, empty/unavailable/selected. Build in Unity using the existing slot textures as a scale/state reference.
5. **P0-E — Selection/target indicator family:** selected card edge/tab, legal target, invalid target, focus ring, current action marker. Build in Unity and pair every color state with form/label.
6. **P0-F — Combat HUD/banners:** encounter header, enemy intent/stat plates, player HP/pending damage and block/attack preview, combat log dividers. Build in Unity; display existing `EnemyView` and supplied character art as art content, not rasterized HUD.
7. **P0-G — Map node glyph family:** implement as simple shared geometry or small sprites in Unity; create after one icon style proof. This is P0 for system completeness, but Combat slice can use existing node system until map phase.
8. **P1/P2 optional print accents:** generate only after the above is tested; no generated asset may be required to read or operate Combat.

### Combat-first acceptance target

At 1920×1080, a player can identify the active District/encounter, enemy group and telegraphed threat, current HP/pending damage, hand values and Mod state, selected cards and valid target, available action, and Gear/Tricks access without opening a tooltip. Full effect text and predicted impact are available in an anchored detail panel. Test 16:10 and ultrawide after the core composition is stable. Preserve current attack, block, targeting, hand, and input-gate behavior.

## 5. Combat mockup specification (1920×1080 reference)

This is a layout specification for a future wireframe/Unity prototype, not a screenshot or a prefab implementation.

### Composition and zones

- **Canvas/background:** keep the core playfield deep ink/slate and visually quiet. Place one low-contrast paper-grain/halftone accent only in outer margins or a fixed HUD plate. Do not stretch the character poster files as a full-screen background.
- **Top run strip (about 6–8% screen height):** compact District/depth and Headliner/run context centered or left aligned; Credits/run status on the opposite end. Thin ink/slate divider. Keep timings and secondary counts smaller but still readable. Use current runtime wording.
- **Enemy group zone (roughly 10–43% height):** center the active enemy group in a horizontal row with consistent gaps. Each `EnemyView` keeps a bounded portrait field at the left/top and a clear text/stat column. Use supplied enemy composition at a readable crop or fit; never cover face/recognizable prop with values. Put title, HP, attack/intent, shield/status in solid paper/slate plates next to or below the image, not over its busy backplate. For a Headliner, allow a larger focal art panel; for regular enemies, use a smaller consistent portrait crop. Keep group width adaptive for multiple enemies.
- **Enemy intent rail (near enemy group):** a compact banner states the known next action/threat and numerical range/telegraph using runtime-approved data. Use a brick-red paper tab or warning notch only for actual danger. Long ability text opens detail, not a paragraph stuffed into the portrait.
- **Player state rail (left or lower-left, roughly 10–18% width):** HP and pending damage first; visible block/defense/status next; Gear capacity and Credits/resource summary below. Use short stacked labels and icon+number, with high-contrast paper panels for critical values. Avoid a stack of unlabeled decorative slots.
- **Handfield (lower middle, about 45–62% width, 32–38% height):** anchor the fan/row in a stable region above the action bar. Maintain legible rank/suit corners and Mod/stat plate. At eight cards, allow modest controlled overlap but keep indices and selection badges clear; selected cards rise and are numbered. At higher counts/narrow width, switch to a compact row/scroll or focus-to-inspect behavior instead of shrinking the whole hand. Keep cards’ white/paper faces visibly distinct from the ink/slate playfield.
- **Gear/Tricks area (left edge or lower-left dock):** compact five-slot rail with icon/art viewport, charge/stack and empty state. On a narrow aspect, collapse labels to icon+counter but preserve tooltip/details on focus. Do not let the rail overlap card corners or block drag targets.
- **Action bar (bottom center-right, roughly 10–14% height):** stable Attack and Block button positions, primary action indicated by current context; show resolved attack/block value and disabled reason above or within a separate summary strip. Secondary controls (sort, recover, deck) remain visually quiet and do not compete with the two main actions.
- **Detail panel (right side, anchored to current selection):** appears on hover/focus/select; width around 300–420 reference pixels, height content-driven but capped to safe area. Paper/light slate body, black heading band, title + concise summary + full effect + target/timing + predicted state change; action confirmation only when needed. It must not cover the active target or critical HP. Clamp/mirror placement based on safe bounds.
- **Combat log:** optional compact lower/side rail with last event, expandable; prioritize current threat and selection cues over historical messages.

### Information hierarchy

1. “What can hit me / what is the enemy doing?”
2. “What is my health and pending damage?”
3. “Which cards can I use, what will their action resolve to, and what target is legal?”
4. “What contextual Gear/Tricks action is available?”
5. Expanded rule explanation, run timing, deck/service secondary details.

### What is stylized versus minimal

- **Stylize:** outer frame/shadow, section header plates, portrait surround, selected-card tab, intent badge, item-slot edge, map category glyphs, small halftone or dry-ink corner accent.
- **Keep minimal/flat:** card values, HP and attack numbers, descriptions, target instructions, disabled reasons, currency, health fills, interactive text. No texture, distressed text, handwritten fake labels, or excessive shadow behind these.
- **Use supplied art:** character portraits and larger Headliner spotlight. Keep the image’s own collage geometry visible; do not generate alternate versions.
- **Build in Unity:** panels, frames, button states, slot states, text, maps, interaction indicators, HP bars, intent badges, category icons, shadows/geometry. Keep all dynamic text and mechanics data live.
- **Image-generated only if needed:** optional non-semantic texture/corner-decoration pieces defined in the prompt document. None are required to reach the mockup acceptance target.

### First-batch assets used by the mockup

Unity-built: panel/frame variants; primary/secondary/destructive buttons; detail card; inventory slot family; card/enemy selected/target states; combat header/intent/HP bands; attack/block summary treatment; map node glyph proof. Reused art: `Assets/Art/Enemies/*.png`, `Assets/Art/Headliner/*.png`, card faces, current slots. Optional generated: paper grain and a few corner accents only after visual review.

## 6. Unity implementation and verification notes (future work)

- Inspect the actual on-disk images in Unity after import; confirm dimensions, alpha, sRGB, compression, filtering, max texture size, and sprite mesh/rect. The asset index did not yet return these newly added art paths during this audit, so verify import/index freshness before serialization references are planned.
- Portrait assignments should follow `EnemyTypeData.portraitSprite` and current GameData/runtime identity. Several art filenames use revised/draft terminology according to the rebranding report; do not rename `enemyName`, change IDs, alter workbook status, or change save behavior as part of UI art hookup.
- Preserve Headliner source-card identity behavior in `EnemyDisplayUI` unless the gameplay/content owner explicitly approves a display priority change.
- Test full-size art and small combat crops against busy poster backgrounds; use a flat plate behind labels and check contrast at actual display size.
- Verify at 1920×1080 first, then 16:10 and 21:9/ultrawide; add narrower/windowed test. Check mouse drag, hover, keyboard/controller focus, action targeting, pause/modal stack, and Escape cancellation.

## 7. Dependencies and unresolved decisions

- Art files are new/untracked project content at the time of this audit; this documentation-only task will not stage or commit them. Confirm whether their files and `.meta` files will be added in a separate art-content change.
- Confirm whether PNG alpha is meaningful; RGBA encoding alone does not establish transparency.
- Align the files whose names are Draft enemy renames with current approved runtime/GameData labels without renaming content during UI work.
- Confirm whether all twelve Headliners are intended in the next production scope; the workbook report records some Headliner ability/scaling content as Draft even while portrait names/art exist.
- Approve final palette and font licenses after a small mixed-art/monochrome readability review. Character accent colors should not be repurposed as global semantics.

---

**Boundary:** This document plans asset/UI production only. It does not modify character images, import/assign assets, edit Unity scenes/prefabs/scripts, alter gameplay, or authorize changes to GameData/save identifiers.
