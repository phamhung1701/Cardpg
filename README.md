# CardPG

CardPG is a single-player, seed-driven card roguelike RPG prototype built with Unity. It combines card-based combat with branching routes and run-specific build choices. The project is actively evolving; descriptions here reflect the current prototype, not a finished or balanced release.

## The run

- Begin with a 40-card collection: Ace through Ten in each of the four suits.
- Travel through branching maps containing combat, Elite, Shop, Event, Upgrade, Risk, and Boss nodes.
- Fight through twelve major maps: four Jacks, then four Queens, then four Kings. Defeating a boss adds that exact face card to the current run.
- Manage persistent HP, your hand, and optional defense between encounters. Each encounter attempts to draw one card, subject to hand capacity.
- Collect Artifacts and apply Enhancements to individual cards to shape a build.

## Combat and builds

Play a card to attack an enemy. During the defense window, discard cards to block distinct incoming attacks, or choose to take the remaining damage. Recover is available with an empty hand. Encounters may contain multiple independently targetable enemies.

Ace pairing allows an Ace and one other card to be committed as a single attack, with deterministic seeded critical hits. The Hands Emblem adds a separate same-rank action: two or three matching-rank cards, with a total action damage cap. Other general poker hands are not part of the current rules.

Artifacts provide run-level effects; Enhancements belong to a specific card instance. A card can currently hold one Enhancement. The current approved Artifact content includes canonical IDs `rel_001`–`rel_014`, `rel_016`, `rel_019`, `rel_023`, and `rel_028`; Chain Armor reduces incoming combat damage before Shield, and Arsenal Emblem raises the hand limit by one. Ace Emblem sets the critical chance to 50% for any attack action containing an Ace, with one critical roll for the action. Retaliation Emblem counterattacks each source enemy whose attack was blocked for 5 damage. Current canonical Tier I Enhancements include Sharpened, Hardened, Mending, Quickdraw, Golden, and Auxiliary. Each in-hand Auxiliary gives +3 attack to card calculations, and each in-hand Golden gives 2 Gold once per attack action. Some prototype Artifact and Enhancement definitions are also retained. Tier upgrades, rarity-weighted offers, and much of the workbook's planned content are not implemented.

## Seeds and replay

New Run accepts an optional seed. Leaving it blank generates a fresh seed when the run starts; entering a seed lets you replay the corresponding deterministic run. Victory and Defeat results show the actual normalized seed and offer Retry Same Seed. Gameplay does not keep the seed prominently displayed during normal play.

## Open and run the project

1. Install **Unity 6000.3.24f1** with the project's required URP and Input System packages.
2. Open this repository in Unity Hub and allow the project to import and compile.
3. Open `Assets/Scenes/MainMenu.unity` and enter Play Mode. If starting directly in gameplay for development, open `Assets/Scenes/Game.unity`.

The project uses the Universal Render Pipeline and Unity's New Input System. There is no standalone release build or save/continue flow documented as part of this prototype.

## Development notes

- `CARDPG_DEVELOPMENT.md` is the implementation and design handoff for the current repository.
- `Locus/knowledge/plan/cardpg-effect-architecture.md` describes the shared typed Artifact/Enhancement effect system and its current limits.
- `CardPG_GameData_organized.xlsx` is the canonical content design workbook for overlapping Artifact and Enhancement definitions; spreadsheet plans are not proof that a row is implemented.

The runtime keeps card identity, ownership, and zones in model state rather than deriving gameplay from UI objects. Runs use independently derived deterministic random streams, and the shared typed effect foundation keeps Artifact and Enhancement behavior out of name-based combat branches.

## Project status

This is a playable prototype, not a final release. UI and artwork are placeholders in places, balance values are provisional, and there is no save system, online play, or cross-version determinism guarantee. Consult the development handoff before treating a planned feature as implemented.
