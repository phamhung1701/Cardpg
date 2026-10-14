# Dealbreak: Initial Balance Analysis

## Evidence and confidence

This is a **code-based risk assessment**, not a simulated balance verdict. No fresh full-run gameplay cohort was collected for this task, so there are no measured win rates, Gear rankings, or economy failure rates to report. The toolkit supplies repeatable collection and aggregation for those questions. All balancing values remain unchanged.

Inspected systems: `CombatManager` and `CombatResolver` (attack commitment, defense assignment, recovery, reactions, damage and victory); `CardManager` (Tricks, wallet and Gear inventory); `RunManager` (seeded encounters, progression, shops, investments and completion); enemy and infinite-map scaling; save restore boundaries. Existing tests cover multi-attacker defense, economy, progression and persistence.

## Potential difficulty spikes

1. **Group composition changes are sharper than base-stat scaling.** `RunManager.ShouldAddGoblinAlly` adds a Goblin to Knights with 50% probability starting at zero-based map index 3 (district 4). Shieldbearers always get a Goblin; War Drummers and elite Captains get two. Per-attacker Block assignment means a large block card cannot automatically cover several attacks. Compare district 3/4 Knight encounters by ally composition, HP loss and block waste before smoothing enemy stats.
2. **Enemy HP outpaces ATK growth.** `EnemyMapScaling` multiplies base HP by `1 + .20 * index` and ATK by `1 + .07 * index`, rounding away from zero. District 12 therefore uses 3.2x HP and 1.77x ATK before special effects. Without sufficient offensive progression, longer fights may compound incoming damage. Compare turns and HP loss jointly rather than treating ATK as the sole difficulty measure.
3. **Full-coverage Block is a threshold mechanic.** Restricted attacks require one sufficient card, unlike ordinary attacks that accept partial/multiple blocks. A small attack increase can invalidate many hand options, producing a discontinuous difficulty jump. Stratify full-coverage enemy encounters rather than averaging them with all elites.
4. **Infinite-mode attrition risk.** After district 12, HP scales by 1.10 and ATK by 1.04 per district with per-step flooring, while `InfiniteRunScaling.ScaleReward` keeps the base reward unchanged. This can increase fight duration and health pressure without matching income. The initial recorder stops at standard completion, so infinite-mode quantification needs a deliberately scoped follow-up capture mode; do not infer it from standard-run statistics.

## Candidate dominant or underperforming mechanics

- **Royal Family instant defeat:** bypasses ordinary damage resolution. Numerical DPS alone would underrate it; use `instant_defeats`, turn count and outcome. Compare setup opportunity cost and frequency before changing thresholds or rewards.
- **Shield and damage reductions:** can prevent large hits without appearing as card Block. The journal keeps these separate. A low block-efficiency number does not imply poor defense if shield prevention is high.
- **Overflow/bonus turns and reactive Gear:** can create high output per player action or prevent enemy responses. Attack count and turn entries have different denominators; do not divide total damage by only manually selected cards. Gear exposure can flag candidate combinations but cannot isolate individual Gear contributions.
- **Trick-dependent Gear:** Preparation Manual grants encounter Block based on distinct Trick types; Scavenger's Pouch and Field Medic's Kit reward the first encounter Trick use with a draw or healing. Compare hoarding vs first-use strategies with fixed seed cohorts. Low Trick use might indicate target restrictions, scarcity, or hoarding—not weak effects.

There is insufficient empirical evidence to declare any Gear or Trick definitively dominant/underperforming. Do not nerf or buff based solely on ownership/win correlations; strong runs acquire more items and survive long enough to expose them in more encounters.

## Economy bottlenecks and variance

- **Investment is positive expectation conditional on reaching settlement:** a 50% chance of a 3x stake payout yields 1.5x expected gross return (0.5x net profit), ignoring caps. Losing immediate liquidity can still prevent survival purchases; players who die before the next shop never realize this expectation. Analyze balances around stake/settlement nodes and include defeated runs, not just survivors.
- **Theft and return inflate gross flows:** theft uses `SpendCredits`; recovered stolen Credits use `AddCredits`. Refunds, sales and payouts also count in gross earned/spent. Net wallet change is informative; gross spend must not be labeled shop demand. This first version records node context but not detailed transaction categories.
- **Supply, rerolls and paid progression compete for liquidity.** Shop copy prices supply draws at 3 Credits per card; refreshes have their own state-based price. Check low balances around shop nodes against subsequent recovery usage and HP loss. Wallet data alone cannot establish inability to afford a desired offer; offer/declined-purchase capture is a possible follow-up, not a claim made by this toolkit.

## Recommended next steps (no changes applied)

1. Collect a fixed-seed cohort under normal settings with a documented strategy; repeat with a different strategy. Keep revision, seed and run IDs.
2. Inspect encounter strata with high HP loss/turns, then split by exact enemy group and Gear loadout. Treat tiny samples as leads, not conclusions.
3. If district-4 allies produce a reproducible jump, consider encounter-composition pacing before global HP/ATK reductions.
4. If long-fight attrition grows faster than build power, evaluate offensive acquisition cadence and shop accessibility before increasing every reward.
5. Compare Block capacity waste, shield prevention and remaining HP together; validate threshold enemies separately.
6. If investment dominates successful purchases yet increases early defeats, investigate the risk/reward presentation and liquidity trade-off before altering payout odds.
7. Only propose numeric tuning after observing distributions and repeatability; use matched seeds to evaluate it. Preserve this task's unchanged gameplay baseline.

## Verification limits

Compilation succeeded and all six Python aggregation tests passed. Unity EditMode tests were added but could not run because workspace test tools are disabled; proceeding with this limitation was explicitly approved. No automated gameplay simulation, interactive playtest cohort, or statistical confidence interval is claimed.
