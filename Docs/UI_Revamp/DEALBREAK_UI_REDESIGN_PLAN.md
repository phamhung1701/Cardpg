# Dealbreak UI/UX Redesign Plan

**Scope:** Planning only. This document proposes a visual and interaction redesign while preserving gameplay rules and existing functionality. No scenes, prefabs, scripts, settings, or art assets are changed by this plan.

## Reference and evidence notes

The earlier request’s `Docs/UI_Revamp/` folder initially had no reference images; this update uses the supplied character-art files now present in `Assets/Art/Enemies/` and `Assets/Art/Headliner/` as the primary visual canon. The images reviewed include nine enemy pieces (Crew Leader, Ghost, Heavy, Hype Man, Pickpocket, Riot Captain, Riot Guard, Scrapper, Security Guard) and twelve Headliner pieces (The Broker, The Chairman, The Charmer, The Courier, The Director, The Fixer, The Foreman, The Handler, The Hitter, The Icon, The Influencer, The Mogul). All are 1792×2400 portrait PNG compositions. The reference screenshots attached earlier depict an older build; use them only for broad UX observations, not labels, current mechanics, or layouts to copy.

The project audit covered `Assets/Scenes/MainMenu.unity`, the active `Assets/Scenes/Game.unity`, UI/front-end scripts, the card/enemy/map/shop prefabs, texture/font indexing, and the confirmed workbook identified in `Docs/Dealbreak_Rebranding_Report.md` as `Dealbreak_GameData_IDs_Terminology_Fixed.xlsx`. That workbook contains mixed statuses. Player-facing copy must be drawn from implemented/current runtime data; draft, deferred, or acceptance-pending workbook proposals are not shipped content. The workbook and report note some open content/runtime discrepancies, so UI work should display runtime-authoritative values and should not silently resolve balance questions.

The attached screenshots show an early menu/settings/run-seed presentation, a route map, and combat with an eight-card hand and enemy cards. They suggest information crowding, weak grouping, small secondary text, unclear visual priority, and a reliance on text-only enemy placeholders. These are useful hypotheses to verify against the current Editor/build; the current Game scene has since gained additional UI and flows.

## 1. Current UI assessment

### Existing structure and strengths

- `MainMenu.unity` has a 1920×1080 reference CanvasScaler and separate Home, Run Setup, Credits, and Settings panels. Resolution, display mode, and VSync are functional settings rather than decorative controls.
- `Game.unity` uses a 1920×1080 screen-space overlay Canvas, with separate drag, front-end overlay, and attack-presentation canvases. The gameplay Canvas contains run status, combat, map, deck view, Black Market, Incident, targeting, reward, and result surfaces.
- `GameplayMenuCoordinator` already centralizes stacked blocking menus, pauses time, gates gameplay input, restores selection, and tracks the selected UI object. Retain this behavior and use it as the navigation foundation.
- `Card.prefab` provides a useful live interaction model: 136×194 base dimensions, face sprites for all 52 standard cards, hover/selected/drag states, stat and Mod plates, and selection badges. Reuse its data and interaction states while improving frame, contrast, and hand spacing.
- `EnemyView.prefab` already represents enemy name, HP, attack, health, state, and target feedback. Its portrait field can display an authored `EnemyTypeData.portraitSprite`; the supplied enemy and Headliner artwork now provides approved character imagery. The existing `EnemyDisplayUI` also handles the Headliner source-card identity path. Reuse these images; do not plan replacement portraits.
- `PathScreenUI` calculates map node positions and responds to map-container size changes. `ShopUI` and `RunEventUI` create some controls and scroll viewports at runtime. `EnhancementTargetUI` supports several contexts (shop, upgrade, Incident, Trick), and `DeckViewerUI` rebuilds card snapshots when its viewport changes.
- Current canonical player-facing terms include **Credits, Gear, Mods, Tricks, Black Market, Incident, District, Enforcer,** and **Headliner**. The workbook lists implemented services including **Stake, Intel,** and **Contract**. Confirm copy against current runtime assets and workbook status when building each screen.

### Main problems and opportunities

