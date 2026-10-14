# Dealbreak UI implementation report

Date: 2026-10-14. Branch: `codex/combat-ui-vertical-slice`.

The full redesign is authored in the existing Game and MainMenu scenes. The implementation uses the three approved UI planning/production documents, supplied art, and implemented runtime content. This report distinguishes rendered verification from exhaustive gameplay coverage. The final regression suite is **not fully green**; six existing gameplay/data failures remain. Windows shutdown was therefore not scheduled.

## Implementation phases

| Phase | Implemented result | Verification |
| --- | --- | --- |
| A — assets and shared system | Supplied print textures, cropped ink accents, angular native frames, theme asset, reusable panel prefab, button roles/states, three licensed TMP font families | Unity compilation, asset/font/reference tests, actual Game View captures |
| B — Combat and HUD | Minimal card faces, compact actions/player HP, portrait enemies, Gear/Trick rail, runtime hand count below cards, timers hidden, details on demand, targeting/selection states | Hover/focus/select, exact targeting, drag/play, Attack/Block/Pass/Take Damage, sorting smoke checks |
| C — Black Market and inventory | Compact Goods/Services offers, contextual details, existing quantity/stake and sell controls, purchase/sale confirmation, build inventory, compact Gear/Trick slots | Gear/Trick purchases, confirmation/cancel, Trick targeting/use, inventory navigation; economy/targeting tests |
| D — Map and encounter overlays | Native route/node styling, hidden-node privacy, Headliner details, combat visibility management, Incidents, rewards, readonly Deck Viewer inspection, Mod picker | Map navigation/details, Incident completion, reward claim/decline, deck scrolling/details, Mod targeting/confirmation |
| E — front end/system screens | Editorial Main Menu with existing Fixer artwork; Continue, Run Setup, Credits, Pause, Settings, save replacement/error prompts, confirmation and result screens | Actual menu captures, Pause/Resume, Settings dropdown/Apply/Back, isolated checkpoint → Main Menu → Continue round trip |
| F — responsiveness/polish | Anchored layouts, bounded scrollable details, brief button/card response, hard shadows, restrained texture density, focus and disabled states | 1920×1080; Combat at 1920×1200, 2560×1080, 1280×720; corrected 16:10 enemy-stat clipping |

## Shared components and assets

`DealbreakUITheme` centralizes fonts, textures, material and palette. `EditorialFrame` renders scalable asymmetric cut corners, ink borders and offset shadows using a Unity UI mesh. `EditorialButton` handles primary/secondary/destructive/quiet states and brief hover/press motion. `DealbreakUIStyle` styles authored and dynamically generated native controls. `EditorialPanel.prefab` is the reusable frame reference. Existing bound gameplay objects were retained.

| Supplied asset | Actual integration |
| --- | --- |
| `UI_PaperHalftone_Mask_01` | Light print treatment on warm-paper enemy/detail/player surfaces |
| `UI_SlatePosterField_Mask_01` | Restrained print strips on menu/modal/section surfaces |
| `UI_DryInk_CornerAccents_Atlas_01` | UV-cropped upper corner marks on native frames |
| `UI_DarkSlate_HUDBacking_Mask_01` | Game/Menu background and dark HUD treatment |
| `UI_CleanGraphicAccents_Atlas_01` | UV-cropped lower corner/divider accents |
| `UI_FrameConcept_Sample_01` | Inspected design reference, retained in theme; not used as a flattened interface |

The two accent atlases have opaque white backgrounds. `DealbreakInkAccent.shader` removes white at render time, including UI stencil/clipping support, while preserving the source PNGs. RawImages crop the original atlases; decorative layers do not receive raycasts. Existing sprite import settings were inspected. Geometry provides scalable corners/borders instead of stretching an unsliced sample image. No character art was regenerated. Nine illustrated enemy identities and twelve Headliners keep their existing portrait crosswalk. The unillustrated Challenger uses the existing fallback; a presentation-only name entry maps its legacy Duelist definition to the workbook name without assigning unrelated art.

Typography uses Barlow Condensed Bold for headings/buttons, Space Grotesk for item names/descriptions and IBM Plex Mono for numeric labels. Source fonts and SIL OFL licenses are included in `Assets/Art/Fonts`. Dynamic TMP assets have a dedicated Liberation Sans symbol fallback for card suits. Existing shared TMP fallback modifications were not committed. Text auto-shrinking is disabled in shared styling; compact labels use explicit sizes and clipping/overflow policies.

## Contextual information and interaction

The existing combat detail panel and ScrollRect were reused by `ContextualTooltipUI`, with `CombatInspectUI` and `ContextualInfoTrigger` adapters. Cards, enemies, Gear, Tricks, Mods, offers, routes and relevant HUD/action labels use runtime data. Card attack/defense values come from existing calculators; inspection does not consume RNG or commit actions. Enemy abilities remain authored data. Selection retains contextual previews and exact target highlighting.

