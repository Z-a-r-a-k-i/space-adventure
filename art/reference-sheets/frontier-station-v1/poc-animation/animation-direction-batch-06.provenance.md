# POC animation visual-direction batch 06 — provenance

Status: retained under owner-delegated continuation

Generated: 2026-07-23

Generator: built-in Codex image-generation tool

Provider model, seed, and job identifiers: not exposed by the built-in tool

## Outputs

| File | Dimensions | SHA-256 |
|---|---:|---|
| `vanguard-weapon-handling-key-poses-v1.png` | 1672 × 941 | `f618192f0f52aec1f1aeba4aa2658deb7ebd1f18d97b695d870e89db459a2c22` |
| `operator-weapon-handling-key-poses-v1.png` | 1672 × 941 | `5361aa46adcaf4b1d038065fc28d09698fa61aa75caca96a319f90a1097f2df0` |
| `protector-weapon-handling-key-poses-v1.png` | 1774 × 887 | `43832cd62d100a43a779e7fae84f7926bcc14ebb1e260cf0099f03b9c9bfaf93` |
| `security-ram-drone-key-poses-v1.png` | 1672 × 941 | `a330a9b6c1f2bd349861f1201bf805872071c0ae88f35d5b9f6ae9b354c88147` |
| `security-gun-sentry-key-poses-v1.png` | 1672 × 941 | `e10275551167fab0943d41826f8a36d74d2409ccca400bdfc988f49c85a33bfc` |

The sheets are animation concept inputs only. Blender owns the normalized
skeleton, weights, attachment transforms, keyframes, curves, root treatment,
clip boundaries, and final publication.

## Vanguard weapon handling

### Input image roles

1. `../poc-models/vanguard-character-turnaround-v1.png` (SHA-256:
   `66858ffda50cb37a113d6a3eeb66165fb57de02d60372a7b1551008bd349d0db`) —
   approved character identity, outfit, proportions, and provisional carry
   hardware.
2. `../poc-models/vanguard-carbine-turnaround-v1.png` (SHA-256:
   `fe6cb280507202cd63e1b72ebf6f1e6329ad165ab1ea96e0b0e517d195c9b099`) —
   approved separate carbine identity and two-hand interfaces.
3. `../../../concepts/frontier-station-v1/tactical-pause-combat.png`
   (SHA-256:
   `e891f3d4c738aa6074d71a5b889b42dc2ffb96b585551e77d6dd4b31b99badfc`) —
   tactical-camera action readability only.

### Prompt

```text
Use case: stylized-concept
Asset type: animation key-pose reference sheet for the separate Vanguard character and carbine assembly
Primary request: Create a clean eight-panel key-pose sequence for the exact approved Vanguard from Image 1 handling the exact approved two-handed carbine from Image 2. Preserve his face, bearded undercut, broad body, complete fixed outfit, armor, proportions, palette, and the weapon's exact broad silhouette. Image 3 provides tactical-camera action readability only. This sheet guides Blender rigging, attachment transfer, and animation; it does not define gameplay timing or root motion.
Scene/backdrop: one exact 4×2 grid on a flat very-dark navy neutral studio field with subtle dividers; one full-body Vanguard per cell at matching scale and front-right three-quarter camera; no room, enemies, target, floor scenery, text, labels, numbers, arrows, VFX, HUD, or logo
Poses in reading order: (1) holstered exploration idle, exact carbine rigidly attached to a clearly provisional low diagonal rear-right/back carry rail, both hands free and relaxed; (2) draw start, right hand reaches the carbine primary grip while left hand prepares to guide the forward support area, torso turns naturally; (3) attachment-transfer landmark, carbine just clear of the carry rail, right hand firmly on primary grip and left hand acquiring support grip, no duplicate weapon; (4) armed low-ready idle, butt seated at shoulder, muzzle safely lowered, stable two-handed grip; (5) raise and aim/wind-up, muzzle aligned forward, shoulders and hips braced, attack source unmistakable; (6) fire/recoil key pose, compact backward shoulder compression and slight muzzle rise, NO muzzle flash or projectile; (7) recovery pose, muzzle returning to aim and body re-centering; (8) holster-transfer landmark, weapon aligned back to the same rear-right carry rail with right hand retaining primary grip and left hand releasing, no duplicate weapon.
Style/medium: polished stylized low-poly 3D character animation model sheet, late-1990s Fallout-inspired retro-industrial science fiction with restrained cyberpunk accents; practical deformation-aware posing, readable broad silhouettes, production reference rather than cinematic illustration
Lighting/mood: identical neutral studio lighting in every cell, soft three-quarter key and restrained cool rim, no dramatic shadows, bloom, smoke, motion blur, or colored wash
Composition/framing: entire head, hands, weapon, and boots visible in every panel with generous padding; consistent camera, scale, ground line, body proportions, outfit, weapon geometry, and handedness; poses must remain readable at tactical distance
Constraints: exactly eight repetitions of the same Vanguard and exactly one instance of the same carbine per panel; weapon remains a separate rigid asset; consistent right-hand primary grip and left-hand support grip; clear grip, stock, muzzle, and carry-rail relationships; no ability, shield, energy barrier, reload, ammunition, loose magazine, sling, scope, alternate weapon, melee strike, root-motion translation, damage, target, muzzle flash, tracer, impact, telegraph, text, letters, numbers, watermark, extra limbs, fused weapon, hand deformation, or clothing redesign. The carry placement is provisional visual fitting guidance, not a final socket transform.
Avoid: identity drift between panels, changing hair or beard, swapped hands, weapon changing shape or scale, floating weapon, duplicate weapon during transfer, finger inside trigger during carry, exaggerated superhero recoil, running or lunging, photorealism, anime style, modern military camouflage
```

