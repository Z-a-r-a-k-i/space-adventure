# POC HUD and dialogue visual-direction batch 05 — provenance

Status: retained under owner-delegated continuation

Generated: 2026-07-23

Generator: built-in Codex image-generation tool

Provider model, seed, and job identifiers: not exposed by the built-in tool

## Outputs

| File | Dimensions | SHA-256 |
|---|---:|---|
| `party-hud-component-direction-v1.png` | 1672 × 941 | `968ad235b4e61131bce544925283433bdf48423cee003e2885982e4458f7f94b` |
| `dialogue-ui-direction-v1.png` | 1672 × 941 | `819e4b2680f5ae6ba6ff5273137506df8d9a927b24c29afeb2575af722f05a68` |

These sheets define presentation composition only. The gameplay core owns
health, targets, destination, cooldown, pending-action, dialogue, availability,
and exchange state.

## Party HUD components

### Input image roles

1. `character-portrait-direction-v1.png` (SHA-256:
   `5b7d00b9d48a0e4aecf72f5380b5a05e16b823fedc72351767c1b77a130284ae`) —
   retained interim identities and portrait treatment.
2. `equipment-icon-direction-v1.png` (SHA-256:
   `44bc3d782b2a02534cd083e8c2c301527975634048a82278fbc00cf287e526c2`) —
   retained carbine and shotgun identities.
3. `tactical-marker-direction-v1.png` (SHA-256:
   `fa9f585c4b584c9343bacdfb21ce6564c843846140e77d476063bba75b5f8ab5`) —
   retained selection, movement, target, pending, and cooldown language.
4. `../../../concepts/frontier-station-v1/tactical-pause-combat.png`
   (SHA-256:
   `e891f3d4c738aa6074d71a5b889b42dc2ffb96b585551e77d6dd4b31b99badfc`) —
   tactical viewport contrast and restrained HUD character.

### Initial prompt

```text
Use case: ui-mockup
Asset type: party combat HUD component visual-direction sheet for the SpaceAdventure POC
Primary request: Create a cohesive six-panel component sheet that defines health, selection, current target, destination, cooldown, and one replaceable pending primary action. Image 1 provides the exact approved interim portraits; use only Vanguard and Protector for this example party and preserve their identities. Image 2 provides the exact approved carbine and shotgun icons. Image 3 provides the retained cyan selection, movement, hostile-red target, and cyan-plus-amber pending/cooldown language. Image 4 provides the tactical viewport contrast and restrained retro-industrial HUD character. This is presentation art for a two-character POC party; it must not invent an ability.
Scene/backdrop: one clean 3×2 grid on a perfectly flat very-dark navy field with subtle dividers; each cell contains one isolated HUD component on a transparent-looking dark neutral plate; no room, floor, full-body characters, scenery, dialogue, logo, or decorative dashboard
Components in reading order: (1) selected Vanguard party card: exact Vanguard portrait crop at left, broad warm-white health bar with a small dark depleted segment, thin cyan selected border, exact carbine icon in a compact socket, and one unobtrusive facing notch; (2) unselected Protector party card: exact Protector portrait crop, broad warm-white health bar at roughly two-thirds with a restrained amber low-health edge, exact shotgun icon, no cyan selection border; (3) current hostile target plate: simplified neutral dark gun-sentry thumbnail inside the approved broken red-orange target ring, one broad red-orange relationship notch, and a small dark health bar with restrained warm-red fill; (4) pending movement action chip: compact cyan path-and-destination symbol from Image 3 attached to a portrait-edge tab, with one amber replacement wedge showing that a newer primary action can replace it; (5) pending attack action chip: exact carbine silhouette plus approved hostile target brackets, joined by one short directional chevron, with the same amber replacement wedge; (6) cooldown/action-status socket: empty dark octagonal action plate with NO ability icon or implied power, a thick cyan radial progress arc, one amber pending wedge, and a clearly visible empty remainder. It demonstrates cooldown and pending state only.
Style/medium: crisp vector-like 2D game UI concept art with subtle beveled industrial plates, late-1990s Fallout-inspired retro-industrial science-fiction RPG with restrained cyberpunk accents; broad shapes, controlled highlights, minimal greebles, feasible as Godot Control nodes, nine-patch frames, textures, and simple shaders
Lighting/mood: UI surfaces are self-lit only; restrained cyan, amber, and red-orange accents close to edges; no scene lighting, lens flare, smoke, particles, glossy glass, or bloom cloud
Color semantics: selected/friendly/movement cyan; replaceable pending action amber; hostile target red-orange; health uses warm off-white fill with dark depletion, with only a restrained amber warning edge at low health; optional violet and destination green are absent because those states are not shown. State shapes must remain readable without color through borders, gaps, arc progress, and distinct component silhouettes.
Composition/framing: exact 3×2 grid; all components fully visible and generously padded; party cards share one consistent width and portrait scale; action chips and radial socket use matching stroke weight; design for a 1280×720 viewport and tactical distance
Constraints: exactly two party identities, Vanguard and Protector; no Operator or Survivor in this sheet; no ability icon, shield, magic, ability effect, skill name, arbitrary queue, second pending action, multiple stacked orders, inventory grid, ammunition, reload counter, minimap, quest list, dialogue box, text, letters, numbers, percentage, pseudo-writing, watermark, health hearts, fantasy ornament, or mobile-game rarity frame. The single pending primary action must visually read as replaceable, not as a queue. UI observes gameplay state and never defines it.
Avoid: inventing abilities, green health bars that collide with destination semantics, violet combat UI, four-person active party, photorealistic HUD, military avionics clutter, tiny illegible marks, glossy mobile buttons, screen-filling opaque panels, generic medieval RPG framing
```

