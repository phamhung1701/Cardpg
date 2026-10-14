# Dealbreak Combat UI vertical slice

Implemented from `DEALBREAK_UI_REDESIGN_PLAN.md`, `DEALBREAK_UI_ASSET_PRODUCTION.md`, and `DEALBREAK_UI_ASSET_PROMPTS.md`. Scope is the existing Game scene's Combat presentation. No combat-manager, card-zone, random-stream, content-name, or save-schema migration is included.

## Presentation and interaction

- A compact District/Headliner/Credits strip, portrait-led enemy row, quieter turn band, readable hand row, stable Attack/Block/Pass bar, Gear/Tricks dock, draw-pile count, and player HP rail.
- Warm paper, slate, and ink surfaces, offset shadows, an angular header notch, and a restrained supplied paper-halftone accent outside live text.
- Standard hand cards retain their original suit/rank art, Mod markers, lift, selection numbers, and drag behavior. Their persistent ATK/DEF plate is hidden only in Combat. Deck snapshots retain their existing stat presentation; missing card art retains the identity/stat fallback.
- The existing Gear hover frame is reused and extracted as `CombatDetailPanel.prefab`: ink title band, clean body, and bounded scroll viewport in the reserved right column. Card, enemy, Gear, and draw-pile hover/focus use this frame. Card/Gear selection pins detail; transient inspection takes precedence. A short pointer grace period permits entering the detail viewport to scroll. Menus/input gates hide inspection.
- Focus and submit use an adapter to the original card, enemy-target, and Gear click handlers. No second gameplay execution path is introduced. Selected cards retain their numbered lift; legal targets get an outline and explicit label. Attack and Block stay in fixed positions.
- Selection shows calculated attack power or the existing authoritative pending-attack block preview. Recover and Take Damage appear in the appropriate combat context. Secondary pile/timing data moves into draw-pile inspection; the recent log is hidden in Combat while its existing producer remains intact.
- A presentation-only portrait catalog maps the nine supplied enemy posters to their existing definitions and the twelve Headliner posters to exact rank/suit identities. Runtime labels, source cards, rewards, IDs, GUIDs, and character pixels are preserved. The unmapped Duelist retains the existing fallback.

## Updated files and assets

- `Assets/Scenes/Game.unity`: existing bound UI objects restyled/repositioned, detail presenter wired, original action/input references retained.
- `Assets/Prefabs/Card.prefab`, `EnemyView.prefab`: inspect/focus adapters and enemy layout/art hook.
- `Assets/Prefabs/CombatDetailPanel.prefab`: reusable frame extracted from the existing Gear tooltip.
- `Assets/Data/CombatPortraitCatalog.asset`: explicit portrait crosswalk; no gameplay content edits.
- `Assets/Scripts/UI/CombatDetailPanelUI.cs`, `CombatInspectUI.cs`, `CombatPortraitCatalog.cs`: shared inspection, compact information hierarchy, and portrait resolution.
- `Assets/Scripts/View/CardView.cs`: Combat-only stat suppression and visual focus.
- `Assets/Scripts/UI/EnemyDisplayUI.cs`: compact intent/status, on-demand full traits, portrait hookup, and selection/focus target feedback.
- `Assets/Scripts/UI/GearRailUI.cs`, `GearIconSlotUI.cs`: shared Combat inspection while retaining selection/merge behavior and the legacy non-combat path.
- `Assets/Scripts/UI/DrawDeckStackUI.cs`: preserve the original card-back aspect ratio.
- `Assets/Editor/CombatUISliceAuthoring.cs`: repeatable Unity authoring pass and opt-in smoke-save isolation. The menu is for `Game.unity` in Edit Mode and saves its changes.
- `Assets/Tests/EditMode/CombatUISliceTests.cs`: inspect purity, modal gating, hand/snapshot distinction, and all twelve Headliner art identities.
- Required supplied art and its existing metadata: `Assets/Art/Enemies/`, `Assets/Art/Headliner/`, and `Assets/Art/UI/UI_PaperHalftone_Mask_01.png`. Other optional UI atlases/masks and `Icon.png` are not included.

## Verification

- Full Unity compilation/domain reload completed, including runtime, Editor authoring, and test scripts. Final Console error count: zero. Live scene scan: zero missing scripts; all required detail references present.
- Final focused EditMode run: **49 passed, 1 skipped, 0 failed** (50 total). Fixtures: CombatUISlice, GearRailUI, DrawDeckStackUI, HandPresentation, PassPlayerTurnUI, ContextualConsumableAndEnemyTargeting, and GameSceneReadability. The existing scene-fit check skips its blank authored selection-summary preview. Earlier adjacent checks also exercised HandFan and Phase7B5CombatFoundation; 74 existing tests passed. The four new tests initially failed due to an EditMode lifecycle-dispatch mistake in the test harness; after correcting it, all four passed, including in the final run. No full-suite run was performed.
- Scripted Play Mode smoke: Game scene loaded, eight-card hand rendered without ATK/DEF plates, card hover/selection/focus displayed live details, Attack consumed one card and dealt 8 damage, Block prevented a pending hit with no HP loss, Pass entered retaliation, and drag input plus EventSystem raycasting committed one card once. Multi-enemy targeting damaged only the chosen enemy. Gear hover and enemy focus used the shared frame; Gear capacity/slots remained live. Draw-pile inspection/count and menu/input gating are covered by the focused checks.
- Visual checks at **1920×1080**, **1920×1200 (16:10)**, and **2560×1080 (ultrawide)**. Corrected enemy-name and draw-pile-caption clipping found during the aspect checks; final labels fit. The right detail column does not overlap enemy targets, hand, actions, or player HP. Screenshots: [1920×1080](Verification/Combat_1920x1080.png), [16:10](Verification/Combat_1920x1200.png), [ultrawide](Verification/Combat_2560x1080.png).
- Smoke interaction used the real UI handlers and Unity EventSystem raycasts rather than human mouse/controller input. Test runs used the Locus Unity Test API; smoke gameplay commands used `Library/CombatUISliceChecks/smoke-save.json`. The initial Play entry resumed the existing run before the isolation hook was explicitly armed; no combat action was taken in that run. Subsequent entries verified save isolation before gameplay commands. Smoke override/session flags were cleared and the Editor returned to Edit Mode at 1920×1080.

## Limitations and later phases

- Attack power is explicitly a pre-resolution forecast. Commit-time action bonuses, target reactions, and critical/chance effects can change final HP loss. The UI does not simulate those by mutating state or consuming RNG. Exact final attack forecasting is deferred; existing block prediction is retained.
- Font-family licensing/selection, item-specific Gear/Trick art, additional decorative accents, narrow-window/high-capacity hand refinement, and human controller/long-description usability review remain polish work. Current TMP fonts and card/character art are reused.
- No Black Market, Map, Main Menu, inventory, or global UI conversion is implemented. Shared Game HUD surfaces and the reused Gear tooltip necessarily inherit their new styling outside Combat; non-combat gameplay and menu logic remain unchanged.
- The supplied character posters are full compositions, fitted into portrait viewports, with no regenerated art. The exact Headliner face-card identity is retained in the name/intent and inspection reward text rather than replacing the character poster.
- The optional texture/frame concept assets are not functional UI and are not used to flatten live controls.