## Operator weapon handling

### Input image roles

1. `../poc-models/operator-character-turnaround-v1.png` (SHA-256:
   `9cc780e475d427792562566f9a080646ac7fb27005a7605d1a89259c3e245b31`) —
   approved character identity, fixed outfit, shoulder sensor, and right-thigh
   holster.
2. `../poc-models/operator-pistol-turnaround-v1.png` (SHA-256:
   `e03b8688bdf1472262849cd7c6bcc1b4350f3fb652c15380b1b06175d6272238`) —
   approved separate pistol identity and primary grip.
3. `../../../concepts/frontier-station-v1/tactical-pause-combat.png`
   (SHA-256:
   `e891f3d4c738aa6074d71a5b889b42dc2ffb96b585551e77d6dd4b31b99badfc`) —
   tactical-camera action readability only.

### Initial prompt

```text
Use case: stylized-concept
Asset type: animation key-pose reference sheet for the separate Operator character and pistol assembly
Primary request: Create a clean eight-panel key-pose sequence for the exact approved Operator from Image 1 handling the exact approved compact pistol from Image 2. Preserve her face, athletic build, high dark bun and side fringe, complete fixed outfit, asymmetric shoulder sensor, proportions, palette, right-thigh holster, and the pistol's exact compact silhouette. Image 3 provides tactical-camera action readability only. This sheet guides Blender rigging, attachment transfer, and animation; it does not define gameplay timing or an active ability.
Scene/backdrop: one exact 4×2 grid on a flat very-dark navy neutral studio field with subtle dividers; one full-body Operator per cell at matching scale and front-right three-quarter camera; no room, enemies, target, floor scenery, text, labels, numbers, arrows, VFX, HUD, or logo
Poses in reading order: (1) holstered exploration idle, exact pistol seated in the approved right-thigh holster, both hands free, alert relaxed posture; (2) draw start, right hand closes on pistol primary grip while left hand remains clear and balanced near torso; (3) attachment-transfer landmark, pistol just clear of the holster in right hand, muzzle safely down, no duplicate weapon; (4) armed low-ready idle, compact pistol held one-handed in the right hand, elbow soft, off-hand balanced and not gripping the weapon; (5) raise and aim/wind-up, right arm extends into a stable deliberate aim while left arm remains a counterbalance near torso, muzzle direction unmistakable; (6) fire/recoil key pose, compact wrist and elbow recoil with slight muzzle rise, NO muzzle flash or projectile; (7) recovery pose, muzzle returning to aim and body re-centering; (8) holster-transfer landmark, right hand aligns pistol back into the same right-thigh holster, left hand remains clear, no duplicate weapon.
Style/medium: polished stylized low-poly 3D character animation model sheet, late-1990s Fallout-inspired retro-industrial science fiction with restrained cyberpunk accents; agile deformation-aware posing, readable broad silhouettes, production reference rather than cinematic illustration
Lighting/mood: identical neutral studio lighting in every cell, soft three-quarter key and restrained cool rim, no dramatic shadows, bloom, smoke, motion blur, or colored wash
Composition/framing: entire bun, hands, pistol, holster, and boots visible in every panel with generous padding; consistent camera, scale, ground line, body proportions, outfit, weapon geometry, and handedness; poses remain readable at tactical distance
Constraints: exactly eight repetitions of the same Operator and exactly one instance of the same pistol per panel; pistol remains a separate rigid asset; consistent right-hand primary grip; NO invented support grip and no required two-hand firearm hold; clear grip, muzzle, and holster relationships; no ability, shield, energy barrier, hacking effect, reload, ammunition, loose magazine, suppressor, alternate weapon, melee strike, root-motion translation, damage, target, muzzle flash, tracer, impact, telegraph, text, letters, numbers, watermark, extra limbs, fused weapon, hand deformation, sexualized pose, or outfit redesign.
Avoid: identity drift between panels, changing hair or sensor module, swapped hands, pistol changing shape or scale, floating or duplicate pistol during transfer, finger inside trigger during carry, exaggerated acrobatic recoil, running or lunging, photorealism, anime style, high heels, exposed midriff, modern military camouflage
```

