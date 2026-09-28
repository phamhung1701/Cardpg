---
id: kd_be979d60-aa63-4b02-b708-f61734df37fc
injectMode: inherit
injectAgents:
- unity
aiEditMode: inherit
---

# Early-access game-data implementation plan

**Status: PLANNED — documentation only.** This plan is not implementation or blanket approval of unresolved content values. No gameplay/source/assets/tests were changed for this document; no commit is requested or performed.

## Authority and baseline

- Source reviewed directly with openpyxl: `CardPG_GameData_organized.xlsx` sheets Enemies, Bosses, Artifacts, Enhancements, Events, Shops, Maps, Consumables, Boss Abilities, Scaling. Re-read exact rows and notes at phase entry; workbook changes supersede this snapshot.
- User approved the eight-phase order, not every TBD value. All phases and ledger rows remain Planned until individually implemented, verified and accepted.
- Effort labels S/M/L/XL are relative engineering scope only, not dates or delivery promises.
- Before edits inspect `git status --short --branch`, preserve dirty user-owned baseline, inspect callers/serialized bindings/tests, and record phase baseline. Never stage/stash/reset/clean/commit/push.
- `RunManager.BuildBossDeck` already creates 12 J/Q/K bosses in deterministic order with rank-based stats/suit abilities. `CreateEliteType` applies ×1.5 HP, +2 ATK, ×2 Gold; authored elites must avoid double application. `EnemyRuntime` copies immutable EnemyTypeData stats.
- Existing `ArtifactUpgradeResolver` and same-slot upgrades exist. Preserve one enhancement/card; do not assume Upgrade nodes are artifact upgrade UI or invent mastery requirements.
- Current backpack: three slots, per-item charges, no stacks and no general reward tray. Full reward must support replace/decline, never silent loss. Rare Pack capacity loss is withdraw-only; partial charges consumed first; rune suits remain separate.
- Boss assignment cached once per seed/map; UI re-entry/retry cannot reroll. Map-entry UI shows cached current-map boss description, never whole preview deck.
- Surveyor’s Map may duplicate already-visible Shops/Elites: decision gate requires useful distinct benefit; do not add a no-op reveal.
- Inspect/preserve dirty M4DevMode state. Prior builds had internal Unity Bee pipeline errors. Capture diagnostics, distinguish infrastructure from code failure; release gate includes actual failure triage and Windows dev/release smoke when supported.
- TBD numeric economy, rarity, prices, growth and quantities require phase-entry approval. Do not invent, and do not claim current test/build status without running it.

## Approved phase order

| Phase | Scope | Effort | Entry gate |
|---|---|---:|---|
| 1 | Scaling + Knight, Shieldbearer, Brute | M | Resolve phase-specific workbook TBDs before code. |
| 2 | Mixed groups + Captain/Royal Knight elites; elites may appear on any map as an optional visible risk with reachable non-elite alternative | L | Resolve phase-specific workbook TBDs before code. |
| 3 | 12 boss variants + cached ability assignment + map-entry UI | XL | Resolve phase-specific workbook TBDs before code. |
| 4 | Oppression, Silence, Withering, Double Strike + Duelist/War Drummer | L | Resolve phase-specific workbook TBDs before code. |
| 5 | Challenger’s Crest, Club Epic, Overflow Epic, Bounty Ledger, Gilded Blade | L | Resolve phase-specific workbook TBDs before code. |
| 6 | Purchase/use hooks, reward tray, Traveler’s Pack tiers + economy artifacts (excluding Gilded Blade) | XL | Resolve phase-specific workbook TBDs before code. |
| 7 | Other emblem upgrades, growth/map artifacts + Master Thief | XL | Resolve phase-specific workbook TBDs before code. |
| 8 | Blood Ritual, Quest, Infinite Mode | XL | Resolve phase-specific workbook TBDs before code. |

## Phase protocol

- Entry: re-read exact workbook rows/notes; inspect relevant implementation, callers, tests, scene bindings, build state and dirty overlap.
- Resolve listed behavioral/numeric decisions with owner; leave unresolved subfeatures blocked rather than guessing.
- Add characterization/focused tests before modifying shared resolution order or ownership. Work one phase/slice at a time.
- Exit: focused tests, relevant regression, full available EditMode; PlayMode/real UI smoke where presentation/input/state matters. Report exact counts, failures, skips, environment and anything not run.
- Check same-seed determinism, cached decisions, reset/re-entry, exact-once reward boundaries and no shared data asset mutation.
- Review diff against baseline. Workbook status updates require separate authorization and evidence. No auto-commit/push.

---

## Phase 1 — Scaling + Knight, Shieldbearer, Brute

**Status:** Planned. **Effort:** M. **Workbook scope:** Scaling sheet; enemy_knight, enemy_shieldbearer, enemy_brute.

