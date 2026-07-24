# POC UI and effects visual-direction batch 04 — provenance

Status: retained under owner-delegated continuation

Generated: 2026-07-23

Generator: built-in Codex image-generation tool

Provider model, seed, and job identifiers: not exposed by the built-in tool

## Outputs

| File | Dimensions | SHA-256 |
|---|---:|---|
| `character-portrait-direction-v1.png` | 1254 × 1254 | `5b7d00b9d48a0e4aecf72f5380b5a05e16b823fedc72351767c1b77a130284ae` |
| `equipment-icon-direction-v1.png` | 1254 × 1254 | `44bc3d782b2a02534cd083e8c2c301527975634048a82278fbc00cf287e526c2` |
| `tactical-marker-direction-v1.png` | 1672 × 941 | `fa9f585c4b584c9343bacdfb21ce6564c843846140e77d476063bba75b5f8ab5` |
| `hostile-telegraph-direction-v1.png` | 1672 × 941 | `ab2ee72355d3f7bf3d765cf7020b671832fa70d6ea3127f89eb04bc898144d6c` |
| `combat-healing-vfx-direction-v1.png` | 1536 × 1024 | `692d83dd01a53acda664fe44777dfaa9aa4986478f41028af87d45e309e1fff6` |
| `station-state-feedback-direction-v1.png` | 1536 × 1024 | `bca39d46a5ac9149e01286ac2b58b4c698a927d8e5bf4919098ee277a76ef958` |

These sheets are visual references, not runtime textures or gameplay
authority. Portrait and equipment-icon directions should be replaced by
approved-model renders when practical.

## Character portraits

### Input image roles

1. `../poc-models/vanguard-character-turnaround-v1.png` (SHA-256:
   `66858ffda50cb37a113d6a3eeb66165fb57de02d60372a7b1551008bd349d0db`) —
   Vanguard identity and fixed outfit.
2. `../poc-models/operator-character-turnaround-v1.png` (SHA-256:
   `9cc780e475d427792562566f9a080646ac7fb27005a7605d1a89259c3e245b31`) —
   Operator identity and fixed outfit.
3. `../poc-models/protector-character-turnaround-v1.png` (SHA-256:
   `56c5bd24c0cf40fc59094f429899297435e55d5f056a1903c2a20e5271fcfb51`) —
   Protector identity and fixed outfit.
4. `../poc-models/station-survivor-turnaround-v1.png` (SHA-256:
   `3b54a69c21a563fd9685737d44ac5e55330e8ca1a0e06186655ee96f780ad169`) —
   Survivor identity and fixed outfit.

### Prompt

```text
Use case: stylized-concept
Asset type: interim party-and-NPC portrait direction sheet for the POC dialogue and party UI
Primary request: Create four polished square bust portraits that faithfully preserve the approved identities and outfits from the four supplied turnaround sheets. Image 1 is Vanguard; Image 2 is Operator; Image 3 is Protector; Image 4 is the station Survivor. Do not blend identities, genders, ages, ethnicities, hair, armor, or accessories between references. These are interim portrait directions; final production portraits may later be rendered from approved 3D models.
Scene/backdrop: one clean 2×2 portrait sheet with four equal square panels, subtle dark navy-to-charcoal studio backdrops, restrained retro-industrial frame edges, no environment, text, names, icons, logos, or UI labels
Subject placement: top-left Vanguard, top-right Operator, bottom-left Protector, bottom-right Survivor. Each panel shows one head-and-upper-torso bust at matching scale, facing slightly toward the center while looking toward camera. Preserve exact facial identity, apparent age, skin tone, hair, beard where applicable, body/shoulder proportions, collar, chest armor or work vest, and role-specific accessory visible in the approved turnaround. Vanguard: sturdy bearded male with textured undercut and broad warm-gray armor. Operator: athletic woman with high dark bun, side fringe, lighter fitted armor and shoulder sensor. Protector: very tall powerful Black male with close-cropped hair, trimmed beard, broad heavy armor, no shield. Survivor: older warm-brown-skinned woman with salt-and-pepper textured side-shaved hair, civilian work vest, amber shoulder lamp, no combat armor.
Expression: calm alert neutral expressions suitable for dialogue and party HUD; Vanguard focused, Operator analytical, Protector steady, Survivor tired but intelligent. No smile exaggeration, shouting, or combat action.
Style/medium: polished stylized low-poly 3D character portrait render, late-1990s Fallout-inspired retro-industrial science-fiction RPG with restrained cyberpunk accents; clean authored game-portrait finish; preserve the supplied model-sheet identities rather than redesigning them
Lighting/mood: consistent soft three-quarter key light and subtle rim separation; party portraits receive restrained cool cyan edge accents, Survivor receives restrained amber edge accent; faces remain naturally readable with no colored wash, fog, bloom, or dramatic shadows
Composition/framing: head, shoulders, and upper chest fully visible with generous padding; consistent eye line and scale; simple beveled square frame around each panel suitable for later cropping; no overlapping panels
Constraints: exactly four people, one per panel, in specified order; faithful identities and fixed outfits; no weapons, shield, helmet, hands, hologram, scenery, duplicate person, extra limbs, text, lettering, numbers, watermark, faction symbol, speech bubble, health bar, selection ring, or invented jewelry; Survivor must remain visibly civilian; portraits must remain distinct at small HUD size
Avoid: identity drift, beauty retouching, photorealism, anime style, painterly brushwork, heroic action poses, exaggerated emotions, modern fashion photography, uniform generic faces
```