Initial correction input:
[party-hud-component-direction-v1-initial.png](provenance-inputs/party-hud-component-direction-v1-initial.png),
1672 × 941, SHA-256
`7a790db33574658a745d5e483cc3d2613560cb0470925cbbb9bf49a6e92e0ab3`.

Result: revised because the unselected party card substituted Operator and a
carbine-like weapon for the requested Protector and shotgun.

### Targeted correction prompt

```text
Use case: precise-object-edit
Asset type: party combat HUD component sheet identity correction
Primary request: Correct ONLY the top-middle unselected party card in Image 1. It incorrectly shows Operator and a carbine-like weapon. Replace that portrait with the exact approved PROTECTOR portrait from the BOTTOM-LEFT panel of Image 2: very tall powerful Black male, close-cropped hair, trimmed beard, broad heavy armor, steady expression. Replace that card's weapon with the exact approved PROTECTOR SHOTGUN from the BOTTOM-LEFT panel of Image 3: broad two-handed shotgun with its distinctive chunky pump and approved proportions.
Invariants: preserve the exact 3×2 grid, dimensions, dividers, dark navy background, all other five panels, top-left Vanguard card, selected cyan outline, health bars, hostile target plate, pending movement chip, pending attack chip, cooldown socket, palette, lighting, framing, plate geometry, spacing, and every effect unchanged. In the corrected top-middle card preserve its existing unselected neutral border, health-bar amount and amber warning edge, portrait scale, weapon scale, and internal layout.
Constraints: top-middle must show Protector and his shotgun only; no Operator, woman, bun hairstyle, carbine, shield, ability icon, text, letters, numbers, logo, watermark, extra UI, or changed panel geometry. Do not modify any other content. Change only the specified portrait and weapon in the top-middle card.
```

Correction input roles were the retained initial output, the retained character
portrait sheet, and the retained equipment-icon sheet. The correction prompt's
Image 1, Image 2, and Image 3 correspond to those files respectively.

## Authored dialogue UI

### Input image roles

1. `character-portrait-direction-v1.png` (SHA-256:
   `5b7d00b9d48a0e4aecf72f5380b5a05e16b823fedc72351767c1b77a130284ae`) —
   retained Survivor and Vanguard identities.
2. `party-hud-component-direction-v1.png` (SHA-256:
   `968ad235b4e61131bce544925283433bdf48423cee003e2885982e4458f7f94b`) —
   retained plate, border, stroke, and restrained emission language.
3. `../../../concepts/frontier-station-v1/station-route-key-art.png`
   (SHA-256:
   `a540d65afb6144030c1177478a0f2b4653146f76ef4eb9a4daf65fbc7e7294b1`) —
   station contrast and amber Survivor association.

### Prompt