1. **The visual system is not yet a consistent product language.** Screenshot-era flat blocks and generic controls do not communicate Dealbreak’s modern underground editorial direction. Establish shared type, spacing, frame, and semantic-color tokens before restyling every screen independently.
2. **Combat’s most important decisions compete for attention.** The player must understand current threat/intent, hand values and Mod effects, valid target, pending damage, and the consequences of playing/blocking. Build a stable hierarchy around threat → hand/action → player condition, rather than presenting every label at similar weight.
3. **Hand readability and manipulation degrade at high card counts and narrow widths.** Fanned/overlapping cards are evocative but can obscure ranks, suit, stats, or selection badges. Establish minimum readable card dimensions, adaptive fan/row behavior, and a deliberate inspect/zoom interaction.
4. **Contextual actions are discoverable only after selection.** Attack/block, enemy targeting, Trick use, sale, discard, Mod targeting, and confirmation need clear “what is selected / what can I do / what will happen / how do I cancel?” feedback, including mouse, keyboard, and controller paths.
5. **Long effects, offer text, and choices compete with visible options.** Current shop/event flows include dynamic scrolling; the redesign should give effect text predictable hierarchy and scroll boundaries rather than shrinking text or letting content run off-screen.
6. **Map nodes need an explicit state and route legend.** Distinguish reachable, completed, unavailable, revealed/hidden, risk, reward, current route, and Headliner destination using more than color alone.
7. **Responsive behavior is only partly assured by a 1920×1080 reference.** Some systems are size-aware, but runtime-created controls and panel layouts need systematic 16:10, ultrawide, and narrower-aspect testing.
8. **Artwork and type need a consistent handoff.** The provided character canon is highly finished and visually specific, while the reusable UI texture set and TMP font source remain limited. Build frames and icons to complement the art’s ink, paper, slate, and restrained accent language; inspect PNG alpha/import behavior before deciding on crops or transparent overlays.

## 2. Proposed visual direction

Treat Dealbreak like a sharply art-directed underground card table publication: editorial typography, cut/angular geometry, bold black drop-shadows, restrained ink-like texture, and clear game-state communication. Use the visual style to frame information, not to decorate every pixel.

- **Composition:** Use an asymmetric but disciplined grid. Anchor the player’s hand and primary action zone; reserve stable zones for threat/intent and run status. Use angled corner cuts, offset rules, and occasional hard-edged shadow slabs to create character without sacrificing alignment.
- **Surfaces:** Anchor the system in warm paper/ivory, slate-blue gray, and near-black ink, matching the supplied poster fields. Use brick red sparingly for danger/destructive confirmation and ochre for Credits/rewards. Cyan, magenta, or lime may appear as character-specific accents only; do not make all of them global status colors. Validity and selection should primarily use shape, outline, label, and placement, with color as a secondary cue.
- **Print treatment:** Add selective halftone/grain to title art, portrait backgrounds, or section dividers. Keep texture out of small text, card values, health bars, and dense information fields. Ensure texture never reduces contrast.
- **Illustration:** The supplied enemy and Headliner compositions are the visual canon. Their hard ink contours, angular silhouettes, cream paper, slate-blue poster fields, halftone/stipple shadows, scuffed print edges, and restrained character-specific accents should directly inform the UI. The previewed PNGs contain full poster backplates; inspect alpha/import settings and use these full compositions thoughtfully instead of assuming transparent cutouts. Do not generate replacement character art.
- **Clarity:** Every critical state must be conveyed by text/shape/icon as well as color. Keep decorative elements behind content and non-interactive.

## 3. Screen-by-screen recommendations

### Main Menu, Run Setup, Credits
- Build a strong logo/title lockup with one editorial illustration or geometric motif, a concise current-build descriptor, and a short, obvious primary action. Keep secondary actions grouped and visually quieter.
- Make New Run and Continue/Run Save choices understandable as distinct paths. Maintain seed entry and randomization; explain blank-seed behavior in one short helper line and show the normalized seed when entered.
- Credits should use readable grouped headings and scroll only when content exceeds the panel; retain a clear return action and keyboard/controller navigation.
- Do not reuse the attached screenshot’s old title copy or button wording without checking current strings.