Initial correction input:
[operator-weapon-handling-key-poses-v1-initial.png](provenance-inputs/operator-weapon-handling-key-poses-v1-initial.png),
1672 × 941, SHA-256
`88255615b060cf40d93a55e10ca7e3866a0beffc54e0a3d80459aaa4d7e68c57`.

Result: revised because the fifth pose drifted into a two-hand pistol hold.

### Targeted correction prompt

```text
Use case: precise-object-edit
Asset type: Operator animation key-pose sheet grip correction
Primary request: Correct ONLY the BOTTOM-LEFT cell, the fifth pose in reading order, of Image 1. It currently reads as a two-hand pistol hold. Preserve the exact approved Operator identity and exact pistol, but move ONLY her left forearm and left hand away from the pistol into a compact open balancing pose near the left side of her torso. The right arm alone remains extended, the exact pistol remains securely in the right hand on its primary grip, and the muzzle stays aligned forward. Her left hand must not touch the pistol, right hand, or right wrist.
Invariants: preserve the exact 4×2 grid, dividers, dark navy background, all other seven cells, all camera angles, scale, lighting, body proportions, face, hair, shoulder sensor, fixed outfit, armor, holster, right-arm pose, right-hand grip, pistol geometry, muzzle direction, framing, and colors unchanged. In the bottom-left cell, change only the left arm and hand enough to create the one-handed aim silhouette.
Constraints: no second hand on the pistol; no support grip; no new object, duplicate pistol, shield, ability, effect, text, letters, numbers, watermark, changed anatomy, extra limb, clothing redesign, or altered panel layout. Change only the specified left arm and hand in the bottom-left cell.
```

Correction input roles were the retained initial output, the approved Operator
turnaround, and the approved pistol turnaround. The correction prompt's Image
1, Image 2, and Image 3 correspond to those files respectively.

## Protector weapon handling

### Input image roles

1. `../poc-models/protector-character-turnaround-v1.png` (SHA-256:
   `56c5bd24c0cf40fc59094f429899297435e55d5f056a1903c2a20e5271fcfb51`) —
   approved character identity, fixed outfit, proportions, and upper-back
   mount.
2. `../poc-models/protector-shotgun-turnaround-v1.png` (SHA-256:
   `89c5555855dccb07b46d5eb2a7a642fff2c02b78f963f89a777a1c0da5b101ce`) —
   approved separate shotgun identity, primary grip, support/pump grip, and
   muzzle.
3. `../../../concepts/frontier-station-v1/tactical-pause-combat.png`
   (SHA-256:
   `e891f3d4c738aa6074d71a5b889b42dc2ffb96b585551e77d6dd4b31b99badfc`) —
   tactical-camera action readability only.

### Prompt

