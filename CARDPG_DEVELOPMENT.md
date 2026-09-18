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

- **`CardInstance`** — One owned runtime card. Holds a stable per-run unique ID, its `CardData` definition, and an optional per-instance `CardEnhancementData`. Enhancement-derived attack/defense and on-play hooks do not mutate the shared definition or other matching cards.
- **`CardCollection`** — Plain runtime model that owns the authoritative `OwnedCards`, `Deck`, `Hand`, and `DiscardPile` zones. It centralizes zone mutations and prevents one instance from occupying multiple zones.
- **`CardManager`** — Run-level card collection/build façade. Builds the 40-card starting collection, creates/destroys card views, handles selection, drawing, discard movement, generated definitions, boss rewards, gold, Artifact ownership, and per-instance Enhancement application. Boss rewards receive new unique IDs and enter the shuffled draw deck.
- **`RelicData` / `CardEnhancementData`** — Authorable Phase 5 Artifact and card-Enhancement definitions. Artifact hooks cover card filters/modifiers, suit powers, enhanced-card synergies, post-victory healing, and bonus gold without combat string-ID dispatch. Enhancements provide attack, defense, healing, or draw hooks on one card instance.
- **`ShopOffer`** — Runtime typed Artifact/Enhancement offer with a stable identity, deterministic target card, price, and presentation data.
- **`CardView` / `Field`** — Presentation and input only. CardView displays a `CardInstance`, supports persistent selection and drag feedback, and issues commands through managers. Field lays out/reorders views. Drag/reorder does not change ownership or card zones.
- **`PlayerRuntime`** — Plain mutable player-health state: configured maximum health, current health, reset/full heal, clamped damage, and defeat status.
- **`CombatManager`** — Authoritative encounter state machine. Resolves card attacks, data-driven Artifact hooks, per-instance Enhancement hooks, pending damage, enhanced defensive discards, Take Damage, Recover, player HP, enemy defeat, modified gold/healing rewards, and one-shot terminal encounter results.
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
- **`RunStatusUI`** — Always-visible gold, draw/discard/hand counts, Artifact/Enhancement counts, map progress, boss index, current boss identity, and compact live run/map timers.
- **`DeckViewerUI`** — Opens/closes a modal draw/discard pile summary plus compact owned-Artifact and enhanced-card details; it closes on run and primary route/modal transitions.
- **`PathScreenUI`** — Builds the current route selection overlay and displays cycle/run-completion state.
- **`ShopUI`** — Opens only for authored shop nodes, displays cached deterministic mixed Artifact/Enhancement offers, handles one-shot purchases, and completes the selected shop node.
- **`RunEventUI`** — Displays authored event/risk choices and the deterministic free Enhancement choices used by Upgrade nodes.
- **`RunSeedUI`** — Displays the active run seed inside gameplay. Seed entry and randomization belong to the frontend Run Setup screen.
- **`RunResultUI`** — Displays exact boss-card reward feedback plus reusable terminal victory/defeat results with seed, progression, bosses defeated, gold, Artifact/build details, total run time, completed map splits, an incomplete defeat split, Retry Same Seed, New Run, and Main Menu actions.
- **`MainMenuUI` / `FrontendMenuNavigator` / `RunSetupUI` / `CreditsUI`** — Present Main Menu, seeded Run Setup, and editable prototype Credits without owning gameplay state.
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

The current repository, including the completed Phase 7 frontend/menu flow and approved Phase 7A interaction foundation, is the implementation baseline. Phase 7B is now implemented; Phase 7C–7E remain approved/planned but not implemented. Preserve the current authored Artifacts, Enhancements, Events, Elites, Bosses, shops, progression, and deterministic-run contracts unless a later revision explicitly changes them.

### Phase 7A — Card Interaction & Selection Foundation — Complete and Approved

Implemented and verified on **September 17, 2026**.