### Combat and Handfield
- Create three stable information bands: **encounter/threat** (enemies and telegraphed action), **player state** (HP, pending damage, defense/status, Gear/Tricks access), and **hand/action** (cards, selection summary, Attack/Block/other valid action).
- Make the hand the focal point. Reduce unnecessary overlap; keep ranks/suits and Mod labels legible at eight cards and test larger supported hand capacities. On tight widths, switch from wider fan to compact row/scroll/inspect behavior instead of scaling all cards until text is tiny.
- Make selected cards visibly lifted/framed and numbered when multiple cards are chosen. Show the resolved attack/block value and any applicable rule constraints before commitment. Separate “selected” from “valid for this target/action.”
- Keep Attack and Block as clear, stable actions with explicit disabled reasons and action cost/outcome. Avoid swapping button positions between combat states.
- Give current enemy intent, attack value, HP, shields/status, and special rule the strongest in-encounter hierarchy. Put detailed ability explanations behind a consistent inspect panel/tooltip.
- Present combat log as a compact, recent-event feed with clear severity and optional expansion; do not let it displace current actionable state.

### Map and route selection
- Make the map read left-to-right with a persistent legend and clear selected/reachable path. Use node shape/icon for node category and separate markers for risk/reward/reveal state.
- At all times summarize current District, route progress/depth, next Headliner, and Credits. Place explanatory node details in a stable selected-node panel rather than cramped node labels.
- Distinguish available nodes by both brighter frame and accessible focus/hover state; unavailable nodes remain visible but subdued. Reveal Intel changes with a brief map cue and status text.
- Preserve seeded route determinism and current navigation; map motion should clarify the selected path rather than obscure route links.

### Black Market — Goods and Services
- Split the surface into clear **Goods** and **Services** sections; keep Credits balance persistently visible. Each offer card should show item/service name, category, price, one-sentence effect, owned/stock/quantity state, and availability reason without requiring selection to reveal basic facts.
- On selection, show a larger details area with full canonical description, costs, limits, and impact. Keep purchase quantity controls adjacent to quantity/price totals. In sell mode, visibly mark owned Gear eligible for sale, show payout before confirmation, and make cancel/return-to-buying obvious.
- For services, distinguish Stake’s risk/uncertain return from deterministic Intel and Contract outcomes. Show requirements and consequences before the final commit step. Present confirmation only when the action is materially irreversible or costly; use explicit confirm/cancel labels.
- Provide clear empty/unaffordable/unavailable states and preserve scroll position when changing selection or buy/sell mode where practical. Do not copy unsupported fees or draft service descriptions from the workbook.

### Gear and Tricks inventory
- Turn the existing Gear rail/backpack slots into a compact but scannable inventory strip with capacity counts, distinct item silhouettes, stack/charge count, and consistent selection outline.
- Keep item name and short effect available on hover/focus; clicking opens a persistent detail card with timing/target/charges and actions that apply in the current context. Empty slots should be visibly empty without resembling disabled controls.
- Inventory actions (use, discard, sell, replace) must be context-aware and must explain why an action is unavailable. Destructive discard/replacement requires a preview and an explicit confirmation consistent with existing gameplay rules.

### Card and item details / tooltips
- Use one reusable detail-panel anatomy: category eyebrow; title; concise effect summary; trigger/timing and target; numeric values/caps; ownership/charges/price; contextual action row. Preserve authored wording and meaning.
- Prefer anchored detail panels for card hover/focus, with a short delay and no pointer obstruction; pin on selection/click and allow dismissal by Escape/click-outside where safe. For controller, show details for the focused object.
- Do not rely on tooltips as the only way to understand a rule or action. Important current enemy intent, price, and target rules stay visible in the main layout.

### Mods and contextual targeting/confirmation
- Enter targeting with a persistent instruction banner that states the chosen Mod/Trick and target rule. Highlight legal targets clearly; dim and label invalid targets with a concise reason. Show the predicted change on the selected card/item before confirmation.
- Keep confirm disabled until a valid target is selected. Back/Cancel and Escape must return to the exact origin context without consuming the item/offer or losing unrelated selection.
- For multi-step actions, show a compact step indicator (e.g., choose offer → choose target → confirm) and a final outcome summary. Preserve all current validation and input-gating logic; UI redesign must not move rule authority out of gameplay systems.

