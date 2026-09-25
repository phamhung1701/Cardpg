# CardPG Development Handoff

Current implementation baseline when this handoff was created: branch `main`, commit `3c1e643` (`feat: complete phase 3 boss progression`). Unity project: Unity 6000.3.24f1, URP, New Input System.

## 1. Game Concept

CardPG is a card-based roguelike/RPG inspired primarily by Regicide's card-combat foundation and Balatro's build progression, rule modification, and run variety.

Baseline combat is single-card play. The player uses cards to attack and may discard cards to reduce incoming damage while also managing persistent player HP. Poker hands and other multi-card combinations are **not** baseline combat. They may later exist as optional, unlockable build archetypes provided by Artifacts, encounters, or other rule-changing effects.

The current prototype has one playable character. Avoid building a large character framework until additional characters are actually required.

## 2. Core Design Rules

These decisions are established and must not be casually reinterpreted:

- A normal run contains **12 major maps**, each ending with one face-card boss.
- Maps 1–4 use the four Jacks, maps 5–8 the four Queens, and maps 9–12 the four Kings.
- Suit order is randomized independently within each rank group each run.
- The player starts without the 12 face cards.
- The starting collection is exactly Ace through Ten of all four suits: **40 cards**.
- Defeating a boss awards that exact rank-and-suit face card for the remainder of the run.
- The twelfth and final King reward is granted before the run completes.
- Current hand capacity is eight cards.
- Each combat encounter attempts to add exactly one card, subject to hand capacity. There is no automatic per-turn draw.
- Player HP persists between encounters and resets on run reset.
- Defense is optional. Defensive discards reduce pending incoming damage.
- The player may explicitly accept the remaining incoming damage through **Take Damage**.
- **Recover** is available with an empty hand during `PlayerTurn`. Recover applies one unblocked current enemy attack and, if the player survives, draws one card without causing a second retaliation.
- Suit powers are Artifact-gated rather than baseline card behavior.
- Poker/multi-card combination gameplay is not baseline combat and may later become an unlockable build archetype.
- One playable character is sufficient for the current prototype.

## 3. Current Architecture

### Architectural contract

Gameplay state must not be derived from Unity UI hierarchy, child counts, or Transform placement.

Card ownership and card zones are authoritative runtime model state. UI is presentation/input only: it may observe state and issue commands, but it must not become the source of truth.

### Runtime responsibilities

- **`CardInstance`** — One owned runtime card with a stable per-run unique ID, immutable `CardData` definition, at most one `CardEnhancementData`, and run-local Enhancement effect state for future counters/scaling. Shared definition assets are not mutated.
- **`CardCollection`** — Plain runtime model that owns the authoritative `OwnedCards`, `Deck`, `Hand`, and `DiscardPile` zones. It centralizes zone mutations and prevents one instance from occupying multiple zones.
- **`CardManager`** — Run-level card collection/build façade. It owns authoritative card zones, gold, Artifact definitions plus run-owned `ArtifactRuntimeInstance` state, the five-slot Persistent capacity/NonSlot exemption, and one Enhancement per stable CardInstance. Reset clears all runtime effect state while preserving definition assets and scene references.
- **`RelicData` / `CardEnhancementData`** — Immutable-in-run content definitions with legacy/runtime ID, explicit canonical spreadsheet ID, rarity/tier/parent metadata, typed effect definitions and a separate rule-modifier extension point. Existing legacy hook fields are a compatibility adapter only for definitions with no typed effects.
- **`GameplayEffectResolver` / `GameplayEffectSource` / typed contexts** — Shared Artifact/Enhancement foundation for side-effect-free numeric modifiers and deterministic reactive effects. It centralizes attack/block preview and execution, uses composable typed conditions, and routes card-commit/player-turn-start reactions through bounded deterministic queues. Rule-changing mechanics remain separate and unimplemented.
- **`ShopOffer`** — Runtime typed Artifact/Enhancement offer with stable slot-based Enhancement identity independent of the eventual chosen card, cached price, and presentation data.
- **`CardView` / `Field`** — Presentation and input only. CardView displays a `CardInstance`, supports persistent selection and drag feedback, and issues commands through managers. Field lays out/reorders views. Drag/reorder does not change ownership or card zones.
- **`PlayerRuntime`** — Plain mutable player-health state: configured maximum health, current health, reset/full heal, clamped damage, and defeat status.
- **`CombatManager`** — Authoritative encounter state machine. It uses the shared effect resolver for attack/block calculation and reactive content, preserves deterministic action/hit/reaction ordering, exposes one player-turn-start boundary for current turn-entry paths, and retains Ace-pair, defense, Shield/multi-hit, death/reward and one-shot result authority.
- **`EncounterResult`** — Independent `Victory` / `Defeat` terminal result used to decouple combat resolution from UI and run progression.
- **`RunManager`** — Owns seeded run startup/restart, the grouped/shuffled 12-boss sequence, exact boss source-card identity, current generated map, active node, node completion/unlocks, deterministic mixed shop offers, deterministic Upgrade-node Enhancement choices, event resolution, exact face-card rewards, boss index, run completion, and the separate run timing/statistics lifecycle. Selection commits a route; successful resolution completes it.
- **`RunTimingStatistics` / `RunMapSplit`** — Plain runtime telemetry state for scaled active-run elapsed time, completed map splits, the current incomplete map, reset/restart, defeat, and abandonment. It has no RNG or UI authority and supports arbitrary map indices.
- **`RunRandomContext` / `DeterministicRandom`** — Stable run-seed hashing and independently derived deterministic streams. Map, boss, card, and shop random consumption do not share mutable random state.
- **`RunMapGenerator`** — Generates a deterministic branching/merging map for any non-negative map index. Current maps contain combat, elite, shop, event, upgrade, risk, and boss nodes with partial hidden information.
- **`RunContentCatalog` / `RunEventDefinition` / `RunEffectResolver`** — Authorable Phase 4 encounter traits and non-combat choices. Prototype effects are limited to HP/max HP, gold, and card draw operations.
- **`EnemyAbility`** — Authorable encounter-start, post-card, healing, attack-growth, and damage-modification hooks used by elites and suit-specific bosses.
- **`GameController`** — Explicitly creates/configures the runtime managers before dependent UI subscriptions, injects Game-scene references and enemy assets, configures provisional player max HP, and consumes the one-shot frontend seed request before starting the established run initialization path.
- **`GameFlowService` / `FrontendLaunchContext`** — Persistent scene-flow boundary and one-shot launch handoff. They route Main Menu/Game transitions and requested destinations without owning run state or creating a second randomization path.
- **`GameplayMenuCoordinator` / `GameplayInputGate`** — Own the stack of blocking gameplay overlays, time suspension, Canvas/input gating, drag cancellation, and selection restoration. Gameplay managers remain authoritative and defensively reject user commands while blocked.
- **`DisplaySettingsService`** — Validates, persists, and applies the functional prototype display surface: resolution, window mode, and VSync.
- **`Singleton<T>`** — Performs lookup and lifetime enforcement but does not silently create unconfigured managers. Manager creation/configuration is explicit through GameController.

### Current UI responsibilities