```text
Use case: stylized-concept
Asset type: animation key-pose reference sheet for the separate Protector character and shotgun assembly
Primary request: Create a clean eight-panel key-pose sequence for the exact approved Protector from Image 1 handling the exact approved two-handed shotgun from Image 2. Preserve his face, dark skin, close-cropped hair, trimmed beard, very tall broad powerful build, complete heavy fixed outfit, upper-back weapon mount, proportions, palette, and the shotgun's exact short broad pump-action silhouette. Image 3 provides tactical-camera action readability only. This sheet guides Blender rigging, support-hand placement, attachment transfer, and animation; it does not define gameplay timing or an active ability.
Scene/backdrop: one exact 4×2 grid on a flat very-dark navy neutral studio field with subtle dividers; one full-body Protector per cell at matching scale and front-right three-quarter camera; no room, enemies, target, floor scenery, text, labels, numbers, arrows, VFX, HUD, or logo
Poses in reading order: (1) holstered exploration idle, exact shotgun rigidly attached to the approved upper-back mounting rail, both hands free and relaxed; (2) draw start, right hand reaches the shotgun primary grip over the shoulder while left hand prepares to guide the forward pump/support area; (3) attachment-transfer landmark, shotgun just clear of the back mount, right hand firmly on primary grip and left hand acquiring the pump/support grip, no duplicate weapon; (4) armed low-ready idle, stock seated at shoulder, wide muzzle safely lowered, stable two-handed grip; (5) raise and aim/wind-up, muzzle aligned forward, heavy armor and body braced, attack source unmistakable; (6) fire/recoil key pose, strong but controlled backward shoulder and torso compression with slight muzzle rise, left support hand remains on pump, NO muzzle flash or projectile; (7) recovery pose with one visible short pump-hand rearward motion beginning after recoil while muzzle returns safely toward aim, no shell or reload action; (8) holster-transfer landmark, shotgun aligned back to the same upper-back mount with right hand retaining primary grip and left hand releasing, no duplicate weapon.
Style/medium: polished stylized low-poly 3D character animation model sheet, late-1990s Fallout-inspired retro-industrial science fiction with restrained cyberpunk accents; heavy deformation-aware posing, readable broad silhouettes, production reference rather than cinematic illustration
Lighting/mood: identical neutral studio lighting in every cell, soft three-quarter key and restrained cool rim, no dramatic shadows, bloom, smoke, motion blur, or colored wash
Composition/framing: entire head, hands, shotgun, back mount, and boots visible in every panel with generous padding; consistent camera, scale, ground line, body proportions, outfit, weapon geometry, and handedness; poses remain readable at tactical distance
Constraints: exactly eight repetitions of the same Protector and exactly one instance of the same shotgun per panel; weapon remains a separate rigid asset; consistent right-hand primary grip and left-hand pump/support grip; clear grip, stock, wide muzzle, pump travel, and back-mount relationships; no shield, energy barrier, ability, reload mechanic, ammunition, shell ejection, loose shell, magazine, alternate weapon, melee strike, root-motion translation, damage, target, muzzle flash, tracer, impact, telegraph, text, letters, numbers, watermark, extra limbs, fused weapon, hand deformation, or outfit redesign. Pump recovery is presentation motion only and does not introduce ammunition gameplay.
Avoid: identity drift between panels, changing hair or beard, swapped hands, shotgun becoming a carbine or changing scale, floating or duplicate weapon during transfer, finger inside trigger during carry, fragile stance, superhero recoil, running or lunging, Space Marine proportions, photorealism, anime style, modern military camouflage
```

## Security ram drone

### Input image roles

1. `../poc-models/security-ram-drone-turnaround-v1.png` (SHA-256:
   `f90dbc7c7ec58509077da1af1e50e1ef22789e4c10faf6bdfde1bdab2a2663cf`) —
   approved machine identity, four-leg topology, reinforced contact source, and
   articulation direction.
2. `../poc-ui/hostile-telegraph-direction-v1.png` (SHA-256:
   `ab2ee72355d3f7bf3d765cf7020b671832fa70d6ea3127f89eb04bc898144d6c`) —
   retained attack-phase separation only.
3. `../../../concepts/frontier-station-v1/tactical-pause-combat.png`
   (SHA-256:
   `e891f3d4c738aa6074d71a5b889b42dc2ffb96b585551e77d6dd4b31b99badfc`) —
   tactical-camera readability only.

### Prompt