### Incidents, rewards, results, settings, overlays
- Incident panels should separate narrative description from choices. Each choice is a full-width selectable option with consequence/cost/risk visible before commitment; scroll long descriptions and choices independently when needed.
- Reward panels should emphasize what is gained, replacement/decline consequences, and any capacity limit. Results should clearly distinguish victory/defeat, seed/replay options, and next steps.
- Settings should group Display controls, expose the functional resolution/mode/VSync controls, show pending vs applied state where appropriate, and keep Apply/Back consistent. Avoid fake/placeholder settings.
- Pause, confirmation, error, save, and other overlays should share a modal frame system, consistent dimming, focused default action, explicit cancel path, and correct stacking/input gate behavior. Do not allow decorative panels to intercept clicks.

## 4. Design system specification

These are initial design tokens to test in Unity, not final locked values. Validate contrast and perceived scale at target resolutions before rollout.

### Typography
Use a licensed, bundled TMP-compatible family with at least a strong condensed/display face and a highly readable text face, after checking glyph coverage and language needs. Liberation Sans is presently available and may serve as a temporary body-font baseline; do not assume a display family already exists. Create TMP assets/fallbacks deliberately and verify punctuation, suit symbols, and icon-font behavior.

| Role | Initial 1080p range | Treatment / use |
|---|---:|---|
| Display/title | 44–64 px | Distinct editorial face, sparse use, short headings |
| Screen/section heading | 28–36 px | Strong weight, sentence/title case or compact uppercase |
| Panel/item title | 20–26 px | Bold, high contrast |
| Body/effect copy | 17–21 px | Comfortable line spacing; avoid all caps |
| Labels/metadata | 14–17 px | Medium weight; secondary contrast, never essential alone |
| Compact numeric/status | 18–24 px | Tabular figures where supported; high clarity |

Do not solve overflow by globally reducing font sizes. Use content-aware wrapping, bounded scroll areas, concise summaries, and expandable detail.

### Palette (proposed starting tokens)

| Token | Starting value | Meaning |
|---|---|---|
| Ink / deepest background | `#11161B` | Dark negative space behind the paper/slate artwork |
| Charcoal surface | `#202830` | Dense gameplay surfaces and lower HUD rails |
| Slate-blue field | `#718394` | Art-derived mid-tone panels and quiet surface blocks |
| Raised slate | `#34424F` | Interactive cards, secondary panels |
| Paper / warm off-white | `#F1EDE3` | Primary text, highlight surfaces, editorial contrast |
| Ink black | `#111315` | Outlines, shadow slabs, strongest text on paper |
| Muted paper text | `#C7C6BC` | Secondary text against dark surfaces |
| Primary action | `#607F98` | Slate-blue action; pair with shape/label, not just hue |
| Brick/crimson | `#A84E4C` | Damage/danger/destructive confirmation, restrained |
| Ochre | `#C5A05A` | Credits/reward accent, limited use |
| Accent rotation (art-dependent) | muted coral / magenta / cyan / acid-lime | Character-specific trim only; do not combine all accents in one screen |
| Disabled | `#68727B` | Disabled controls with readable labels |

Check text/background contrast for all states. Use semantic color consistently and pair it with iconography, shape, label, or pattern.

### Components and interaction states
- **Panels:** Build primarily from Unity/UI geometry: firm rectangular/slanted paper or slate plates, clipped corners, offset black ink slab/shadow, occasional torn/rough edges only as static corner accents. Match the art’s graphic backplates, not ornate borders. Reserve halftone for small inset fields and never put it behind body text.
- **Buttons:** Use mostly Unity-built hard-edged plates with a bold ink edge, slate or warm paper fill, and compact accent rule/stripe. State changes echo graphic cutouts and print over-registration: hover shifts/lifts slightly; pressed compresses; selected gets a clear double-line or paper tab; destructive uses brick red. No glowing neon or soft pill buttons. Keep hit target and keyboard/controller focus readable.
- **Cards:** Preserve familiar suit/rank readability. Use restrained geometric edge/plate accents for state, stronger frame for selected/targetable, and clear independent markers for Mod/temporary status. Preserve the card’s base interaction and animation, not its old visual treatment by default.
- **Map nodes:** Use flat, high-contrast category glyphs that share the characters’ thick black contour and angular silhouette language; make combat/Black Market/Incident/Headliner readable at small size. Use color sparingly and pattern/shape/state tags in addition. Do not generate detailed miniature art for every node.
- **Tooltip/detail:** Consistent width limits, hierarchy, padding, shadow/frame, rich-text rules, and safe-screen clamping. Long copy scrolls or expands.
- **Feedback:** Brief attack/block/credit/reward cues, clear HP change direction and amount, confirmation state, and concise accessible text cues. Motion must not carry critical information alone.