- Hand dragging now uses a temporary visual slot so pickup does not collapse the hand; meaningful horizontal crossings reorder only presentation order, while cancelled/invalid drags restore cleanly without changing authoritative card zones.
- Card selection is ordered presentation/input state with numbered defensive selection, deterministic toggling, drag-set reuse, and cleanup on actions, encounters, modals, resets, and restarts. PlayerTurn permits exactly zero or one selected card: selecting another replaces the prior selection, while clicking the selected card deselects it.
- EnemyAttacking permits ordered multi-card defense selection only while authoritative selected block is below pending damage. The final required card may over-block; further additions are rejected until deselection lowers selected block below the requirement. Artifact and Enhancement defense effects resolve through `CombatManager.CalculateCardDefense`.
- The enemy presentation is the context-sensitive card drop target: PlayerTurn drops attack that target, while EnemyAttacking drops defend against that target's pending attack. The target boundary accepts an `EnemyRuntime` and remains compatible with later independently targetable enemies without implementing Phase 7B.
- The separate Play and Discard-to-Defend controls were replaced by one contextual primary action button. The temporary dedicated defense drop zone was removed.
- Defensive actions accept an atomically validated selected-card set through either the enemy drop target or contextual button, total authoritative defense values, consume each card exactly once, and preserve all cards on invalid attempts.
- Added 7 focused Phase 7A regression tests, bringing the EditMode suite to 65 passing tests.
- Play Mode verified PlayerTurn replacement/deselection, capped defensive multi-selection, legal final-card over-block, deselection reopening selection, context-sensitive enemy attack/defense drops, absence of the dedicated defense target, contextual-button defense, exact consumption, ownership conservation, stable dragging, Main Menu → Game initialization, restart cleanup, and a clean Console.
- Phase 7A received manual player approval and unlocked Phase 7B.

### Phase 7B — Multi-Enemy & Enemy Identity — Complete

Implemented and verified on **September 17, 2026**.

- Combat encounters now own an ordered collection of independently targetable `EnemyRuntime` instances while retaining a selected target for contextual-button input and compatibility with single-enemy content.
- Enemy presentations use three reusable bound slots. Card drops attack or defend against the exact bound runtime enemy, clicking an enemy selects the contextual-button target, defeated/fled enemies are removed, and remaining presentations close ranks.
- Surviving enemies contribute their individual attack values to one authoritative pending-damage total. Recovery damage also uses the total surviving enemy attack.
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

### Phase 7C — Ace Pairing

- An Ace may pair with exactly one other card; both cards are played/consumed and base damage is their combined value.
- The pair may critically hit for ×2 total damage. The prototype critical chance is 25%, must remain configurable, and is not considered balanced.
- Critical randomness must use the Fully Seeded Run deterministic RNG architecture.
- General poker/multi-card combat remains deferred.

### Phase 7D — Map Revision

- Elite nodes remain visible even when distant.
- Revisit adding 2–3 route depths, but do not lock the increase until revised combat is playtested against the full twelve-map run length.
- Prevent useless early-shop experiences and provide meaningful shop access on the approach to a boss.
- Final generation constraints follow revised-combat playtesting.

### Phase 7E — Shop / Build UX

- Shop-offer clicks inspect/select first, followed by explicit Buy confirmation.
- Enhancement acquisition lets the player choose the eligible owned card instead of receiving a preselected target.
- Show owned Artifacts during combat.
- Add an Artifact capacity/limit after its exact value is decided.
- An early-shop Investment or Betting economy mechanic is under consideration; its rules are undecided and must not be implemented yet.

### Approved Frontend Follow-up