Details support pointer hover and EventSystem focus, a short leave grace period, selection/click pinning, safe outside/Escape dismissal, bounded placement, native scrolling and Page Up/Down for pinned long text. Combat reserves the right-side information area; offer/deck details avoid their main action controls. Readonly deck snapshots have separate inspect controls and remain incapable of play/drag. Destructive commands and purchases retain their existing validation/transaction authority.

Hand cards keep rank/suit/artwork/selection order. Persistent ATK/DEF and redundant name plates are hidden in the normal hand and Mod-target hand presentation. Hover details expose them. The hand count is dynamically `current/capacity`, centered directly beneath the Handfield. Draw count stays on the draw stack without a permanent DRAW PILE heading. Timer labels are disabled in the HUD and removed from the result summary; timing functionality and save fields remain intact. Map/Shop/Incident overlays hide unrelated combat presentation, and card targeting keeps only its relevant hand presentation available.

## Updated files

- Scenes: `Assets/Scenes/Game.unity`, `Assets/Scenes/MainMenu.unity`.
- Existing prefabs: `EnemyView`, `PathNodeButton`, `ShopItemButton`. New: `EditorialPanel`.
- Presentation assets: `Assets/Data/CombatPortraitCatalog.asset`, `Assets/Resources/DealbreakUITheme.asset`, supplied UI PNGs/metas, accent shader/material, font sources/licenses/TMP assets.
- New UI scripts: `DealbreakUITheme`, `EditorialFrame`, `EditorialButton`, `DealbreakUIStyle`, `DealbreakUIRuntime`, `ContextualInfoTrigger`, `ContextualTooltipUI`, `BuildInventoryUI`, `ShopOfferUI`.
- Existing UI scripts: `ActionButtonsUI`, `CombatDetailPanelUI`, `CombatInspectUI`, `CombatPortraitCatalog`, `ConsumableRewardUI`, `DeckViewerUI`, `EnemyDisplayUI`, `EnemyGroupUI`, `EnhancementTargetUI`, `GearIconSlotUI`, `GearRailUI`, `KeyButtonBorderStyle`, `PathNodeButtonUI`, `PathScreenUI`, `RunEventUI`, `RunResultUI`, `RunStatusUI`, `ShopUI`, `View/CardView`.
- Front end/development presentation: `PauseMenuUI`, `RunSaveMenuUI`, `SavePromptUI`, `SettingsMenuUI`, `DevModePanelUI`.
- Authoring: `Assets/Editor/DealbreakUIOverhaulAuthoring.cs` safely updates existing objects and saves authored scenes/prefabs.
- Tests: new `DealbreakUIOverhaulTests`; updated presentation assertions in `DeckViewerUITests`, `Phase7DMapVisibilityTests`, `Phase7EGearHudTests`, `Phase7EShopSelectionTests`, `Phase7FrontendTests`, `RunResultPresentationTests`.

Managers, gameplay data definitions/assets, RNG, stable content IDs and save schemas were not changed. Existing GUIDs and bound objects were preserved. Deck Viewer cleanup and Enemy Group EditMode destruction were corrected as presentation lifecycle fixes.

## Actual Unity screenshots

All files below were captured from connected Locus Game View, not generated mockups. Primary captures are 1920×1080. Runtime fixtures were used to reach encounters/offers/rewards; victory/defeat were presentation fixtures, not claimed completed runs.

| Screen/state | Capture |
| --- | --- |
| Combat minimal | [Combat_Normal.png](VerificationFull/Combat_Normal.png) |
| Card details | [Combat_CardHover.png](VerificationFull/Combat_CardHover.png) |
| Enemy details | [Combat_EnemyHover.png](VerificationFull/Combat_EnemyHover.png) |
| Selection/targets | [Combat_Targeting.png](VerificationFull/Combat_Targeting.png) |
| Map | [Map_Normal.png](VerificationFull/Map_Normal.png) |
| Map node details | [Map_NodeHover.png](VerificationFull/Map_NodeHover.png) |
| Black Market Goods | [BlackMarket_Goods.png](VerificationFull/BlackMarket_Goods.png) |
| Black Market Services details | [BlackMarket_Services.png](VerificationFull/BlackMarket_Services.png) |
| Gear/Trick inventory | [Inventory.png](VerificationFull/Inventory.png) |
| Main Menu/Continue | [MainMenu.png](VerificationFull/MainMenu.png) |
| Pause | [Pause.png](VerificationFull/Pause.png) |
| Settings | [Settings.png](VerificationFull/Settings.png) |
| Run Setup | [RunSetup.png](VerificationFull/RunSetup.png) |
| Save replacement prompt | [SaveConfirmation.png](VerificationFull/SaveConfirmation.png) |
| Purchase confirmation | [PurchaseConfirmation.png](VerificationFull/PurchaseConfirmation.png) |
| Credits/font credits | [Credits.png](VerificationFull/Credits.png) |
| Incident | [Incident.png](VerificationFull/Incident.png) |
| Reward choice | [Reward.png](VerificationFull/Reward.png) |
| Mod targeting | [ModTargeting.png](VerificationFull/ModTargeting.png) |
| Deck Viewer / details | [DeckViewer.png](VerificationFull/DeckViewer.png), [DeckViewer_Details.png](VerificationFull/DeckViewer_Details.png) |
| Victory / Defeat | [Victory.png](VerificationFull/Victory.png), [Defeat.png](VerificationFull/Defeat.png) |
| 16:10 / ultrawide / windowed Combat | [Combat_16x10.png](VerificationFull/Combat_16x10.png), [Combat_Ultrawide.png](VerificationFull/Combat_Ultrawide.png), [Combat_Windowed.png](VerificationFull/Combat_Windowed.png) |