- **`ActionButtonsUI`** — Player HP, current combat state, pending damage, state-specific action guidance, selected attack/defense feedback, combat log, and Play / defensive discard / Take Damage / Recover command availability.
- **`EnemyDisplayUI`** — Enemy identity, HP bar and value, attack, encounter guidance, and state coloring.
- **`RunStatusUI`** — Always-visible gold, draw/discard/hand counts, owned-Artifact names/icons and occupied slot count/capacity in a bounded scrollable combat strip, Enhancement count, map progress, boss identity, and compact live run/map timers. The strip reads `CardManager.ownedArtifacts` and its authoritative capacity properties on existing build/run refresh events; it owns no inventory.
- **`DeckViewerUI`** — Opens/closes a modal draw/discard pile summary plus compact owned-Artifact and enhanced-card details; it closes on run and primary route/modal transitions.
- **`PathScreenUI`** — Builds the current route selection overlay and displays cycle/run-completion state.
- **`ShopUI` / `EnhancementTargetUI`** — Shop selects/inspects cached offers before Buy. Enhancement Buy opens a shared owned-card picker; selecting a target spends nothing, and confirmation authoritatively validates and applies/charges once. The same picker supports free Upgrade rewards with Back/cancel.
- **`RunEventUI`** — Displays authored event/risk choices and deterministic free Enhancement choices used by Upgrade nodes; Upgrade Enhancement clicks open the shared target picker without changing Event resolution.
- **`RunSeedUI`** — The former persistent gameplay seed readout is disabled in the Game scene; the authoritative run seed remains on `RunManager` and appears in terminal results.
- **`RunResultUI`** — Displays exact boss-card reward feedback plus reusable terminal victory/defeat results with seed, progression, bosses defeated, gold, Artifact/build details, total run time, completed map splits, an incomplete defeat split, Retry Same Seed, New Run, and Main Menu actions.
- **`MainMenuUI` / `FrontendMenuNavigator` / `RunSetupUI` / `CreditsUI`** — Present Main Menu, optional seed entry (blank generates a fresh seed on launch), and editable prototype Credits without owning gameplay state.
- **`PauseMenuUI` / `SettingsMenuUI` / `ConfirmationDialogUI`** — Reusable pause, functional display settings, and destructive-action confirmation presentation. Settings is shared by Main Menu and Pause.

## 4. Completed Development Phases

### Phase 0 — Baseline and Design Decisions

Established the twelve-boss progression, exact run-local face-card rewards, Artifact-gated suit powers, optional card defense, eight-card hand capacity, one attempted draw per encounter, persistent HP, and empty-hand emergency recovery. A local source-control recovery baseline was created before gameplay refactoring.

### Phase 1 — Card State / Initialization Foundation

- Introduced `CardInstance` and `CardCollection` runtime models.
- Added stable unique per-run card IDs and authoritative owned/deck/hand/discard state.
- Card views are generated from model state; Transform children are not gameplay state.
- Added persistent click selection and presentation-only drag/reordering.
- Consolidated run startup into exactly one initialization path.
- Made manager creation/configuration explicit and corrected Game-scene references.
- Reset removes stale cards, views, selection, gold, relic ownership, and generated definitions.
- Verified card conservation, identity, restart behavior, direct Game entry, and Main Menu → Game entry.

### Phase 2 — Combat Foundation

- Added `PlayerRuntime` and persistent player HP.
- Implemented optional defense, pending/residual damage, explicit Take Damage, and full-block behavior.
- Implemented empty-hand Recover with one unblocked attack and one draw on survival, without a second retaliation.
- Encounter start now attempts to add one card rather than refilling the hand.
- Added one-shot `EncounterResult` events and duplicate terminal-result/reward protection.
- Integrated player health and combat actions into the prototype UI.
- Verified partial defense, full defense, residual damage, recovery, lethal defeat, one-card encounter draw, victory reward, and no duplicate results.

### Phase 3 — Twelve-Boss Run Progression

- Starting collection is now 40 Ace–Ten cards with zero face cards.
- Rewarded face cards receive unique runtime IDs and exact rank/suit identity.
- Retained four randomized Jacks → four randomized Queens → four randomized Kings.
- Generated bosses preserve an exact source card rather than relying on display-name parsing.
- Node selection no longer completes nodes or unlocks successors prematurely.
- Victory completes the active node; defeat grants no progression.
- Boss rewards and boss-index advancement occur immediately on victory and no longer depend on shop completion.
- The final King is awarded before run completion.
- Restart restores the intended initial state.
- A full accelerated twelve-boss Play Mode run was verified through final victory.

### Phase 3.5 — Prototype UI Rebuild

- Replaced the legacy Game Canvas with a responsive 1920×1080 reference layout using anchored Unity UI/TMP panels.
- Added a persistent run/resource header, focused enemy panel, central combat feedback, separated player/action area, bottom-anchored hand, and modal path/deck/shop/result overlays.
- Added `RunStatusUI` and `RunResultUI` so resource/progression display and terminal/reward presentation remain separate from combat input.
- Rebuilt `Card.prefab` without its per-card world-space Canvas and removed debug Hover/Drag labels while preserving persistent selection and drag/reordering.
- Rebuilt path/shop button prefabs with readable dimensions, naming, and interaction colors.
- Added exact boss-card reward feedback and final run victory/defeat restart presentation without moving gameplay authority into UI.
- Verified the existing 30 EditMode tests, direct Game entry, Main Menu → Game, card selection/reordering, Play, defense, Take Damage, Recover, status/resource updates, boss rewards, run completion/defeat, restart, and a 16:9 Game view.

### Phase 4 — Map / Encounter Expansion + Fully Seeded Run

- Added one player-visible string seed controlling the complete prototype run.
- Added stable seed normalization/hashing and independently derived streams for boss order, deck operations, map layout/content, and shop offers.
- Removed gameplay use of `UnityEngine.Random`, unseeded `System.Random`, and runtime-dependent string hashing.
- Replaced isolated linear lanes with deterministic five-column, three-row branching maps containing forks, merges, hidden nodes, and a final boss.
- Added proper combat, elite, shop, event, upgrade, risk/reward, and boss node flows.
- Converted the post-combat shop into an explicit route node with deterministic offer ordering.
- Added six authored non-combat prototype events with immediate HP/max-HP, gold, and draw effects; lethal health costs are rejected.
- Activated the enemy ability pipeline with an elite Momentum trait and four suit-specific boss traits.
- Added run-seed display/input, event choice presentation, route connections, node-type styling, and enemy trait feedback to the Phase 3.5 UI.
- Restart now replays the same seed while restoring baseline maximum HP, 40 owned Ace–Ten cards, zero face cards, zero gold, and boss index zero. A separate New Seed action creates a different run.
- Verified 39 EditMode tests, deterministic replay/stream independence, arbitrary map indices, shop determinism, all Phase 4 node flows, elite/boss traits, Main Menu → Game, and an accelerated seeded twelve-boss victory.

### Phase 5 — Build Progression

- Replaced hard-coded suit-relic ID checks with authorable Artifact filters and effect hooks while preserving the four established suit powers.
- Added four new prototype Artifacts for enhanced-card attack/defense synergies, post-victory healing, and bonus gold.
- Added per-instance card Enhancements. One owned card may receive one persistent run-local Enhancement without changing its unique ID, definition, or matching cards.
- Added Sharpened, Reinforced, Mending, and Quickdraw prototype Enhancements for attack, defense, healing, and draw effects.
- Converted Upgrade nodes into deterministic free Enhancement choices tied to eligible owned card instances.
- Expanded shops to cached deterministic mixed Artifact/Enhancement offers with stable offer identities, exact prices, ownership/eligibility checks, and duplicate-purchase rejection.
- Kept build random generation on independently derived context streams so card/deck random consumption does not alter shop or Upgrade offers.
- Added card and run-header presentation for Enhancement/build state and updated the shop presentation for mixed offers.
- Preserved restart, card conservation, single-card combat, seeded replay, and all Phase 1–4 run/map/encounter contracts.
- Poker/multi-card combat remains intentionally deferred as an optional future archetype.
- Verified 45 EditMode tests plus Play Mode shop purchase, Upgrade choice, enhanced-card/Artifact synergy, same-seed restart cleanup, UI presentation, and clean Console behavior.

