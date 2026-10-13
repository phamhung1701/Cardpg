# Dealbreak UI Asset Generation Prompt Pack

**Purpose:** Ready-to-use prompts for optional, non-semantic UI decoration that complements the supplied Dealbreak Headliner/enemy illustrations. These prompts do not request new characters, functional controls, readable text, game symbols, map icons, logos, or gameplay data.

## Visual canon to attach as references

When the image-generation tool supports reference images, attach 2–3 of the supplied files representing different accent colors and roles, for example:
- `Assets/Art/Enemies/Scrapper.png` — rough street-level silhouette, orange/rust accent.
- `Assets/Art/Enemies/Riot Guard.png` — armored silhouette, slate/cream/ochre contrast.
- `Assets/Art/Headliner/The Charmer.png` or `Assets/Art/Headliner/The Chairman.png` — Headliner poster treatment with restrained coral or lime accent.

Use those files only as style references for ink weight, print texture, palette, and geometric composition. Do not ask the generator to redraw, alter, or replace the pictured characters. The art previews are 1792×2400 poster compositions; they are not UI panels or transparent portrait cutouts.

### Shared prompt rules

- Dealbreak style = modern underground neo-punk editorial print illustration: warm paper/ivory, slate blue-gray, near-black ink, bold angular cut-paper geometry, hard cel-shaded planes, thick contour, sparse halftone/stipple and dry-ink wear, occasional quiet accent hue.
- Keep all outputs free of words, letters, numbers, logos, characters, faces, card suits, fake UI controls, icons, and game-state symbols.
- Texture stays low contrast and secondary. Preserve a clean central/negative-space region for Unity text where relevant.
- No medieval/fantasy ornaments, brushed-metal sci-fi panels, neon glow, glossy gradients, pastel casual mobile look, soft pill shapes, or dense distressed noise.
- Import assets as UI textures/sprites only after visual review. The prompts are not a substitute for Unity-built 9-slice panels, buttons, icons, health bars, slots, or selection states.

## Prompt 1 — Paper Grain / Fine Halftone Mask

**Asset name:** `UI_PaperHalftone_Mask_01`

**Purpose:** Optional reusable low-frequency paper/halftone mask for a few large static UI plates or art-adjacent areas. Never place visible grain beneath small text or card values.

**Recommended dimensions/aspect:** 1024×1024 square, seamless/tileable. Produce a grayscale mask (white = texture visible, black = clear) plus a preview. Alpha not required for the grayscale master; Unity material controls opacity and tint.

**Reusable set:** Yes. It should tile and remain neutral enough to pair with both paper and slate surfaces.

**Prompt:**

> Create a seamless square grayscale texture mask for Dealbreak’s UI, using the attached Dealbreak character posters only as references for their subtle print grain and halftone treatment. Show sparse, fine comic-print halftone dots, lightly worn paper fibers, a few tiny dry-ink flecks and gentle unevenness. Keep dot size small and consistent, contrast very low, distribution balanced on all edges so the tile repeats invisibly. Leave the image visually calm; no large stains or focal marks. Flat 2D print texture, monochrome grayscale only, no lighting, no perspective, no shadows, no gradient wash. This is a reusable material mask, not a panel image. No text, symbols, objects, characters, outlines, borders, vignette, or color. Ensure it remains subtle at 25% opacity over warm paper or slate blue-gray.

**Avoid / review gates:** Reject obvious seams, large blobs, dense black patches, moiré, high-frequency noise, paper-yellow color baked into the texture, or any pattern that harms legibility. Test at actual UI scale before acceptance.

## Prompt 2 — Slate Poster-Field Texture Mask

**Asset name:** `UI_SlatePosterField_Mask_01`

**Purpose:** Optional slate-blue-gray print texture for a large combat banner backing, enemy-art surround, or Headliner preview plate. Unity sets the final surface color; the generated file supplies only texture modulation.

**Recommended dimensions/aspect:** 1024×1024 seamless square grayscale mask; no alpha required for master. A separate color preview may show muted slate but must not be the runtime source texture.

**Reusable set:** Yes. Low-contrast and neutral, so runtime tint can match the approved panel palette.

**Prompt:**