## Equipment icons

### Input image roles

1. `../poc-models/vanguard-carbine-turnaround-v1.png` (SHA-256:
   `fe6cb280507202cd63e1b72ebf6f1e6329ad165ab1ea96e0b0e517d195c9b099`) —
   approved carbine identity.
2. `../poc-models/operator-pistol-turnaround-v1.png` (SHA-256:
   `e03b8688bdf1472262849cd7c6bcc1b4350f3fb652c15380b1b06175d6272238`) —
   approved pistol identity.
3. `../poc-models/protector-shotgun-turnaround-v1.png` (SHA-256:
   `89c5555855dccb07b46d5eb2a7a642fff2c02b78f963f89a777a1c0da5b101ce`) —
   approved shotgun identity.
4. `../poc-models/field-aid-turnaround-v1.png` (SHA-256:
   `c00a16542e4b97932f816fb65e157251ca3855c28b67424bcec545d3831bc48f`) —
   approved field-aid identity.

### Prompt

```text
Use case: stylized-concept
Asset type: POC equipment-icon direction sheet for party HUD and healing-item UI
Primary request: Create four polished square inventory/action icons that faithfully preserve the approved separate assets from the supplied reference sheets. Image 1 is Vanguard's broad two-handed carbine; Image 2 is Operator's compact pistol; Image 3 is Protector's broad shotgun; Image 4 is the selected handled field-aid medkit. Do not blend silhouettes or redesign any asset. These are icon directions; final icons may later be rendered from approved production models.
Scene/backdrop: one clean 2×2 icon sheet with four equal square dark navy-to-charcoal panels and subtle beveled retro-industrial frame edges; no environment, characters, hands, text, names, numbers, logos, labels, or UI bars
Subject placement: top-left carbine, top-right pistol, bottom-left shotgun, bottom-right field-aid medkit. Each icon contains exactly one complete object at generous scale, using a consistent front-right three-quarter product angle and consistent studio grounding. Preserve every asset's approved broad silhouette, proportions, material separation, grips, muzzle, stock, pump where applicable, medkit handle, corner caps, front latch, and restrained status accents.
Style/medium: polished stylized low-poly 3D hard-surface game-icon render, late-1990s Fallout-inspired retro-industrial science fiction with restrained cyberpunk accents; crisp silhouette, controlled bevel highlights, reduced small detail, readable at 64–96 pixels
Lighting/mood: consistent soft neutral studio key with restrained cyan edge separation for weapons and restrained amber edge separation for field aid; no colored wash, bloom, dramatic shadows, fog, muzzle flash, or healing effect
Composition/framing: objects centered diagonally within square frames, complete and uncropped, with even padding and consistent apparent scale by visual weight. Weapon muzzles point safely toward upper-right and remain visible; medkit stands upright in front-right three-quarter orientation. No human scale silhouettes in these icon panels.
Color palette: approved dark navy/charcoal mechanical bodies, warm-gray protective plates, restrained neutral metal, tiny cyan weapon accents, tiny amber and muted cyan medkit indicators; dark desaturated background; no hostile red, destination green, optional violet, or bright white field
Constraints: exactly four panels in specified order; one object per panel; faithful approved identities; carbine and shotgun must remain visibly two-handed and distinct; pistol must remain compact; field aid must not resemble a toolbox or ammunition case; no ammunition, loose magazine, shells, scope, sling, shield, hands, character, duplicate item, extra attachment, projectile, tracer, glow effect, medical cross, lettering, numbers, watermark, or scenery
Avoid: generic weapon silhouettes, photorealistic product photography, flat monochrome pictograms, anime icons, loot rarity borders, fantasy ornament, excessive glow, noisy greebles, cropped objects
```

