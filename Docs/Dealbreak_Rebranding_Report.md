# Dealbreak Branding and GameData Rework Report

**Status:** Rebranding/content-name pass completed for matched content. The confirmed source of truth was `Dealbreak_GameData_IDs_Terminology_Fixed.xlsx`. Gameplay values, serialized IDs, asset filenames/GUIDs, class/field/enum names, and save keys were preserved. Unresolved GameData/runtime conflicts are recorded below.

## Updated

- Updated README and active project guidance to use Dealbreak terminology and point to the confirmed workbook; historical CardPG technical/design documents now identify their terminology as superseded.
- Updated **120 ScriptableObject display names** from the Rename Index, including Gear, Mods, Tricks, Incidents, implemented Enemies, and the twelve generated Headliner names. Updated **113 descriptions** from workbook fields; the 13 Incident assets also received workbook descriptions and choice labels/results. For Gear/Mods/Tricks, descriptions use labeled workbook cells (Trigger/Condition/Effect/Value/Limit or the applicable category fields).
- Updated runtime player-facing labels for Credits, Gear, Mods, Tricks, Black Market, Incident, District, Enforcer, Headliner, and Perfect Block; changed the service labels to Stake, Intel, and Contract.
- Updated Main Menu title/credits and Game-scene labels, including Gear, Black Market, Headliner, Incident, and Credits text. `ProjectSettings.asset` already had `productName: Dealbreak` in the pre-existing worktree change; it was not edited here.
- Updated affected test expectations to the new canonical names/labels.

## Missing or mismatched content

- Workbook rows **Ghost Lens (`gear_035`)** and **Dealbreaker (`gear_036`, Legendary)** are Draft and have no matching Unity Gear asset. `Dealbreaker` remains distinct from the game title and was not fabricated. **Network Map (`gear_132`)** is explicitly deferred and has no matching asset.
- The workbook lists `Dealbreak ID` → `Legacy Unity ID` mappings. A read-only audit matched 117 content crosswalk objects; **44 runtime `id` fields differ from the workbook’s Legacy Unity ID**. The mismatches are 10 Gear, 9 Mod, 6 Incident, and 19 Trick entries. For many Mods/Tricks and some Gear, `canonicalId` matches the workbook’s legacy ID while the `id` used by runtime/save lookup is a descriptive slug; the four early Gear entries (`artisan_tools`, `golden_compass`, `reinforced_sleeves`, `victory_draught`) have blank `canonicalId` and differ from `rel_101`–`rel_104`. Incident assets also retain descriptive runtime IDs such as `roadside_cache` rather than `evt_001`. These values were deliberately not changed.
- Three implemented/acceptance-pending Enemy names were synchronized: Goblin → Scrapper, Thief → Pickpocket, and Master Thief → Ghost. Draft Enemy renames (Knight/Security Guard, Brute/Heavy, Shieldbearer/Riot Guard, War Drummer/Hype Man, Duelist/Challenger, Goblin Captain/Crew Leader, Royal Knight/Riot Captain) were left unchanged because current runtime logic branches on some legacy `enemyName` values. Refactoring those checks is a separate task. `EnemyTypeData` has no serialized description field; workbook ability-description text was not moved into a new field or used to alter current AI behavior.
- Twelve generated Headliners now use workbook names by rank/suit. `headliner_013` / The House remains an undefined optional placeholder and was not added as an encounter. Headliner names and ability/scaling proposals are marked Draft in the workbook; no proposed mechanics were implemented.
- The workbook guide says IDs remain stable, while the Rename Index provides new canonical prefixes (`gear_*`, `mod_*`, `foe_*`, `headliner_*`, `incident_*`, `service_*`) mapped to legacy IDs (`rel_*`, `enh_*`, `enemy_*`, `boss_*`, `evt_*`, `shop_*`). The runtime lookup/save IDs may additionally be descriptive slugs. A migration must resolve this conflict explicitly; no IDs were changed here.
- The four early Gear rows (`gear_101`–`gear_104`) provide names but no structured effect/description fields. Their display names were updated, but existing descriptions remain pending authoring rather than being invented.

## Conflicts and future implementation

- Runtime has a Supply Service, absent from the workbook’s canonical Services list.
- Workbook Contract cost is **10 Credits**, while runtime charges **20 Credits**. This task changed terminology/display text only; the value mismatch remains for a separate gameplay decision.
- The workbook’s newly proposed Enemy roster, Headliner ability pool, district scaling, and other Draft content remain unimplemented. Workbook Gear rows also include Draft, deferred, and acceptance-pending entries; status must be checked row-by-row before treating them as shipped.

## Serialization risks and verification

- Save capture/restore resolves Gear, Mods, Tricks, and Incidents by existing `id` fields; their identifiers were left intact. Renaming IDs or asset files would risk save compatibility and Unity references. Remaining legacy identifiers include `RelicData`/`ArtifactRuntimeInstance`, `CardEnhancementData`, `ConsumableData`, `MapNodeType.Boss/Elite/Event/Shop`, old field names such as `gold`/`ownedArtifacts`, and the `CardPG.DevMode.*` PlayerPrefs prefix. `Assets/Editor/GameDataGenerator.cs` still contains legacy generated content names/descriptions despite its updated menu titles; do not run it until it is updated to use the workbook. These items are candidates for a separate serialization-aware refactor.
- Unity compilation and domain reload completed successfully. Five focused EditMode tests passed: canonical asset immutability, canonical content definitions, Game-scene readability, 12-Headliner progression, and result-screen summary/retry. Two stale assertions were updated to the new Gear/credits/District labels; the full test suite was not run.
- Git boundary approved: stage the rebranding task files and touched documentation; leave `.plastic` metadata and all unrelated pre-existing changes unstaged.