### Phase 6 — Regression / Balance / Polish

Completed and verified on **September 17, 2026**.

- Added explicit ATK/DEF values to card presentation, including Enhancement bonuses.
- Added state-specific combat guidance for attacking, defense, empty-hand Recover, route selection, victory, and defeat.
- Added visible reasons for disabled shop and event choices, including insufficient gold, owned/ineligible offers, and lethal health costs.
- Expanded Deck Details with compact owned-Artifact and enhanced-card summaries.
- Added explicit terminal **Restart Same Seed** and **New Random Seed** actions; all run-start paths clear stale result/reward UI.
- Closed Deck Details automatically across route, encounter, event, shop, Upgrade, result, and restart transitions.
- Made numeric Artifact attack stacking acquisition-order independent using a canonical multiplier-then-flat-bonus rule while keeping Spade/Heart/Diamond side effects separate.
- Fixed cached Artifact offers to charge the displayed cached price even if the backing definition changes later.
- Rejected negative spending/prices and improved ordered event-cost validation and failure explanations.
- Added empty-shop and no-eligible-Enhancement Upgrade handling coverage.
- Performed a conservative balance review. The 30 HP baseline, current enemy/boss curve, route cadence, Artifact prices, Enhancement prices, and event values remain the accepted prototype baseline; Golden Compass economy remains a future monitoring point rather than receiving speculative churn.
- Added 7 focused Phase 6 regression tests, bringing the EditMode suite to 52 passing tests.
- Play Mode verified card readability, disabled-choice feedback, build inspection, terminal seed actions, modal cleanup, clean restart state, and Main Menu → Game initialization.

### Phase 7 — Frontend & Menu Flow

Completed and verified on **September 17, 2026**.

- Rebuilt `Assets/Scenes/MainMenu.unity` into a reusable frontend with New Run, Settings, Credits, and Quit. No non-functional Continue, difficulty, or audio controls were added.
- Added a dedicated Run Setup screen with random seed generation, custom seed entry, seed preview, and Back navigation. All launches feed the existing normalized fully seeded run path.
- Added a persistent scene-flow service and one-shot launch context so Main Menu/Game transitions do not duplicate or own gameplay state.
- Added a centralized gameplay modal coordinator with nested Pause → Settings/Confirmation behavior, cached time-scale restoration, Canvas/raycast gating, drag cancellation, selection discipline, and manager-level input guards.
- Added Resume, reusable Settings, confirmed Restart Same Seed, and confirmed Abandon Run actions. Scene transitions defensively restore global runtime state.
- Added functional persisted display settings for available resolution, Windowed/Borderless/Exclusive Fullscreen mode, and VSync, including unavailable-value normalization. Audio remains intentionally absent until an audio system exists.
- Added an editable prototype Credits screen.
- Replaced the terminal overlay with a reusable Victory/Defeat results screen showing seed, map progress, bosses defeated, gold, Artifacts, enhanced-card count, and final reward when available, with Retry Same Seed, New Run, and Main Menu actions.
- Added 6 focused Phase 7 frontend tests, bringing the EditMode suite to 58 passing tests.
- Play Mode verified application Main Menu startup, random/custom seed starts, deterministic same-seed restart, pause/resume, nested settings, confirmation cancel/confirm behavior, blocked gameplay input, abandon-to-menu cleanup, victory/defeat results, all results actions, functional settings presentation, saved clean scenes, and a clean Unity Console.

## 5. Post-Phase-6 Player Playtest Revision Roadmap

The current repository, including the completed Phase 7 frontend/menu flow, approved Phase 7A interaction foundation, and completed frontend seed follow-up, is the implementation baseline. Phase 7B and Phase 7C are implemented; Phase 7D is accepted with the existing five-depth map topology and Shop placement (explicit zero-depth change). **Phase 7E Slices 1–4 are complete.** Preserve the current authored Artifacts, Enhancements, Events, Elites, Bosses, shops, progression, and deterministic-run contracts unless a later revision explicitly changes them.

### Phase 7A — Card Interaction & Selection Foundation — Complete and Approved

Implemented and verified on **September 17, 2026**.

- Hand dragging now uses a temporary visual slot so pickup does not collapse the hand; meaningful horizontal crossings reorder only presentation order, while cancelled/invalid drags restore cleanly without changing authoritative card zones.
- Card selection is ordered presentation/input state with numbered defensive selection, deterministic toggling, drag-set reuse, and cleanup on actions, encounters, modals, resets, and restarts. PlayerTurn permits exactly zero or one selected card: selecting another replaces the prior selection, while clicking the selected card deselects it.
- EnemyAttacking permits ordered multi-card defense selection under the current per-attacker rule: each selected card must fully block one distinct pending attack. Selection rejects insufficient or unmatched additions; deselection remains available. Artifact and Enhancement defense effects resolve through `CombatManager.CalculateCardDefense`.
- The enemy presentation is the context-sensitive card drop target: PlayerTurn drops attack that target, while EnemyAttacking drops defend against that target's pending attack. The target boundary accepts an `EnemyRuntime` and remains compatible with later independently targetable enemies without implementing Phase 7B.
- The separate Play and Discard-to-Defend controls were replaced by one contextual primary action button. The temporary dedicated defense drop zone was removed.
- Defensive actions accept an atomically validated selected-card set through either the enemy drop target or contextual button, consume each accepted card exactly once, and preserve all cards on invalid attempts. Each card blocks at most one distinct attack; excess defense does not carry to another attacker.
- Added 7 focused Phase 7A regression tests, bringing the EditMode suite to 65 passing tests.
- The original Phase 7A Play Mode approval covered the then-current additive defense rule. The September 24, 2026 per-attacker defense revision was code/test verified; its later Game-scene UI-event smoke is recorded in the final milestone pass below.
- Phase 7A received manual player approval and unlocked Phase 7B.

### Phase 7B — Multi-Enemy & Enemy Identity — Complete

Implemented and verified on **September 17, 2026**.

- Combat encounters now own an ordered collection of independently targetable `EnemyRuntime` instances while retaining a selected target for contextual-button input and compatibility with single-enemy content.
- Enemy presentations use three reusable bound slots. Card drops attack or defend against the exact bound runtime enemy, clicking an enemy selects the contextual-button target, defeated/fled enemies are removed, and remaining presentations close ranks.
- Surviving enemies snapshot separate positive attacks for the defense window. Each defense card may fully block one distinct attack; unresolved attacks remain individually tracked for defense, then resolve as one aggregate combat-damage hit when the player takes the remainder. Recovery damage still uses the total surviving enemy attack.
- Normal Goblin encounters create three distinct Goblins. Each has 3 HP, 2 attack, and a secured 3-gold reward; each survivor contributes its own 2 damage.
- Defeating any enemy draws exactly one card. Defeated-enemy gold is secured during the encounter and paid only after encounter victory, so player defeat still grants no encounter reward.
- Thief now has 12 HP, 1 attack, and a 15-gold defeated reward. If still alive after the second completed player turn, it flees, clears the encounter, grants no Thief gold, and does not trigger the kill draw.
- Elite and boss construction remains single-enemy and did not inherit normal Goblin group size or Thief flee behavior. Knight and unrelated enemy/boss balance was unchanged.
- Added 5 focused Phase 7B regression tests, bringing the EditMode suite to 70 passing tests.
- Final Play Mode integration verified Main Menu → Game initialization, three distinct Goblin targets, drag and clicked-button targeting, kill draws, survivor attack totals, 9-gold Goblin victory, Thief timing/no-reward flee, single-enemy presentation, restart cleanup, and a clean Console.

