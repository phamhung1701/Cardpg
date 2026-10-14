# Gameplay Balance Toolkit

## Record and export

1. Enter Play Mode at the main menu. Select **Tools > Dealbreak > Balance > Record New Runs** (checkmark means enabled). Enable **after** entering Play Mode because a domain reload resets static settings.
2. Disable development cheats. Start a **new** run using the desired seed. Play normally; no gameplay values or random streams are changed by the recorder.
3. A completed, defeated, abandoned, reset/restarted, or interrupted run exports automatically to `Application.persistentDataPath/BalanceTelemetry/<run-id>.json`. The menu **Open Export Folder** reveals that location. **Export Current Journal** writes an in-progress snapshot without ending recording; later exports replace the same run ID.
4. Copy journals from the same game/content revision into a collection directory. Keep the Git revision and playtester/policy notes alongside that collection. The journal includes Unity/game versions, but does not hash game assets.
5. Run from the repository root:

```sh
python -B Tools/Balance/analyze.py "path/to/journals" --output "path/to/analysis"
python -B -m unittest discover -s Tools/Balance -p 'test_*.py' -v
```

Use a separate output directory, not the journal directory. Outputs are `runs.csv`, `encounters.csv`, `hp.csv`, `usage_and_economy.csv`, and `summary.json`. No third-party Python dependencies. CSV strings that could be spreadsheet formulas are prefixed with an apostrophe; JSON retains original text.

For a development player, set `GameplayBalanceTelemetry.Enabled = true` from your existing development harness **before starting a new run**. `TryExport()` exports on demand. No new player-facing UI or scene components are installed.

## Metrics and interpretation

The version-1 JSON is a bounded event journal; `a/b/c/d` are typed by `kind`:

| Event | Meaning |
| --- | --- |
| `encounter_start/end` | Encounter kind / result; map and node are captured with the event. JSON map is zero-based; encounter CSV map is one-based. |
| `enemy` | Display name, starting HP (`a`), starting effective ATK (`b`); repeated rows retain group composition. |
| `player_turn` | Player-turn entry, including bonus turns and the opening turn; not completed actions or enemy turns. |
| `attack_action` | Successful card attack commitment; `a` is number of cards. Overflow attacks also count. |
| `damage_dealt/received` | Actual HP lost (`a`), requested damage (`b`), shield prevention (`c`), modifier prevention (`d`). Includes reactive damage. Overkill is not HP damage. |
| `instant_defeat` | Explicit instant-kill mechanic and victim HP before defeat; separate from numerical damage. |
| `block` | Actual blocked amount (`a`), committed calculated block capacity (`b`). Efficiency = SUM(a)/SUM(b); no block is blank/null, not zero. Shields and other reductions are separate. |
| `gear_exposure` | One row per owned Gear instance at encounter start, using canonical ID or asset-name fallback. This measures loadout usage, **not activations or causal contribution**. |
| `trick_used` | Definition ID for a successfully spent charge, including the Trick that ends combat. |
| `credits` | Signed actual wallet delta (`a`) and resulting balance (`b`). Positive deltas are earned, negative deltas spent/lost. Includes theft, refunds, sale proceeds and investments, not just shop purchases. Code calls currency Credits; these are the requested gold statistics. |
| `hp` | Current HP (`a`) and maximum HP (`b`) after notifications; includes initial health and healing/max-health progression. Notifies may repeat unchanged values. |
| `run_hp_cost` | Actual non-combat HP cost (`a`), separate from combat damage; do not double-count it with HP snapshots. |
| `lethal` | Damage origin, such as EnemyAggregate, Recovery or Reactive. Aggregate hits cannot identify an individual attacking enemy. |
| `invalid` | Forced defeat or reaction-processing fault; excluded from balance aggregates. |

Duration uses scaled Unity game time: time spent deciding counts, while a timeScale=0 pause does not. It is not CPU time or bot decision speed. Compare playtest durations only under comparable speed/pause conditions.

`summary.json` groups complete encounters by map and encounter kind, reporting sample count, defeat rate, HP loss, turns, seconds and weighted block efficiency. Completion rate includes only eligible terminal runs (`completed` or `defeat`). Interrupted/abandoned/in-progress runs are not defeats. Complete encounters from interrupted runs can contribute to encounter strata; this still introduces selection bias.

Truncated, development-modified, and invalid runs remain in raw CSVs but are excluded from aggregates. Duplicate run IDs and unsupported schemas are rejected rather than silently pooled. Repeated seeds are permitted: `unique_terminal_seeds` distinguishes coverage from replay count. Use the encounter CSV's enemy composition and Gear columns to subdivide map/type groups. Loadout correlations are not evidence that Gear caused a win.

## Boundaries and collection protocol

- Opt-in and event-driven; no per-frame recorder, RNG calls, combat duplication, balance edits, or save-schema changes. Disk export occurs at termination or explicit request, not on ordinary actions. At most 100,000 events per run; further events are dropped and `truncated` is set. Export failures warn and retain the journal for retry.
- Save/load continuity is deliberately unsupported: restoring a save interrupts an active journal and does **not** start a partial run mislabeled as complete. Record fresh runs for completion statistics. Infinite continuation after standard completion is not recorded; standard completion remains the terminal outcome.
- Disabling the checkbox affects future new runs; an already recording run finishes normally. Editor stop exports an interruption. Process crashes/domain reloads can lose events since the last manual snapshot. Exported telemetry is local only.
- Journals contain seeds and authored names/IDs, not player identity. They are observations, not replay files.
- Use a fixed game/content revision, normal development settings, a documented strategy, and the same seed set when comparing changes. Report sample sizes and distributions; use more than one playtester/strategy and inspect low-sample outliers before changing values.
- Full seeded automated gameplay simulation is intentionally deferred. RNG is deterministic, but valid actions require CardViews, Fields, singleton managers and interaction gates; events, replacement choices and save checkpoints need a scene-driven decision harness. A resolver-only bot would omit significant mechanics and misrepresent balance. The analyzer aggregates real runs across seeds without pretending to simulate them.

## Verification

- Unity full compilation/domain reload: successful.
- Python analysis tests: six passed (weighted block/actual HP, eligibility, incomplete encounters, schema/duplicate checks, CSV safety, defeat attribution).
- Six new EditMode tests in `Assets/Tests/EditMode/GameplayBalanceTelemetryTests.cs` cover wallet deltas, damage fields, JSON round-trip, bounded capture, idempotent finish/late Trick attribution, and disabled recording. **Not executed:** Unity Test tools are disabled in this workspace; the user approved proceeding with that limitation.
- When enabled, run that fixture plus `PerAttackerDefenseTests`, `CombatShopEconomyTests`, `RunProgressionTests`, and `PersistentRunSaveTests` in Unity Test Runner.
- Manual smoke check: enable recording in Play Mode, start a fresh seeded run, attack/block/use a Trick, buy an item and heal, then abandon. Export and verify wallet deltas match the HUD, actual HP loss excludes shield/overkill, block ratio is <=1, and abandonment does not enter the completion denominator. Repeat with a defeat, a lethal Trick, save/load and a dev-modified run. These interactive checks remain to be performed.