```text
Use case: ui-mockup
Asset type: authored-dialogue UI component visual-direction sheet for the SpaceAdventure POC
Primary request: Create a cohesive four-panel dialogue UI direction that clearly distinguishes NPC speech, available responses, an unavailable response, and exchange completion. Image 1 provides the exact approved interim portraits; use the station Survivor as the speaker and Vanguard as the responding protagonist while preserving their identities. Image 2 provides the retained retro-industrial HUD plate, border, stroke, and restrained emission language. Image 3 provides the dark frontier-station contrast and amber Survivor association. This is visual presentation only and must not invent dialogue content, facts, choices, or gameplay outcomes.
Scene/backdrop: one clean 2×2 grid on a perfectly flat very-dark navy field with subtle dividers; isolated UI components only with a faint desaturated station blur inside the largest speech plate, no complete room, full-body people, weapons, combat markers, logo, or decorative cockpit dashboard
Panels in reading order: (1) NPC speech block: exact Survivor portrait crop on the left in a warm-gray beveled frame with a tiny restrained amber identity notch, a broad dark speech plate on the right containing three clean warm-off-white horizontal content bars of varied length as NON-TEXT layout placeholders, and one small downward continue chevron; (2) available response list: exact Vanguard portrait crop in a compact left tab and three broad response plates stacked vertically, the first focused with cyan corner brackets, the second neutral but clearly available with a complete warm-gray border, and the third also available with a complete border; use horizontal placeholder bars only; (3) unavailable response state: three stacked response plates at matching scale, the first two available, the third visibly unavailable through lower contrast, an interrupted border, one amber constraint notch, and a small closed circular endpoint—do not use a padlock, red hostile color, or text; (4) exchange end state: a compact collapsed dialogue plate showing Survivor and Vanguard portrait tabs facing inward, empty center space, two inward-closing warm-gray chevrons, and a subdued amber-to-neutral status line that resolves into a single grounded endpoint; it should read as the conversation ending, not as mission completion or destination success.
Style/medium: crisp vector-like 2D game UI concept art with subtle beveled industrial plates, late-1990s Fallout-inspired retro-industrial science-fiction RPG with restrained cyberpunk accents; tactile warm-gray framing, broad readable shapes, reduced ornament, feasible with Godot Control nodes, nine-patch textures, portrait crops, and simple focus shaders
Lighting/mood: self-lit UI only; soft neutral portrait light, restrained amber on Survivor identity, cyan only on current response focus; no scene lighting, particles, smoke, lens flare, reflections, or bloom cloud
Color semantics: cyan means current selection/focus; amber means Survivor identity or constrained/unavailable information; dark neutral and warm off-white carry speech and response structure. Do not use destination green, optional-interaction violet, or hostile red. Availability must remain readable without color through complete versus interrupted borders, contrast, and endpoint shape.
Composition/framing: exact 2×2 grid; all plates fully visible and generously padded; portrait crops share consistent eye line and scale; response rows have large click targets and consistent width; components are readable at 1280×720 and leave most of the tactical viewport unobscured when implemented
Constraints: preserve Survivor and Vanguard identities; no actual dialogue text, pseudo-writing, letters, numbers, names, quotation marks, speech bubbles, branching tree, morality icon, persuasion percentage, dice, skill check, relationship meter, quest reward, mission-complete green, free-text input, keyboard, microphone, ability icon, shield, weapon, hostile target, minimap, watermark, or invented lore. Horizontal bars are layout placeholders, not legible text. Exactly one focused response and exactly one unavailable example. Dialogue UI observes validated dialogue state and never creates it.
Avoid: visual-novel full-screen portrait layout, giant opaque bottom panel, fantasy parchment, glossy mobile choice buttons, neon overload, terminal-green monochrome, cinematic subtitles, real words, gibberish glyphs, four-character active party, photorealistic UI
```

## Review boundary

- Portraits and item icons remain interim until matching approved production
  models can be rendered.
- The HUD demonstrates one current or pending primary action, never an action
  queue.
- The empty cooldown socket deliberately reserves no ability identity.
- Dialogue bars are non-text layout placeholders; authored copy and response
  availability remain content and gameplay concerns.
- Neither sheet is a runtime texture atlas or a final Control-node layout.
