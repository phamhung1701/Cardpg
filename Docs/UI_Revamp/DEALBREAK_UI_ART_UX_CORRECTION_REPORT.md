# Dealbreak UI art-direction and UX correction

Date: 2026-10-14. This pass builds on the existing Game/MainMenu scenes and the approved UI redesign, production and prompt documents. It changes presentation and transaction routing in the UI, not gameplay rules, content identities, authored prices, random streams or save schemas.

## Implemented compositions

| Screen | Correction |
| --- | --- |
| Black Market | Full-screen printed slate environment, independent Goods/Services scroll columns, warm paper offers with name, full runtime description, price and availability visible. Selection reveals an in-card BUY control. The second click commits through the existing RunManager transaction. Ordinary purchases have no separate confirmation dialog or bottom BUY. Purchased finite stock and completed one-time services disappear immediately. Embedded quantity/stake controls retain existing validation. Selling retains the existing explicit sale workflow. BUILD opens inventory; refresh and exit retain their callbacks. |
| Combat | Open HUD/hand/action composition, more separation between enemies, paper portrait mounts with an internal ink border and clipped art window. Existing images are cropped/scaled without replacement. Names and HP/attack have separate bands. Valid targets receive an ochre outer rim. ATK/DEF remain in contextual details; hand count remains centered beneath the cards; timer displays remain absent. |
| Map | Full-screen route network without enclosing frame. Smaller node glyphs and increased lane spacing. Combat diamonds, Black Market squares, Incident circles and Headliner hexagons use existing progression states. Unrevealed labels produce the same neutral circle regardless of the secret node kind, preserving hidden information. The Map toggle hides behind the Deck Viewer and blocking menus. |
| Incidents | Editorial title and narrative on the left; paper choice cards on the right. Descriptions, known costs, conditions and outcomes are visible without hover. Existing hidden information and effects are retained. |
| Main Menu | Open title/navigation on the left; existing Fixer artwork integrated as a full-height hero on the right. Transparent menu controls emphasize typography and focus/hover colour. Continue uses the existing save controls. |
| Inventory | Open two-column Gear/Trick build view, capacities above, paper item rows with full descriptions. Rows route to existing contextual actions. |
| Deck Viewer | Open textured background, larger adaptive grid up to ten columns, transparent viewport, readable title and compact close control. Inspection wrappers retain runtime details without decorative frames or playable card input. Scrolling remains available for larger decks. |
| Rewards | Open background, readable heading and paper choices with descriptions visible; existing claim, decline and replacement logic retained. |
| Pause | Narrow left-side slate navigation rail with lighter dimming; existing menu stacking/resume retained. |
| Settings / Run Setup / Credits | Open forms/content rather than enclosing modal cards. Shared native inputs/dropdowns/toggles retained. VSync label and checkbox separated. |
| Victory / Defeat | Full-screen summary on the left and navigation on the right, rather than a framed popup over combat. Scrollable long summaries retained. |
| Genuine dialogs | Confirmation infrastructure remains for consequential actions such as abandoning/restarting/overwriting, and existing destructive item actions. |

## Shared presentation and assets

`EditorialSurface` distinguishes Open, Paper, Slate, Portrait, Node and Dialog treatments. Existing theme, TMP fonts, frame mesh and button behavior are reused. Ordinary controls now use straight compact silhouettes; paper/portrait surfaces retain hard shadows. Tiny repeated corner decorations are suppressed on the open compositions. Hover/focus/pressed/disabled styling remains; dense card/enemy/Gear details continue to use the existing contextual tooltip system. Essential offers, build descriptions, rewards and Incident decisions no longer require inspection popups.

Actual supplied assets remain referenced through `DealbreakUITheme`: dark slate HUD texture for environmental fields, paper halftone for light paper grain, existing slate poster field for the shared background, and original graphic/ink accent materials where the retained dialog/detail treatment uses them. The frame concept remains a reference, not a flattened interactive screen. No character, card, texture or font raster was regenerated. Existing Barlow Condensed, Space Grotesk, IBM Plex Mono and suit fallback assets/licenses are retained.

