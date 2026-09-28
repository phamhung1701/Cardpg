# CardPG

CardPG is a single-player, seed-driven card roguelike RPG prototype built with Unity. It combines card-based combat with branching routes and run-specific build choices. The project is actively evolving; descriptions here reflect the current prototype, not a finished or balanced release.

## The run

- Begin with a 40-card collection: Ace through Ten in each of the four suits.
- Travel through branching maps containing combat, Elite, Shop, Event, Upgrade, Risk, and Boss nodes.
- Fight through twelve major maps: four Jacks, then four Queens, then four Kings. Defeating a boss adds that exact face card to the current run.
- Manage persistent HP, your hand, and optional defense between encounters. Each encounter attempts to draw one card, subject to hand capacity.
- Collect Artifacts and apply Enhancements to individual cards to shape a build.

## Combat and builds

Play a card to attack an enemy. During the defense window, discard cards to block distinct incoming attacks, or choose to take the remaining damage. Recover is available with an empty hand. Encounters may contain multiple independently targetable enemies. Ace pairing combines an Ace and one other card into a single seeded-critical attack. The Hands Emblem enables two- or three-card same-rank attacks; with the Royal Family Heirloom, the exact 10–J–Q–K–A sequence in one effective suit can instantly defeat its target. Other general poker hands are not baseline rules.

Artifacts provide run-level effects; Enhancements belong to a specific card instance. Cards can hold one Enhancement. The current build also includes tiered Artifact upgrades, charged Runes stored in three non-stacking backpack slots, and consumables that can be used during a run. Overflow auto-plays up to two cards (Common) or three cards (Rare) drawn while the hand is full, sharing that budget across nested draws within one player action. Rare Cheater grants one non-recursive bonus action before the enemy responds; Common Cheater retains its first-turn behavior. These are prototype systems and their values remain subject to balance review.

## Development milestones

Content milestones 1–4 are implemented in the current checkout, extending the existing seeded run and combat loop with permanent deck editing, charged Runes and other consumables in a three-slot non-stacking backpack, tiered Artifact upgrades, Hands and Royal Family group attacks, and bounded Overflow auto-plays and Rare Cheater bonus actions. Queen, King, and God are name-only workbook placeholders that still require gameplay specifications; existing authored bosses remain playable.

The remaining roadmap is:

- **Milestone 5 — Epic Arsenal / Club:** not implemented.
- **Milestone 6 — Blood Ritual:** not implemented.
- **Milestone 7 — Quests:** not implemented.

The most recent recorded regression checkpoint reports **329 EditMode tests and 3 PlayMode tests passing**. This is historical verification from the project notes; it has not been rerun as part of this README update.

## Seeds and replay

New Run accepts an optional seed. Leaving it blank generates a fresh seed when the run starts; entering a seed lets you replay the corresponding deterministic run. Victory and Defeat results show the actual normalized seed and offer Retry Same Seed. Gameplay does not keep the seed prominently displayed during normal play.

## Open and run the project

1. Install **Unity 6000.3.24f1** with the project's required URP and Input System packages.
2. Open this repository in Unity Hub and allow the project to import and compile.
3. Open `Assets/Scenes/MainMenu.unity` and enter Play Mode. If starting directly in gameplay for development, open `Assets/Scenes/Game.unity`.

The project uses the Universal Render Pipeline and Unity's New Input System. There is no standalone release build or save/continue flow documented as part of this prototype.

## Dev Mode

The Editor sandbox is available from `Assets/Scenes/Game.unity` via **CardPG → Development → Dev Mode**. Windows builds made with **Development Build** enabled also expose a Dev Mode entry on the Main Menu. Choose an optional seed and the Infinite Health, Infinite Money, and All Eligible Shop Offers toggles, then select **Start Dev Run**; ordinary New Run remains unaffected. During an active dev run, the top-of-screen DEV MODE banner and the DEV control open the in-run panel. Cheats are off on application launch, reset when returning to the Main Menu or starting a normal run, and remain enabled across Retry Same Seed. **Disable Cheats** affects only future actions; it does not restore spent HP/Gold or reverse purchases. **Lose Run Now** requires confirmation and routes through the ordinary defeat result.

To make a Windows test build, select **File → Build Profiles → Windows → Build**, enable **Development Build**, and build to a test folder. Development controls and cheat hooks are compiled out of ordinary non-development player builds. A Development Build is intentionally not protected from its users. Dev overrides do not bypass Artifact capacity, duplicate prevention, enhancement eligibility, or purchase validation. The Editor shop browser remains an Editor-only tool.

## Development notes

- `CARDPG_DEVELOPMENT.md` is the implementation and design handoff for the current repository.
- `Locus/knowledge/plan/cardpg-effect-architecture.md` describes the shared typed Artifact/Enhancement effect system and its current limits.
- `CardPG_GameData_organized.xlsx` is the canonical content design workbook for overlapping Artifact and Enhancement definitions; spreadsheet plans are not proof that a row is implemented.

The runtime keeps card identity, ownership, and zones in model state rather than deriving gameplay from UI objects. Runs use independently derived deterministic random streams, and the shared typed effect foundation keeps Artifact and Enhancement behavior out of name-based combat branches.

## Project status

This is a playable prototype, not a final release. UI and artwork are placeholders in places, balance values are provisional, and there is no save system, online play, or cross-version determinism guarantee. Consult the development handoff before treating a planned feature as implemented.
