# Dealbreak central stage and run-flow correction

Implemented against the existing Game/MainMenu scenes, approved UI textures, portrait catalog and runtime content. The supplied combat image is a composition reference, not a flattened interface asset.

## Changes

- `SharedContentStageUI` assigns the same unframed content bounds to Combat, Black Market, Map and Incidents. The existing background, title, District/Headliner context, Credits and enlarged Gear/Trick rail form the shared shell. Combat portraits occupy the upper stage; Shop/Map/Incident can use the lower part when the hand/actions are hidden. Each retains its own functional layout and existing controls.
- Removed the selection-order number and its badge backing. Lift, outline, drag and target states remain. ATK/DEF stay in contextual details, and hand capacity stays beneath the hand.
- Enlarged Gear slots from 44 to 62 design pixels, increased Trick slot footprints and Draw Pile presentation, and strengthened heading/action/numeric typography. Removed the Combat Build control; inventory remains accessible through Pause and the Black Market.
- Defense Pass invokes the existing `TakeRemainingDamage` path, with the same Shield, reaction and sequencing behavior. The duplicate Take Damage control is hidden. Player-turn Pass retains its existing action. Pending damage is compact and readable alongside player HP.
- Existing Black Market select → inline BUY → finite-stock removal flow is preserved. Prices and BUY totals are compact numeric Credits amounts, using the shared Credits HUD rather than a duplicate balance. Services retain their existing constraints, embedded quantity/stake controls and authority.
- Map and Incident backgrounds no longer replace the shell. The Map retains route shapes, reachability and hidden-node privacy; narrative and choice effects remain visible in the Incident layout.
- Removed in-run seed text/tooltips, the long seed explanation from Run Setup, redundant Pause key instructions and the orphaned header notch. Seed entry remains a pre-run configuration control; the active run seed is displayed in results.
- Pause offers Resume, Settings, Inventory, Back to Main Menu and Abandon Run. Back saves a safe checkpoint; during combat it warns that Continue returns to the existing checkpoint. It refuses a failed safe-checkpoint write. Abandon retains destructive confirmation, ends saving and opens the actual Defeat results screen. Ordinary same-seed retry controls are hidden; the legitimate Infinite Mode continuation remains available when eligible. Legacy fields/callback identities are retained.
- Combat contextual details use the right-hand inspect rail above Draw Pile, keeping the enemy stage unobstructed. Long descriptions retain scrolling.

## Updated implementation

- New: `Assets/Scripts/UI/SharedContentStageUI.cs` and `Assets/Editor/DealbreakCentralStageAuthoring.cs`, including Unity metadata.
- Updated authoring hook: `DealbreakUIArtCorrectionAuthoring.cs`.
- Scene/prefab: `Assets/Scenes/Game.unity`, `Assets/Scenes/MainMenu.unity`, `Assets/Prefabs/EnemyView.prefab`.
- Presentation/input adapters: `CardView.cs`, `ActionButtonsUI.cs`, `DealbreakUIRuntime.cs`, `DealbreakUIStyle.cs`, `GearRailUI.cs`, `ShopOfferUI.cs`, `ContextualTooltipUI.cs`, `RunSeedUI.cs`, `RunResultUI.cs`, `PauseMenuUI.cs`.
- Regression coverage: `CombatUISliceTests.cs` and `RunResultPresentationTests.cs`.

No manager gameplay/economy calculations, save schemas, content IDs, authored portraits or random streams were changed. Existing Barlow Condensed, Space Grotesk, IBM Plex Mono and theme assets are reused. Existing slate/paper texture integration is retained on the shell and portrait/offer surfaces.

## Verification

Final evidence and test snapshots are under `VerificationCentralStage/`. Captures are actual Locus Game View PNGs, not mockups. Controlled fixtures expose Shop and Incident nodes without a full playthrough; Combat uses three runtime Scrappers backed by the matching authored portrait. Fixture route access does not alter authored content or production saves.

Final compilation completed through Locus. The relevant 14 EditMode groups passed **133/133**, including new defense-Pass, selection-badge and abandoned-result checks. The existing PlayMode suite passed **7/7**. JSON summaries are `EditMode_Final.json` and `PlayMode_Final.json`.

