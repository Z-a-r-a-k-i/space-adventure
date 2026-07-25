# Candidate selection

Status: candidate 01 provisionally selected pending owner visual review

The run permits one initial candidate and one retry only for a named defect.
Any selection is provisional pending the owner's final visual review.

## Candidate 01

- Tripo task/model ID: `01bb9aea-6b10-419d-bbeb-9648c9867a97`
- Review state: provisionally selected
- Credits: `55`
- Studio geometry: 1,006,324 vertices; 1,946,373 triangles
- Raw export:
  `raw/weapon.crew.vanguard_carbine.v1__raw__tripo-v3.1__candidate-01.glb`
  (58,235,208 bytes,
  `E8CBCBA5F12FB0DD61304A5C425306D996413494AAB1FB385683EDA1BCA64FE7`)

Selection checks:

- unmistakable medium-long broad carbine rather than a shotgun;
- coherent stock, receiver, primary grip, reachable support grip, thick barrel,
  and real open muzzle;
- no character, hand, ammunition, loose magazine, or extra attachment;
- no missing back, underside, or paper-thin surfaces;
- clean enough topology for the brief's 12,000-triangle hard limit;
- restrained navy/charcoal, warm-gray, and cyan palette;
- normalizable to the 0.82 m x 0.13 m x 0.27 m envelope;
- plausible `socket.grip.primary`, `socket.grip.support`, and
  `socket.attack.muzzle.primary` placement; and
- plausible complete Vanguard hand, shoulder, muzzle-line, and carry fit.

Candidate 01 passed every listed visual check in Studio:

- the medium-long broad carbine silhouette is distinct from the Protector
  shotgun;
- stock, receiver, primary grip, reachable support grip, barrel, and open
  muzzle are coherent from four inspected views;
- no character, hand, ammunition, loose magazine, or unrelated object was
  generated;
- the opposite side, top, underside, and muzzle show no obvious missing or
  paper-thin surface;
- restrained navy/charcoal, warm-gray, and cyan cues match the approved
  Vanguard family; and
- the model has plausible locations for all three required sockets.

The candidate is therefore selected for Blender processing. No second
candidate was generated because there is no named generation defect to
correct. The selection remains reversible and provisional, and the weapon
remains separate from the character model.

## Blender processing outcome

Candidate 01 remains the provisional selection after Blender processing. The
predecessor separate static staging GLB is 143,436 bytes and meets the static
geometry, envelope, material, marker, orientation, and rig-payload contracts.
Its Blender contact sheet passed provisional visual inspection. The normalized
source is migrated, but the GLB is not published into
`game/Assets/Published/` on this branch until fresh Godot import,
complete-assembly validation, and 7.5 m / 14.5 m / 20 m captures pass. Final
owner visual approval remains pending.

The predecessor exact Vanguard assembly decision is separately `revise`.
Current character socket/rest-pose measurements show a 0.99300232 m
support-hand gap, 608 hand/body overlap pairs, and 937 holster/body overlap
pairs. The muzzle line is clear. These are character-socket/pose integration
blockers, not reasons to distort or reject the compliant static weapon.

No second Tripo candidate was generated, no additional credit was consumed,
and no gameplay identity or behavior was inferred.