### Layout, motion, and navigation
- Establish an 8 px base spacing rhythm with larger 16/24/32 px group spacing and consistent screen-safe margins. Use anchors/layout groups for stable regions; avoid brittle absolute-position one-offs.
- At 1920×1080, allocate minimum legible space first, then distribute surplus. For 16:10 and 4:3, permit panel reflow, scrolling, or alternate hand presentation. For ultrawide, constrain content to a readable max-width and use side rails for persistent status rather than stretching text.
- Use short, purposeful transitions (roughly 120–220 ms for selection/hover and 180–300 ms for panel entry/exit). Keep combat response crisp; provide a reduced-motion option if project settings architecture supports it.
- Ensure EventSystem selection, navigation order, focus restoration, and Escape behavior match `GameplayMenuCoordinator` and current input gates. Test mouse, keyboard, and controller-equivalent navigation even if controller support is partial.

## 5. Existing asset reuse opportunities

### Reuse as-is (subject to visual QA)
- 52 playing-card face sprites in `Assets/Art/Clubs/`, `Diamonds/`, `Hearts/`, `Spades/`; standard card semantics are central to the game.
- Existing card backs `Assets/Art/Back Blue.png` and `Assets/Art/Back Red.png` where those variants still serve current presentation.
- The `Card.prefab` data wiring and interaction model (hover/selection/drag/selection order), `PathNodeButton.prefab` and `ShopItemButton.prefab` binding patterns.
- `GameplayMenuCoordinator` modal-stack/input restoration behavior, `PathScreenUI` map state/layout behavior, and gameplay event-driven refresh patterns. These are functional systems, not decorative assets.

### Reuse with adjustments
- `Assets/Art/itemSlot_default.png`, `itemSlot1_gray.png`, `itemSlotSelected1_gray.png`, and `EmptyItemSlot10_darkTheme.png`: inspect at target scale; harmonize borders, angular framing, semantic selected/empty states, and palette.
- `SquareButton3_ultramarine.png`: retain only if it can be brought into the new system as a scalable, accessible primary-button surface.
- Existing `Card.prefab` plates, outline, and shadow: restyle for the neo-punk editorial kit while preserving rank/suit/stat readability and target states.
- `Assets/Art/Enemies/` (9 images) and `Assets/Art/Headliner/` (12 images): supplied visual canon, preserve as authored. Reuse compositions as authored portraits/banner art where framing allows; check their embedded paper/slate backplates, alpha, crop, compression, and alpha-edge behavior in Unity. Do not replace or regenerate characters.
- Existing scene panels, buttons, scroll regions, and overlays: audit each for existing serialized references and gameplay bindings before visual refactor. Avoid replacing referenced objects solely to restyle them.

### Replace only after comparison
- Flat/default panels, inconsistent borders, placeholder iconography, and purely decorative surfaces that conflict with the approved art direction. Replacement must retain interaction target size, clipping, raycast, and serialization behavior.
- Do not replace the playing-card artwork or supplied character illustrations. The 21 new enemy/Headliner poster compositions are the approved visual canon; adapt UI around their line weight, palette, halftone, framing and variable character accents.

## 6. Missing asset production checklist

Create these only after visual prototypes confirm need. Recommended paths are proposals; confirm project art-folder conventions before adding files. Keep editable master sources (PSD/AI/SVG as licensed/available) alongside exported PNGs. UI textures should use alpha where needed, sensible compression/filtering, and atlasing where appropriate. Slice scalable frames in Sprite Editor and validate corners/edges at small and large sizes.