## Tactical markers

### Input image roles

1. `../../../concepts/frontier-station-v1/tactical-pause-combat.png` (SHA-256:
   `e891f3d4c738aa6074d71a5b889b42dc2ffb96b585551e77d6dd4b31b99badfc`) —
   existing combat readability, selection, movement, and hostile-state language.
2. `../../../concepts/frontier-station-v1/station-route-key-art.png` (SHA-256:
   `a540d65afb6144030c1177478a0f2b4653146f76ef4eb9a4daf65fbc7e7294b1`) —
   route, optional-interaction, utility, and destination semantics.

### Prompt

```text
Use case: ui-mockup
Asset type: tactical command-marker visual-language sheet for the SpaceAdventure POC
Primary request: Design a cohesive set of six clean Godot-authored tactical markers based on the supplied game concepts. Image 1 is the existing combat readability, cyan selection ring, movement path, and hostile-red telegraph reference. Image 2 is the route, optional-violet terminal, cyan utility, and destination-green semantic reference. Refine these into one consistent retro-industrial tactical UI family without reproducing either room or any character.
Scene/backdrop: one clean 3×2 grid on a perfectly flat very-dark navy background, each cell containing exactly one isolated marker centered with generous padding; subtle thin dividers only; no floor, environment, props, people, text, labels, numbers, logos, or HUD panels
Markers in reading order: (1) selected-friendly unit marker: cyan double-ring with four broad segmented brackets and a small facing notch; (2) movement path: broad readable cyan dashed curve ending in a solid directional chevron and compact destination footprint; (3) optional interaction highlight: restrained violet angular corner brackets plus a soft inner outline, clearly different from selection; (4) destination/completion marker: green doorway-shaped open chevron surrounding a grounded endpoint disk, clearly different from interaction; (5) hostile target feedback: red-orange broken ring with four inward threat ticks and a strong directional notch; (6) pending-action/cooldown marker: dark circular base with a thick cyan progress arc, one amber queued-action wedge, and a clear empty remainder, with no numbers.
Style/medium: crisp vector-like 2D game UI concept art with subtle beveled industrial thickness, restrained soft emissive edge, late-1990s tactical science-fiction RPG character, clean enough to reproduce procedurally in Godot shaders and line geometry
Lighting/mood: self-lit UI marks only; restrained bloom confined close to edges; no lens flare, particles, smoke, reflections, or cast shadows
Color semantics: friendly/select/movement cyan; optional interaction violet; destination/completion green; hostile/invalid red-orange; pending action cyan with one amber queue accent. Never swap these meanings. Shapes must remain recognizable without color.
Composition/framing: exact six-cell grid, consistent stroke weight and visual scale; every marker shown as a complete top-down symbol; use broad arcs, gaps, corners, and chevrons that survive 1280×720 tactical view and color-blind adjustments; no tiny glyphs
Constraints: UI markers only; no character silhouettes, weapons, shields, ability icons, health hearts, minimap, cursor arrow, text, letters, numbers, percentage, labels, watermark, faction logo, environment, 3D floor, photorealistic lighting, or baked gameplay range. Marker geometry is visual style, not authoritative distance or timing.
Avoid: fantasy runes, neon cyberpunk overload, mobile-game glossy buttons, military HUD clutter, fine dotted noise, full circles distinguished only by color, realistic hologram projection, screenshots of the game
```

Terminology correction: the exact generation prompt above used
`queued-action wedge` and `queue accent`. Those phrases describe the amber
replacement cue only and are superseded by the accepted
single-replaceable-pending-action contract. They do not authorize an action
queue or stacked orders.

## Hostile telegraphs

### Input image roles

1. `../poc-models/security-ram-drone-turnaround-v1.png` (SHA-256:
   `f90dbc7c7ec58509077da1af1e50e1ef22789e4c10faf6bdfde1bdab2a2663cf`) —
   approved ram-drone identity and front contact source.
2. `../poc-models/security-gun-sentry-turnaround-v1.png` (SHA-256:
   `89872e6b84578fd213f9dc10b17d8905c88ca6621592e55f5c575f7a96f8d101`) —
   approved gun-sentry identity and integrated muzzle.
