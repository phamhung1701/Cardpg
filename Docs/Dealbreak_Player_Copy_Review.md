# Dealbreak Player-Facing Copy Review

**Scope:** Issue #35 review of Main Menu, Run Setup, route map, Shop, combat guidance, and tutorial/help surfaces. The approved instruction for this pass was to remove the Main Menu tagline entirely; the DEALBREAK title remains authoritative. No gameplay rules, costs, seed behavior, or content identifiers were changed.

## Decisions and edits

- **Main Menu:** disabled the secondary tagline “A FULLY SEEDED CARD ROGUELIKE PROTOTYPE” at the user’s direction. The DEALBREAK title and primary navigation remain. This intentionally leaves no tagline because the user selected “remove tagline” rather than another definition.
- **Run Setup / seed:** retained the single actionable helper “Leave blank for a fresh random seed.” It explains what a blank field does; no redundant “A seed is…” definition was found. Custom seeds continue to display their normalized value.
- **Combat guidance:** shortened ordinary attack guidance to “Select a card to attack, or drag it to an enemy.” Removed the instruction to press the adjacent Attack button while retaining the distinct drag alternative. Shortened the Block prompt to “Block attacks with cards, or take the remaining damage.” The damage warning remains explicit.
- **Targeting:** replaced repeated “choose/click” wording with concise target and cancel guidance; confirmation remains explicit for applying a selected enhancement/trick.
- **Route map:** node buttons use short category/action labels (e.g. COMBAT, ELITE, SHOP, UPGRADE); hidden events/risks remain concealed. The screen retains actionable route/progress guidance.
- **Shop:** kept the GOODS and SERVICES headings, service-use limit, current price, unavailable reason, and descriptions for risk, cost, quantity, and outcomes. These are decision-critical rather than redundant.
- **Tutorial/help:** no standalone tutorial/help copy surface was found in the current scenes or runtime UI scripts; no new explanatory panel was added.

## Verification

- `GameSceneReadabilityTests` checks the 1920×1080 reference layout, text fit, and primary hit targets.
- `Phase7FrontendTests` retains blank-seed, custom-seed, normalization, and one-shot handoff coverage.
- A focused copy audit test verifies the DEALBREAK menu title, hidden tagline, and absence of the cited redundant phrases in visible menu/game text.

The tagline removal supersedes the prior tagline; there is no replacement short description in this pass.
