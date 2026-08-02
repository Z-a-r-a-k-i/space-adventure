# Asset brief — station service terminal v1

Status: approved visual direction; production deferred to Phase 5

## Contract

| Field | Requirement |
|---|---|
| Asset ID | `prop.station.service_terminal.v1` |
| Role | Optional station interaction presentation |
| Gameplay wrapper | `interaction.service_terminal` |
| Reference | `art/reference-sheets/frontier-station-v1/station-service-terminal-turnaround-v1.png` |
| Visual envelope | 0.80 m wide × 1.30 m high × 0.42 m deep |
| Axes | `+Y` up, `-Z` front |
| Pivot | Ground-plane center beneath the body |
| Budget | 4,000 triangles, 3 material slots, one 1024² texture set maximum |

The authored Godot wrapper owns identity, interaction, approach, collision, and
state. The model is a static presentation asset with no rig or animation.

## Visual requirements

- Stable freestanding pedestal, raised display hood, and one dominant screen.
- Chunky industrial construction with two or three broad panel breaks.
- Violet is the primary optional-interaction cue; cyan remains subordinate.
- The front and role must remain readable at the 14.5 m default camera and at
  20 m.
- No legible text, logos, loose cables, dense greebles, green destination cue,
  hostile red sensor, baked lighting, or generated collision.

## Production gate

Author or generate a fresh static source only when Phase 5 activates the
asset. Normalize it in Blender, validate the GLB, inspect it live in Blender
and Godot, and keep the existing greybox wrapper until human approval.