### Playtest Telemetry — Run Timer and Map Splits — Complete

Implemented and focused-verified on **September 17, 2026**.

- Added UI-independent runtime timing statistics for total run duration, completed map splits, and the current incomplete map, without participating in deterministic RNG.
- Timing starts fresh for every new run, same-seed restart, and random-seed run; defeat and abandonment stop the timer without leaking state into the next attempt.
- Scaled gameplay delta time counts route, combat, shop, event, and upgrade participation while explicit Pause contributes no elapsed time.
- Boss completion finalizes the current map split and begins a fresh timer for the next map. Map indices are not capped at twelve.
- Added a compact gameplay header readout for Run Time and Map Time plus terminal Victory/Defeat timing summaries with hour-safe formatting and incomplete defeat labeling.
- Added focused timing, split, restart, arbitrary-map-index, duration-format, and results-summary coverage. Nine directly relevant EditMode tests passed, and focused Play Mode verification confirmed live advance, Pause freeze, resume, and runtime label updates with a clean Console.

### Phase 7C — Ace Pairing — Implemented

Implemented and code-verified on **September 23, 2026**. An Ace pairs with exactly one other card (including another Ace); both are discarded atomically as one action against one enemy. Damage sums the two cards' independently modified attacks, then doubles on a critical. Each card's matching Artifact and Enhancement on-play effects executes once in selection order. The default critical chance is configurable at 25% on CombatManager; successful pair commitments roll an independent `combat-critical` seeded stream, while single cards and invalid pairs do not consume it. Player-turn selection permits a legal Ace pair without enabling general multi-card poker combat. Earlier focused EditMode regressions passed 46/46 and the then-current complete suite passed 111/111. The Game-scene UI-event Ace-pair smoke was subsequently performed in the final milestone pass below. Prototype critical balance remains provisional.

### Pre-Phase-7D Playtest Corrections — Implemented

Implemented and code-verified on **September 24, 2026**.

- Combat and Elite route nodes show only `Combat` or `Elite`; authored enemy identity remains hidden until the encounter begins. Node descriptions and encounter selection are unchanged.
- Enemy retaliation preserves one attack requirement per positive surviving enemy. One card can fully block at most one attack when its defense meets that attack; excess is lost, deterministic auto-matching uses strongest cards against the strongest attacks they can block, partial commits are allowed, and invalid/unmatched batches consume nothing.
- Remaining unblocked enemy attacks still resolve together as one `EnemyAggregate` damage hit, preserving the existing one-charge Shield behavior.
- Focused map/defense and related 7A/7B/7B.5 regressions passed **44/44**; the then-current complete EditMode suite passed **121/121** and discovered PlayMode suite passed **1/1**. Later UI-event Game-scene checks of generic node wording and per-attacker defense are recorded in the final milestone pass below.

### Phase 7D — Map Revision — Accepted with Current Topology

- Distant Elite nodes remain revealed and retain identifying color even before they are accessible. Hidden Event/Risk nodes remain hidden. Focused map tests passed 8/8 and the complete EditMode suite passed 113/113 on September 23, 2026.
- **Approved September 24, 2026: explicit zero-depth change.** Keep the current five route depths; do not add the proposed 2–3. Combat presentation/animations are still planned and expected to increase actual encounter/run duration, so expanding map topology preemptively is not warranted.
- Keep existing map topology and Shop placement unchanged. No Investment/Betting or unrelated economy changes. Phase 7D is accepted on that basis; no additional shop-access/affordability guarantee is asserted. The twelve-map pacing study is intentionally deferred, not a blocker for this prototype milestone. Final interactive checks have their own evidence and limits below.

### Phase 7E — Shop / Build UX — Complete (Slices 1–4)

Implemented and verified on **September 24, 2026**. Clicking an offer now selects it without purchasing, shows its cached price/description/availability in a compact Shop detail area and gives the selected offer a persistent tint/label. A separate **Buy Selected** button confirms through `RunManager.PurchaseShopOffer`, which rechecks the active uncompleted Shop node, cached offer/price, Artifact identity/ownership, Enhancement target eligibility, Gold and existing input/purchase guards. Failed attempts spend/grant nothing and show the existing unavailable reason. Shop refresh, close, node completion and run reset/restart clear selection; a purchased or otherwise unavailable offer cannot retain a stale selection. Existing offer generation, target selection, economy and map topology are unchanged.

- New `Phase7EShopSelectionTests`: 7/7 passed for inspect/switch with no spend/grant, successful cached-price Buy, insufficient Gold, duplicate Artifact, ineligible Enhancement, removed cached offer, invalid cached price and close/restart cleanup. Focused Shop tests: 12/12; full EditMode: 128/128.
- Game-scene Play Mode smoke: inspected and switched offers at 0 Gold, confirmed no Gold/item change until Buy, observed insufficient-Gold reason, bought an Artifact for its displayed 15g (50g → 35g), observed duplicate rejection with no extra charge, checked selected-state/detail/button presentation and zero Console warnings/errors. This was an accelerated Shop-node smoke, not a full twelve-map playtest.
- Files changed for this slice: `Assets/Scripts/UI/ShopUI.cs`, `Assets/Scripts/Managers/RunManager.cs` (purchase validation guard only), `Assets/Scenes/Game.unity` (Shop-only Buy/detail layout and references), `Assets/Tests/EditMode/Phase7EShopSelectionTests.cs` and generated `.meta`; roadmap/handoff documents updated. No later 7E slice was started.

**Slice 2 implemented and verified September 24, 2026.** Shop Enhancement offers and free Upgrade choices retain deterministic Enhancement identity without embedding a final target CardInstance. Shop Buy opens a shared scrollable owned-card picker; selecting/switching/back never spends Gold. Confirm atomically revalidates Shop node, cached offer/price, affordability and chosen owned unenhanced card, then charges exactly once and applies to that exact card. Upgrade choices use the same explicit target selection and confirmation while remaining free; the node completes only on a successful application. Ineligible cards remain visible but distinguished with reasons; a failed confirmation leaves the offer/node and Gold intact for another choice. Picker state clears on close and run reset/restart. No target-choice RNG consumption, Enhancement-balance, Shop placement or other gameplay changes.

- Files changed for Slice 2: `Assets/Scripts/Data/ShopOffer.cs`, `Assets/Scripts/Managers/RunManager.cs`, `Assets/Scripts/UI/ShopUI.cs`, `Assets/Scripts/UI/RunEventUI.cs`, new `Assets/Scripts/UI/EnhancementTargetUI.cs` (+ `.meta`), the target-picker portion of `Assets/Scenes/Game.unity`, updated `Assets/Tests/EditMode/Phase5BuildProgressionTests.cs` and `Assets/Tests/EditMode/Phase7EShopSelectionTests.cs`, new `Assets/Tests/EditMode/Phase7EEnhancementTargetTests.cs` (+ `.meta`), and handoff/roadmap documents. Pre-existing dirty-worktree changes remain user-owned.
- Eleven new Slice 2 EditMode tests passed 11/11; focused 7E/Phase 5 tests passed **24/24**, full EditMode passed **139/139**. Tests cover explicit target and same-rank/suit instance independence, stale/removed/already-enhanced targets, retry without double charge, cached price, single payment, free Upgrade completion, cancel/reset, and target-independent deterministic offer/map signatures.
- Game-scene Play Mode Shop and Upgrade smokes: inspected/select-switched, cancelled before purchase, rejected an invalidated target without Gold loss, then confirmed a different card and charged the cached 15g once (40g → 25g). Upgrade cancelled without completing; rejected an ineligible card and applied to another owned instance for zero Gold. Picker displayed 40 owned cards with eligibility/selection feedback; Console warnings/errors **0**. These were accelerated scene smokes, not a full twelve-map playtest.

