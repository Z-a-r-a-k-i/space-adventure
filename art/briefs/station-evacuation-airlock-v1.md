# Asset brief — station evacuation airlock v1

Status: accepted by the project owner for Phase 3 production on 2026-08-02;
integrated candidate awaiting final owner visual approval

## Contract

| Field | Requirement |
|---|---|
| Asset ID | `assembly.station.evacuation_airlock.v1` |
| Role | Route destination with visibly opening rigid leaves |
| Gameplay wrapper | `interaction.evacuation_airlock` |
| Reference | `art/reference-sheets/frontier-station-v1/poc-models/evacuation-airlock-reference-v1.png` |
| Envelope | 3.20 m wide × 2.85 m high × 0.50 m deep maximum |
| Axes | `+Y` up, `-Z` front |
| Pivot | Ground-plane center of the closed doorway |
| Budget | 6,000 triangles and 3 material slots maximum |

The Godot wrapper owns identity, collision, interaction, completion state, and
door timing. `Door_Left` and `Door_Right` are presentation-only rigid parts.

## Visual requirements

- Broad armored frame and two clearly separated sliding leaves.
- Green status header and control panel make the destination readable at the
  default tactical-camera distance.
- Closed and open states must be unambiguous without skeletal deformation.
- No baked lighting, generated collision, rig, legible text, or unrelated
  machinery.

## Production gate

Author dimensionally in Blender, validate leaf names, closed positions, axes,
and pivot, then inspect the exact GLB both alone and in the live Godot route.
