---
id: kd_ef340882-97a5-496f-9dca-1e025bd7e73e
injectMode: inherit
injectAgents:
- unity
aiEditMode: inherit
---

# Combat & Shop Economy Rebalance handoff

## Status

All six implementation slices are present and compile. **Regression acceptance remains blocked**: Unity Test tools report disabled, and the user reports an error loading this workspace's settings, preventing enablement. Two runner attempts were rejected before execution. Luna completed the initial read-only architecture audit; its final review failed at the usage limit. No independent final review is claimed.

The user confirmed Perfect Parry uses the **whole enemy-response window**, with reward candidates snapshotted before defense so an empty Draw Pile cannot award a returned defense card.

## Implemented behavior and decisions

- **Perfect Parry:** sum effective committed card Block, including the existing Artifact/Enhancement calculation, across partial and multi-card submissions. Compare against positive modified incoming damage using the existing ordinary-aggregate versus Double Strike hit grouping. Award once on resolution, not for overblock, zero modified damage, rejected actions, or positive damage leaking through to HP/Shield. Preserve normal defense card recycling and existing effect timing. Reward draws only from the original window's deck candidates still in the Draw Pile, respecting normal hand capacity and never recycling or invoking overflow.
- **Checkpoints:** preserve five integer route depths and the existing twelve-map progression. Add visible fractional-column Shop nodes after Depths 2 and 5; all incoming routes merge through each checkpoint. Replace old random route Shop slots with Combat. Existing completion/Artifact visit hooks run once; route-depth accounting excludes checkpoints and Boss. No automatic purchase is required to leave.
- **Persistence:** a RunManager-owned dictionary keyed by map/node retains each Shop's offers, purchased identities, used Services, quantity/stake choices, refresh count and completion state. This is **run-local persistence**, not a new disk-save/Continue system.
- **Services:** exactly two distinct seeded types from Guidance, Quest, Investment and Supply. A separate map-seeded choice guarantees Supply at one checkpoint; the other can also roll it. Quest reuses Contract and retains its priced-purchase prerequisite and objective selection/cancel flow. Services are never rerolled and remain used after Goods refresh.
- **Supply:** one flexible-quantity purchase per Shop, 3 Gold per actual draw, bounded by real Gold, existing deck and normal hand capacity. The card model commits zones and payment before notifications; RunManager prevents reentrant/repeated service purchases. No discard recycling, generated cards or overflow.
- **Refresh:** generation-aware offers reject stale confirmations. Costs are 3, 5, 8, 12, 17, 23, 30…; count resets at a new checkpoint. Refresh changes Goods only and excludes Goods content purchased in that Shop. Unsold content can be rolled again when eligible. Existing eligible Artifact-upgrade preference and price modifiers remain. No authored rarity system was replaced.
- **Distribution:** configurable RunManager `midShopGoods` / `preBossShopGoods` vectors specify Artifact/Enhancement/Consumable slots, defaulting to 2/2/1 and 1/1/3. Every category has at least one slot, subject to eligible content remaining.
- **UI:** separate scrollable Goods and Services columns, quantity entry shared with the existing Investment control, paid Refresh button, clear availability feedback and five-depth HUD/map layout. No scene/prefab or content asset edits were needed.

No Boss HP/damage, starting player HP, Artifact authored price, global Gold reward value or Artifact/Enhancement effect definition was changed. Existing recycling remains only on its original paths.

## Verification actually performed

### Compilation and automated suites

- Final Unity compilation/domain reload: **successful**, Editor returned to Edit Mode.
- Final Unity Console query: **0 warnings, 0 errors**.
- `git diff --check`: passed before handoff.
- EditMode suite: **0 executed, 0 passed, 0 failed — blocked**, not a passing gate.
- PlayMode suite: **0 executed, 0 passed, 0 failed — blocked**, not a passing gate.
- Added **18 focused EditMode cases** (9 defense/strict draw, 8 economy, 1 UI) and updated fixtures affected by mandatory Shop topology and seeded Service pairs. They compiled but were not executed.

### Targeted runtime and presentation checks

These were direct staged runtime inspections/UI callbacks, **not Unity Test Framework results or a natural playthrough**:

