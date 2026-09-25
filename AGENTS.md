# CardPG agent instructions

Place this file at the root of the CardPG Unity repository. It applies to the whole repository. A task prompt defines the work for that task; this file supplies project context and safety rules. Do not treat a proposed phase as implemented without checking the current code.

## Project at a glance

CardPG is a Unity card combat roguelike. A run has 12 maps and progresses through four Jack bosses, four Queen bosses, then four King bosses. A defeated boss awards the exact face card associated with that boss. The starting deck has exactly 40 cards: A through 10 in four suits, with no face cards. Cards have runtime identity and authoritative deck, hand, and discard zones.

Combat currently includes persistent player HP, optional defense by discarding cards, empty-hand Recover, multiple enemies with exact targeting, kill draws, secured rewards, and Thief fleeing. Runs use seeded random streams. Preserve existing mechanics and timing unless the task explicitly changes them.

## Where to look

- Start with the files related to the requested change and their tests. Read broader documentation only when the task needs it.
- `CARDPG_DEVELOPMENT.md` records milestones and history; verify its claims against the current repository.
- `Cardpg_GameData.xlsx` contains planned content. It is not proof that an effect is implemented or approved for rollout. Do not silently replace working behavior with workbook wording.
- Important areas to locate as needed: `Assets/Scripts/Managers/CombatManager.cs`, `RunManager.cs`, `CardManager.cs`; card/runtime models under `Assets/Scripts/Data/`; randomness under `Assets/Scripts/Randomness/`; tests under `Assets/Tests/`.
- For current behavior, the code, authored assets, and passing tests are primary evidence. For intended changes, follow the explicit task prompt and record ambiguities instead of guessing.

## Working rules

1. Check `git status` before editing. Preserve unrelated dirty changes. Make a focused diff and do not reformat unrelated files.
2. State the files and behavior in scope, inspect their callers and tests, then change the smallest coherent slice. Do not rescan the whole project, roadmap, and workbook for every small task.
3. Keep gameplay behavior deterministic. Preserve existing random streams and consumption order; previews must not change state or consume RNG. Do not use scene hierarchy, C# event subscription order, hash iteration, or Unity instance IDs to determine gameplay order.
4. Keep runtime state on runtime objects, not shared ScriptableObject definitions. Preserve serialized fields, GUIDs, `.meta` files, and scene/prefab references. For asset changes, use Unity APIs or careful verified edits.
5. Keep card ownership and zone transitions authoritative. A UI `CardView` may validate an interaction, but combat resolution should use the committed `CardInstance` and exact `EnemyRuntime` target.
6. Avoid content-ID checks in central managers. Prefer existing typed Artifact, Enhancement, and `EnemyAbility` authoring structures with focused behavior code. One mechanic must have one active execution path.
7. Compile and run relevant EditMode tests after meaningful changes. Use PlayMode tests or an explicit Unity smoke check when scenes, prefabs, input, or presentation are affected. Report what actually ran, its result, and what could not be run.
8. At the end, report changed files, behavior changes, tests, unresolved decisions, and risks. Do not claim a phase is complete based only on code inspection or historical test counts.

## Combat invariants

- Preserve the 40-card starting deck; J/Q/K progression and exact boss rewards; run/map/shop flow; current Artifact, Enhancement, enemy, defense, Recover, and presentation behavior unless explicitly changed.
- Ordinary surviving-enemy retaliation currently becomes one aggregate pending-damage instance. Do not split it into separate enemy attacks as a side effect of introducing hit resolution.
- Current numeric card damage is `(base card value + Enhancement attack bonus) × Artifact multipliers + Artifact flat bonuses`. Preview and execution must share the calculation. The example `(4 + 3) × 2 + 3` yields `17`.
- Preserve action timing where relevant: discard before on-play draw/recycling; Artifact side effects before Enhancement play effects; damage before a surviving target's post-card ability; kill draw before encounter progression; Thief timing per completed card action.
- Fleeing is distinct from death and grants neither kill draw nor defeated-enemy gold. Death, reward, and terminal encounter results must each happen once. The final boss reward precedes run completion.
- Event HP costs and explicit resource payments are separate from combat damage. They must not accidentally consume combat Shield or trigger combat reactions.

## Current planned work: Phase 7B.5

The next proposed implementation is **Combat Resolution Foundation**. Implement it only when the task explicitly authorizes implementation. Its goal is one committed action, one or more independently resolved hits, structured damage requests/results, deterministic ordered reactions, and safe death/action completion. Shield and multi-hit are minimal validation mechanics, not a content rollout.

For this phase:

- A multi-hit card commits once; card consumption, once-per-card effects, draw/heal/recycle, and turn counters occur once. Resolve each hit separately. Stop hitting a target once it dies; do not retarget automatically.
- Shield is encounter-local block-instance charges on player and enemies. One charge blocks one otherwise-positive combat hit completely; zero or already fully prevented damage consumes no charge. Apply Shield per hit, never per entire action.
- The **Recover–Shield interaction is an open gameplay decision**. Keep its policy explicit; do not present a temporary implementation choice as a final game rule.
- Keep reactions as a small, deterministic FIFO operation queue with a simple, observable runaway guard. Use phase, source type, stable ownership/acquisition order, then authored handler order; numeric priority is exceptional.
- Preserve existing behavior and RNG order throughout migration. Characterize fragile baseline behavior first, then add damage contracts, action context, ordered hooks, safe completion, Shield, multi-hit, and integration checks in coherent increments.
- Do not add Ace pairing, criticals, tiers, consumables, AoE, wildcard/effective suits, Shifting, new shop pools, or a generic effect framework as part of this phase.

If the current repository already completed part or all of this phase, inspect the implementation and tests before using these planned-work instructions. Update this section when the phase is accepted so it does not become stale.
