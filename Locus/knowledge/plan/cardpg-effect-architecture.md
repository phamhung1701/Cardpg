---
id: kd_3bf08839-a0d3-4ce0-af24-4f533deb510a
injectMode: inherit
injectAgents:
- unity
aiEditMode: inherit
---

# CardPG shared gameplay-effect architecture

Implemented September 25, 2026 for the first canonical Artifact/Enhancement content migration. This document describes the code that exists now, its deterministic contracts, and the deliberate v1 limits. It does not authorize the next workbook content batch.

## Goals and boundaries

The architecture provides one typed foundation for effects sourced by owned Artifacts and card-local Enhancements. It separates immutable ScriptableObject definitions from run-owned mutable state, centralizes attack/block calculation for preview and execution, and routes reactive combat effects through deterministic queues. It intentionally avoids both ID/name dispatch and a generic scripting language.

V1 migrates only canonical Tier I `rel_001`–`rel_004` and `enh_001`–`enh_004`. Tier upgrades, rarity distribution, extra turns, poker/rank actions, effective suits, overflow play, map effects, random effects, and the remaining workbook content are not implemented.

## System responsibilities

- `Assets/Scripts/Combat/GameplayEffects.cs`
  - Defines effect kinds, triggers, categories, typed conditions and source/context types.
  - `GameplayEffectResolver` is the shared modifier/reactive foundation for both Artifacts and Enhancements.
  - `ArtifactRuntimeInstance` owns one acquired Artifact definition plus run-local `GameplayEffectState`.
  - A `CardInstance` is the runtime source for its attached Enhancement and owns its own `GameplayEffectState`.
  - `GameplayRuleModifierData` is a separate typed extension point for rule-changing mechanics; v1 has no rule subclasses or rule execution.
- `Assets/Scripts/Data/RelicData.cs`
  - Immutable Artifact authoring metadata: legacy runtime ID, canonical ID, display metadata, rarity, tier, parent canonical ID, capacity category, typed effects, and optional typed rule modifiers.
  - Existing legacy prototype hook fields remain only as a compatibility adapter for definitions without typed effects. If `effects` is non-empty, typed effects are authoritative.
- `Assets/Scripts/Data/CardEnhancementData.cs`
  - Immutable Enhancement authoring metadata and typed effect/rule definitions.
  - Legacy attack/defense/heal/draw fields are likewise used only when no typed effects exist.
- `Assets/Scripts/Managers/CardManager.cs`
  - Owns Artifact definitions and the run-local `ArtifactRuntimeInstance` map, five-slot Persistent capacity, NonSlot exemption, Enhancement attachment and stable CardInstances.
  - `Reset` clears owned definitions and Artifact runtime state. A rebuilt run creates fresh CardInstances and fresh Enhancement state.
- `Assets/Scripts/Data/CardInstance.cs`
  - Keeps stable per-run identity, one Enhancement definition, and run-local Enhancement effect state.
  - Card-local attack/block flats are resolved through the shared effect resolver.
- `Assets/Scripts/Managers/CombatManager.cs`
  - Calls the shared resolver for attack/block preview and execution.
  - Creates authoritative card-commit and player-turn-start contexts and uses `CombatReactionQueue` for reactive effects.
  - All player-turn entry paths used by current combat call the same `BeginPlayerTurn` boundary, including encounter start, full defense, remaining-damage resolution and zero-attack retaliation.
- `Assets/Scripts/Combat/CombatReactionQueue.cs`
  - Provides bounded deterministic ordering. V1 adds a `PlayerTurnStarted` phase.
- `Assets/Editor/GameDataGenerator.cs`
  - Retained for compatibility, but updated so an accidental explicit invocation would recreate the eight canonical definitions rather than restore conflicting legacy effects. It was not run for this migration.

## Shared effect source model

`GameplayEffectSource` is either:

1. an `ArtifactRuntimeInstance`, active because its definition is owned; or
2. a stable `CardInstance`, active only for that card's attached Enhancement and only where the trigger semantics make that source relevant.

Both source types expose the same effect definitions, source category, display metadata and runtime-state extension point. Artifact and Enhancement adapters are thin; there is no separate Relic effect engine and Enhancement effect engine.

ScriptableObject definitions are not mutated during a run. Mutable future counters, charges and pity state belong in `ArtifactRuntimeInstance.State`; per-card accumulated Enhancement state such as future Devouring scaling belongs in `CardInstance.EffectState`. `GameplayEffectState` currently offers indexed counters as an extension point; none of the eight migrated effects needs mutable state.

## Effect categories

### Numeric pipeline modifiers

Current typed kinds:

- `FlatAttack`
- `AttackMultiplier`
- `FlatBlock`
- `BlockMultiplier`

They are evaluated without side effects or RNG and feed one authoritative calculation path used by UI previews and committed combat execution.