- Traversed **10,493 routes across 240 generated maps**: every route contained five ordinary depths and both mandatory Shops.
- Inspected **240 map Shop pairs**: each Shop had two distinct Services; every map had Supply. **480 refreshed inventories** were unchanged by unrelated card RNG consumption.
- Exact parry smoke: effective Block 6, one committed defense card and one eligible reward draw; repeated Take Damage produced no extra draw.
- Additional staged parry cases: **3 passed / 0 failed** for post-modifier damage, initially empty Draw Pile, and cumulative partial submissions. Player HP stayed 30 in each exercised case.
- Supply via Shop UI: quantity 2, Gold **150→144**, Hand **6→8**, Draw Pile **32→30**; repeated purchase rejected.
- Six UI Goods refreshes charged **68g total**, advanced to **30g**, preserved Services and Supply's used state. Stale generation requests were rejected without payment. An initial same-frame scripting check selected an inactive row awaiting Unity destruction; selecting active rows across frames corrected the harness and the UI sequence passed.
- Final checkpoint leave: completion count **0→1 exactly once**, route depths completed remained **0**, successors unlocked and active node cleared; repeated leave did nothing. Existing completion hooks are preserved separately from depth count.
- Captured Game view at 1920×1080 and inspected the separate Shop sections plus the five-depth map with both Shops. No human mouse/scroll feel or other aspect-ratio claim.

## Changed files

### Runtime and UI

- `Assets/Scripts/Data/CardCollection.cs`
- `Assets/Scripts/Data/RunMapGenerator.cs`
- `Assets/Scripts/Data/ShopOffer.cs`
- `Assets/Scripts/Data/ShopCheckpointState.cs` (new, with Unity-generated `.meta`)
- `Assets/Scripts/Managers/CardManager.cs`
- `Assets/Scripts/Managers/CombatManager.cs`
- `Assets/Scripts/Managers/RunManager.cs`
- `Assets/Scripts/UI/PathScreenUI.cs`
- `Assets/Scripts/UI/RunStatusUI.cs`
- `Assets/Scripts/UI/ShopUI.cs`

### Tests

- `Assets/Tests/EditMode/CombatShopEconomyTests.cs` (new, with Unity-generated `.meta`)
- `Assets/Tests/EditMode/PerAttackerDefenseTests.cs`
- `Assets/Tests/EditMode/EarlyMapShopTests.cs`
- `Assets/Tests/EditMode/DevModeTests.cs`
- `Assets/Tests/EditMode/LowerEffortShopTests.cs`
- `Assets/Tests/EditMode/Phase2EliteRouteTests.cs`
- `Assets/Tests/EditMode/Phase6RegressionTests.cs`
- `Assets/Tests/EditMode/Phase7DMapVisibilityTests.cs`
- `Assets/Tests/EditMode/Phase7EShopSelectionTests.cs`
- `Assets/Tests/EditMode/Phase8QuestOfferTests.cs`

### Documentation

- `DEALBREAK_DEVELOPMENT.md`
- `Locus/knowledge/plan/combat-shop-economy-rebalance-report.md`

Pre-existing/unrelated work was not reverted or overwritten. Editor/tool-managed changes visible in TMP fallback font, Plastic metadata and Locus workspace metadata are not intentional economy deliverables. No staging, commit or push was performed.

## Remaining verification

1. Repair/enable workspace Unity Test tools, run focused `PerAttackerDefenseTests`, `CombatShopEconomyTests`, Shop selection/Quest/Investment tests, map/elite tests, then full EditMode and PlayMode suites. Full regression status is unknown; additional old fixture expectations may need updating.
2. Independent read-only final review once Luna quota is available.
3. Play an unaccelerated twelve-map run, checking empty/full deck and hand states, Double Strike, typed block modifiers, Shield/encounter Block interactions, lethal Retaliation, Shop exits, Quest cancellation, Investment settlement and refresh affordability.
4. Human keyboard/mouse checks for Supply quantity entry, Goods and Services scrolling, refresh/Buy/Continue, Enhancement confirmation after refresh and narrow viewport layouts.
5. Guidance may remain unavailable at the pre-boss Shop because no reachable hidden Event/Risk remains; Quest can also be unavailable under its existing prerequisites. This preserves Service mechanics rather than introducing new fallback rewards.

## Suggested playtest metrics

- Parry opportunities, exact/over/under-block rates, rewards denied by empty original deck/full hand, and HP saved per response.
- Gold on entering/leaving each checkpoint; spend split across Goods, Services and Refresh; unused Shop rate.
- Supply availability, purchase frequency, quantity, hand/deck exhaustion and effective cost per additional combat action.
- Refresh count/cost distribution, purchased Goods categories, unavailable offers, Artifact capacity and enhancement saturation.
- HP, hand and Draw Pile count on entering Bosses; deaths by depth/map; boss win rate and twelve-map completion time.
- Investment net return, Quest completion/failure, unavailable Guidance frequency and income attributable to existing Artifact visit hooks.

Do not change the restricted balance numbers until these observations establish whether further tuning is warranted.