### Entry decisions
- Approve tie-breaking after multiplication and any unresolved base stats; Knight approved reference is 16 HP / 4 ATK.

### Executable tasks
- [ ] Inspect current source, data, tests and exact workbook rows; record baseline behavior.
- [ ] Calculate map-scaled HP/ATK using HP factor `1 + 0.2 × (map−1)` and ATK factor `1 + 0.07 × (map−1)`, rounded once after multiplication using the approved tie rule. Apply only to base stats. Keep Captain/War Drummer bonuses fixed and excluded. Preview and spawned runtime share the same result.
- [ ] Use exact workbook stat/behavior baselines: Knight 16 HP / 4 ATK (proposed change from current Unity asset 25/8); solo introduction around Map 3 and later may appear with one weak ally. Shieldbearer 10 HP / 3 ATK begins each encounter with one Shield; proposed pairing is Shieldbearer + Goblin. Brute 18 HP / 3 ATK, alone or Duelist + Brute, alternates normal response and telegraphed attack at 2× ATK. Gold remains TBD. Do not modify assets at runtime.
- [ ] Use existing ownership/resolution hooks first; introduce a narrow service only when a missing seam is demonstrated.
- [ ] Add focused automated tests before wiring UI; preserve cancel, failure, reset and retry paths.
- [ ] Wire only necessary UI/scene affordance through existing bindings; avoid broad scene/YAML redesign.
- [ ] Run regression, inspect logs/diff, list unresolved decisions, and request phase acceptance.

### Files and reusable hooks
- `RunManager` scaling/construction; `EnemyTypeData`; `EnemyRuntime`; `PathScreenUI`/`EnemyGroupUI` previews. Reuse pure helper if present.

### Dependencies
- Baseline inspection and approved data decisions; no prior phase dependency.

### Tests and acceptance
- Tests: map 1/2/12, fractional rounding, runtime-preview parity, repeated instantiation without compounding, source asset immutability.
- Acceptance: Exact approved stats at all maps; one rounding step; no elite bonus or source mutation layered accidentally.
- Evidence: test counts, build/Bee result and diagnostics, smoke steps, Console classification, seed, baseline and changed files.

---

## Phase 2 — Mixed groups + Captain/Royal Knight elites; optional elite route on all maps

**Status:** Planned. **Effort:** L. **Workbook scope:** Enemies sheet; all map encounter definitions.

### Entry decisions
- Decide group composition budget, elite frequency/spawn count and whether current visible elite routes already meet this goal. Preserve an optional elite alternative route on each map, never make it mandatory.

### Executable tasks
- [ ] Inspect current source, data, tests and exact workbook rows; record baseline behavior.
- [ ] Author mixed groups and elite variants. Captain: 16 HP / 3 ATK, spawns with 2 Goblins, each +1 ATK while Captain lives; group base total 22 HP / 9 ATK. Royal Knight: 32 HP / 5 ATK, alternates Shield + normal attack and telegraphed 2× heavy attack. Both may appear on any map, including Map 1, as visibly risky optional encounters with reachable non-elite alternatives. Do not force an elite spawn on every map. Prevent generic `CreateEliteType` ×1.5 HP/+2 ATK/×2 Gold from double-applying to authored elites; Gold remains TBD. Keep Captain/Drummer ally bonus fixed and unscaled.
- [ ] Use existing ownership/resolution hooks first; introduce a narrow service only when a missing seam is demonstrated.
- [ ] Add focused automated tests before wiring UI; preserve cancel, failure, reset and retry paths.
- [ ] Wire only necessary UI/scene affordance through existing bindings; avoid broad scene/YAML redesign.
- [ ] Run regression, inspect logs/diff, list unresolved decisions, and request phase acceptance.

### Files and reusable hooks
- `RunManager` encounter generation/`CreateEliteType`; `RunMapGenerator`; `EnemyTypeData`/`EnemyRuntime`; `PathScreenUI`; `Phase7DMapVisibilityTests` and run tests.

### Dependencies
- Phase 1 accepted; do not pull later-phase behavior forward.

### Tests and acceptance
- Tests: Seed/map matrix for reachability, optionality, composition, exact elite stats, deterministic signatures and unchanged unrelated route streams; UI smoke.
- Acceptance: All maps permit the authored elite encounter to be eligible without forcing it to spawn; whenever an elite route/encounter is generated it is optional and has a reachable non-elite alternative. Authored stats correct and deterministic.
- Evidence: test counts, build/Bee result and diagnostics, smoke steps, Console classification, seed, baseline and changed files.

---

## Phase 3 — 12 boss variants + cached ability assignment + map-entry UI

**Status:** Planned. **Effort:** XL. **Workbook scope:** Bosses and Boss Abilities sheets, all 12 boss IDs.