### Reactive effects

Current typed kinds include:

- `Heal`
- `Draw`
- `BonusGold`

The compatibility adapter also maps the four still-supported prototype families used by non-migrated definitions/tests: reduce enemy attack by card value, recycle discard by card value, and draw by card value. Canonical Heart, Diamond and Spade definitions do not use those legacy effects.

### Rule modifiers

Rule-changing mechanics do not pass through numeric/reactive `Apply` behavior. `GameplayRuleModifierData` is an intentionally empty typed base extension point stored separately on Artifact and Enhancement definitions. A future extra-turn, effective-suit, overflow or poker permission must have a dedicated typed rule and authoritative manager integration. No such mechanic is implemented here.

## Implemented lifecycle contexts

V1 introduces only boundaries required by the migrated content:

- **AttackCalculated / DefenseCalculated:** side-effect-free card value calculation using the source CardInstance and owned Artifact sources.
- **CardCommitted:** once per committed card, not once per hit. Context carries action ID, card, target, card order, hit count, managers and a stable negative committed-card hit index. Heart, Diamond and Quickdraw use this boundary.
- **PlayerTurnStart:** once whenever current combat authoritatively begins a player turn. Context carries action ID, player, managers and turn number. Enhancement sources are snapshotted from the authoritative `CardCollection.Hand` order. Mending uses this boundary.
- **EncounterWon:** deterministic Artifact reward summary for existing Victory Draught and Golden Compass compatibility.

Existing damage/hit/enemy lifecycle stays with `CombatActionContext`, `DamageRequest`, `DamageResult`, `CombatResolver` and `CombatReactionQueue`. Future HitResolved or EnemyKilled content should integrate there rather than subscribing uncontrolled listeners. EncounterStart, DefenseCommitted, DrawAttempt, NodeCompleted, HandEnter and HandLeave effect contexts are not implemented until approved content requires them.

## Deterministic ordering

No effect application relies on Unity object discovery, scene hierarchy, dictionary enumeration, display name, runtime ID or event subscription order.

- Committed cards execute in the authoritative selected-card order already supplied to `TryPlayCards`.
- For one committed card, owned Artifact sources are enqueued in acquisition/list order, then the card's Enhancement source.
- Effects inside one source execute in serialized array order.
- `CombatReactionQueue` orders by phase, source category (Artifact before Enhancement), source order, handler/effect index, priority, hit index and action ID, with a bounded operation limit.
- Player-turn-start Artifacts use acquisition order; Mending Enhancement sources use authoritative hand-zone order, not Transform order.
- Numeric modifiers do not consume RNG. Reactive applicability checks do not consume RNG. Future random effects must request an explicitly named run-seeded stream and document whether consumption is once per action, source, card or hit.

## Conditions and filters

`GameplayEffectDefinition.conditions` is a small composable typed list. V1 condition kinds are:

- `CardSuit`
- `EnhancedCard`

All conditions must match. Enhancement source identity is enforced by the source adapter: a card-local effect applies only to its source CardInstance. Conditions are data and never compare display names or IDs. Additional rank, attack-threshold or HP-threshold conditions should be added only with approved content and the authoritative value/snapshot semantics defined.

## Modifier ordering and preview parity

`GameplayEffectResolver.CalculateAttack` and `CalculateBlock` are the only active calculation path used by `CombatManager`, and `CardView` obtains its preview through those manager methods.

V1 ordering is:

1. base card attack/block value;
2. card-local flat Enhancement modifier;
3. card-local multiplier, if a future Enhancement supplies one;
4. matching Artifact multipliers;
5. matching Artifact flat bonuses.

This preserves the prior attack contract `(base + Enhancement flat) × Artifact multipliers + Artifact flats`. It also implements the approved defense contract `(base block + Hardened +3) ×2` for a qualifying Spade. Club Emblem multiplies only each qualifying Club card before Ace-pair card damage is summed. Ace-pair selection, critical roll and combined-damage timing are otherwise unchanged.

## Canonical Tier I content and legacy crosswalk

Unity GUIDs, asset paths, scene references and cached-offer runtime IDs were preserved. Canonical IDs are explicit metadata rather than inferred from names.