**Slice 3 implemented and focused-verified September 24, 2026.** The normal combat HUD now shows a compact, bounded, scrollable list of all owned Artifact icons/names directly from `CardManager.ownedArtifacts`, beneath the run header and above enemies. It refreshes through `RunStatusUI`'s existing `OnBuildChanged` subscription and the existing run/reset notifications; the zero-owned state says “None owned.” No second inventory, arbitrary display cap, Artifact rule/capacity, or Deck Details changes. Authored descriptions remain in Artifact data; no separate tooltip/inspection framework was added.

- Slice 3 files changed relative to this session: `Assets/Scripts/UI/RunStatusUI.cs`, the owned-Artifact strip and serialized label binding in `Assets/Scenes/Game.unity`, new `Assets/Tests/EditMode/Phase7EArtifactHudTests.cs` (+ generated `.meta`), and the three handoff/roadmap documents. All other pre-existing dirty changes were preserved.
- Focused EditMode **5/5 passed**: empty display, acquisition event updates, all eight Artifact names/icons, `CardManager.Reset`, and seeded run start/restart. The full EditMode suite was **not run**, as requested for this isolated UI slice; the previous Slice 2 full-suite checkpoint remains 139/139.
- Game-scene Play Mode smoke entered a Combat node and viewed 0, 1, then all 8 authored Artifacts. All eight were identifiable in the bounded strip at the captured Game-view size, with hand, enemy and action controls unobscured. Restart showed 0 again, still with 40 starting cards. Console warnings/errors: **0**.

**Slice 4 implemented and Phase 7E accepted September 24, 2026.** `CardManager` defines the single base capacity of **5 occupied persistent Artifact slots**, counted from `ownedArtifacts`. All eight existing authored Artifacts default to Persistent. `RelicData` has a category flag for future **NonSlot** temporary/system/quest Artifacts (including a possible Contract); none is currently authored, and no temporary-Artifact system was built. Duplicates are rejected first. At five occupied slots, a sixth persistent acquisition is rejected before spending or modifying ownership; nothing is replaced, sold or removed, and there are no capacity upgrades. `RunManager` surfaces **Artifact Capacity Full** on cached Shop offers without hiding them. The existing select → inspect → Buy flow and cached prices remain intact; Enhancements remain purchasable at full capacity. The existing combat strip and header show occupied slots as `0/5` through `5/5`; the list still shows all owned Artifacts, including future NonSlot items.

- Files changed for Slice 4 relative to this session: `Assets/Scripts/Data/RelicData.cs`, `Assets/Scripts/Managers/CardManager.cs`, `Assets/Scripts/Managers/RunManager.cs`, `Assets/Scripts/UI/RunStatusUI.cs`, updated `Assets/Tests/EditMode/Phase7EArtifactHudTests.cs` and `Assets/Tests/EditMode/Phase7EShopSelectionTests.cs`, new `Assets/Tests/EditMode/Phase7EArtifactCapacityTests.cs` (+ generated `.meta`), and the three handoff/roadmap documents. No scene or authored Artifact asset needed modification; existing dirty scene and other user changes were preserved.
- Focused Phase 7E/Phase 5 EditMode gate passed **35/35**; full EditMode regression passed **150/150**, zero failures/skips. Tests cover fifth/sixth boundaries, Gold/inventory safety, duplicates, NonSlot exemption, cached Shop offer and unavailable reason, selectable over-cap offer, Enhancement at full capacity, HUD count, and reset/restart/new run. All eight authored Artifact assets were checked in Editor as Persistent.
- Accelerated Game-scene Play Mode gate: bought four Artifacts through the authoritative model and the fifth from the Shop for its cached 15g (200g → 185g); the sixth Shop offer remained inspectable and showed **Artifact Capacity Full** but Buy spent no Gold and granted nothing. Shop Enhancement targeting still applied for its cached 12g (185g → 173g) at 5/5. Combat HUD visibly listed five owned Artifacts and `5/5` without obscuring the enemy/hand/actions. Same-seed restart restored zero Artifacts, `0/5`, zero Gold and 40 cards. Console warnings/errors: **0**. No full twelve-map gameplay pacing claim is made.

**Phase 7E acceptance:** all four slices passed their specified focused and Game-scene checks; the final full EditMode gate passed 150/150. No further 7E implementation is pending.

### Remaining future work (outside Phase 7E)

- Future Artifact capacity upgrades, replacement/sale and temporary/Contract content are not implemented; the five-slot base rule is complete.
- Investment/Betting is undecided and excluded.

### Approved Frontend Seed Follow-up — Complete

Implemented and verified on **September 24, 2026**. Run Setup now opens with blank optional seed entry; blank (including whitespace) generates a new seed only on Start, not the fixed `CARDPG` fallback. Explicit custom seeds retain the established trim-only normalization, and the existing Randomize button still supplies a generated seed when requested. The existing one-shot `GameFlowService` → `FrontendLaunchContext` → `RunManager.StartRunWithSeed` handoff remains unchanged. The Game scene's persistent seed label and updater are disabled without removing run seed data. Victory and Defeat results still read the actual `RunManager.RunSeed`; Retry Same Seed uses `RestartRun`, while New Run returns to blank setup.

- Files changed for this follow-up: `Assets/Scripts/Frontend/RunSetupUI.cs`, `Assets/Scenes/Game.unity` (seed label/updater flags only relative to this follow-up), `Assets/Tests/EditMode/Phase7FrontendTests.cs`, `CARDPG_DEVELOPMENT.md`, and both roadmap handoff documents. All pre-existing dirty-worktree changes remained user-owned; no gameplay/RNG architecture was changed.
- Focused frontend EditMode **8/8**; relevant frontend/seeded-run/timing/Phase 5/critical regression **39/39**, zero failures/skips. Tests cover blank/fresh seeds, custom normalization and deterministic replay, one-shot handoff, generated-seed Victory and Defeat text, same-seed retry, and a subsequent blank new run not reusing the previous seed. A separate full EditMode suite was not run for this follow-up; the prior Phase 7E full-suite checkpoint remains 150/150.
- Main Menu → blank setup → Game Play Mode smoke produced `D3B53FC8` with the gameplay label hidden; accelerated Combat defeat displayed that seed in Results, Retry retained it, then New Run returned to blank setup and produced `D47D3507`. Explicit `  Share-7E  ` started as `Share-7E`, appeared on Defeat Results and survived Retry. Results were reached through an accelerated encounter defeat, not a complete twelve-map victory playthrough; Victory result seed was checked in EditMode. The Console showed zero warnings/errors at the Play Mode smoke check, but a later final check found one unrelated Unity AI Toolkit account-service warning (`Account API did not become accessible within 30 seconds`); therefore a warning-free final Console cannot be claimed. Earlier combat-input and twelve-map pacing playtests remain Pending and outside this follow-up.