### Entry decisions
- Keep existing 12-boss rank/suit roster and approved 30 HP / 6 ATK shared baseline. Boss reward Gold remains TBD except retained legacy values. Resolve ability rounding details before implementation.

### Executable tasks
- [ ] Inspect current source, data, tests and exact workbook rows; record baseline behavior.
- [ ] Assign abilities once at map generation and cache per seed/map; never reroll on map UI open, retry or re-entry. Jacks/Queens one ability; Kings two distinct: at most one hard counter. If none, exactly one defensive + one offensive; if one hard counter, second from remaining five non-hard-counter abilities. Suit immunity intrinsic and separate. For phase 3, only enable Heavy Swing (alternate normal/150% ATK response), Desperation (+25% ATK below 50% HP), Regeneration (heal 5% maximum HP after response, max 3 activations; rounding decision required), and Guarded (combat start/after each response: first non-immune hit reduced 50%; immunity does not consume it). Temporarily constrain assignment pool to implemented/verified abilities until phase 4 adds Silence, Withering, Oppression, Double Strike; enable those only after tests pass. Show only cached current-map boss description, not whole deck preview.
- [ ] Use existing ownership/resolution hooks first; introduce a narrow service only when a missing seam is demonstrated.
- [ ] Add focused automated tests before wiring UI; preserve cancel, failure, reset and retry paths.
- [ ] Wire only necessary UI/scene affordance through existing bindings; avoid broad scene/YAML redesign.
- [ ] Run regression, inspect logs/diff, list unresolved decisions, and request phase acceptance.

### Files and reusable hooks
- `RunManager.BuildBossDeck` and map cache; `EnemyAbility`/`EnemyTypeData`; `PathScreenUI`/`RunStatusUI` map entry; boss tests.

### Dependencies
- Phase 2 accepted; do not pull later-phase behavior forward.

### Tests and acceptance
- Tests: Roster/order; valid pairing categories; same-seed stability, changed-seed behavior, no UI reroll; displayed description equals runtime boss.
- Acceptance: Deterministic valid assignment, cached and accurately displayed; no unapproved stats/rewards.
- Evidence: test counts, build/Bee result and diagnostics, smoke steps, Console classification, seed, baseline and changed files.

---

## Phase 4 — Oppression, Silence, Withering, Double Strike + Duelist/War Drummer

**Status:** Planned. **Effort:** L. **Workbook scope:** Boss Abilities and Enemies sheets.

### Entry decisions
- Approve remaining rounding. Confirm combat damage/heal/effect order with phase 3 assignment.

### Executable tasks
- [ ] Inspect current source, data, tests and exact workbook rows; record baseline behavior.
- [ ] Silence and Withering apply only while boss is above 50% HP. Silence suppresses passive and triggered held-card effects, including Auxiliary, Mending and Golden. Withering blocks healing and prevented healing cannot trigger overheal/Vitality. Oppression adds +2 damage per held card beyond 3 once to response budget before Double Strike split. Double Strike is two hits at 60% normal ATK with approved per-hit rounding. Add Duelist: announces a suit each turn; first attack with that suit deals 50% damage, not immunity (rounding TBD). Add War Drummer: after each enemy response, grant allies +1 ATK for encounter up to +3 per ally; no self-buff, death stops future growth, existing buffs remain, fixed bonus not map-scaled.
- [ ] Use existing ownership/resolution hooks first; introduce a narrow service only when a missing seam is demonstrated.
- [ ] Add focused automated tests before wiring UI; preserve cancel, failure, reset and retry paths.
- [ ] Wire only necessary UI/scene affordance through existing bindings; avoid broad scene/YAML redesign.
- [ ] Run regression, inspect logs/diff, list unresolved decisions, and request phase acceptance.

### Files and reusable hooks
- `CombatManager`; `GameplayEffects`; `EnemyAbility`; `EnemyRuntime`; `CardManager` held-effect resolution; `EnemyGroupUI`.

### Dependencies
- Phase 3 accepted; do not pull later-phase behavior forward.

### Tests and acceptance
- Tests: card counts 3/4, hit split and mitigation, Silence passive/trigger, Withering no-heal/no-overheal, thresholds, fixed ally bonus and interrupted/defeated response.
- Acceptance: One bonus budget, correct effects ordering, enemy behavior parity and correct preview/runtime.
- Evidence: test counts, build/Bee result and diagnostics, smoke steps, Console classification, seed, baseline and changed files.

---

## Phase 5 — Challenger’s Crest, Club Epic, Overflow Epic, Bounty Ledger, Gilded Blade

**Status:** Planned. **Effort:** L. **Workbook scope:** Artifact rows rel_136, rel_034, rel_110, rel_126, rel_128.

### Entry decisions
- Confirm overflow resolution/hand-count definition and Bounty Ledger reward base/rounding/composition. Preserve upgrade chains; do not invent one.