```text
Use case: stylized-concept
Asset type: machine rig and animation key-pose reference sheet for `machine.security.ram_drone.v1`
Primary request: Create a clean eight-panel pose sequence for the exact approved compact quadruped security ram drone from Image 1. Preserve its identical low broad chassis, four short sturdy articulated legs, broad feet, warm-gray plates, dark navy mechanics, protected red-orange sensor, thick reinforced forward wedge bumper, and central compression linkage. Image 2 provides source-specific hostile phase separation only; do not include its UI graphics. Image 3 provides tactical-camera readability only. This sheet guides Blender joint design and in-place animation; gameplay owns translation, timing, contact, damage, and interruption.
Scene/backdrop: one exact 4×2 grid on a flat very-dark navy neutral studio field with subtle dividers; one complete ram drone per cell at matching scale and front-right three-quarter camera, facing right; no room, characters, targets, text, labels, numbers, arrows, VFX, telegraph lane, HUD, or logo
Poses in reading order: (1) stable idle/scan with level chassis and all four feet planted; (2) locomotion contact pose with front-right and rear-left legs advanced, body level, no world translation; (3) turn/weight-shift pose with legs re-planted asymmetrically and sensor looking into the turn; (4) alert/target-acquisition pose with chassis slightly raised, bumper facing decisively forward, sensor focused above bumper; (5) brace/wind-up pose with all four legs lowered and widened, body compressed rearward over the feet, forward bumper pulled back, visible compression linkage loaded; (6) release/contact landmark with chassis extended forward over planted legs and reinforced bumper at the furthest credible strike position, sensor protected and NOT used as contact point, no target or impact effect; (7) rebound/recovery pose with front suspension compressed, body rocked backward and feet retaining traction; (8) disabled/shutdown pose with chassis settled low, sensor dark, legs folded safely outward, bumper intact.
Style/medium: polished stylized low-poly 3D hard-surface animation model sheet, late-1990s Fallout-inspired retro-industrial science fiction with restrained cyberpunk accents; mechanically credible articulation, broad tactical silhouettes, production reference rather than cinematic illustration
Lighting/mood: identical neutral studio lighting in every cell, restrained cool rim and warm metal highlights; only the approved sensor emits a small red-orange point except it is dark in shutdown; no dramatic shadows, bloom, smoke, sparks, motion blur, or colored wash
Composition/framing: entire machine, feet, bumper, sensor, and linkage visible in every panel with generous padding; consistent camera, scale, ground line, geometry, materials, leg count, facing, and proportions; brace, contact, rebound, and shutdown silhouettes must differ clearly at tactical distance
Constraints: exactly eight repetitions of the same machine, exactly FOUR legs and four feet in every non-occluded pose, one sensor, one reinforced bumper, no gun or separate weapon; contact source is the bumper, never the sensor; no root-motion path, target, impact, damage, telegraph, selection ring, projectile, muzzle flash, ability, shield, claws, horn, blade, wheels, tracks, cables, loose parts, text, letters, numbers, watermark, changed armor, extra limbs, or duplicated machine. Poses are articulation landmarks, not accepted animation timing.
Avoid: machine identity drift, changing quadruped into tripod or insect, fifth leg, fragile needle feet, sensor becoming a weapon, bumper becoming horns, giant squash-and-stretch, airborne leap, cinematic collision, photorealism, anime mecha, neon overload
```

## Security gun sentry

### Input image roles

1. `../poc-models/security-gun-sentry-turnaround-v1.png` (SHA-256:
   `89872e6b84578fd213f9dc10b17d8905c88ca6621592e55f5c575f7a96f8d101`) —
   approved machine identity, three-leg topology, integrated firearm, and
   articulation direction.
2. `../poc-ui/hostile-telegraph-direction-v1.png` (SHA-256:
   `ab2ee72355d3f7bf3d765cf7020b671832fa70d6ea3127f89eb04bc898144d6c`) —
   retained attack-phase separation only.
3. `../../../concepts/frontier-station-v1/tactical-pause-combat.png`
   (SHA-256:
   `e891f3d4c738aa6074d71a5b889b42dc2ffb96b585551e77d6dd4b31b99badfc`) —
   tactical-camera readability only.

### Prompt