### Final Prototype Regression and Handoff — September 24, 2026

**Scope:** approved Phase 7D, Phase 7E Slices 1–4, and frontend seed follow-up are implemented; this pass changed **documentation only** (`CARDPG_DEVELOPMENT.md`, `Locus/knowledge/plan/cardpg-remaining-roadmap-implementation.md`, `Locus/knowledge/plan/cardpg-roadmap-resume.md`). The initial dirty tree, scenes, assets, gameplay code, balance and RNG architecture were not intentionally changed; no files were staged or committed. The remaining roadmap implementation plan is complete **for the currently approved scope**, not for future content or polish.

**Automated:** Unity Test Framework discovered **152 EditMode** tests; full run **152/152 passed**, 0 failed/skipped/inconclusive. It discovered exactly **one PlayMode** test, `Phase7B5CombatPlayModeTests.ShieldedMultiHit_ResolvesAsIndependentRuntimeHits`, **1/1 passed**; no existing PlayMode tests directly cover Ace-pair input, Shop/Enhancement targeting, Artifact capacity or frontend handoff. The suite includes card collection/zones/identity and 40-card setup, twelve-boss J/Q/K progression and exact rewards, deterministic run/map/shop/crit streams, single/paired card and defense rules, multi-enemy behavior, Shield/hit lifecycle, build/Shop/Enhancement capacity and target guards, timing/reset, and blank/custom/one-shot frontend seed behavior. This is test/code evidence, not a fresh twelve-map playthrough.

**Game-scene UI-event smoke (PASS unless qualified):** from Main Menu blank setup, seed `133F8702` launched Game with 40 owned cards and 12 bosses; an available Goblin map button said only `Combat`, and entered three individually bound 3-HP/2-ATK Goblins. Pointer-dispatched enemy/card clicks and action buttons targeted Goblin 3, produced three independent pending attacks; two selected defense cards blocked two distinct attacks, leaving one 2-damage pending attack. Take Damage resolved one aggregate hit, consumed one staged player Shield, left HP 30/30, and returned to PlayerTurn. This preserves independent **block requirements**, not three separate incoming HP hits. In a staged single-target scene encounter, a three-hit card action against two enemy Shield charges produced hit indices 0/1/2, two blocked hits and one 9-HP hit, discarding only one card. Another staged three-hit action against a shielded Goblin killed only its selected target after hit 1, stopped hit 2, retained two other Goblins and emitted no premature terminal result; a staged single-enemy lethal repeat emitted exactly one terminal event/reward and rejected a second action. **Limit:** multi-hit count used `CombatManager.TryPlayCards(..., hitCount: 3)` in live Play Mode; no authored card/player-facing control currently issues three hits, so an authored multi-hit card input smoke is **PENDING**, not a player-UI PASS. The per-hit runtime behavior and scene state passed.

**Ace-pair UI-event smoke (PASS for the exercised path):** ordinary non-Ace selection replaced the previous single card; clicking Ace + a Four selected two, with an attempted third click rejected and a `PLAY ACE PAIR` action label. Button commit consumed each card ID once, logged Mending +2 HP then Quickdraw +1 draw once each in order, and used the first seeded `combat-critical` roll (13.993 < 25%) to double combined damage to 10 against a staged 120-HP enemy. A defense-card click/button then blocked its 2-ATK retaliation; a subsequent ordinary non-Ace single attack dealt 10 without losing the ability to continue. Separate EditMode tests cover Ace+Ace, invalid pair no RNG consumption, and deterministic critical replay. UI events were dispatched through Unity EventSystem and Button callbacks in Play Mode, not a human mouse/drag session; unperformed physical drag targeting remains **PENDING**.

**Compact flow smoke (PASS for the exercised path):** map combat was followed by an accelerated encounter victory to reach an existing Shop node (not a natural economy playthrough). Shop select/inspect spent 0g; Buy paid the cached 20g price for Heart Power from staged 200g → 180g. Four additional existing Artifacts were granted for staging; HUD showed 5/5 and five names. The second inspectable Shop Artifact reported `Artifact Capacity Full` and Buy neither charged Gold nor granted a sixth. At 5/5, an Enhancement offer opened a 40-card target picker; selecting card #1 spent nothing and Confirm applied Sharpened exactly to #1 for its cached 12g (180g → 168g). An accelerated Defeat result showed `Seed  133F8702`; Retry Same Seed restored that seed, 40 cards, 0 Artifacts/Enhancements and 0 Gold. Main Menu → Run Setup then launched custom `  Milestone-Custom  ` as `Milestone-Custom`, with gameplay seed label hidden. Full Victory result seed and frontend one-shot/replay cases passed EditMode; a natural final-boss victory was **not** replayed here.

**Console:** this pass's warnings/errors query returned **0** during and after the Game-scene smoke. The earlier frontend follow-up logged one Unity AI Toolkit account-service warning (`Account API did not become accessible within 30 seconds`); its stack originated in `com.unity.ai.assistant` cloud/account availability, not CardPG gameplay. No CardPG warning/error was observed in this pass. Do not extrapolate to a permanently warning-free Editor or modify gameplay to suppress unrelated service noise.

**Remaining debt / next-development options (not implemented or approved by this pass):** Recover currently treats incoming recovery damage as one shieldable combat hit; its policy is explicitly temporary pending a design decision. No authored multi-hit card exists for a player-driven input check; physical mouse/drag interaction and a complete meaningful twelve-map pacing study remain unperformed. The pacing study is deliberately deferred because 7D kept five route depths and future combat animation/presentation may change run time; it does **not** block this milestone. Potential separately scoped next steps include combat presentation/animations, enemy artwork, playtest-led pacing/Shop decisions, and a deliberate Recover–Shield ruling. New content, Artifact upgrades/replacement, extra depths, balance and unrelated systems remain future decisions, not this milestone.


### Content Expansion Architecture — Shared Effects + Canonical Tier I Core — Complete

Implemented and verified on **September 25, 2026**.

- Added one shared typed foundation for Artifact and Enhancement numeric modifiers/reactive effects, with deterministic source/effect order, composable suit/enhanced-card conditions, authoritative CardCommitted and PlayerTurnStart contexts, and a separate typed Rule Modifier extension point. No name/ID behavior dispatch or generic scripting language was added.
- Added run-owned `ArtifactRuntimeInstance` state and CardInstance-owned Enhancement state. Shared ScriptableObject definitions remain immutable during play; reset detaches/clears runtime state. Future counters have a home without implementing their mechanics.
- Centralized attack/block semantics for preview and execution. Current order is base + card-local flat, then multipliers, then Artifact flats. This preserves attack stacking and implements `(base block + Hardened 3) ×2` for Spades. Club applies ×2 per qualifying card before Ace-pair summation.
- Added explicit canonical metadata while retaining GUID-safe runtime IDs. Crosswalk: `rel_001`→`club_power`, `rel_002`→`heart_power`, `rel_003`→`diamond_power`, `rel_004`→`spade_power`; `enh_001`→`sharpened`, `enh_002`→`reinforced` (display **Hardened**), `enh_003`→`mending`, `enh_004`→`quickdraw`.
- Migrated only the approved canonical core: Club ×2 per Club card; Heart heals 1 per committed Heart; Diamond fixed Draw 2 up to capacity; Spade block ×2 with no ATK reduction; Sharpened +3 attack; Hardened +3 block; Mending heals 2 once per in-hand instance at every player-turn start and not on play; Quickdraw draws 1 on play. Conflicting Heart recycle, rank-based Diamond draw, Spade ATK reduction and Mending on-play heal are absent from these definitions.
- Preserved five Persistent slots, NonSlot exemption, cached Shop prices/offers, explicit Enhancement targets, one Enhancement per card, zones/identity, Ace pairing, critical RNG, Shield/multi-hit and restart semantics. No Tier II/III, upgrades, rarity distribution, map effects, extra turns, poker rules or other workbook content was implemented.
- Changed implementation/content files: new `Assets/Scripts/Combat/GameplayEffects.cs` (+ `.meta`), `Assets/Scripts/Combat/CombatReactionQueue.cs`, `Assets/Scripts/Data/RelicData.cs`, `CardEnhancementData.cs`, `CardInstance.cs`, `Assets/Scripts/Managers/CardManager.cs`, `CombatManager.cs`, `Assets/Editor/GameDataGenerator.cs` (safety alignment only; not executed), the four existing core Artifact assets, four existing Enhancement assets, and new `Assets/Tests/EditMode/CoreGameplayEffectsTests.cs` (+ `.meta`). Architecture/handoff documents were updated; no scene reference change was required. During the unexpected Editor restart Unity generated untracked `Assets/_Recovery.meta` and `Assets/_Recovery/`; they were not used, modified or removed by the implementation.
- Verification: focused architecture/core content **8/8**; full EditMode **160/160**; discovered PlayMode Shield/multi-hit **1/1**. Game-scene smoke passed Club+Sharpened, Heart, fixed Diamond Draw 2, Quickdraw, Spade+Hardened order/no old ATK reduction, and Mending turn transitions/no play heal. Final smoke Console warnings/errors: **0**.