### Executable tasks
- [ ] Inspect current source, data, tests and exact workbook rows; record baseline behavior.
- [ ] Implement rel_136 Challenger’s Crest: one starting Shield only for Elite/Boss combat. Implement rel_034 Club Emblem Epic: Club attack damage ×1.25 for each Club in hand; workbook says upgrade from rel_025 but does not say “Keep,” so determine replacement effect from exact row/approved design rather than assuming prior effect remains. Implement rel_110 Overflow Epic: retain Rare effect as specified, auto-play combat draws beyond full hand for bonus equal to current hand count, max 3 per root player action with nested draws sharing budget. Implement rel_126 Bounty Ledger: Elite +50%, Boss +25% Gold; approve rounding. Implement rel_128 Gilded Blade: opening attack +1 damage per 10 Gold held; approve rounding. All reward modifiers resolve once at authoritative reward settlement.
- [ ] Use existing ownership/resolution hooks first; introduce a narrow service only when a missing seam is demonstrated.
- [ ] Add focused automated tests before wiring UI; preserve cancel, failure, reset and retry paths.
- [ ] Wire only necessary UI/scene affordance through existing bindings; avoid broad scene/YAML redesign.
- [ ] Run regression, inspect logs/diff, list unresolved decisions, and request phase acceptance.

### Files and reusable hooks
- `CombatManager`; `GameplayEffects`; `CardManager`; `RunManager` reward resolution; `ArtifactUpgradeResolver`; existing HUD if needed.

### Dependencies
- Phase 4 accepted; do not pull later-phase behavior forward.

### Tests and acceptance
- Tests: start contexts; Club held count/order; full-hand draw, recursion and ownership; reward modifier composition; duplicate callback/retry; upgrade compatibility.
- Acceptance: Each trigger fires at authoritative boundary exactly once; no recursive draw, loss or duplicate award.
- Evidence: test counts, build/Bee result and diagnostics, smoke steps, Console classification, seed, baseline and changed files.

---

## Phase 6 — Purchase/use hooks, reward tray, Traveler’s Pack tiers + economy artifacts

**Status:** Planned. **Effort:** XL. **Workbook scope:** rel_120/121/123/117/129/125/127/124/131/130/122 (Gilded Blade `rel_128` is phase 5).

### Entry decisions
- Require approval for all TBD prices, rarity, growth, reward quantities. Backpack shrink is withdraw-only. Rare Pack stacks up to 3 identical consumables/slot with per-item charges, partially-used-first, separate rune suits. Full backpack reward must allow replace/decline; never silent loss. Current inventory has 3 slots, per-item charges, no stacking/general reward tray.

### Executable tasks
- [ ] Inspect current source, data, tests and exact workbook rows; record baseline behavior.
- [ ] Implement rel_120 Common Traveler’s Pack: +2 backpack slots, from 3 to 5, still nonstacking. rel_121 Rare: retain five slots; up to 3 identical consumables per slot, separate item charges, consume partially used first, keep different rune suits separate, and on capacity loss withdraw-only. rel_123 Preparation Manual: starting block per distinct consumable type carried (amount TBD). rel_117 Scavenger’s Pouch: first consumable used each encounter draws one. rel_122 Field Medic’s Kit: first consumable per encounter also heals (amount TBD). rel_129 Scavenger’s Satchel: every 3 combat wins gives one random non-enhancement consumable; full claim tray replace/decline. rel_130 Alchemist’s Kit: at each new map choose one of three random enhancement consumables; cache choice, no enemy enhancement drops.
- [ ] Implement rel_124 Merchant’s Badge: 20% eligible-purchase discount before cashback; approve eligibility/rounding. rel_125 Cashback Token: refund 15% actual Gold spent, no free-purchase cashback and never above actual cost. rel_131 Merchant’s Gift: every 3 paid item purchases grants one random non-enhancement consumable; free rewards do not count; full tray replace/decline. Define purchase counter reset scope before implementation. rel_127 Golden Vault: Boss defeat grants 10% of unspent Gold; approve rounding and settlement order. Keep Gilded Blade in phase 5.
- [ ] Add success-only hooks at authoritative completed purchase and consumable-use points; failed/cancelled actions emit nothing. Implement inspect/claim/replace/decline reward tray with idempotent grant. No item may disappear silently.
- [ ] Use existing ownership/resolution hooks first; introduce a narrow service only when a missing seam is demonstrated.
- [ ] Add focused automated tests before wiring UI; preserve cancel, failure, reset and retry paths.
- [ ] Wire only necessary UI/scene affordance through existing bindings; avoid broad scene/YAML redesign.
- [ ] Run regression, inspect logs/diff, list unresolved decisions, and request phase acceptance.