| Asset | Purpose / recommended design | Export guidance | Folder proposal | Priority |
|---|---|---|---|---|
| Angular UI frame kit: large/medium/small panels, dividers, corner brackets | Reusable editorial framing with hard shadow and restrained distress; supports modal, shop, tooltip, and status surfaces | PNG with transparency; source vector/PSD; 9-slice panels with safe center/edge margins; test multiple aspect ratios | `Assets/Art/UI/Frames/` | P0 |
| Button-state kit: primary/secondary/destructive/quiet | Consistent angular interaction surfaces, clear pressed/focus/disabled state; no text baked in | Transparent PNG or sliced/vector-style sprites; 9-slice; separate optional state accents rather than many baked combinations | `Assets/Art/UI/Buttons/` | P0 |
| Semantic UI icon family | HP, pending damage, block, attack, Credits, Gear, Trick, Mod, target, risk, reward, lock, confirm/cancel | 24/32/48 px master grid, monochrome source plus controlled accent variants; transparent SVG source and/or PNG exports; no color-only meaning | `Assets/Art/UI/Icons/` | P0 |
| Enemy/Headliner portrait collection | Supplied canon: 9 enemy + 12 Headliner images in `Assets/Art/Enemies/` and `Assets/Art/Headliner/` | Reuse without replacement; inspect alpha/import, compression, texture size, full poster backplate and fit/crop options | Existing folders | Existing canon |
| Gear/Trick/service category marks | Distinguish item families and service risk/benefit at a glance; consistent line weight and silhouette | 48–96 px transparent icons, vector masters; atlas grouping | `Assets/Art/UI/Icons/Items/` | P1 |
| Selective print textures | Low-frequency halftone/grain for title/portrait/frame backplates only | Seamless grayscale/alpha tile 512×512 or 1024×1024; low contrast; tileable; optional shader/material use | `Assets/Art/UI/Textures/` | P2 |
| Dealbreak brand/title lockup (if not already available/approved) | Editorial wordmark with controlled geometric cut, legible at menu and compact overlay sizes | Vector master plus transparent horizontal and compact PNG exports; target wide ratio ~3:1 and square mark | `Assets/Art/UI/Brand/` | P1 |

No placeholder art should be generated as a substitute for this checklist. For fonts, select an appropriately licensed family and document license/source; font files and TMP atlases are not assumed to be missing artwork assets, but must be budgeted and validated for glyph coverage before implementation.

## 7. Phased implementation roadmap

### Phase 0 — Content and responsive prototype specification
- **Goals:** Confirm current copy/status source, use the supplied 21-image character canon, agree on tokens and screen-state matrix; validate poster-image import/crop behavior and prototype typography/panel density before broad conversion.
- **Screens/components:** Design-system sample board; inventory GameData labels/descriptions; document resolution/aspect and input cases.
- **Assets:** P0 frame/button/icon prototypes; font shortlist with license and glyph review; inspect the supplied enemy/Headliner PNG alpha, import settings and target UI crops. Do not plan replacement portraits.
- **Unity systems/prefabs:** No mechanics changes. Prototype in an isolated UI test scene or safe branch; inspect CanvasScaler settings, `GameplayMenuCoordinator`, and reusable prefabs.
- **UX improvement:** Resolve visual direction and minimum readability early, avoiding a large one-off restyle.
- **Risks:** Workbook has mixed statuses and known content/runtime discrepancies; supplied art filenames include revised terminology that may not be approved as runtime labels. Asset index freshness/import state must be checked before portrait references are assigned.
- **Verification:** Art/content owner sign-off; contrast and type-scale review; prototype captured at 1920×1080, 16:10, and ultrawide.

### Phase 1 — Shared UI primitives and combat handfield vertical slice
- **Goals:** Establish tokens, frames, button variants, focus/hover/disabled behavior, tooltip/detail panel, and responsive content boundaries; prove the densest interaction zone.
- **Screens/components:** `Game.unity` combat/player/enemy/hand/action presentation; card selection, attack/block, enemy targeting, status and detail feedback.
- **Assets:** Approved P0 panel/button/icon kit; adjusted card surfaces; reuse supplied enemy/Headliner art (no character replacement generation).
- **Unity systems/prefabs:** `Card.prefab`, `EnemyView.prefab`, `ActionButtonsUI`, `EnemyDisplayUI`, `EnemyGroupUI`, `Field`/`CardView`, `AttackCardPresentationUI`, EventSystem and input-gating flows. Keep combat rules in existing gameplay systems.
- **UX improvement:** Immediately improves primary decision clarity, target validity, hand legibility, and action confidence.
- **Risks:** Card layout and dynamic scaling; overlapping canvases/raycasters; regression to drag/selection/hover; target feedback hidden by new frames.
- **Verification:** Play through representative combat at eight-card and maximum supported hand size; test select/deselect/multi-select, attack, block, invalid/valid target, Trick targeting, pause/modal restoration, keyboard/controller focus, and resolution matrix. Confirm no gameplay outcomes changed.