3. `tactical-marker-direction-v1.png` (SHA-256:
   `fa9f585c4b584c9343bacdfb21ce6564c843846140e77d476063bba75b5f8ab5`) —
   retained tactical-marker stroke, emission, and palette language.

### Initial prompt

```text
Use case: ui-mockup
Asset type: hostile attack-telegraph visual-language sheet for the two approved POC machine archetypes
Primary request: Design a cohesive six-panel top-down telegraph sequence for the approved body-ram drone and integrated-gun sentry. Image 1 is the exact ram-drone visual and reinforced front contact source. Image 2 is the exact gun-sentry visual with separate sensor and muzzle. Image 3 is the approved-in-direction tactical-marker stroke, glow, and color language. Telegraph geometry communicates visual style only; it must not establish gameplay range, width, target validity, or timing.
Scene/backdrop: one clean 3×2 grid on a perfectly flat very-dark navy background, subtle dividers only; top row shows ram-drone presentation phases, bottom row shows gun-sentry presentation phases; no environment, floor texture, text, labels, numbers, logos, health bars, or characters
Top row — body ram: (1) compact top-down ram-drone silhouette with a restrained red-orange source ring at its reinforced front bumper and a backward brace/compression cue; (2) wind-up telegraph as a broad short red-orange lane extending directly from the bumper, with thick broken edges, one large direction chevron, and a decisive capped contact zone; (3) release/contact visual as a bright compact bumper-origin shock wedge at the lane end followed by a darker broken recovery ring, without depicting damage or moving the machine through animation
Bottom row — integrated gun: (1) top-down sentry silhouette with a small red-orange muzzle-origin notch and restrained tracking arc separate from its sensor; (2) wind-up/aim telegraph as a narrow long red-orange sight corridor from the muzzle with two broad parallel edge lines, a clear forward arrow, and an outlined endpoint bracket; (3) release/recovery visual as a brief bright muzzle pulse, one thin cosmetic tracer line, and a darker recoil/recovery arc around the gun mount, without an impact explosion
Style/medium: crisp vector-like 2D tactical UI and VFX concept art, subtle industrial bevel thickness, restrained localized emission, late-1990s tactical science-fiction RPG visual language, reproducible with Godot line geometry, meshes, particles, and shaders
Lighting/mood: self-lit telegraphs on dark neutral field; brighter amber-red at imminent release and darker red-orange during early wind-up/recovery; restrained bloom only; no fog, smoke, cinematic lighting, or floor reflections
Composition/framing: exact 3×2 grid; each cell complete and generously padded; consistent top-down scale within each machine row; broad shapes and gaps readable at 1280×720 and 20 m tactical view. Machine silhouettes are simplified neutral dark shapes with enough approved outline to identify source direction.
Color semantics: hostile telegraphs use red-orange with a small amber-hot release accent; inactive/recovery portions become darker desaturated red. No friendly cyan, optional violet, destination green, healing teal, or ability colors.
Constraints: source-specific telegraphs only; ram signal must originate from armored bumper, not sensor; gun signal must originate from visible muzzle, not sensor; wind-up, release/contact, and recovery must remain visually distinct; no baked exact distance values; no damage numbers, blood, gore, shield, ability effect, area explosion, selection ring, friendly path, projectile model, environment, text, letters, numbers, watermark, or photorealistic game screenshot
Avoid: identical telegraphs for both enemies, full red opaque floor fills, fine laser-grid clutter, fantasy runes, neon overload, missile warning UI, military scope reticle, giant explosions, authoritative hitboxes presented as art
```

Initial correction input:
[hostile-telegraph-direction-v1-initial.png](provenance-inputs/hostile-telegraph-direction-v1-initial.png),
1672 × 941, SHA-256
`adf2fc72609526a5a7b33bd9071c386faa4b684afd90581c6b5c8a67058e33ad`.

Result: revised because the bottom-row sentry drifted from the approved tripod
into a four-legged top-view silhouette.

### Targeted correction prompt