### Files and reusable hooks
- `RunManager` purchase path/cached offers; `ShopUI`; `CardManager` inventory/use validation; `ConsumableInstance`; `RunStatusUI`; narrowly scoped tray only if existing UI cannot host.

### Dependencies
- Phase 5 accepted; do not pull later-phase behavior forward.

### Tests and acceptance
- Tests: Failed/cancelled actions no trigger; cashback free purchase zero and capped to actual cost; stack/charge order; shrink withdrawal; tray replacement/cancel; reset/death; deterministic offers.
- Acceptance: Settlement-based, exactly-once hooks; no silent drops, duplicate payouts, asset mutations or unsupported persistence.
- Evidence: test counts, build/Bee result and diagnostics, smoke steps, Console classification, seed, baseline and changed files.

---

## Phase 7 — Other emblem upgrades, growth/map artifacts + Master Thief

**Status:** Planned. **Effort:** XL. **Workbook scope:** Remaining upgrade, growth, map artifact rows; Master Thief enemy.

### Entry decisions
- Approve every TBD growth increment/cap/reset/scope and rarity/price. Surveyor’s Map needs redundancy decision because Shops/Elites may already be visible. No invented mastery conditions. Growth is run-local, not asset mutation.

### Executable tasks
- [ ] Inspect current source, data, tests and exact workbook rows; record baseline behavior.
- [ ] Implement rel_114 Ace Rare: keep Common crit chance and return Ace to hand after a critical Ace pair once per encounter. rel_115 Ace Epic: keep Rare behavior and guarantee crit when paired with Face card. Check returns against hand capacity/ownership.
- [ ] Implement rel_108 Diamond Epic: keep Rare draw effect; every third manually played Diamond chooses a discard card to draw. rel_030 Arsenal Epic: Face cards in hand increase hand size with diminishing returns; workbook does not say “Keep,” so confirm replacement semantics. rel_105 Heart Epic: keep Rare healing/Gold; overheal becomes run-local Vitality spent on next Heart attack (conversion/cap TBD). rel_116 Mimic Epic: keep base; matching-rank held cards trigger in-hand effects one extra time.
- [ ] Implement rel_112 Dwarf Rare: keep Common bonus action; rank below 5 strengthens next higher-rank attack (amount TBD). rel_113 Dwarf Epic: keep Rare; kill with prepared higher-rank attack permanently strengthens low-rank source (amount/scope TBD). rel_109 Hands Epic: keep Rare, allow four matching ranks, each card’s on-play effect triggers twice; prohibit replay/recursive bonus action. rel_118 Balancer’s Scale: alternate manually played low-rank and Face cards; repeating group resets bonus; threshold/bonus TBD.
- [ ] Implement rel_106 Spade Rare: keep doubled block; fully block with one Spade to draw one. rel_107 Spade Epic: keep Rare; unused Spade block adds damage to next attack (amount/expiry TBD). rel_111 Retaliation Epic: counterattack kill permanently increases counterattack damage (amount TBD). rel_139 Crown of Endurance: Boss kill permanently increases max HP, with additional growth if encounter had zero-HP loss; approve amounts and define zero-HP loss boundary. rel_138 Kingslayer’s Mark: against Boss, play 3 different ranks to empower next attack, consume charge and repeat; define manual trigger/amount.
- [ ] Implement rel_135 Hidden Trail once per map: reroll a reachable hidden node to a different hidden type; never Boss, create nodes or bypass routes. rel_132 Surveyor’s Map reveals all Shops/Elites without connectivity change only if useful/nonredundant after explicit gate. rel_133 Wanderer’s Boots: after 3 distinct node types in a map, heal and gain Gold once/map (amounts TBD). rel_134 Warpath Banner: consecutive combat wins increase next combat starting block (amount/cap/reset TBD). rel_119 Hunter’s Ledger: each Elite kill permanently strengthens first attack each encounter (amount/scope TBD). rel_137 Trophy Rack: Elite kill permanently strengthens opening attack vs Boss (amount/scope TBD). rel_140 Hammer: every 3 nodes choose one of 3 Common Enhancements and apply to chosen eligible card instead of random enhancement; preserve one-enhancement/card and decide fallback if no eligible card.
- [ ] Master Thief: 24 HP / 4 ATK; after first attack steals up to 8 Gold; announces escape and flees after player’s fourth action unless killed. Kill returns stolen Gold plus normal Elite reward; flee gives no kill reward; at zero player Gold, still fights and timer continues. Define whether bonus action counts as player action. Avoid double elite modifiers.
- [ ] Use existing ownership/resolution hooks first; introduce a narrow service only when a missing seam is demonstrated.
- [ ] Add focused automated tests before wiring UI; preserve cancel, failure, reset and retry paths.
- [ ] Wire only necessary UI/scene affordance through existing bindings; avoid broad scene/YAML redesign.
- [ ] Run regression, inspect logs/diff, list unresolved decisions, and request phase acceptance.