### Phase 2 — Black Market, Gear/Tricks, descriptions, and contextual confirmations
- **Goals:** Unify item/offer details and contextual actions; make price, requirement, quantity, sale payout, target, risk, and confirmation explicit.
- **Screens/components:** Black Market Goods/Services, Gear rail/inventory, backpack/Tricks, Mod/Trick targeting, offer detail, sell mode, service confirmation.
- **Assets:** Item/service category icons; adjusted slot family; detail/tooltip frames; optional approved Gear illustrations.
- **Unity systems/prefabs:** `ShopUI`, `ShopItemButton.prefab`, `EnhancementTargetUI`, `ArtifactRailUI`, `RunStatusUI`, `ConsumableSlotDragUI`, `ConfirmationDialogUI`, current GameData ScriptableObjects. Preserve stable IDs, save lookup, targeting validation, and transaction rules.
- **UX improvement:** Better comparison, fewer accidental purchases/discards, improved effect comprehension and state continuity.
- **Risks:** Shop dynamically creates controls; affordability/requirements can change; sales/replacements are consequential; source of truth includes pending/draft descriptions.
- **Verification:** Test all implemented goods/services, zero/low Credits, unavailable reasons, bounded quantities, target selection/cancel, sale preview/cancel/confirm, full inventory/replacement/decline, long copy, scrolling and input gating. Assert values/outcomes unchanged.

### Phase 3 — Map, Incident, reward, and deck/support panels
- **Goals:** Make route and node state easy to parse; make choice consequences legible; align long-content scrolling and result feedback with shared components.
- **Screens/components:** Map/path panel, node focus/selection/legend, Incident, rewards, deck viewer, run status, results.
- **Assets:** Node category/state symbols, selective texture accents; no new art unless approved.
- **Unity systems/prefabs:** `PathScreenUI`, `PathNodeButton.prefab`, `RunEventUI`, `ConsumableRewardUI`, `DeckViewerUI`, `RunResultUI`, `RunStatusUI` and existing transitions.
- **UX improvement:** Better route planning and understanding of risk, reward, and irreversible choices; fewer hidden/ambiguous map states.
- **Risks:** Seeded path/reveal logic, map resizing, very long Incident content, card snapshot performance and modal stacking.
- **Verification:** Test all node states and reveal changes, long/short descriptions, all choice interaction types, reward capacity/decline, deck resizing, victory/defeat/retry and modal return behavior at target aspect ratios. Confirm deterministic run state remains unchanged.

### Phase 4 — Main menu, settings, pause, credits, and global polish
- **Goals:** Complete brand presentation and consistent navigation, including applied display settings and save/continue paths.
- **Screens/components:** Main menu, run setup/save menu, settings, credits, pause, confirmation, error/result overlays.
- **Assets:** Brand lockup if needed; approved title illustration/texture; shared panels/buttons/icons.
- **Unity systems/prefabs:** `MainMenuUI`, `FrontendMenuNavigator`, `RunSetupUI`, `RunSaveMenuUI`, `SettingsMenuUI`, `DisplaySettingsService`, `GameplayMenuCoordinator`, `PauseMenuUI`, `SavePromptUI`, confirmation/result UI.
- **UX improvement:** Clearer first-run entry, better navigation and visual continuity across frontend/gameplay.
- **Risks:** Save prompts, scene transitions, resolution apply/restore, Escape/back stack and selection restoration.
- **Verification:** New run/random/custom seed, save/continue/error, every display setting and back path, quit confirmation, pause/settings nested stack, keyboard/controller navigation, and window/display transitions.