```text
Use case: precise-object-edit
Asset type: hostile attack-telegraph visual-language sheet correction
Primary request: Correct ONLY the gun-sentry machine silhouettes in the entire bottom row of Image 1. They currently read as four-legged cross-shaped machines. Replace each bottom-row machine with a faithful simplified TOP-DOWN silhouette of the approved THREE-LEGGED tripod gun sentry from Image 2: one central chassis, exactly three sturdy legs spaced approximately 120 degrees apart, one integrated gun projecting forward to the right, and a rear counterweight/head housing. Keep the gun muzzle at the exact origin of every existing bottom-row telegraph. Do not change the top-row ram drone.
Invariants: preserve the exact 3×2 grid, panel dividers, dark navy background, all red-orange source arcs, aim corridor, endpoint bracket, muzzle pulse, tracer, colors, glow strength, spacing, scale, framing, and sequence. Preserve all top-row content unchanged. Preserve the bottom-row telegraph geometry unchanged except for minimal alignment needed to keep it attached to the corrected muzzle.
Constraints: exactly three legs on each bottom-row sentry silhouette; no fourth leg, humanoid limbs, separate weapon, wheels, or tracks. No text, labels, numbers, logos, UI bars, environment, characters, new effects, or watermark. Change only the specified bottom-row machine silhouettes and their necessary muzzle alignment.
```

Correction input roles were the retained initial output and the approved gun-
sentry turnaround. The correction prompt's Image 1 and Image 2 correspond to
those files respectively.

## Combat and healing VFX

### Input image roles

1. `equipment-icon-direction-v1.png` (SHA-256:
   `44bc3d782b2a02534cd083e8c2c301527975634048a82278fbc00cf287e526c2`) —
   retained equipment palette, material, and scale language.
2. `../../../concepts/frontier-station-v1/tactical-pause-combat.png` (SHA-256:
   `e891f3d4c738aa6074d71a5b889b42dc2ffb96b585551e77d6dd4b31b99badfc`) —
   tactical effect scale and encounter context.

### Prompt

```text
Use case: stylized-concept
Asset type: combat-and-healing VFX visual-language sheet for Godot-authored POC effects
Primary request: Create six cohesive isolated effect directions that complement the supplied approved equipment-icon and tactical-combat references. These are visual style targets for Godot particles, meshes, trails, lights, and shaders; they must not encode damage, hit detection, gameplay timing, or ability mechanics.
Scene/backdrop: one clean 3×2 grid on a perfectly flat very-dark navy background with subtle dividers; exactly one isolated effect centered per cell; no environment, floor, characters, weapons, machines, text, labels, numbers, logos, or HUD panels
Effects in reading order: (1) compact pistol muzzle flash: short asymmetric warm-white core with restrained amber forks and one tiny cyan ion edge; (2) heavier carbine/shotgun muzzle flash: broader angular warm-white/amber blast with a chunky pressure ring, still compact enough for tactical view; (3) cosmetic tracer/projectile: one clean fast streak with warm-white head, amber core, and rapidly fading thin cyan tail, no missile body; (4) generic metal/armor hit effect: directional fan of broad amber sparks, two small warm fragments, and a restrained red-orange impact crescent, no explosion; (5) ram-drone body-contact effect: low wide red-orange shock wedge with a white-hot contact line and two short mechanical debris sparks, clearly a physical bumper impact rather than magic; (6) healing-item effect: compact upward soft-teal and amber pulse made from two broken rings, three broad rising motes, and one gentle central cross-like negative-space glint without drawing a literal medical cross or using destination green.
Style/medium: crisp stylized low-poly/tactical 2D-and-3D VFX concept art, late-1990s retro-industrial science-fiction RPG with restrained cyberpunk accents; bold readable shapes, limited particle count, short local glow, feasible in Godot without flipbook-heavy simulation
Lighting/mood: self-lit effects only on neutral dark background; warm physical weapon/impact energy, red-orange hostile contact, soft teal-plus-amber healing; restrained bloom close to cores; no fog field or cinematic lighting
Composition/framing: exact six-cell grid, generous padding, complete effects with no cropping, consistent visual weight; show each as a single representative peak frame with a faint secondary trail indicating decay. Effects must remain identifiable at 1280×720 and tactical distances.
Color semantics: weapon and ordinary hit effects use warm white/amber with tiny cyan technology accents; hostile body impact uses red-orange; healing uses soft desaturated teal plus amber and explicitly avoids destination green, optional violet, and hostile red.
Constraints: effects only; no shield, ability-specific effect, fireball, lightning spell, grenade, explosion cloud, smoke wall, blood, gore, damage number, projectile weapon model, selection ring, telegraph lane, environment, text, letters, numbers, watermark, or photorealistic fire. Muzzle flashes, tracers, impacts, and healing are cosmetic presentation driven by observed gameplay state.
Avoid: neon overload, giant opaque blooms, realistic gunpowder photography, fantasy magic, medical green cross, rainbow palette, dense particle noise, screen-filling effects, lens flare, anime speed lines
```

