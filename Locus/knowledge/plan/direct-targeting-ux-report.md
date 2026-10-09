---
id: kd_ee5f1e8c-ea6f-49d3-b078-4b06391986d4
injectMode: inherit
injectAgents:
- unity
aiEditMode: inherit
---

# Direct board targeting — implementation and verification

## Result

Combat consumable targeting now uses the existing hand CardViews. Mirror, Torch, suit runes, and Enhancement consumables share a contextual prompt and explicit Confirm/Cancel. There is no second representation of a hand card in this flow. No scene, prefab, content definition, RNG, combat-resolution, route-connectivity, or card-zone rule was changed.

Outside combat, existing rules permit Mirror/Torch/Enhancement consumables to target owned cards outside the hand. That capability is preserved: a bounded off-hand list contains only cards not currently in hand, while hand cards are selected directly. Suit runes remain hand-only under existing rules.

## Audit classification

A = select an already-visible object directly. B = present choices/details not already visible; a lightweight panel is justified. C = intentionally modal interruption.

| Flow / source | Class | Finding / disposition |
| --- | --- | --- |
| Mirror/Torch hand targeting — EnhancementTargetUI | A | Previously rebuilt all owned cards, including hand cards, in the large generic picker. Converted to real-hand targeting in combat. |
| Enhancement consumables and suit runes — ActionButtonsUI | A | Separate callback/selection ownership, immediate Enhancement consumption and rune-slot confirmation. Consolidated under the shared contextual presenter; explicit Confirm/Cancel for all. |
| Shop/Upgrade/Event Enhancement card acquisition — EnhancementTargetUI | A | Already used real hand cards, unlike the older handoff documentation. Reused and improved its existing path, eligibility feedback, lifecycle cleanup, and compact prompt. Existing Enhancement replacement review/second confirmation preserved. |
| Noncombat owned-deck consumable targets — EnhancementTargetUI | A+B | Hand stays direct; only off-hand cards receive separate choice entries. No silent hand-only restriction. |
| Artifact merge — ArtifactRailUI / ArtifactIconSlotUI | A | Already used physical slots, but a click invoked both detail and merge callbacks. Fixed exclusive merge clicks, eligible/invalid/selected feedback, and lifecycle cleanup. |
| Defensive discard/block — ActionButtonsUI / CombatManager | A | Already direct hand selection with block preview and Take Damage. Preserved; normal combat actions are disabled only while a contextual hand target is active. |
| Ace pairing, normal play, enemy targeting/drop — CardManager / CardView / EnemyDisplayUI / CardActionDropTarget | A | Already direct. No duplicate picker or Ace rule change. |
| Route selection — PathScreenUI / PathNodeButtonUI | B presentation, A node input | Nodes are actual objects on the route map, not duplicated into a second picker. Full route panel retained; connectivity untouched. |
| Event discard-two and exact-total-value discard — RunEventUI.ShowDiscardSelection | A | Remaining anti-pattern: creates DiscardCard entries for visible hand cards. Deferred: its value-constrained transactional event/reward flow was not migrated into the consumable presenter. |
| Shop Sell — ShopUI.RefreshSellItems | A | Remaining anti-pattern: owned Artifact/backpack entries are recreated in the Shop sell list. Deferred: sale prices, selection/confirmation and source-instance ownership need a dedicated slot-sale interaction. Buy offers are not duplicates. |
| Consumable reward replacement — ConsumableRewardUI.RebuildReplacements | A | Remaining anti-pattern: recreates owned backpack slots as replacement buttons. Deferred: queued reward ownership/input gating differs from ordinary consumable use and must remain atomic. |
| Blood Ritual capacity replacement — RunManager.ShowBloodRitualReplacementPrompt | A | Remaining anti-pattern: owned Artifacts become generated Event replacement choices. Deferred: cost/reward/cancel semantics and Artifact instance mapping need a specific integration. |
| Diamond Emblem discard retrieval — RunEventUI.ShowArtifactDiscardChoice / CombatManager.OpenNextDiamondDrawChoice | B | Discard cards are not visible on the board. Separate choices remain legitimate. Existing blocking pending-action continuation retained; could be visually lighter. |
| Shop Buy offers / investment controls | B | New offers/terms, not copies of board objects. Retained. |
| Event/Risk choices; Upgrade Enhancement offers; Event Enhancement type choice; Hammer reward/retry/decline | B | New authored choices, not hand targets. Retained. Hand-target stage is direct. Generic panel size/darkness remains a possible later polish task. |
| Contract choice | B | New contract terms/objectives, not a second physical route-selection picker. Retained. |
| Consumable reward offers | B | New content choices; retained. Replacement-slot duplication is separately listed above. |
| Draw Deck viewer — DeckViewerUI | B | Read-only views of cards not visible in the hand; intentional browse panel, not a hand picker. Retained. |
| Artifact tooltip/detail | B | Optional information presentation. Retained for normal clicks; never opened by merge target clicks. |
| Boss reward banner | Not blocking | Notification of the exact earned card; no separate target picker. Retained. |
| Pause, Settings, destructive confirmation, terminal run results, development menu | C | Intentional blocking states coordinated by GameplayMenuCoordinator. Retained. |