> Make a seamless 1:1 grayscale surface mask for a Dealbreak poster-style UI field. Use the supplied Dealbreak enemy and Headliner art as style reference: slate-blue-gray printed paper, bold angular cut-paper atmosphere, restrained halftone stipple, tiny scuffed ink and a few faint dry-brush imperfections. Translate this into a neutral grayscale tile only; no actual slate color baked in. Keep the center mostly calm and open for live Unity text or a character image, with very subtle edge-to-edge texture and no hard vignette. Balanced tileable edges, flat top-down 2D texture, reproducible at small and large scales. No text, typography, symbols, characters, portraits, UI frames, borders, buttons, lighting, gradients, neon, or perspective.

**Avoid / review gates:** Do not accept a complete poster composition, torn-paper framing, dark center vignette, strong colored pixels, opaque border, or texture noise visible through fine typography. Unity must own the panel shape and text.

## Prompt 3 — Dry-Ink Corner / Edge Accent Sheet

**Asset name:** `UI_DryInk_CornerAccents_Atlas_01`

**Purpose:** Optional one-off static print-wear accents for panel corners or banner edges, complementing the supplied posters’ distressed borders without baking any UI shape or content.

**Recommended dimensions/aspect:** 2048×1024 landscape transparent PNG atlas. Arrange 8–12 separated marks with generous gutters: irregular dry-brush corner scuffs, small halftone clusters, short broken diagonal ink strokes, and one or two slate/rust offset-print fragments. Export each as a sprite after review; do not use as a tiled repeat texture.

**Transparency:** True transparent background, clean antialiased alpha around each isolated mark. If the image tool cannot emit alpha, generate on a uniform chroma-key background only as an intermediate and mask carefully during art prep; do not ship a visible key color.

**Reusable set:** Yes, as a small decorative sprite atlas. Every mark must stand alone and be rotatable/flippable without showing a hidden rectangle. They are optional decoration, not interactive state graphics.

**Prompt:**

> Create a 2048×1024 landscape transparent sprite sheet of 8–12 separate, non-overlapping dry-ink print accents for Dealbreak UI. Use the supplied character posters as visual reference for bold black ink, rough halftone, scuffed print edges and sharp geometric fragments. Include a varied set of small clipped-corner scuffs, short broken diagonal strokes, sparse halftone wedges, and a few muted slate-blue-gray or brick-red offset-print chips. Keep every accent compact, separated by wide transparent gutters, with irregular hand-printed edges and clear silhouettes. Use near-black ink as the majority, with restrained slate and brick accent marks; no other bright colors. These are detachable decorative overlays to sit behind content at low opacity, not complete panels. True transparent canvas and clean alpha edges. No words, letters, numbers, logos, characters, faces, card suits, symbols, buttons, frames, complete borders, gradients, drop shadows, glow, or background fill.

**Avoid / review gates:** Reject joined marks, full rectangular backgrounds, oversized grunge, harsh red splatter, symmetrical ornamental corners, vector-clean corporate decoration, neon, or any mark that reads as an attack/target/status icon. Check the marks against the supplied art and remove anything that competes with character silhouettes.

## Non-generated P0 assets (build in Unity)

Do not ask an image model to make these assets: panel/frame family, button states, tooltips/detail frame, shop/inventory slot family, selection/highlight/target indicators, combat banners/HUD decorations carrying live text or values, map-node icon family, card states, HP/attack indicators, or any character portrait. Build these with Unity UI geometry, 9-sliced sprites, TMP, and current supplied art. Image generation is optional for the three decorative texture/edge assets above only.

## Prompt handoff and acceptance

1. Attach representative references from the supplied canon; do not upload only one color variant.
2. Generate several candidates for texture/edge pieces, then select the most restrained candidate that visibly harmonizes with the art at actual UI scale.
3. Export source and runtime files separately. Keep editable masters; preserve alpha for accent sprites. For masks, use linear/grayscale workflow appropriate to the eventual Unity material.
4. Verify seamless edges for tile assets; verify alpha halos and sprite boundaries for the accent sheet.
5. Test in the Combat mockup at 1920×1080 and a reduced 16:10/ultrawide layout. If the texture makes any text, suit, health bar, or target state harder to read, omit it.
6. No prompt in this pack authorizes generation or replacement of enemy/Headliner art, and no image output should be committed as part of the documentation task.