### Phase 5 — Accessibility, compatibility, and performance pass
- **Goals:** Audit all screens for readable scale, contrast, overflow, interaction target size, input focus, and layout/performance issues.
- **Screens/components:** Entire UI suite, including worst-case hand, longest description, most offers, widest enemy group, nested modals.
- **Assets:** Final texture atlases/compression, font fallback/glyph coverage, reduced-motion behavior.
- **Unity systems/prefabs:** CanvasScaler/layout groups, raycasters, ScrollRects, TMP assets, animation/transitions, tests and screenshots.
- **UX improvement:** Reliable play across supported displays and input methods; fewer clipped labels or unreachable actions.
- **Risks:** Increased draw calls/Canvas rebuilds, content/localization expansion, unusual aspect ratios and font fallback metrics.
- **Verification:** Automated layout/content checks where possible; screenshots and manual walkthrough at 1920×1080, 16:10, 21:9/ultrawide, and a narrower 4:3 or windowed viewport. Profile rebuild hotspots; verify no clipped/overlapped actionable UI, color-independent state, focus path, and reduced-motion setting if supported.

## 8. Risks and dependencies

- **Source-of-truth/status:** The confirmed workbook mixes implemented, Draft, deferred, and acceptance-pending entries. UI must not expose draft names/effects by assumption. Use runtime-authoritative values until content owners resolve differences.
- **Serialization and saves:** Preserve ScriptableObject IDs, asset GUIDs, serialized fields, and event bindings. Prefer restyling existing objects/components over delete-and-recreate. Audit usages before renaming methods/fields/components.
- **Logic/UI coupling:** Current UI scripts own substantial presentation flow and dynamically create controls. Separate visual component composition from action authority incrementally; do not rewrite gameplay logic as part of a visual phase.
- **Character art:** The supplied 21 poster-like character images are the current canon and should be reused, not replaced. Their graphic paper/slate backplates appear integral in the previews; despite RGBA PNG encoding, do not assume transparent cutouts—inspect alpha/import behavior before cropping or layering. The existing `EnemyTypeData.portraitSprite`/`EnemyDisplayUI` path offers a natural integration point, subject to current GameData status and explicit asset assignment.
- **Text expansion:** Long descriptions and future localization can break fixed panels. Design bounded text regions, scalable height/scroll behavior, and test maximum strings early.
- **Resolution differences:** CanvasScaler reference resolution is 1920×1080 but does not guarantee good layout at 16:10, 21:9, or 4:3. Define safe margins, max content width, and alternate hand/map behavior.
- **Input/accessibility:** New overlays can accidentally block gameplay, lose keyboard selection, or make mouse-only states. Preserve modal stack and `GameplayInputGate`; test selection restoration and cancellation paths.
- **Texture/font pipeline:** TMP font licensing, glyph coverage, atlasing, crisp card values, and contrast under texture overlays require early approval. Halftone must not create noise behind small copy.
- **Motion/performance:** Avoid per-frame layout rebuilds and oversized animated canvases. Reuse canvases and pool/rebuild only when state changes; test Canvas rebuild and snapshot allocations as the UI grows.
- **Screenshots and text:** Attached screenshots are old-build references. Keep the eventual implementation aligned with current canonical names and approved descriptions, not screenshot strings.

## 9. Recommended first implementation task

Build a small **shared UI token and combat handfield vertical slice** before converting the full game. Use the supplied 21 enemy/Headliner images as the visual authority: inspect representative compositions in full, validate import/crop behavior, and prototype paper/slate frames, hard ink shadow, a small halftone accent, a card interaction-state treatment, enemy status/intent hierarchy, and one reusable detail panel. Use existing character art directly; generate no replacement portraits. Wire only to existing combat/card events and targeting APIs. Test the current supported hand sizes at 1920×1080, 16:10, and ultrawide before expanding to Black Market and inventory.

This slice targets the highest-frequency, most information-dense screen and will validate both the art direction and responsive layout assumptions without changing mechanics. Record the resulting type/spacing/color tokens and interaction-state rules as the reusable contract for subsequent phases.

---

**Audit boundary:** This plan is based on the project state inspected for this task and the currently identified workbook/report. It is a design proposal, not an implementation commitment or authorization to change gameplay, canonical content, assets, or save data.