| Canonical | Asset | Legacy runtime ID retained | Migrated typed behavior |
| --- | --- | --- | --- |
| `rel_001` Club Emblem | `Assets/Data/Relics/ClubPower.asset` | `club_power` | Per-card Club attack ×2. |
| `rel_002` Heart Emblem | `Assets/Data/Relics/HeartPower.asset` | `heart_power` | Heal 1 per committed Heart card; no discard recycle. |
| `rel_003` Diamond Emblem | `Assets/Data/Relics/DiamondPower.asset` | `diamond_power` | Draw up to 2 per committed Diamond; no rank-based count. |
| `rel_004` Spade Emblem | `Assets/Data/Relics/SpadePower.asset` | `spade_power` | Spade block ×2 after card-local flats; no enemy-ATK reduction. |
| `enh_001` Sharpened | `Assets/Data/Enhancements/Sharpened.asset` | `sharpened` | This CardInstance has +3 attack. |
| `enh_002` Hardened | `Assets/Data/Enhancements/Reinforced.asset` | `reinforced` | This CardInstance has +3 block; display name is now Hardened. |
| `enh_003` Mending | `Assets/Data/Enhancements/Mending.asset` | `mending` | Heal 2 at each player-turn start while this CardInstance is in hand; no heal on play. |
| `enh_004` Quickdraw | `Assets/Data/Enhancements/Quickdraw.asset` | `quickdraw` | Draw 1 when this CardInstance is committed. |

All eight are Common, Tier 1, with no parent ID. Tier replacement, rarity distribution and pricing from the spreadsheet remain unimplemented. Existing prototype prices remain unchanged because the spreadsheet has no approved prices for these rows.

## Adding a simple Artifact

1. Confirm the mechanic fits an existing numeric or reactive trigger. If it changes legal actions, turns, targeting, zones or map rules, use the rule-modifier path instead.
2. Create or update a `RelicData` asset without changing another asset's GUID.
3. Set a unique legacy/runtime `id`, canonical spreadsheet `canonicalId`, display metadata, rarity, tier, explicit `upgradeFromId`, capacity category, and typed `effects` in deliberate serialized order.
4. Compose typed conditions; do not add ID/name checks to managers.
5. Leave legacy hook fields neutral when typed effects are present.
6. Add the definition deliberately to `GameController.relicCatalog`; files in the folder are not auto-discovered.
7. Add focused calculation/reaction/order/reset tests and Shop/capacity regression.

## Adding an Enhancement

1. Create or update a `CardEnhancementData` asset with canonical metadata and ordered typed effects.
2. Use numeric effects for card-local attack/block values and reactive triggers only where that source CardInstance is authoritative.
3. Leave legacy fields neutral when typed effects exist.
4. Add it deliberately to `GameController.enhancementCatalog`; Shop/Upgrade offers remain deterministic and target-neutral.
5. Keep mutable per-card progress on `CardInstance.EffectState`, never on the shared asset.
6. Verify one-Enhancement-per-card, exact target identity, preview/execution parity, hand/deck/discard trigger semantics and reset.

## When a mechanic is a Rule Modifier

Use a Rule Modifier rather than a normal effect if the mechanic changes what is legal or how the state machine advances, for example:

- grants or suppresses turns;
- permits same-rank, poker or five-card actions;
- changes a card's effective suit;
- plays overflow cards automatically;
- changes target count or selection rules;
- changes ownership/zone legality rather than reacting after an authoritative action.

A rule must integrate with its owning manager's validation and execution boundary. It must not masquerade as a numeric modifier, invoke UI commands, or mutate Transform/UI state.

## Future extension points and deliberate v1 limits

- **Counters/charges:** use source runtime `GameplayEffectState`; define reset scope and stable effect-key migration before content uses it. Current indexed counters are sufficient as an extension point but have no save/version migration contract.
- **Devouring/per-card scaling:** store accumulated bonus on the stable CardInstance state, separated from the Enhancement asset so future replacement can preserve it if approved.
- **Map/node hooks:** add a typed NodeCompleted/map context in RunManager with a named independent seeded stream. Do not let Artifacts query or mutate PathScreenUI.
- **Extra turns:** route every grant through the authoritative player-turn-start boundary and add re-entry/runaway guards before authoring Cheater/Dwarf content.
- **Poker/effective suit:** add dedicated action validation and effective-card context; do not mutate shared `CardData` or generalize the numeric resolver into a hand evaluator.
- **Legacy compatibility:** legacy fields remain for the four non-migrated prototype Artifacts and tests that create definitions in memory. They are adapted into the shared resolver only when no typed effects exist. This is a migration bridge, not a second active path for canonical content.
- **Unsupported v1 triggers:** no typed map, draw-attempt, hand-enter/leave, matched-defense, enemy-kill or hit-resolved content; no random effect; no rule modifier implementation; no tier upgrade/replacement UI; no save/load format.

## Verification checkpoint

- Focused `CoreGameplayEffectsTests`: **8/8 passed**.
- Full EditMode suite: **160/160 passed**, zero failures/skips/inconclusive.
- Discovered PlayMode Shield/multi-hit test: **1/1 passed**.
- Game-scene smoke passed Club+Sharpened, Heart heal, fixed Diamond Draw 2, Quickdraw, Spade+Hardened ordering, no old Spade ATK reduction, Mending at player-turn starts and no Mending heal on play.
- Console warnings/errors during final scene smoke: **0**.