```text
Use case: stylized-concept
Asset type: machine rig and animation key-pose reference sheet for `machine.security.gun_sentry.v1`
Primary request: Create a clean eight-panel pose sequence for the exact approved tall THREE-LEGGED security gun sentry from Image 1. Preserve its identical central chassis, exactly three sturdy reverse-jointed support legs spaced around the body, broad feet, compact sensor head on yaw joint, one integrated rectangular gun with deep visible muzzle, protected recoil slide, rear counterweight, warm-gray plates, dark navy mechanics, and separate red-orange sensor. Image 2 provides source-specific hostile phase separation only; do not include its UI graphics. Image 3 provides tactical-camera readability only. This sheet guides Blender joint design and in-place animation; gameplay owns movement, timing, firing, projectile logic, damage, and interruption.
Scene/backdrop: one exact 4×2 grid on a flat very-dark navy neutral studio field with subtle dividers; one complete sentry per cell at matching scale and front-right three-quarter camera, integrated gun facing generally right; no room, characters, targets, text, labels, numbers, arrows, VFX, telegraph corridor, HUD, or logo
Poses in reading order: (1) stable idle/scan with exactly three feet planted, sensor head turned slightly off-axis and gun relaxed below firing line; (2) locomotion contact pose with one front leg advanced, one rear leg pushing, third leg stabilizing, chassis level, no world translation; (3) turn/weight-shift pose with exactly three feet re-planted asymmetrically, chassis yawing while rear counterweight balances; (4) alert/track pose with sensor and integrated gun rotating toward the same forward sector while remaining visually separate; (5) aim/wind-up pose with three legs widened and braced, gun elevated and precisely aligned, recoil slide fully forward, muzzle direction unmistakable; (6) fire/recoil landmark with gun slide visibly compressed backward into protected housing, chassis absorbing a small rearward pitch, NO muzzle flash or projectile; (7) recovery pose with recoil slide returning forward, gun re-centering and legs maintaining brace; (8) disabled/shutdown pose with sensor dark, gun lowered, chassis slumped safely between three folded-out legs, muzzle unobstructed.
Style/medium: polished stylized low-poly 3D hard-surface animation model sheet, late-1990s Fallout-inspired retro-industrial science fiction with restrained cyberpunk accents; mechanically credible articulation, broad tactical silhouettes, production reference rather than cinematic illustration
Lighting/mood: identical neutral studio lighting in every cell, restrained cool rim and warm metal highlights; only the approved sensor emits a small red-orange point except it is dark in shutdown; no muzzle light, dramatic shadows, bloom, smoke, sparks, motion blur, or colored wash
Composition/framing: entire machine, all visible legs and feet, sensor, muzzle, recoil slide, and counterweight visible with generous padding; consistent camera, scale, ground line, geometry, materials, THREE-LEG topology, facing, and proportions; track, aim, recoil, recovery, and shutdown silhouettes must differ clearly at tactical distance
Constraints: exactly eight repetitions of the same machine; EXACTLY THREE legs and exactly three feet in each pose, spaced approximately 120 degrees around one chassis; one integrated gun only, one deep muzzle only, one separate sensor only; muzzle is the attack source, never the sensor; gun remains integrated and may not become handheld or detachable; no fourth leg, humanoid arms, separate weapon, ammunition, magazine, shell, root-motion path, target, impact, damage, telegraph, selection ring, projectile, muzzle flash, ability, shield, claws, blade, wheels, tracks, cables, loose parts, text, letters, numbers, watermark, changed armor, or duplicated machine. Poses are articulation landmarks, not accepted animation timing.
Avoid: machine identity drift, cross-shaped four-legged silhouette, quadruped, biped, extra support strut mistaken for a leg, sensor becoming a muzzle, gun changing shape or side, fragile needle feet, giant recoil, airborne hop, cinematic firefight, photorealism, anime mecha, neon overload
```

## Review boundary

- The eight cells are ordered animation landmarks, not authored frame counts or
  clip timing.
- Human weapon carry and transfer poses must be fitted and validated with the
  normalized character and separate weapon in Blender. No depicted carry
  transform is authoritative.
- Combat clips remain in-place. Gameplay owns movement and attack resolution.
- Machine legs, weapon slides, compression linkages, and shutdown silhouettes
  are articulation targets that final geometry must reconcile.
- Generated rigs and clips remain donor inputs. Blender owns production
  authority.
- Ability-specific clips remain blocked.