Live checks performed through existing runtime UI handlers:

- Selected card retained its lifted/outlined state with empty order text and an inactive badge. Selected details rendered outside the enemy stage.
- Player-turn Pass entered defense. Defense Pass resolved 6 pending damage exactly once (30 → 24 HP), returned to Player Turn and kept the same eight hand cards.
- Attack entered targeting; clicking the middle Scrapper defeated that exact runtime enemy, leaving the other two at 3 HP. A short drag returned cleanly with eight hand cards, no stuck drag/input gate; rank/suit sorting remained functional.
- Offer selection left 100 Credits unchanged. BUY charged 20 once (100 → 80), granted one Gear copy and removed the offer. Repeating the stale purchase request granted/charged nothing further.
- Stake controls stayed inside the selected service. Stake 2 charged 80 → 78 and removed the one-time service. The final quantity/BUY layout has a 10 design-pixel gap between the plus control and BUY.
- Dead Drop narrative, choice labels and +8 Credits/+6 HP effects were visible directly. Taking Credits changed 78 → 86 and returned to Map.
- Pause Inventory opened without a stuck Pause gate. Settings opened and Back restored Pause. Back to Main Menu checkpointed successfully; actual Continue restored 86 Credits and one Gear item.
- Cancelling Abandon kept the isolated run save. Confirming Abandon removed that isolated save and displayed the actual Defeat result with its seed and current summary; no ordinary retry control remained.

`FinalAudit.json` records **0 missing scripts, 0 broken serialized object references, 0 dirty scenes**, and compilation idle after saving. The final live Console check contained **0 errors/warnings**. Production `active-run.json` and its backup both retained their original SHA-256 hashes. Verification used `Library/UIArtCorrection/smoke-save.json`; its override and both smoke flags were cleared before finishing. Unity was left in Edit Mode with the saved Game scene.

### Actual Game View evidence

| Context | Screenshot |
| --- | --- |
| Combat, 1920×1080 | [Combat](VerificationCentralStage/Combat_1920.png) |
| Selected card and contextual details | [Selection](VerificationCentralStage/Combat_Selected.png) |
| Combat, 1280×720 | [Windowed Combat](VerificationCentralStage/Combat_1280.png) |
| Defense, 1280×720 | [Defense](VerificationCentralStage/Combat_Defense_1280.png) |
| Goods/Services in shared stage | [Black Market](VerificationCentralStage/Shop_1920.png) |
| Selected inline purchase | [Selected offer](VerificationCentralStage/Shop_Selected.png) |
| Successful purchase and removed offer | [Purchase](VerificationCentralStage/Shop_PurchasedRemoved.png) |
| Embedded service controls | [Stake](VerificationCentralStage/Shop_ServiceSelected.png) |
| Map in shared shell | [Map](VerificationCentralStage/Map_1920.png) |
| Direct Incident narrative/choices | [Incident](VerificationCentralStage/Incident_1920.png) |
| Pause navigation | [Pause](VerificationCentralStage/Pause_1920.png) |
| Settings | [Settings](VerificationCentralStage/Settings_1920.png) |
| Main Menu and verified Continue | [Main Menu](VerificationCentralStage/MainMenu.png) |
| Actual confirmed Abandon result | [Defeat](VerificationCentralStage/Defeat_Abandoned.png) |

An automatic approval-review usage limit interrupted the final capture/audit sequence. After the user requested continuation, the normal Locus tools succeeded; the final captures and saved-scene audit were completed. No approval check was bypassed.

## Compromises

- Dense Shop descriptions and long Incident choices use existing scroll views within the central stage. Not every offer is simultaneously visible.
- The compact inspect rail prioritizes unobstructed targets; long detail text requires scrolling.
- Some implemented enemies lack matching approved portraits and keep their existing fallback. No unrelated illustration was assigned to those identities.
- The supplied reference includes richer environmental collage. This pass uses the existing slate/paper treatment and native dynamic controls; it does not bake the reference screenshot into the game.
- This is focused UI verification, not a full 12-District run or a resolution sweep of every unchanged screen. Previously documented unrelated broader gameplay/data test failures were not changed as part of this presentation task.