## Review boundary

- Portrait and equipment sheets are interim references until matching approved
  production models can be rendered.
- Tactical markers and effects define style and color semantics, not gameplay
  state, distance, timing, collision, or target membership.
- Hostile telegraphs remain conditional on accepted Phase 4 attack shapes and
  timings.
- Ability-specific icons and effects remain blocked until their mechanics are
  accepted.

## Station state feedback

### Input image roles

1. `../station-service-terminal-turnaround-v1.png` (SHA-256:
   `b433bae19a05a506257692b0e9e5c13235295cb3306acfb56a2166eb15c85503`) —
   approved terminal identity and violet optional-interaction surface.
2. `../poc-models/station-structure-kit-reference-v1.png` (SHA-256:
   `f518b123fc6b2f67dda71cb846cad76aa334617c72bfec5e52f4fd817e4e3738`) —
   approved station structure and cyan route-strip family.
3. `../poc-models/evacuation-airlock-reference-v1.png` (SHA-256:
   `27dca5bb57d4087188feb3423efad4b5288623c8301a06d64f4f92754baabd3b`) —
   approved airlock identity and destination-green surfaces.
4. `tactical-marker-direction-v1.png` (SHA-256:
   `fa9f585c4b584c9343bacdfb21ce6564c843846140e77d476063bba75b5f8ab5`) —
   retained tactical-marker glow and stroke language.

### Prompt

```text
Use case: ui-mockup
Asset type: station material-and-state feedback visual-direction sheet for Godot-authored POC presentation
Primary request: Create a coherent before/after state sheet for the approved service terminal, route strip, and evacuation airlock. Image 1 is the exact approved terminal identity and violet optional-interaction surface. Image 2 is the approved station structure and cyan route-strip family. Image 3 is the exact approved airlock identity and destination-green surfaces. Image 4 is the retained tactical-marker glow/stroke language. Preserve all object identities; change only engine-driven light, material, outline, and state presentation.
Scene/backdrop: one clean 2×3 grid on a very-dark navy neutral studio field with subtle dividers; left column shows inactive/closed baseline, right column shows active/available/completed state; no full room, people, text, labels, numbers, logos, HUD bars, or gameplay cursor
Top row — optional service terminal: left shows the approved terminal in idle state with violet screen very dim, cyan strip subdued, no outline; right shows the identical terminal available for inspection with a readable violet screen, restrained violet angular interaction outline close to its silhouette, and one slightly brighter cyan status strip. No text or screen UI.
Middle row — station route strip: left shows one approved protected route-strip insert mounted in a small neutral floor-channel sample, dark and nearly inactive; right shows the identical insert active with clean cyan segmented light flowing in one direction through three broad sections and a subtle forward chevron rhythm. No arrows floating above the floor and no destination green.
Bottom row — evacuation airlock: left shows the approved airlock closed with green status strips dim and control panel idle; right shows the identical airlock fully open with leaves retracted, clear opening, brighter destination-green header and side strips, and a restrained grounded green completion threshold. No force field or changed geometry beyond the approved open state.
Style/medium: polished stylized low-poly 3D presentation mockup with crisp vector-like Godot material and outline effects, late-1990s Fallout-inspired retro-industrial science fiction with restrained cyberpunk accents; broad tactical-camera readability, feasible with Godot materials, lights, meshes, and simple shaders
Lighting/mood: dark neutral studio presentation; state surfaces provide localized restrained emission and subtle nearby spill only; no cinematic fog, bloom cloud, lens flare, reflections, or baked environment lighting
Color semantics: optional interaction violet only on terminal; route/utility cyan only on route and minor terminal status; destination/completion green only on airlock. Shapes and state changes must remain readable without color through brightness, outline, segmentation, and open/closed silhouette.
Composition/framing: exact 2×3 grid; each before/after pair uses identical scale, angle, framing, base materials, and object geometry except the approved airlock leaves changing state; full objects or isolated module samples visible with generous padding
Constraints: no hostile red, survivor amber, shield, ability effect, selection ring, enemy telegraph, character, weapon, scenery, text, letters, numbers, watermark, screen glyphs, invented terminal controls, extra route modules, force field, particles, or gameplay range. These states are presentation responses to observed gameplay state, not gameplay authority.
Avoid: color meanings swapped between rows, giant bloom, neon overload, photorealistic lighting, holographic UI, complete isometric game screenshot, altered asset designs, green terminal, violet door, cyan destination
```