- Seed entry is optional. Normal random runs should not emphasize or show the seed during gameplay.
- Reveal the run seed on the results screen for replay/sharing.
- Keep custom-seed entry available through New Run setup.

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
- Artifact effects are resolved through authored typed data rather than hard-coded Artifact ID branches in combat.
- Shop offers are cached for the active node and are deterministic for seed/map/node regardless of card-stream consumption.
- Upgrade choices are deterministic for seed/map/node and apply to the exact offered owned card instance.
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
- PlayerTurn card selection contains at most one card; selecting another replaces it and selecting it again deselects it. Future legal combinations must explicitly extend this policy.
- EnemyAttacking selection may add cards only while authoritative selected defense is below pending damage; the final added card may over-block, selected cards remain deselectable, and selection reopens after block falls below pending damage.
- Enemy presentations resolve both attack and defense drops contextually through authoritative targeted combat actions; there is no dedicated defense drop zone.
- Encounters may contain multiple distinct `EnemyRuntime` instances; actions validate their exact target, each surviving enemy contributes its attack, and victory occurs only after no active enemies remain.
- Defeating an enemy draws exactly one card. Encounter gold from defeated enemies is paid only on encounter victory and remains unavailable on player defeat.
- Defeat preserves completed timing splits and exposes the current map as incomplete; boss victory records exactly one split and starts the next map timer.
- New runs and both restart paths clear all prior timing data; abandonment stops active timing before returning to the frontend.
- Run timing uses scaled gameplay delta time, remains frozen during explicit Pause, and never affects seeded gameplay outcomes.

The current discovered EditMode suite contains **76 tests**. Focused verification for run timing and its directly related pause/results integration passed 9/9 tests. Existing coverage includes card identity/conservation, player health, starting collection, exact rewards, grouped bosses, no premature node completion, deterministic random streams, seeded replay, arbitrary-index map generation, branching topology, mixed-shop determinism, cached-price integrity, Artifact hooks and stacking order, per-instance Enhancements, Upgrade choices and empty pools, purchase guards, ordered event validation, overlay reset, build reset, enemy ability hooks, risk safety, restart health reset, one-shot frontend routing, seed generation, modal stacking/input blocking, display-setting normalization, blocked gameplay commands, results retry/summary behavior, state-authoritative PlayerTurn selection, capped defensive multi-selection with legal over-block, atomic multi-card defense, contextual enemy attack/defense forwarding, invalid-action preservation, stable hand drag slots, transient interaction cleanup, multi-enemy targeting, survivor attack totals, per-kill draws, secured group rewards, authored Goblin/Thief identity rules, Thief flee timing, multi-enemy presentation binding, run timing reset/splits/incomplete state, and hour-safe duration formatting. Play Mode verification has additionally covered live run-timer advance, explicit Pause freeze, resume, and the gameplay timer display.

## 7. Known Temporary / Legacy Systems

Working gameplay foundations should be preserved, but the following systems are intentionally temporary:

- The gameplay UI was rebuilt in Phase 3.5; its layout and visuals remain temporary rather than final art.
- The Phase 4 map uses a fixed five-column/three-row prototype template; richer generation rules and additional topology variety remain future content work.
- Normal enemies remain the original three definitions; elites use scaled normal-enemy stats plus one authored trait.
- Shops now contain eight prototype Artifacts and four prototype Enhancements; prices and effect values remain prototype balance.
- One Enhancement per card is the current prototype rule. Replacement, removal, stacking, card-target reroll, and player-selected Enhancement targeting are not implemented; player-selected targeting is planned for Phase 7E.
- Upgrade nodes currently grant deterministic card Enhancements to preselected eligible cards; the older authored health-focused Upgrade event assets remain unused prototype content.
- General poker/multi-card combat remains deferred; only the specifically approved Ace-pair mechanic is planned for Phase 7C.
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

### Phases 7C–7E — Approved Playtest Revisions — Not Implemented

The remaining ordered revision scope is defined in Section 5. Ace pairing/critical hits, map revisions, and shop/build UX feedback remain approved deferred work. Later phases must preserve seeded determinism and current authored content unless their approved scope says otherwise.

## 9. Next Task

The lightweight run timer and map-split telemetry is implemented and ready for pacing playtests. Use it while manually playtesting and approving **Phase 7B — Multi-Enemy & Enemy Identity**. Do not begin Phase 7C until the revised combat flow is accepted.

After approval, the next implementation phase is **Phase 7C — Ace Pairing**. The repository remains the implementation source of truth, and Phase 7C–7E items are planned rather than completed. Do not implement general poker combat, undecided Artifact-capacity rules, or Investment/Betting rules.

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
