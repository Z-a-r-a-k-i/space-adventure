# Asset brief — Operator / Medic v1

Status: owner authorized the retained Operator identity as the third playable
Medic on 2026-09-12. Production presentation is available; live handling and
owner visual review remain part of the station-completion gate.

| Field | Contract |
| --- | --- |
| Asset ID | `character.crew.operator.v1` |
| Gameplay role | Third recruit; pistol, single-ally heal, ground healing field |
| Separate weapon | `weapon.crew.operator_pistol.v1` |
| Approved reference | `art/reference-sheets/frontier-station-v1/poc-models/operator-character-turnaround-v1.png` |
| Handling reference | `art/reference-sheets/frontier-station-v1/poc-animation/operator-weapon-handling-key-poses-v1.png` |
| Current source and reproduction | [Production record](../source/character.crew.operator.v1/production.md) |

Preserve the adult female identity, high dark bun, restrained side fringe,
slim athletic proportions, navy undersuit, light warm-gray armor, shoulder
sensor, pouches and sparse cyan accents. One fixed outfit; no inventory,
embedded weapon, sexualized redesign, or heavy Protector proportions.
Compare the three crew at the tactical camera. Current neutral height is
approximately 1.74 m.

The owner requested reuse of retained sources. The retained A-pose geometry
is rebound in Blender to anatomy fitted from the accepted Mixamo hierarchy;
the old generic 59-bone rig and its one-frame landmarks are discarded. Keep
the accepted rig's parent relationships and rest orientations, rebake actual
locomotion and down motion, normalize no more than four influences per vertex,
and verify exact exported GLB motion. This bounded retained-source recovery
replaces the earlier instruction to wait for an unstarted Operator provider run.

Publish at most 35,000 triangles, 18 skinned meshes, eight material slots,
64 bones, and two de-lit 2048 texture sets. Current material IDs remain
`mat.operator.surface.pbr` and `mat.operator.undersuit.navy`. Use meters,
Godot +Y up and -Z forward; no generated collision or authoritative movement.

Expose `socket.weapon.hand_primary` and `socket.weapon.holster_primary`.
Fit the separate pistol to the right hand and right-thigh holster; the left
hand remains clear and has no support-grip contract. Publish holstered idle,
walk/locomotion aliases, draw, armed idle/walk, primary attack, holster and down.
Healing feedback reads core events and never resolves healing from animation.

The original reference and retained source were approved for bounded production
on 2026-07-24. No new Tripo/Mixamo submission, API call, purchase, or generation
is required for this recovery. Preserve original identity and provenance.
Review exact GLB grounding, joints, single-handed grip, muzzle line, draw,
holster, retry and down at 7.5, 14.5 and 20 m in Godot under the
[art pipeline](../../docs/ART-PIPELINE.md). Technical checks do not replace the
owner's visual and handling acceptance.
