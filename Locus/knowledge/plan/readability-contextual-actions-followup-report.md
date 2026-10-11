---
id: kd_c17e0673-1782-443a-8270-58fb4d99c138
injectMode: inherit
injectAgents:
- unity
aiEditMode: inherit
---

# Readability and contextual actions — follow-up verification

## Baseline and scope

The requested phase was already substantially implemented in HEAD `abb840d` (`Improve readability and contextual targeting UX`), following `97682ec` (direct hand targeting). This task audited and refined that committed implementation instead of introducing duplicate targeting systems. Read `AGENTS.md`, the relevant architecture/UI sections of `CARDPG_DEVELOPMENT.md`, and `plan/direct-targeting-ux-report.md`; inspected the live Game scene, major TMP labels, card/enemy/offer/node prefabs, managers, contextual presenters, selling authority and relevant tests.

No gameplay/effect definitions, economy, damage, eligibility, card ownership, selection rules, Ace pairing, routes or RNG were changed. Nothing staged, committed or pushed.

## 1. Typography hierarchy before / after

The existing Canvas uses Scale With Screen Size, reference 1920×1080, match 0.5. HEAD already had primary actions 24–26, titles 20–28, normal gameplay text 16–20, and secondary/run metadata 13–16. These were retained, not multiplied globally.

Remaining contextual weaknesses addressed:

| Element | Before this task | After |
| --- | --- | --- |
| Artifact merge labels | 10–14 autosize, 24-high buttons | 16–20 autosize, 40-high buttons |
| Artifact merge guidance | 9–12 | 16 |
| Artifact description | 85% of 14–18 autosized text | Fixed 18 body, fixed 22 bold name |
| Consumable description | 17, overlapping button reservation possible | 18, explicit separate text/button space |
| Consumable Use/Sell | 100×40 | 112×44, label 22 |
| Authored target Confirm/Cancel | Autosize minimum 12 | Minimum 20, maximum 22 |
| Attack authored preview / guidance | PLAY CARD / Press Play | ATTACK / Press Attack |

Artifact details now use a 240-wide adjacent panel with content-driven height. Consumable details use a 270-wide adjacent panel with content-driven height and a 64-unit bottom text reservation. On narrower screens, the panel stays above overlapping actual hand visuals rather than covering them. Its bounded per-frame placement uses a reusable CardView list and corner buffer.

## 2. Artifact detail behavior

Existing actual-slot selection and instance identity were retained. Selecting another owned Artifact updates the adjacent name/description and highlight. Empty slots have no detail action. Clicking the selected slot again or its close button clears details. Hover no longer adds a duplicate tooltip over selected details. Normal card selection and enemy targeting clear Artifact details. Enemy targeting prevents a competing merge mode. Consumable selection closes merge/details; beginning merge clears Consumable selection. The rail reserves additional vertical control space only when needed.

## 3. Consumable Use/Sell behavior

Retained existing select-first interaction: first click selects/highlights the owned instance and shows details; Use starts the existing definition-driven effect/targeting flow. It does not resolve on first click. Sell is visible but disabled/subdued outside Shop, with a Sell in Shop hint. Both actions have larger hit targets and no detail-text overlap. Source replacement/removal/reordering invalidates captured enemy-targeting context.

## 4. Shop-context ownership

Unchanged: `RunManager.CanSellItems` derives from the active incomplete Shop node. The contextual sale calls `RunManager.SellConsumable` with the selected exact instance; UI does not compute payout. Shop-panel active state is not selling authority. Outside-Shop command rejection and one-time payout were verified live.

## 5. Normal Attack targeting

Retained: ask `CombatManager.CanPlayCards` for valid targets; one valid enemy resolves immediately, multiple enemies enter the existing shared direct EnemyView mode. Actual EnemyView clicks resolve against the exact runtime enemy. The primary action becomes a clickable Cancel during targeting; Escape remains supported. Cancel preserves the selected attack cards. Guidance now consistently says Attack and actual targeted enemies say Click to target.

## 6. Offensive Consumable targeting

Retained: CardManager supplies definition-derived target type/count/eligibility; one valid enemy resolves after Use, multiple enemies share the same actual-enemy interaction as Attack. No consumable-name special cases or new picker were added.

Fixed `HandleEnemiesChanged`'s dangling `else`, which previously skipped rebuilding Consumable target candidates. Enemy-list refresh now retains a valid Consumable context. Deck/Consumable changes reconcile captured sources and cancel invalid contexts, rather than leaving dead highlights/input ownership.

## 7. Interaction ownership

No new gameplay state machine. `CardManager` still owns the mutually exclusive hand/enemy target callbacks. `ActionButtonsUI` retains the small Attack/Consumable target context and selected Consumable identity. `EnhancementTargetUI` owns the existing direct hand prompt, selected card IDs and Confirm/Cancel. `ArtifactRailUI` owns detail/merge selection and now explicitly clears it when competing interaction takes ownership. Combat/run managers remain authoritative.

## 8. Files changed by this task