### Files and reusable hooks
- `ArtifactUpgradeResolver`; `RelicData`; `CardManager`/`GameplayEffects`; `RunManager` kill/map hooks; `RunMapGenerator`/`PathScreenUI`; `EnemyRuntime`; existing upgrade UI only if separately applicable.

### Dependencies
- Phase 6 accepted; do not pull later-phase behavior forward.

### Phase 7 sub-batches and focused acceptance
Complete and review these separately rather than implementing the entire XL phase at once:
1. **Growth bookkeeping:** Retaliation, Hunter's Ledger, Trophy Rack and Crown. Test correct killer/source attribution, Elite versus Boss classification, blocked versus actual HP loss, one reward per death, restart clears growth, and no changes to shared RelicData.
2. **Action preparation:** Dwarf tiers, Balancer and Kingslayer. Test manual versus automatic plays, bonus actions, grouped-card order, repeated ranks, source card destruction/duplication, charge consumption and encounter reset. Confirm all thresholds and buff magnitudes first.
3. **Card ownership and choices:** Ace tiers, Diamond and Hammer. Test full-hand returns, one physical Ace identity, critical misses, once-per-encounter limits, empty discard/eligible pools, modal cancellation and deferred action continuation. Queue choices rather than opening nested conflicting modals.
4. **Hand engines:** Arsenal, Heart and Mimic. Decide Arsenal's diminishing-return formula and replacement penalties; do not resurrect the rejected J–Q–K court proposal. Test hand shrink without forced discard, Silence toggling at its threshold, Withering producing no Vitality, spent-versus-held snapshots and nonrecursive Mimic triggers.
5. **Grouped offense and defense:** Hands and Spade tiers. Test four-card legality, exactly two eligible effect activations without replaying ownership/action spending, enemy death midway, one victory, one-Spade full block, excess-block banking and next-attack consumption. Retain approved Recover behavior.
6. **Route mechanics:** Hidden Trail, Surveyor, Wanderer and Warpath. Test map resets, visited-node distinctness, hidden-node eligibility, no Boss rerolls, no connectivity change, deterministic re-entry and invalidating any cached encounter/reward for a changed node. Define starting-block duration/consumption before Warpath; do not confuse it with Shield.
7. **Master Thief:** Test insufficient/zero Gold, single theft, fourth-action boundary including the agreed bonus-action rule, lethal hit before escape, no reward on flee, stolen Gold returned exactly once on death, and encounter cleanup. Steal/refund must not multiply through reward modifiers accidentally.

### Tests and acceptance
- Tests: Upgrade base-effect retention; one enhancement/card; growth run persistence/reset and no SO mutation; hidden reroll deterministic/reachable; once-per-map; Hammer choices/cancel; Master Thief stats; manual vs auto triggers.
- Acceptance: Only workbook deltas added; map connectivity unchanged; no invented mastery, no data asset mutation.
- Evidence: test counts, build/Bee result and diagnostics, smoke steps, Console classification, seed, baseline and changed files.

---

## Phase 8 — Blood Ritual, Quest, Infinite Mode

**Status:** Planned. **Effort:** XL. **Workbook scope:** Events/Shops/Bosses/Scaling/Consumables and run lifecycle.

### Entry decisions
- Historical Blood Ritual proposal, confirmed in workbook: **Everything** sets max HP to 5 and grants a Legendary Artifact; **A Pool** costs 15 current HP and grants an Epic Artifact; **A Drop** costs 5 current HP and upgrades a random Artifact by one tier. Keep current HP distinct from max HP. Never heal from max-HP change; define/test nonlethal handling for paid HP options before implementation. Quest needs complete objective/failure/expiry/reward definitions. Infinite Mode proposal is +10% HP / +4% ATK compounded after map 12; it remains provisional and needs explicit rounding and reward-limit approval.

### Executable tasks
- [ ] Inspect current source, data, tests and exact workbook rows; record baseline behavior.
- [ ] Blood Ritual follows current workbook exactly: Everything sets max HP to 5 then grants Legendary Artifact; A Pool costs 15 current HP and grants Epic Artifact; A Drop costs 5 current HP and upgrades random Artifact by one tier. Confirm before applying costs; do not allow unintended death, do not heal from max HP reduction, and handle empty inventory/tier caps by an explicit approved rule. Quest is complete framework: purchase prerequisite, non-slot Contract, objective tracking, expiry before next map, success reward, failure cost, retry/reset. Infinite Mode begins after map 12 only by explicit choice; proposed continuation scaling is +10% HP / +4% ATK compounded per further map, with rounding, boss/encounter generation and reward bounds approved first.
- [ ] Use existing ownership/resolution hooks first; introduce a narrow service only when a missing seam is demonstrated.
- [ ] Add focused automated tests before wiring UI; preserve cancel, failure, reset and retry paths.
- [ ] Wire only necessary UI/scene affordance through existing bindings; avoid broad scene/YAML redesign.
- [ ] Run regression, inspect logs/diff, list unresolved decisions, and request phase acceptance.