Removed the seed-determinism slogan, offer-selection instruction, pause reassurance and map encounter-selection boilerplate. Enabled ordinary combat buttons no longer open redundant explanatory tooltips. Disabled action reasons, targeting/cancellation guidance for non-obvious modes, requirements, warnings and gameplay descriptions remain.

## Updated implementation files

- Scenes: `Assets/Scenes/Game.unity`, `Assets/Scenes/MainMenu.unity`.
- Prefabs: `EnemyView`, `PathNodeButton`, `ShopItemButton`.
- New reusable components: `EditorialSurface.cs`, `MapNodePresentation.cs` (with Unity metadata).
- Authoring: new `DealbreakUIArtCorrectionAuthoring.cs`; existing `DealbreakUIOverhaulAuthoring.cs` reapplies the correction after its original authoring pass.
- Shared presentation: `DealbreakUIStyle`, `EditorialButton`, `DealbreakUIRuntime`.
- Screens and interactions: `ShopUI`, `ShopOfferUI`, `RunEventUI`, `PathScreenUI`, `EnemyDisplayUI`, `GearIconSlotUI`, `ActionButtonsUI`, `BuildInventoryUI`, `DeckViewerUI`, `ConsumableRewardUI`, `PauseMenuUI`, `SettingsMenuUI`, `RunSaveMenuUI`.
- Focused regression tests: `Assets/Tests/EditMode/Phase7EShopSelectionTests.cs` adds inline purchase once/stock removal, switching/cancellation without payment, and embedded stake/one-time service checks.

Unity APIs save the bound scenes and prefab while preserving existing component references and GUIDs. Managers, ScriptableObject definitions, character artwork and save contracts are not changed.

## Runtime verification and save isolation

Verified in the connected Unity 6000.3.24f1 Editor through Locus. Screenshot fixtures use the real scene controls and authoritative runtime definitions. Some encounters were reached by selecting an accessible runtime test node; rewards and result screens were explicitly invoked as presentation fixtures, not as claims of completing a full run.

- Shop: first click selects without payment; in-card BUY charges once and removes finite offer. Reusing the stale selected ID neither charges nor grants twice. Switching/clearing selection does not buy.
- Stake service: embedded stepper changes stake 1→2 and the displayed BUY cost; purchase charges 2 once, creates the existing pending investment and removes the completed service.
- Inventory: Gear/Trick descriptions render; selecting a Trick routes to existing Use context, opens Mod targeting and cancellation retains the item. Gear sale pays the existing half-price amount and removes the owned item.
- Combat: exact target click consumed the selected card once and affected only the chosen enemy. Live hand drag began, moved and returned with eight cards, restored raycasts and no input gate. Rank/suit sorting still dispatches to the existing handfield.
- Incident: Dead Drop shows the narrative and known +8 Credits/+6 HP decisions directly; the chosen effect completes and returns to Map.
- Navigation: Shop/Map/Incident/build transitions, Main Menu, Pause/Resume, Settings Back/Apply, reward claim and Continue exercised with real buttons. A reward fixture granted one Trick and closed.
- Save/Continue: safe Shop checkpoint succeeded; actual Continue restored 83 Credits, one Trick and zero Gear after the purchase/service/sale smoke flow. A final fresh session separately restored 75 Credits, one Gear item and one Trick after ordinary purchases.

All new runtime save operations were redirected to `Library/UIArtCorrection/smoke-save.json`. The normal `Application.persistentDataPath/active-run.json` and its backup were absent at this pass's initial native check; this absence is the comparison baseline. The separate Editor isolation hook is enabled only by a verification SessionState flag. It is cleared when verification ends. No production save is intentionally created, deleted or overwritten.

## Visual evidence