- `Assets/Scenes/Game.unity` — Attack preview text; target Confirm/Cancel font bounds (Unity API edits).
- `Assets/Scripts/UI/ActionButtonsUI.cs` — target reconciliation/clickable Cancel, input guard, contextual typography/layout, narrow-screen hand avoidance and selection coordination.
- `Assets/Scripts/UI/ArtifactRailUI.cs` — adjacent detail typography, generated merge control sizes/spacing, tooltip and selection ownership cleanup.
- `Assets/Scripts/UI/EnemyDisplayUI.cs` — direct-target guidance and normal-guidance restoration.
- `Assets/Tests/EditMode/ContextualConsumableAndEnemyTargetingTests.cs` — six added NUnit cases for enemy-list refresh, removed/moved source, removed attack source, clickable cancel and text/button separation.
- `Assets/Tests/EditMode/ArtifactRailUITests.cs` — one added targeting/tooltip/merge exclusion case; generated readability assertions; compact detail width expectation updated to 240.
- `Assets/Tests/EditMode/GameSceneReadabilityTests.cs` — authored target-button minimum-size assertions.
- This report.

Pre-existing dirty fallback-font and workspace metadata changes and untracked documentation were not intentionally modified or reverted. A previously untracked Directory.Build.props was absent in the final status after interruption; this task did not remove it.

## 9. Tests and verification limitation

- Full Unity compilation/domain reload succeeded after source changes, including test sources.
- `git diff --check`: passed.
- Focused test execution was attempted twice via the dedicated test tool. Both attempts returned: **Unity Test tools are disabled for this workspace. Enable them in Settings > Unity Connection.**
- The tool-based permission question also failed to read workspace configuration. No test-tool setting was changed or bypassed.
- Therefore **neither focused automated tests nor the full EditMode/PlayMode suites ran in this task**. No current test pass count or no-regression claim is made.
- Read-only discovery before edits reported 528 EditMode / 6 PlayMode tests. The older direct-targeting report records 519 total / 498 passed / 21 failures and 6/6 PlayMode; that is historical evidence, not the current baseline.
- Required follow-up: enable test tools; run focused contextual/Artifact/direct-hand/readability fixtures, then the full EditMode suite and compare failure identities with the historical report (and preferably a clean HEAD baseline). Do not fix unrelated failures as part of this task.

## 10. Play Mode smoke

Programmatic real button callbacks and CardView/EnemyView pointer handlers in the Game scene, with temporary runtime encounter/item fixtures. No smoke setup was persisted into the scene or content assets.

Verified:

- Artifact click shows adjacent description/highlight; choosing a hand card clears it.
- Selected Consumable outside Shop: Use enabled, Sell disabled; no effect from selection.
- Shop selection: Use/Sell enabled. Mirror sold once: Gold 0→2, matching the existing price/2 payout; repeated callback paid nothing. Shop exit clears selection; authoritative outside-Shop sale rejected.
- Mirror: select→Use→actual hand card→Confirm; zero duplicate hand entries, owned cards 40→41, source consumed and targeting cleared.
- Offensive Consumable with two enemies: invalid outside-encounter target rejected; cancel costs nothing; real second EnemyView click changed HP 100/100→100/95, consumed once, cleared targeting.
- Normal attack with two enemies: Cancel preserved selection; re-enter and real first EnemyView click changed HP 100/95→91/95.
- One enemy: offensive item 100→95 and normal attack 95→90 without target mode.
- Enemy-list refresh retained a valid Consumable context. Removed/moved source or discarded attack card cleared targeting without applying damage.
- Victory→GameWon, defeat→GameOver, restart→Idle: no target/Consumable context remained; restart emptied backpack.
- Artifact merge and Consumable context exclude one another; enemy-target guidance restores on cancellation.
- Visual captures inspected at 1920×1080 and 1280×800. A discovered narrow-screen panel/hand overlap was fixed and rechecked successfully.
- Final Console error query: zero errors. Editor returned to Edit Mode, Game-view size restored to 1920×1080.

Representative local screenshots:

- `Library/Locus/Screenshots/locus_game_20261011_102123_750.png` — adjacent Artifact details, 1920×1080.
- `Library/Locus/Screenshots/locus_game_20261011_102239_406.png` — Shop Use/Sell, 1920×1080.
- `Library/Locus/Screenshots/locus_game_20261011_120020_264.png` — corrected Consumable/hand separation, 1280×800.
- `Library/Locus/Screenshots/locus_game_20261011_120042_568.png` — final direct enemy targeting and Cancel, 1920×1080.

## 11. Remaining polish / scope

The underlying broad readability pass was already committed. Small card stat/enhancement labels (13–15 / 12–14 reference sizes), dense Shop offer rows and very long descriptions still merit human readability testing. The narrower viewport scales text down and remains less comfortable than full HD. Not every Artifact description, boss name, event choice, localization, resolution or human mouse-hover interaction was visually exercised.

Existing non-target choice panels and deferred duplicated-object flows (Event discard selection, backpack reward replacement, Artifact Shop-sell list, Blood Ritual replacement) remain outside this refinement. No new large modal was introduced. Automated-suite access remains the material completion blocker.