### Files and reusable hooks
- `RunEventDefinition`/`RunEventUI`; `RunManager` shop and contract lifecycle; `PlayerRuntime`; result/restart UI; scaling and map/boss generation; narrow contract state.

### Dependencies
- Phase 7 accepted; do not pull later-phase behavior forward.

### Tests and acceptance
- Tests: Ritual lethal boundaries; Quest gating, single active contract, success/failure/expiry exactly once; Infinite opt-in/exit, deterministic beyond 12, cap and retry/reset.
- Acceptance: No partial ambiguous quest, forced endless mode or unbounded reward loop; exact decisions and determinism.
- Evidence: test counts, build/Bee result and diagnostics, smoke steps, Console classification, seed, baseline and changed files.

---

## Coverage ledger — 38 non-Implemented artifact records

IDs/names below were read from the current workbook with openpyxl and are mapped exactly once. Phase assignment is sequencing only, not implementation status. Phase 8 adds Blood Ritual/Quest/Infinite Mode, not another artifact row.

| Workbook ID | Workbook name | Planned phase | Tier / rarity as authored | Status |
|---|---|---:|---|---|
| `rel_111` | Retaliation Emblem | 7 | Tier 3 / Epic (upgrade from `rel_022`) | Planned; verify exact workbook condition/value at phase entry. |
| `rel_139` | Crown of Endurance | 7 | Tier 1 / TBD | Planned; verify exact workbook condition/value at phase entry. |
| `rel_138` | Kingslayer's Mark | 7 | Tier 1 / TBD | Planned; verify exact workbook condition/value at phase entry. |
| `rel_034` | Club Emblem | 5 | Tier 3 / Epic (upgrade from `rel_025`) | Planned; verify exact workbook condition/value at phase entry. |
| `rel_123` | Preparation Manual | 6 | Tier 1 / TBD | Planned; verify exact workbook condition/value at phase entry. |
| `rel_117` | Scavenger's Pouch | 6 | Tier 1 / TBD | Planned; verify exact workbook condition/value at phase entry. |
| `rel_129` | Scavenger's Satchel | 6 | Tier 1 / TBD | Planned; verify exact workbook condition/value at phase entry. |
| `rel_120` | Traveler's Pack | 6 | Tier 1 / Common | Planned; verify exact workbook condition/value at phase entry. |
| `rel_121` | Traveler's Pack | 6 | Tier 2 / Rare (upgrade from `rel_120`) | Planned; verify exact workbook condition/value at phase entry. |
| `rel_114` | Ace Emblem | 7 | Tier 2 / Rare (upgrade from `rel_014`) | Planned; verify exact workbook condition/value at phase entry. |
| `rel_115` | Ace Emblem | 7 | Tier 3 / Epic (upgrade from `rel_114`) | Planned; verify exact workbook condition/value at phase entry. |
| `rel_108` | Diamond Emblem | 7 | Tier 3 / Epic (upgrade from `rel_027`) | Planned; verify exact workbook condition/value at phase entry. |
| `rel_110` | Overflow Emblem | 5 | Tier 3 / Epic (upgrade from `rel_021`) | Planned; verify exact workbook condition/value at phase entry. |
| `rel_126` | Bounty Ledger | 5 | Tier 1 / TBD | Planned; verify exact workbook condition/value at phase entry. |
| `rel_125` | Cashback Token | 6 | Tier 1 / TBD | Planned; verify exact workbook condition/value at phase entry. |
| `rel_128` | Gilded Blade | 5 | Tier 1 / TBD | Planned; verify exact workbook condition/value at phase entry. |
| `rel_127` | Golden Vault | 6 | Tier 1 / TBD | Planned; verify exact workbook condition/value at phase entry. |
| `rel_124` | Merchant's Badge | 6 | Tier 1 / TBD | Planned; verify exact workbook condition/value at phase entry. |
| `rel_131` | Merchant's Gift | 6 | Tier 1 / TBD | Planned; verify exact workbook condition/value at phase entry. |
| `rel_136` | Challenger's Crest | 5 | Tier 1 / TBD | Planned; verify exact workbook condition/value at phase entry. |
| `rel_137` | Trophy Rack | 7 | Tier 1 / TBD | Planned; verify exact workbook condition/value at phase entry. |
| `rel_119` | Hunter's Ledger | 7 | Tier 1 / TBD | Planned; verify exact workbook condition/value at phase entry. |
| `rel_130` | Alchemist's Kit | 6 | Tier 1 / TBD | Planned; verify exact workbook condition/value at phase entry. |
| `rel_140` | Hammer | 7 | Tier 2 / Rare (upgrade from `rel_009`) | Planned; verify exact workbook condition/value at phase entry. |
| `rel_030` | Arsenal Emblem | 7 | Tier 3 / Epic (upgrade from `rel_029`) | Planned; verify exact workbook condition/value at phase entry. |
| `rel_122` | Field Medic's Kit | 6 | Tier 1 / TBD | Planned; verify exact workbook condition/value at phase entry. |
| `rel_105` | Heart Emblem | 7 | Tier 3 / Epic (upgrade from `rel_026`) | Planned; verify exact workbook condition/value at phase entry. |
| `rel_116` | Mimic Emblem | 7 | Tier 2 / Epic (upgrade from `rel_017`) | Planned; verify exact workbook condition/value at phase entry. |
| `rel_112` | Dwarf Emblem | 7 | Tier 2 / Rare (upgrade from `rel_013`) | Planned |
| `rel_113` | Dwarf Emblem | 7 | Tier 3 / Epic (upgrade from `rel_112`) | Planned |
| `rel_135` | Hidden Trail | 7 | Tier 1 / TBD | Planned |
| `rel_132` | Surveyor's Map | 7 | Tier 1 / TBD | Planned |
| `rel_133` | Wanderer's Boots | 7 | Tier 1 / TBD | Planned |
| `rel_134` | Warpath Banner | 7 | Tier 1 / TBD | Planned |
| `rel_109` | Hands Emblem | 7 | Tier 3 / Epic (upgrade from `rel_020`) | Planned |
| `rel_118` | Balancer's Scale | 7 | Tier 1 / TBD | Planned |
| `rel_106` | Spade Emblem | 7 | Tier 2 / Rare (upgrade from `rel_004`) | Planned |
| `rel_107` | Spade Emblem | 7 | Tier 3 / Epic (upgrade from `rel_106`) | Planned |

