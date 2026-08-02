# POC UI and effects visual-direction batch 04 — provenance

Status: retained visual direction

Generated: 2026-07-23 with the built-in Codex image-generation tool. Provider
model, seed, and job identifiers were not exposed.

## Retained outputs

| File | Dimensions | SHA-256 |
|---|---:|---|
| `character-portrait-direction-v1.png` | 1254 × 1254 | `5b7d00b9d48a0e4aecf72f5380b5a05e16b823fedc72351767c1b77a130284ae` |
| `equipment-icon-direction-v1.png` | 1254 × 1254 | `44bc3d782b2a02534cd083e8c2c301527975634048a82278fbc00cf287e526c2` |
| `tactical-marker-direction-v1.png` | 1672 × 941 | `fa9f585c4b584c9343bacdfb21ce6564c843846140e77d476063bba75b5f8ab5` |
| `combat-healing-vfx-direction-v1.png` | 1536 × 1024 | `692d83dd01a53acda664fe44777dfaa9aa4986478f41028af87d45e309e1fff6` |
| `station-state-feedback-direction-v1.png` | 1536 × 1024 | `bca39d46a5ac9149e01286ac2b58b4c698a927d8e5bf4919098ee277a76ef958` |

## Source roles

- Approved character turnarounds supplied portrait identities.
- Approved weapon and field-aid turnarounds supplied equipment silhouettes.
- `tactical-pause-combat.png` supplied tactical contrast and hostile-state
  language.
- `station-route-key-art.png`, the terminal turnaround, structure kit, and
  airlock reference supplied station state and palette semantics.

## Frozen direction

- Portraits and equipment icons are interim until production-model renders are
  available.
- Cyan means friendly selection, movement, and focus; violet means optional
  interaction; green means destination or completion; red-orange means hostile;
  amber is a restrained caution or physical-impact accent.
- Tactical markers and VFX are broad, low-noise, readable at 1280×720, and
  feasible with Godot controls, meshes, particles, and shaders.
- Station feedback changes presentation only; gameplay owns state, target
  membership, range, timing, collision, damage, and outcomes.
- The removed hostile-machine sheet and telegraph direction are not production
  references. New Phase 4 machines require fresh rigid-machine references and
  attack telegraphs after their gameplay shapes are accepted.