Actual Locus Game View captures, not mockups, are under [VerificationCorrection](VerificationCorrection/):

- [Combat normal](VerificationCorrection/Combat.png), [selection/targeting with card details](VerificationCorrection/Combat_Targeting.png).
- [Black Market Goods/Services](VerificationCorrection/Shop_GoodsServices.png), [selected offer](VerificationCorrection/Shop_Selected.png), [successful stock removal](VerificationCorrection/Shop_PurchasedRemoved.png), [selected service](VerificationCorrection/Shop_ServiceSelected.png), [service removed](VerificationCorrection/Shop_ServiceRemoved.png).
- [Map](VerificationCorrection/Map.png), [Incident](VerificationCorrection/Incident.png), [Inventory](VerificationCorrection/Inventory.png).
- [Main Menu](VerificationCorrection/MainMenu.png), [Pause](VerificationCorrection/Pause.png), [Settings](VerificationCorrection/Settings.png), [Run Setup](VerificationCorrection/RunSetup.png).
- [Deck Viewer](VerificationCorrection/DeckViewer.png), [Reward fixture](VerificationCorrection/Reward.png), [Defeat fixture](VerificationCorrection/Defeat.png), [Victory fixture](VerificationCorrection/Victory.png).
- Core Combat, Shop and Map have 1280×720 and 2560×1080 captures in the same folder; primary captures are 1920×1080. Layout, card/count placement, scrolling, readable offers, unobstructed actions and portrait separation were reviewed at these sizes.

Screenshot review led to fixes for portrait target rims, unknown-node shape privacy, selected-offer sizing, stale service reasons, embedded numeric alignment, inventory container lookup, Deck grid/viewport, VSync spacing and the result panel name. Original raster assets remain unchanged.

## Tests and limitations

| Final validation | Result | Evidence |
| --- | --- | --- |
| Affected EditMode groups (Shop, Incident layout, Map privacy, frontend, UI, targeting, economy, persistence, result and deck presentation) | 130 passed, 0 failed | `VerificationCorrection/EditMode_FinalVerified.json`, run `13f2a3307d0f4aafa8ac56051451a30f` |
| PlayMode suite | 7 passed, 0 failed | `VerificationCorrection/PlayMode_Final.json`, run `8a8c7f6935a44104bdf78d61a2d6da31` |
| Final compiler/Console | Compilation converged; 0 error/warning entries at final runtime check | Locus recompile and Console results |
| Game/MainMenu reference and save audit | 0 missing scripts, 0 broken serialized object references, 0 dirty open scenes; Editor stopped and assets saved | `VerificationCorrection/FinalAudit.json` |
| Production save comparison | Active file and backup remain absent, matching this pass's initial native check | `VerificationCorrection/FinalAudit.json` |
| Verification teardown | Both smoke flags false; save override cleared | `VerificationCorrection/FinalAudit.json` |

Interim JSON results are retained as troubleshooting evidence, not final passing claims. One interim Shop fixture incorrectly assumed a seeded Stake service; it was corrected to use the fixture's existing service replacement helper. A later compact Trick-label change failed on null test icons and caused cascading fixture failures; it was repaired with a null guard, then all 130 affected tests passed on a fresh compiled assembly.

This is focused verification, not an exhaustive 12-District playthrough or coverage of every authored enemy, Gear, service and Incident combination. Victory/Defeat captures are presentation fixtures. Physical fullscreen switching cannot be proved by an Editor Game View resolution change. Existing long GameData descriptions can require scrolling. Existing generic confirmation/detail framing is deliberately retained for actual dialogs and dense contextual information.

The preceding overhaul reported six broader gameplay/data test failures (Rune charge expectations, a Guidance fixture and legacy duplicate-Gear assertions). This correction does not change those rules/data or claim to fix that baseline. Unrelated user changes, workbook, icon, project settings, generated fallback font, Plastic metadata and Locus workspace data are excluded from the commit.