**Ledger integrity:** 38 distinct workbook records; 38 phase assignments; 38 unique IDs. Phase mapping checked against phase scopes; all rows retain Planned status. Tier and rarity shown are workbook values, including TBD where unset.

## Cross-phase invariants

1. Opening a UI is read-only; cached decisions do not consume RNG again.
2. Cancel/failure never charges, consumes, grants, or completes unless workbook explicitly requires it.
3. Every gameplay effect has one authoritative owner and resolution point; UI callbacks/retries cannot duplicate it.
4. Separate authored base stats, map scaling, elite modifiers, ally aura, temporary ability and runtime current stats; test each once and in order.
5. Tier upgrades preserve prior-tier effects only where the workbook says “Keep” (or its upgrade relation explicitly requires it); Arsenal/Club Epic behavior must follow the row definition, not a blanket retention assumption. Authoritative acquisition checks enforce duplicate/capacity rules.
6. Keep stack count, slot count, individual charges and charge-order distinct.
7. Permanent-looking growth is run-local unless separate meta-progression approval exists; never mutate shared ScriptableObjects.
8. Test defeat, victory, restart, node completion, map transition and scene re-entry cleanup.
9. UI description derives from same cached configuration the runtime executes.
10. Make numeric rounding explicit and test boundaries; no repeated-rounding drift.
11. Use deterministic random context/named streams and cache first-generated rewards/options.
12. Infrastructure-blocked tests remain unverified; do not weaken criteria or claim success.

## Final release/handoff gate

- Run all available EditMode/PlayMode tests and report discovered counts, skips and infrastructure failures.
- Build Windows development/release configurations where available; investigate actual Bee errors instead of citing historical builds.
- Smoke seeded run, combat, boss entry description, shop purchase, consumable/reward tray, map route, boss completion, retry/reset and Infinite opt-in if approved. Human interaction is required where interaction acceptance matters; automated callbacks are not human acceptance.
- Compare same-seed boss assignment/retry/re-entry and different-seed stream independence.
- Review final diff/status against original session baseline; update workbook only for verified rows when authorized.
- Handoff implemented scope, open decisions, deferred items, exact evidence/logs/manual checks. Do not auto-commit/push.

## Exclusions

- God content is deferred/unknown; do not invent scope or sequence it into these phases without a separately approved design.
- Do not revive rejected Fire Lantern/Cheater healing/light-to-tier suggestions or superseded boss abilities.
- Do not add speculative save persistence, broad generic frameworks, extra map depths, or unapproved UI redesign. Any staging/commit/push requires separate authorization; never do so automatically.
- Do not treat the 38-row ledger as proof all other workbook content is approved or implemented.
- No row/phase leaves Planned without runtime behavior, UI where applicable, tests, evidence and acceptance.