The detailed contracts and extension guidance are in `Locus/knowledge/plan/cardpg-effect-architecture.md`. Stop before another content batch.

## 6. Verified Invariants / Regression Requirements

Future changes must preserve these behaviors unless the user explicitly approves a design change:

- Every owned runtime card has a unique ID.
- Card ownership is conserved across owned/deck/hand/discard operations unless a deliberate system adds or removes ownership.
- UI hierarchy and Transform children are never authoritative card state.
- Reset removes stale runtime state and restores the intended starting state.
- A new run starts with 40 Ace–Ten cards and zero face cards.
- A boss reward adds exactly one matching face card.
- Duplicate boss rewards are rejected.
- Boss progression is grouped Jack → Queen → King.
- Each rank group contains all four suits exactly once.
- Node completion occurs after successful encounter resolution, not selection.
- Defeat does not grant node or boss progression.
- Player HP persists between encounters.
- Encounter terminal results, rewards, and gold cannot be emitted repeatedly.
- Final boss progression cannot index beyond the twelve-boss list.
- Restart restores 40 owned cards, zero face cards, boss index zero, baseline maximum/current HP, and incomplete run state.
- The same normalized run seed plus the same player decisions reproduces boss order, map generation/content, deck operations, shop offers, events, rewards, and combat randomness.
- Random consumption in one named subsystem does not change unrelated subsystem results.
- Map generation accepts arbitrary map indices and is not architected around a hard twelve-map generation limit.
- Card Enhancements belong to one `CardInstance`; applying one preserves that card's ID and does not mutate its `CardData` or another matching card.
- A card currently accepts at most one Enhancement, and invalid/duplicate applications do not spend gold or complete an Upgrade twice.
- Artifact and Enhancement effects share one typed resolver/source model; canonical behavior does not branch on display names or IDs.
- Shared ScriptableObject definitions remain immutable during a run. Artifact state is run-owned; Enhancement mutable state is CardInstance-owned; reset creates/clears runtime state.
- Attack and block preview/execution share one modifier path. Sharpened/Club and Hardened/Spade ordering follows the approved canonical rules.
- CardCommitted effects trigger once per committed source card, not per hit. PlayerTurnStart effects use the authoritative hand zone and trigger once per qualifying CardInstance per player-turn start.
- Persistent Artifacts occupy one of five base slots; a sixth cannot be acquired or purchased, costs no Gold, and does not replace ownership. NonSlot temporary/system/quest categories are exempt but still obey duplicate rules. Enhancements are unaffected.
- Shop offers are cached for the active node and are deterministic for seed/map/node regardless of card-stream consumption.
- Upgrade Enhancement choices are deterministic for seed/map/node; the final owned card target is chosen by the player and validated at confirmation, not baked into the offer or RNG.
- Restart clears gold, Artifacts, Enhancements, active build offers, and stale build UI while restoring the 40-card baseline.
- Numeric Artifact attack results do not depend on Artifact acquisition order; multipliers resolve before flat bonuses.
- Shop purchases charge the cached displayed offer price, not a later-mutated definition price.
- Negative prices or spending requests cannot grant content or increase gold.
- Event affordability/lethality validation follows authored effect order and supplies a player-visible failure reason.
- New-run, same-seed restart, and route/modal transitions clear stale terminal, reward, and deck-detail overlays.
- Main Menu/Game transitions use a one-shot seed handoff and the established `RunManager.StartRunWithSeed` path; frontend menus never become authoritative run state.
- Blocking frontend menus suspend time, disable underlying gameplay UI/raycasts, cancel transient card interaction, and prevent manager commands until the final blocking layer closes.
- Nested Settings and confirmation dialogs unwind one layer at a time and cannot prematurely resume gameplay.
- Restart/Retry Same Seed preserves the normalized run seed and deterministic initial map/hand signature; New Run returns to seeded Run Setup.
- Scene transitions and modal teardown restore time scale and gameplay input state.
- Display settings expose only functional resolution, supported display mode, and VSync controls; invalid persisted values normalize to supported values.
- PlayerTurn selection permits one ordinary card or one legal two-card Ace pair; it does not enable other multi-card poker combinations.
- EnemyAttacking snapshots one positive pending attack per surviving enemy. Each selected defense card must fully block one distinct attack, excess defense never carries to another attack, deterministic auto-matching is strongest-to-strongest-blockable, and invalid batches preserve every card.
- Enemy presentations resolve both attack and defense drops contextually through authoritative combat actions; there is no dedicated defense drop zone. Defense drops use the selected batch and deterministic auto-matching rather than assigning a card to the visual target.
- Combat and Elite route labels do not reveal authored enemy identity before entry.
- Encounters may contain multiple distinct `EnemyRuntime` instances; actions validate their exact target, each surviving enemy contributes its attack, and victory occurs only after no active enemies remain.
- Defeating an enemy draws exactly one card. Encounter gold from defeated enemies is paid only on encounter victory and remains unavailable on player defeat.
- Defeat preserves completed timing splits and exposes the current map as incomplete; boss victory records exactly one split and starts the next map timer.
- New runs and both restart paths clear all prior timing data; abandonment stops active timing before returning to the frontend.
- Run timing uses scaled gameplay delta time, remains frozen during explicit Pause, and never affects seeded gameplay outcomes.

The September 25, 2026 shared-effect/core-content gate passed **160/160 EditMode**, **1/1 discovered PlayMode**, focused **8/8**, and the eight-effect Game-scene smoke with zero warnings/errors. The prior final-prototype evidence remains historical; no additional spreadsheet content, Tier gameplay or deferred rule modifier is claimed implemented.

## 7. Known Temporary / Legacy Systems

Working gameplay foundations should be preserved, but the following systems are intentionally temporary:

- The gameplay UI was rebuilt in Phase 3.5; its layout and visuals remain temporary rather than final art.
- The Phase 4 map uses a fixed five-column/three-row prototype template; richer generation rules and additional topology variety remain future content work.
- Normal enemies remain the original three definitions; elites use scaled normal-enemy stats plus one authored trait.
- Shops contain four canonical Tier I Emblems, four retained prototype Artifacts outside this migration, and four canonical Tier I Enhancements; prices remain prototype balance. Tier upgrades/replacement and rarity distribution are not implemented.
- One Enhancement per card remains the prototype rule. Replacement, removal, stacking and card-target reroll remain deferred; Shops and Upgrade rewards now let the player explicitly choose a valid owned card at confirmation.
- Upgrade nodes offer deterministic free Enhancement choices; player-selected owned-card targeting replaces the old preselected-card behavior. Older authored health-focused Upgrade event assets remain unused prototype content.
- General poker/multi-card combat remains deferred; only the approved Ace-pair action is implemented. No player-facing authored multi-hit card exists for a physical input smoke, although the combat runtime supports staged multi-hit actions.
- Bosses now have suit-specific prototype traits, but rank-specific rules and more elaborate boss restrictions are deferred.
- Multi-enemy combat is implemented for Phase 7B. The current scene provides three reusable enemy presentation slots because authored Phase 7B groups contain at most three enemies; larger future groups would require additional slots or dynamic pooling.
- Player maximum HP 30 is provisional.
- Enemy/boss stats, hand size, prices, and other balance values are prototype tuning.
- Boss rewards currently enter the shuffled draw deck; this may be tuned later.
- Gold, Artifact ownership, and build catalogs still live in CardManager; broader run/build-state organization can be revisited if save/load or a larger progression layer is introduced.
- The rebuilt card UI uses suit-tinted temporary panels and text rather than final card art.
- The enemy portrait remains a styled placeholder label rather than authored enemy artwork.
- Seed generation is intentionally local-only; cross-version determinism, networking, cloud seed exchange, and save/load persistence are not implemented.
- The frontend deliberately has no Continue/save-resume, profiles, achievements, metaprogression, character selection, fake difficulty, or placeholder audio settings.
- Display settings are prototype desktop controls. Additional Audio, Gameplay, and Accessibility categories are structured for later expansion but contain no fake options.
- `Unity6_Recreation_Guide.md` predates the Phase 1–4 implementation and still describes the older 52-card starting-deck prototype. It is historical reference, not current design authority; the repository now starts runs with 40 Ace–Ten cards.

### Phase 3.5 cleanup outcome

- Removed the stale `DeckViewerUI.contentContainer` field.
- Replaced generic button-label and ambiguous status object names with responsibility-specific names.
- Removed `Card.prefab/Panel/StateLabel` debug interaction text and the per-card world-space Canvas, CanvasScaler, and GraphicRaycaster.
- Retained the dedicated screen-space `DragCanvas`, which remains required by card drag presentation.
- Kept `GameController` under `Assets/Scripts/UI/` to avoid unrelated file relocation during the UI-only phase.

## 8. Roadmap Status

### Phase 3.5 — Prototype UI Rebuild — Complete

The temporary gameplay UI was rebuilt without changing the Phase 1–3 gameplay architecture. Further visual polish belongs in later UX/art work rather than Phase 4 system implementation.

### Phase 4 — Map / Encounter Expansion + Fully Seeded Run — Complete

Phase 4 provides deterministic run replay, branching/hidden maps, proper route-node resolution, representative normal/elite/shop/event/upgrade/risk/boss content, and extensible authored enemy hooks. The implementation deliberately avoids card enhancements, new artifact progression, expanded economy content, and other Phase 5 systems.

### Phase 5 — Build Progression — Complete

Phase 5 provides authorable Artifacts, per-instance card Enhancements, deterministic mixed shops, Upgrade-node Enhancement choices, and prototype build synergies while preserving baseline single-card combat. The optional poker/multi-card archetype was explicitly deferred.

### Phase 6 — Regression / Balance / Polish — Complete

Phase 6 adds focused regression hardening, clearer build/combat decision feedback, modal/run-flow cleanup, explicit terminal seed choices, and an evidence-based conservative prototype balance baseline. No new gameplay architecture or post-roadmap system was introduced.

### Phase 7 — Frontend & Menu Flow — Complete

Phase 7 adds a coherent Main Menu, seeded Run Setup, reusable Pause/Settings/confirmation navigation, editable Credits, functional persisted display settings, and reusable Victory/Defeat results while preserving the Phase 0–6 gameplay and deterministic-run architecture. It is now part of the approved baseline.

### Phase 7A — Card Interaction & Selection Foundation — Complete and Approved

Phase 7A provides stable physical hand dragging/reordering, combat-state selection policies, context-sensitive enemy attack/defense drops, capped atomic multi-card defense, and one contextual card-action control while preserving authoritative card/combat state.

### Phase 7B — Multi-Enemy & Enemy Identity — Complete

Phase 7B provides independently targetable runtime enemy groups, per-survivor attack totals, kill-triggered draws, secured per-enemy rewards, three-Goblin encounters, and the two-turn Thief flee rule without changing unrelated enemy, elite, or boss balance.

### Phase 7C — Ace Pairing — Implemented

Ace pairing and configurable seeded criticals are implemented; current full EditMode regression and the EventSystem-dispatched Game-scene pair/button check passed. Human mouse/drag targeting remains Pending, as distinguished in Section 5.

### Phases 7D–7E — Approved Playtest Revisions — 7D Accepted / 7E Complete

Phase 7D retains five depths and current Shop placement (explicit zero-depth change). Phase 7E includes Shop inspect/select → Buy, player-selected Enhancement targets, owned-Artifact combat HUD and a five-slot base capacity for Persistent Artifacts. NonSlot Artifact category support is structural only; no Contract content or replacement/capacity-upgrade system exists. The frontend seed follow-up is complete: blank setup generates a seed on Start, gameplay hides its seed label, and both Results modes retain the actual seed for replay and retry. Investment/Betting remains undecided.

### Content Expansion Architecture — Core Tier I Migration — Complete

Artifacts and Enhancements now share the typed source/resolver architecture documented in `Locus/knowledge/plan/cardpg-effect-architecture.md`. The eight approved canonical Tier I entries are migrated with explicit canonical metadata and GUID-safe legacy ID crosswalks. The current gate is **160/160 EditMode**, focused **8/8**, and **1/1 PlayMode**.

## 9. Next Task

The shared effect foundation and approved eight-entry migration are complete. Future work must begin from `Locus/knowledge/plan/cardpg-effect-architecture.md` and requires a separately approved content batch. Do not infer implementation of any other workbook row. Candidate future work includes design-led selection of the next low-risk typed effects; rule modifiers, Tier gameplay, extra turns, poker, map hooks and remaining audited content stay deferred.

## 10. Instructions for Future Locus Sessions

1. Read `CARDPG_DEVELOPMENT.md` before beginning substantial project work.
2. Treat the current repository state as the implementation source of truth.
3. This document provides historical/design context but does not override working code when they disagree. Investigate discrepancies rather than blindly trusting either source.
4. Do not broadly re-audit completed phases unless there is evidence of a regression, inconsistency, or architectural issue.
5. Before modifying a system, inspect only the code, scenes, assets, and necessary dependencies relevant to the current task.
6. Do not assume deferred features are already implemented.
7. Preserve established gameplay invariants unless the user explicitly approves a design change.
8. Update this document after each major completed phase when gameplay contracts, architecture, temporary systems, or roadmap status materially change.
9. Keep updates concise. This file exists to reduce context usage, not become a transcript or development diary.
10. Do not copy completion reports or tool logs verbatim into this document.