## Ownership and rules

- CardManager.BeginHandTargeting owns the single contextual hand-click callback and eligibility predicate. Existing BeginHandEnhancementTargeting/IsHandEnhancementTargeting remain compatible names over the same ownership, not separate modes.
- Without a context, existing CombatManager/CardManager normal-play, Ace-pair, and defensive selection policies remain in control.
- EnhancementTargetUI owns source ConsumableInstance identity, stable selected card IDs, prompt, confirmation, cancellation, off-hand choices, and acquisition origin restoration.
- ActionButtonsUI launches/delegates targeting, including consumable drag-to-card, rather than maintaining a competing rune/Enhancement selection list.
- CardManager provides consumable eligibility, minimum/maximum, hand-scope, and whole-selection validation. UI never decides validity by display name or card Transform order.
- CardManager.GetArtifactMergeResult is shared by Artifact merge preview/eligibility and authoritative merge resolution. Runtime Artifact IDs remain authoritative.
- Source replacement/removal, selected target removal, state/node changes, route presentation, restart, disable, resolve and cancel clear the context. End clears target tint and selection on tracked views, not only the combat-selected list.

## Exact consumable interaction

1. Click a targeted backpack consumable, or drag it onto a valid hand card.
2. A small named prompt appears above the hand; the full-screen target-panel background is reduced to that bounded prompt.
3. Eligible cards receive a green outline/tint and an additive small lift. Invalid cards dim and suppress hover; selected cards retain the existing amber selection/lift. Sorting remains visual-only.
4. Click real CardViews to select/deselect. Single-target effects replace the selected target; multi-target effects stop at the gameplay-provided maximum.
5. Confirm is unavailable below the minimum or for an invalid whole selection. Confirm calls the existing authoritative operation; only successful resolution consumes a charge or changes cards. Cancel/Escape costs nothing.
6. Normal card input returns immediately. No animation completion is required for correctness.

## Files changed

- Assets/Scripts/Managers/CardManager.cs — shared targeting callback/presentation cleanup, consumable query facade, shared merge query.
- Assets/Scripts/View/CardView.cs — eligible/invalid/selected presentation layered onto existing hover/selection motion.
- Assets/Scripts/UI/EnhancementTargetUI.cs — unified contextual consumable and acquisition presenter; off-hand-only list; lifecycle/source validation; compact layout snapshots.
- Assets/Scripts/UI/ActionButtonsUI.cs — delegation and contextual action gating; removed separate direct-rune/Enhancement selection implementation.
- Assets/Scripts/UI/ArtifactRailUI.cs — merge eligibility feedback, query reuse, lifecycle cancellation.
- Assets/Scripts/UI/ArtifactIconSlotUI.cs — exclusive merge versus detail clicks and selection feedback.
- Assets/Tests/EditMode/DirectHandTargetingUITests.cs (+ Unity-generated .meta) — 10 focused query/UI/cancellation/merge tests.
- Assets/Tests/EditMode/RunProgressionTests.cs — migrated the existing restart test from removed ActionButtons private fields to the unified presenter, preserving pending-ID/context cleanup assertions.
- This execution report.

No assets were deleted. Existing serialized panel/list references are retained because off-hand choices and new Enhancement choices still use them. Retired code: ActionButtons' direct target slot/ID/view lists and begin/commit/cancel handlers, EnhancementTargetUI's old large consumable Show path, and all hand-card entry creation in its consumable picker. Unrelated workspace/Plastic metadata and other concurrent files were not intentionally edited. Nothing was staged, committed, or pushed.

## Automated verification

- Unity compilation/domain reload: successful.
- Pre-change full EditMode: 509 total, 488 passed, 21 failed.
- Final focused interaction/targeting/Artifact/defense/fan/run regression: 70/70 passed.
- Final full EditMode: 519 total, 498 passed, 21 failed. Failure names match the pre-change baseline exactly; all 10 new tests pass.
- Pre-change and final PlayMode suite: 6/6 passed.
- git diff --check: passed.

The full suite is NOT green. Unchanged baseline failures:

- BreadConsumableTests.UseBread_HealsUpToMaxAndConsumesOneOnlyAfterAnEffectiveUse
- DeckMutationTests.CatalogDefinitions_AreAvailableFromShopDropsAndEvents
- DeckMutationTests.InputGate_BlocksMutationWithoutSpendingCharges
- DeckMutationTests.Rune_SameSuitAndCancellationCostNothing_ThenSpendsThreeInOneSlot
- DeckViewerUITests.ClosedViewer_UpdatesCountWithoutCreatingSnapshotsThenRebuildsWhenOpened
- DeckViewerUITests.DrawAndReset_RefreshSnapshotListAndRestoreStartingDeckCount
- DeckViewerUITests.Layout_UsesMultipleRowsAndReadableCardScale
- DeckViewerUITests.Refresh_RendersExactlyTheAuthoritativeDeckInstancesAndCount
- DeckViewerUITests.Snapshots_AreNoninteractiveAndDoNotJoinHandSelection
- LowerEffortShopTests.Guidance_CostsFiveGoldRevealsOneReachableHiddenEventOrRiskAndIsOneTime
- Phase6ArtifactTests.TravelerPack_RareStacksThreeWithIndividualCharges_PartialFirst_AndShrinkIsWithdrawOnly
- Phase7ArtifactMechanicsTests.HammerRare_NoEligibleTargetOffersRetryOrDeclineWithoutSilentLoss
- Phase7BMultiEnemyTests.EnemyGroupUI_BindsOnePresentationPerActiveEnemy
- Phase7EArtifactCapacityTests.DuplicateRuleStillWinsAtCapacity_AndInvalidPriceDoesNotGrant
- Phase7EArtifactCapacityTests.NonSlotArtifact_IsOwnedButDoesNotConsumeCapacity_AndDuplicateStillRejected
- Phase7EEnhancementTargetTests.HandFieldClick_ArmsRealCardView_RequiresExplicitReplacementAndChargesOnce
- Phase7EEnhancementTargetTests.HandFieldTarget_CancelAndRestartClearPendingChoiceWithoutPayment
- Phase7EEnhancementTargetTests.UpgradeTargetCancel_DoesNotCompleteNode
- Phase8QuestOfferTests.ContractSelectionCancelKeepsGoldAndReturnsToCurrentShop
- Phase8QuestOfferTests.FailedAndFreeShopPurchasesDoNotUnlockContractOffer
- Phase8QuestOfferTests.ShopContractCostsTwentyAndOffersReachableEliteObjective

## Game-scene Play Mode smoke

Programmatic button callbacks and CardView pointer events were used in Assets/Scenes/Game.unity; these were temporary runtime experiments, not persistent scene edits.

Verified:
- Mirror opened with zero card-list entries; real-card click enabled confirmation; owned count increased 40→41 exactly once, source consumed, prompt/highlights cleared.
- Torch capped selection at two; cancel preserved ownership/source and normal card selection worked immediately; confirmation destroyed exactly two cards.
- A same-suit rune target dimmed and rejected selection; zero selection disabled Confirm; three valid cards changed suit together and cleared context.
- Off-combat Mirror presented exactly 32 off-hand choices for 40 owned/eight hand, while real-hand targeting remained active.
- Upgrade acquisition selected a real hand card and applied the Enhancement/completed the node with no lingering mode.
- Two matching Artifact slots selected and merged via Confirm; detail panel remained closed; merge controls cleared.
- Source removal and selected target removal closed the prompt and cleared input ownership.
- Victory cleared targeting; after resolving the legitimate reward choice, next encounter entered PlayerTurn with no context.
- Defeat entered GameOver with context cleared; same-seed restart returned Idle with eight cards and no target state.
- Rank/Suit sorts preserved authoritative hand IDs; hover worked; a drag began/ended back in hand without changing ownership.
- Normal Play entered defense, a selected hand card showed block contribution/remaining damage, Block and Take Damage were available appropriately, and defense returned PlayerTurn without targeting leakage.
- Final Console error query: zero errors. Editor returned to Edit Mode.

Visual capture at 1920×1080 showed the bounded Mirror prompt above the existing fan, board/enemies visible, and no duplicate hand grid/full-board dimmer. Human mouse feel and alternate aspect ratios remain unverified. The final smoke hand had no legal Ace pair; Ace selection is covered by the passing automated regression suite rather than a claimed live pair smoke.

## Remaining scope / next manual pass

The A-class Event-discard, Shop-sell, backpack-replacement, and Blood-Ritual replacement flows above remain intentionally unmigrated. They need their own transaction-aware input integrations; no claim is made that every gameplay panel has been removed. New-content choice panels, route browsing, discard retrieval, and the draw viewer could receive lighter visual treatment separately.

Manual feel check: enter combat, use Mirror, hover/select/cancel and immediately play or drag a card; repeat with Torch and a rune, sort while targeting, then test Upgrade replacement confirmation and Artifact merge. Repeat at a narrower Game-view aspect ratio. Check the existing baseline failures separately rather than treating historical green test counts as the current baseline.