## Verification results and limits

- Unity recompiled successfully after final code changes. Scene tests found no missing scripts, incomplete shared detail bindings or missing theme/font/texture references. Unity confirmed scene saving succeeded with zero dirty open scenes in Edit Mode. Assets and authored prefabs were saved through Unity APIs.
- Focused runs passed: 50 presentation/fix checks; 22 layout/deck/scene checks; 63 card/Trick/targeting checks; 12 final presentation checks; 30 final reference/front-end/portrait checks. These overlap and must not be summed as unique tests.
- Focused PlayMode run: **5 passed, 0 failed** (`Phase7B5CombatPlayModeTests`, `SaveLoadSceneTransitionTests`). JSON evidence is in `VerificationFull`.
- Final broader relevant EditMode run: **333 total; 326 passed; 6 failed; 1 skipped**. Full failure records: [EditMode_FinalRegression.json](VerificationFull/EditMode_FinalRegression.json). The earlier broader run exposed six presentation lifecycle/assertion failures that were corrected and rechecked; it also exposed the three Rune failures below.
- Remaining unchanged failures: three `DeckMutationTests` assert Rune charge/catalog values (HEAD asset description says three uses, serialized `uses` is one); `LowerEffortShopTests.Guidance_CostsFiveCreditsRevealsOneReachableHiddenEventOrRiskAndIsOneTime`; two `Phase7EGearCapacityTests` assert older duplicate-Gear rejection/precedence. These tests call unchanged managers/data directly. The failing test files and their gameplay authority have no task diff. Gameplay/data changes to resolve them are outside this redesign.
- Exact-target smoke: playing selected 8♣ changed enemy HP from `3,3,3` to `3,0,3`; other targets were unchanged. Drag/play consumed the committed hand card once. Block/Take Damage followed existing preview values (30 HP after blocking, 28 after accepting the remaining two damage). Pass entered defense; rank/suit sorts retained card identities.
- Existing purchase validation/confirmation, Trick sale cancel, Gear/Trick inventory, Blast Patch targeting/applying Volatile, map navigation, Incident completion and reward claim/decline were exercised. Settings dropdown/Apply/Back and Pause/Resume were checked. Final isolated Continue round trip restored 80 Credits and one Trick through the actual Main Menu button.
- Responsive visual checking was strongest for Combat. Other screens were captured at the primary resolution; every rare Incident subtype, every Gear/Trick, all twelve Headliner encounters, arbitrary localization/long names and every resolution/screen combination were not exhaustively exercised. No claim of an entire run playthrough or pixel-perfect coverage is made. An illustrated Challenger portrait remains unavailable; the correct fallback is retained. Additional bespoke map-node icons or transition animation are optional future polish.

## Save safety incident and final disposition

One earlier smoke session entered Play after an arming snippet failed compilation and wrote to the player save location. This was reported during the task. The session was stopped with an isolated store override, and the latest automatic backup was restored. Both the unintended smoke save and recovered save were preserved locally under `Library/UIOverhaulSafetyBackup` (excluded from Git). The restored active save was validated through `SaveGameService.TryRead`; its SHA-256 matches the preserved recovered copy:

`BCBBB10C74A42206C420C5453BABA7D56C4B5188B5BA5ADD4E03F5EAADFEB97B`

All later smoke sessions explicitly checked a `Library/CombatUISliceChecks/smoke-save.json` store before interacting. Final checks confirmed the restored player save still has the same hash and is readable. This confirms the recovered backup, not an independently recorded exact task-start snapshot. Editor smoke flags/store overrides were cleared after returning to Edit Mode.

Unrelated pre-existing modifications in Plastic metadata, shared Liberation Sans fallback, Locus workspace metadata, ProjectSettings, Icon, the workbook and Directory.Build.props were retained outside the commit. Because the regression run has unresolved failures and the save safety incident prevents an unconditional success claim, **no Windows shutdown command was issued**.
